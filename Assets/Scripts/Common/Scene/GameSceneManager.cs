using System;
using System.Collections;
using System.Collections.Generic;
using Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Persistent owner of runtime scene operations and LIFO presentation visits.</summary>
public sealed class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager Instance { get; private set; }

    [SerializeField, Min(0f)]
    private float fadeDuration = .4f;
    private SceneCatalog catalog;
    private SceneTransitionPanel transitionView;
    private UIHandle transitionHandle;
    private SceneOperation currentOperation;
    private string pendingScenePath, replacementScenePath;
    private bool singleSceneActivated;
    private readonly List<Visit> visits = new();
    private Visit initializingVisit;
    private IDisposable transitionInput;
#if UNITY_EDITOR
    // Fault injection for the Editor regression. Never present in player builds.
    internal Func<Scene, bool> RejectUnloadForVerification;
#endif
    private sealed class Visit
    {
        public SceneVisitHandle Handle = new(Guid.NewGuid());
        public SceneId Id;
        public Scene Source, Target;
        public ISceneVisitLifecycle Lifecycle;
        public ISceneSuspension Suspension;
        public ScenePresentationVisibility IncomingPresentation;
        public IDisposable IncomingUI;
        public object Context;
        public bool Faulted;
    }

    public SceneCatalog Catalog => catalog;
    public bool IsLoading => currentOperation != null;
    public SceneOperation CurrentOperation => currentOperation;
    public int VisitCount => visits.Count;
    public SceneVisitHandle CurrentVisit => visits.Count == 0 ? default : visits[visits.Count - 1].Handle;
    public bool HasFaultedVisit => visits.Exists(v => v.Faulted);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime()
    {
        if (Instance != null)
            SceneManager.sceneLoaded -= Instance.HandleSceneLoaded;
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;
        var existing = FindFirstObjectByType<GameSceneManager>();
        if (existing != null)
            existing.Initialize();
        else
            new GameObject(nameof(GameSceneManager)).AddComponent<GameSceneManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            Destroy(this);
            return;
        }

        Initialize();
    }

    private void Initialize()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
        catalog = Resources.Load<SceneCatalog>(SceneCatalog.ResourcePath);
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    public bool TryGetLoadedScene(SceneId id, out Scene scene)
    {
        scene = default;
        if (catalog == null || !catalog.TryGetPath(id, out var path))
            return false;
        scene = SceneManager.GetSceneByPath(path);
        return scene.IsValid() && scene.isLoaded;
    }

    public bool TryGetVisit(SceneId id, out SceneVisitHandle handle)
    {
        for (int i = visits.Count - 1; i >= 0; i--)
            if (visits[i].Id == id)
            {
                handle = visits[i].Handle;
                return true;
            }

        handle = default;
        return false;
    }

    public bool IsSceneSuspended(Scene scene) => visits.Exists(v => v.Source.IsValid() && v.Source == scene);
    public bool IsScenePending(Scene scene) => IsLoading && string.Equals(scene.path, pendingScenePath, StringComparison.OrdinalIgnoreCase);
    public bool TrySetActiveScene(Scene scene, out string error)
    {
        if (IsLoading || HasFaultedVisit)
        {
            error = "场景操作尚未结束。";
            return false;
        }

        if (visits.Count > 0 && visits[visits.Count - 1].Target != scene)
        {
            error = "不能绕过当前展示会话切换活动场景。";
            return false;
        }

        return SetActive(scene, out error);
    }

    private static bool SetActive(Scene scene, out string error)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            error = "目标场景尚未加载。";
            return false;
        }

        if (SceneManager.GetActiveScene() == scene)
        {
            error = null;
            return true;
        }

        if (!SceneManager.SetActiveScene(scene))
        {
            error = $"设置活动场景失败：{scene.path} (handle={scene.handle}, active={SceneManager.GetActiveScene().path})。";
            return false;
        }

        error = null;
        return true;
    }

    private bool Validate(SceneId id, bool allowVisit, out string error)
    {
        if (!isActiveAndEnabled)
            error = "场景管理器尚未启用。";
        else if (IsLoading)
            error = "场景操作正在进行，请勿重复请求。";
        else if (HasFaultedVisit)
            error = "请先重试清理失败的场景会话。";
        else if (!allowVisit && visits.Count > 0)
            error = "请先返回，或使用当前会话的替换接口。";
        else if (catalog == null || !catalog.TryGetPath(id, out var path))
            error = $"SceneCatalog 未正确登记：{id}。";
        else if (!Application.CanStreamedLevelBeLoaded(path))
            error = $"场景未加入当前构建配置：{path}";
        else
        {
            error = null;
            return true;
        }

        return false;
    }

    public bool CanLoad(SceneId id, out string error) => Validate(id, false, out error);
    /// <summary>Compatibility: true means accepted, not completed.</summary>
    public bool TryLoad(SceneId id, out string error, SceneTransitionMode mode = SceneTransitionMode.Immediate)
    {
        var operation = LoadAsync(id, new SceneLoadOptions { Transition = mode });
        error = operation.Error;
        operation.OnCompleted(r =>
        {
            if (r.Accepted && !r.Succeeded)
                Debug.LogError(r.Error);
        });
        return operation.Accepted;
    }

    public SceneOperation LoadAsync(SceneId id, SceneLoadOptions options = null)
    {
        var settings = options?.Copy() ?? new SceneLoadOptions();
        if (!ValidOptions(settings, out var error) || !Validate(id, false, out error))
            return Reject(error);
        if (settings.LoadMode == LoadSceneMode.Additive && TryGetLoadedScene(id, out _))
            return Reject("目标场景已经加载。");
        return Begin(operation => LoadRoutine(operation, id, settings), settings);
    }

    public SceneOperation EnterVisitAsync(SceneId id, ISceneVisitLifecycle lifecycle, object context = null, SceneTransitionMode transition = SceneTransitionMode.Immediate)
    {
        if (lifecycle == null)
            return Reject("缺少场景生命周期适配器。");
        var settings = new SceneLoadOptions
        {
            LoadMode = LoadSceneMode.Additive,
            Context = context,
            Transition = transition
        };
        if (!ValidOptions(settings, out var error) || !Validate(id, true, out error))
            return Reject(error);
        if (TryGetVisit(id, out _) || TryGetLoadedScene(id, out _))
            return Reject("目标展示场景已经存在。");
        var visit = new Visit
        {
            Id = id,
            Source = SceneManager.GetActiveScene(),
            Lifecycle = lifecycle,
            Context = context
        };
        return Begin(operation => EnterRoutine(operation, visit), settings, operation =>
        {
            // Capture before input/fade can advance the movement state machine.
            visits.Add(visit);
            initializingVisit = visit;
            operation.Visit = visit.Handle;
            visit.Suspension = visit.Lifecycle.SuspendSource(visit.Source);
        });
    }

    public SceneOperation AdoptDirectScene(Scene scene, ISceneVisitLifecycle lifecycle, object context = null)
    {
        if (IsScenePending(scene))
            return currentOperation;
        if (IsLoading || HasFaultedVisit || lifecycle == null || !scene.IsValid() || !scene.isLoaded || catalog == null || !catalog.TryGetId(scene.path, out var id))
            return Reject("无法接管直接启动的展示场景。");
        if (TryGetVisit(id, out var handle))
        {
            var ready = new SceneOperation
            {
                Accepted = true,
                Scene = scene,
                Visit = handle
            };
            ready.Finish();
            return ready;
        }

        return Begin(operation => AdoptRoutine(operation, id, scene, lifecycle, context), new SceneLoadOptions());
    }

    public SceneOperation ReturnAsync(SceneVisitHandle handle)
    {
        if (!GetTop(handle, out var visit, out var error))
            return Reject(error);
        if (!visit.Source.IsValid() || !visit.Source.isLoaded)
            return ReplaceFromVisitAsync(handle, SceneId.Sample);
        return Begin(operation => ReturnRoutine(operation, visit), new SceneLoadOptions());
    }

    public SceneOperation ReplaceFromVisitAsync(SceneVisitHandle handle, SceneId id, SceneLoadOptions options = null)
    {
        if (!GetTop(handle, out _, out var error))
            return Reject(error);
        var settings = options?.Copy() ?? new SceneLoadOptions();
        settings.LoadMode = LoadSceneMode.Single;
        if (!ValidOptions(settings, out error))
            return Reject(error);
        if (catalog == null || !catalog.TryGetPath(id, out var path) || !Application.CanStreamedLevelBeLoaded(path))
            return Reject($"场景 {id} 未正确登记到构建配置。");
        return Begin(operation => LoadRoutine(operation, id, settings), settings);
    }

    public SceneOperation UnloadAsync(Scene scene)
    {
        if (IsLoading || visits.Count > 0)
            return Reject("存在场景操作或展示会话，请使用 ReturnAsync。");
        if (!scene.IsValid() || !scene.isLoaded || SceneManager.sceneCount <= 1)
            return Reject("无法卸载无效场景或最后一个场景。");
        return Begin(operation => UnloadCore(operation, scene), new SceneLoadOptions());
    }

    private bool GetTop(SceneVisitHandle handle, out Visit visit, out string error)
    {
        visit = visits.Count > 0 ? visits[visits.Count - 1] : null;
        if (IsLoading)
            error = "场景操作正在进行。";
        else if (visit == null || !handle.IsValid || !visit.Handle.Equals(handle))
            error = "会话已失效或不是最上层会话。";
        else
        {
            error = null;
            return true;
        }

        return false;
    }

    private static bool ValidOptions(SceneLoadOptions options, out string error)
    {
        error = !Enum.IsDefined(typeof(LoadSceneMode), options.LoadMode) || !Enum.IsDefined(typeof(SceneTransitionMode), options.Transition) ? "无效的加载或过渡模式。" : null;
        return error == null;
    }

    private static SceneOperation Reject(string error)
    {
        var operation = new SceneOperation();
        operation.Finish(error ?? "请求被拒绝。");
        return operation;
    }

    private SceneOperation Begin(Func<SceneOperation, IEnumerator> body, SceneLoadOptions options, Action<SceneOperation> beforeFade = null)
    {
        var operation = new SceneOperation
        {
            Accepted = true
        };
        currentOperation = operation;
        singleSceneActivated = false;
        initializingVisit = null;
        StartCoroutine(Execute(operation, body, options, beforeFade));
        return operation;
    }

    private IEnumerator Execute(SceneOperation operation, Func<SceneOperation, IEnumerator> body, SceneLoadOptions options, Action<SceneOperation> beforeFade)
    {
        string failure = null;
        bool prepared = false;
        yield return Guard(Work(), e => failure = e);
        if (failure != null)
        {
            if (initializingVisit != null)
            {
                var incomplete = initializingVisit;
                string cleanupError = null;
                yield return Guard(CleanupEntry(operation, incomplete), e => cleanupError = e);
                if (cleanupError != null)
                {
                    incomplete.Faulted = true;
                    failure += "\n清理失败：" + cleanupError;
                    failure += "\n残留场景仍受会话管理，请调用 ReturnAsync(CurrentVisit) 再次返回。";
                }
            }

            if (prepared && !singleSceneActivated)
            {
                try
                {
                    options.Rollback?.Invoke();
                }
                catch (Exception e)
                {
                    failure += "\n业务恢复失败：" + e.Message;
                }
            }

            if (singleSceneActivated)
            {
                failure += "\n新场景已激活，旧场景不能自动回滚。";
                failure += "\n可使用 ReturnToSampleAsync() 返回探索。";
            }

            operation.Scene = SceneManager.GetActiveScene();
            var remaining = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                remaining.Add(scene.path + (scene == operation.Scene ? " [active]" : ""));
            }

            failure += "\n当前驻留场景：" + string.Join(", ", remaining);
        }

        initializingVisit = null;
        pendingScenePath = replacementScenePath = null;
        if (UIManager.Instance != null && transitionHandle.IsValid)
        {
            // 淡入已完成，同帧释放遮罩和输入，避免来源玩家先更新一帧。
            UIManager.Instance.Release(transitionHandle);
        }
        transitionHandle = default;
        transitionView = null;
        transitionInput?.Dispose();
        transitionInput = null;
        currentOperation = null;
        operation.Finish(failure);
        IEnumerator Work()
        {
            beforeFade?.Invoke(operation);
            UIManager ui = UIManager.Instance;
            if (ui == null)
                throw new InvalidOperationException("场景切换需要已初始化的 UIManager。");
            transitionInput ??= ui.AcquireTransitionInput();
            transitionHandle = ui.Open(UIId.SceneTransition, ownerSceneHandle: ui.gameObject.scene.handle);
            if (!ui.TryGetPanel(transitionHandle, out transitionView))
                throw new InvalidOperationException("请在 UIRegistry 中登记 SceneTransition Overlay 面板。");
            while (transitionView.State == UIState.Opening)
                yield return null;
            if (options.Transition == SceneTransitionMode.Fade)
            {
                operation.Stage = SceneOperationStage.FadingOut;
                yield return transitionView.FadeTo(1f, fadeDuration);
            }

            prepared = true;
            options.Prepare?.Invoke();
            yield return body(operation);
            if (options.Transition == SceneTransitionMode.Fade)
            {
                operation.Stage = SceneOperationStage.FadingIn;
                yield return transitionView.FadeTo(0f, fadeDuration);
            }
        }
    }

    // Drive nested iterators ourselves: errors must return through the same cleanup path.
    private static IEnumerator Guard(IEnumerator routine, Action<string> fail)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        try
        {
            while (stack.Count > 0)
            {
                object next = null;
                bool moved = false;
                Exception failure = null;
                try
                {
                    moved = stack.Peek().MoveNext();
                    if (moved)
                        next = stack.Peek().Current;
                }
                catch (Exception e)
                {
                    failure = e;
                }

                if (failure != null)
                {
                    fail(failure.ToString());
                    yield break;
                }

                if (!moved)
                {
                    try
                    {
                        (stack.Pop() as IDisposable)?.Dispose();
                    }
                    catch (Exception e)
                    {
                        failure = e;
                    }

                    if (failure != null)
                    {
                        fail(failure.ToString());
                        yield break;
                    }

                    continue;
                }

                if (next is IEnumerator nested)
                    stack.Push(nested);
                else
                    yield return next;
            }
        }
        finally
        {
            while (stack.Count > 0)
            {
                try
                {
                    (stack.Pop() as IDisposable)?.Dispose();
                }
                catch (Exception e)
                {
                    fail(e.ToString());
                }
            }
        }
    }

    private IEnumerator LoadCore(SceneOperation operation, SceneId id, LoadSceneMode mode, bool setActive)
    {
        if (!catalog.TryGetPath(id, out var path))
            throw new InvalidOperationException("场景登记已改变。");
        pendingScenePath = path;
        if (mode == LoadSceneMode.Single)
            replacementScenePath = path;
        operation.Stage = SceneOperationStage.Loading;
        var loading = SceneManager.LoadSceneAsync(path, mode);
        if (loading == null)
            throw new InvalidOperationException("Unity 没有创建加载操作。");
        while (!loading.isDone)
        {
            operation.Progress = Mathf.Clamp01(loading.progress / .9f) * .8f;
            yield return null;
        }

        var scene = SceneManager.GetSceneByPath(path);
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException("加载结束但目标场景不存在。");
        operation.Scene = scene;
        if (setActive && !SetActive(scene, out var error))
            throw new InvalidOperationException(error);
    }

    private IEnumerator LoadRoutine(SceneOperation operation, SceneId id, SceneLoadOptions options)
    {
        yield return LoadCore(operation, id, options.LoadMode, options.SetActive);
        // Raw Additive is a scene operation only. Use EnterVisitAsync to apply a
        // presentation lifecycle, including source suspension and UI initialization.
        if (options.LoadMode == LoadSceneMode.Additive)
            yield break;
        var entry = FindEntry(operation.Scene);
        if (entry != null && entry.VisitLifecycle != null)
        {
            yield return AdoptRoutine(operation, id, operation.Scene, entry.VisitLifecycle, options.Context);
        }
        else if (entry != null)
        {
            operation.Stage = SceneOperationStage.Initializing;
            yield return entry.InitializeScene(default, options.Context);
        }
    }

    private IEnumerator AdoptRoutine(SceneOperation operation, SceneId id, Scene target, ISceneVisitLifecycle lifecycle, object context)
    {
        var visit = new Visit
        {
            Id = id,
            Target = target,
            Lifecycle = lifecycle,
            Context = context
        };
        visits.Add(visit);
        initializingVisit = visit;
        operation.Scene = target;
        operation.Visit = visit.Handle;
        pendingScenePath = target.path;
        visit.Suspension = lifecycle.SuspendSource(default);
        StageIncoming(visit);
        operation.Stage = SceneOperationStage.Initializing;
        yield return lifecycle.InitializeTarget(target, visit.Handle, context);
        ShowIncoming(visit);
        initializingVisit = null;
    }

    private IEnumerator EnterRoutine(SceneOperation operation, Visit visit)
    {
        yield return LoadCore(operation, visit.Id, LoadSceneMode.Additive, true);
        visit.Target = operation.Scene;
        operation.Stage = SceneOperationStage.Initializing;
        yield return visit.Lifecycle.InitializeTarget(visit.Target, visit.Handle, visit.Context);
        ShowIncoming(visit);
        initializingVisit = null;
    }

    private IEnumerator CleanupEntry(SceneOperation operation, Visit visit)
    {
        if (!visit.Target.IsValid() && TryGetLoadedScene(visit.Id, out var found))
            visit.Target = found;
        if (visit.Source.IsValid() && visit.Source.isLoaded && !SetActive(visit.Source, out var error))
            throw new InvalidOperationException(error);
        (visit.Suspension as IScenePresentationSuspension)?.ShowPresentation();
        if (visit.Target.IsValid() && visit.Target.isLoaded)
        {
            if (SceneManager.sceneCount <= 1)
                throw new InvalidOperationException("直接启动场景初始化失败，需要返回 Sample。");
            yield return UnloadCore(operation, visit.Target);
        }

        visit.Lifecycle.ReleaseTarget(visit.Target);
        visit.IncomingUI?.Dispose();
        visit.IncomingUI = null;
        visit.IncomingPresentation = null;
        visit.Suspension?.Resume();
        visits.Remove(visit);
    }

    private IEnumerator ReturnRoutine(SceneOperation operation, Visit visit)
    {
        operation.Visit = visit.Handle;
        IDisposable targetHold = null;
        bool completed = false;
        try
        {
            targetHold = visit.Lifecycle.SuspendTarget(visit.Target);
            if (!SetActive(visit.Source, out var error))
                throw new InvalidOperationException(error);
            // Source gameplay stays paused, but its camera must cover the asynchronous unload.
            (visit.Suspension as IScenePresentationSuspension)?.ShowPresentation();
            (targetHold as IScenePresentationSuspension)?.HidePresentation();
            if (visit.Target.IsValid() && visit.Target.isLoaded)
                yield return UnloadCore(operation, visit.Target);
            targetHold?.Dispose();
            targetHold = null;
            visit.Lifecycle.ReleaseTarget(visit.Target);
            visit.IncomingUI?.Dispose();
            visit.IncomingUI = null;
            operation.Stage = SceneOperationStage.Restoring;
            visit.Suspension?.Resume();
            visits.Remove(visit);
            operation.Scene = visit.Source;
            completed = true;
        }
        finally
        {
            try
            {
                if (!completed && visit.Target.IsValid() && visit.Target.isLoaded && !visit.Faulted)
                    (visit.Suspension as IScenePresentationSuspension)?.HidePresentation();
                targetHold?.Dispose();
            }
            finally
            {
                if (!completed && visit.Target.IsValid() && visit.Target.isLoaded)
                {
                    SetActive(visit.Target, out _);
                }
                else if (!completed)
                {
                    visit.Faulted = true;
                }
            }
        }
    }

    private IEnumerator UnloadCore(SceneOperation operation, Scene scene)
    {
        operation.Stage = SceneOperationStage.Unloading;
        if (SceneManager.sceneCount <= 1)
            throw new InvalidOperationException("不能卸载最后一个场景。");
#if UNITY_EDITOR
        if (RejectUnloadForVerification?.Invoke(scene) == true)
            throw new InvalidOperationException("Injected backend unload refusal; scene remains loaded.");
#endif
        var unload = SceneManager.UnloadSceneAsync(scene);
        if (unload == null)
            throw new InvalidOperationException("Unity 没有创建卸载操作。");
        yield return unload;
        if (scene.IsValid() && scene.isLoaded)
            throw new InvalidOperationException("卸载结束后场景仍存在。");
        operation.Scene = scene;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive && initializingVisit != null && string.Equals(scene.path, pendingScenePath, StringComparison.OrdinalIgnoreCase))
        {
            // sceneLoaded runs before Start/first rendering. Stage the incoming view
            // while its initialization yields; the paused source still supplies the image.
            initializingVisit.Target = scene;
            StageIncoming(initializingVisit);
            return;
        }

        if (mode != LoadSceneMode.Single || !string.Equals(scene.path, replacementScenePath, StringComparison.OrdinalIgnoreCase))
            return;
        singleSceneActivated = true;
        // Before the new scene's Start: Battle must not inherit presentation timeScale=0.
        DiscardVisits();
    }

    private void DiscardVisits()
    {
        for (int i = visits.Count - 1; i >= 0; i--)
        {
            visits[i].IncomingUI?.Dispose();
            try
            {
                visits[i].Lifecycle.ReleaseTarget(visits[i].Target);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            try
            {
                visits[i].Suspension?.Discard();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        visits.Clear();
    }

    internal static ISceneEntryPoint FindEntry(Scene scene)
    {
        ISceneEntryPoint entry = null;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (component is ISceneEntryPoint candidate)
                {
                    if (entry != null)
                        throw new InvalidOperationException("展示场景只能包含一个场景入口。");
                    entry = candidate;
                }

        return entry;
    }

    // 独立启动或 Single 初始化失败后的退出，仍通过同一套互斥请求。
    public SceneOperation ReturnToSampleAsync()
    {
        return visits.Count > 0 ? ReplaceFromVisitAsync(CurrentVisit, SceneId.Sample) : LoadAsync(SceneId.Sample);
    }

    private static void StageIncoming(Visit visit)
    {
        visit.IncomingPresentation = new ScenePresentationVisibility(visit.Target);
        visit.IncomingPresentation.Hide();
        visit.IncomingUI = Game.UI.UIManager.Instance?.SuspendScenePresentation(visit.Target.handle);
    }

    private static void ShowIncoming(Visit visit)
    {
        if (visit.IncomingPresentation == null || !visit.IncomingPresentation.HasDisplayCamera)
            throw new InvalidOperationException("目标展示场景没有可用的屏幕相机。");
        (visit.Suspension as IScenePresentationSuspension)?.HidePresentation();
        visit.IncomingPresentation.Show();
        visit.IncomingPresentation = null;
        visit.IncomingUI?.Dispose();
        visit.IncomingUI = null;
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        DiscardVisits();
        if (UIManager.Instance != null && transitionHandle.IsValid)
            UIManager.Instance.Release(transitionHandle);
        transitionInput?.Dispose();
        transitionInput = null;
        currentOperation?.Finish("场景管理器已销毁。");
        currentOperation = null;
        Instance = null;
    }
}
