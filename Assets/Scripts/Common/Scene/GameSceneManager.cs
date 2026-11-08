using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Persistent owner of runtime scene operations and LIFO presentation visits.</summary>
public sealed class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager Instance { get; private set; }
    [SerializeField, Min(0f)] private float fadeDuration = .4f;
    private SceneCatalog catalog;
    private CanvasGroup fade;
    private GameObject recovery;
    private Action retry;
    private SceneOperation current;
    private string pendingPath, replacingPath;
    private bool singleActivated;
    private readonly List<Visit> visits = new();
    private Visit failedEntry;
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
        public object Context;
        public bool Faulted;
    }

    public SceneCatalog Catalog => catalog;
    public bool IsLoading => current != null;
    public SceneOperation CurrentOperation => current;
    public int VisitCount => visits.Count;
    public SceneVisitHandle CurrentVisit => visits.Count == 0 ? default : visits[visits.Count - 1].Handle;
    public bool HasFaultedVisit => visits.Exists(v => v.Faulted);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime()
    {
        if (Instance != null) SceneManager.sceneLoaded -= Instance.HandleSceneLoaded;
        Instance = null;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var existing = FindFirstObjectByType<GameSceneManager>();
        if (existing != null) existing.Initialize();
        else new GameObject(nameof(GameSceneManager)).AddComponent<GameSceneManager>();
    }
    private void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; Destroy(this); return; }
        Initialize();
    }
    private void Initialize()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
        catalog = Resources.Load<SceneCatalog>(SceneCatalog.ResourcePath);
        EnsureFadeOverlay();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }
    public bool TryGetLoadedScene(SceneId id, out Scene scene)
    {
        scene = default;
        if (catalog == null || !catalog.TryGetPath(id, out var path)) return false;
        scene = SceneManager.GetSceneByPath(path);
        return scene.IsValid() && scene.isLoaded;
    }
    public bool TryGetVisit(SceneId id, out SceneVisitHandle handle)
    {
        for (int i = visits.Count - 1; i >= 0; i--)
            if (visits[i].Id == id) { handle = visits[i].Handle; return true; }
        handle = default;
        return false;
    }
    public bool IsSceneSuspended(Scene scene) => visits.Exists(v => v.Source.IsValid() && v.Source == scene);
    public bool IsScenePending(Scene scene) => IsLoading && string.Equals(scene.path, pendingPath, StringComparison.OrdinalIgnoreCase);
    public bool TrySetActiveScene(Scene scene, out string error)
    {
        if (IsLoading || HasFaultedVisit) { error = "场景操作尚未结束。"; return false; }
        if (visits.Count > 0 && visits[visits.Count - 1].Target != scene)
        { error = "不能绕过当前展示会话切换活动场景。"; return false; }
        return SetActive(scene, out error);
    }
    private static bool SetActive(Scene scene, out string error)
    {
        if (!scene.IsValid() || !scene.isLoaded) { error = "目标场景尚未加载。"; return false; }
        if (SceneManager.GetActiveScene() == scene) { error = null; return true; }
        if (!SceneManager.SetActiveScene(scene)) { error = $"设置活动场景失败：{scene.path} (handle={scene.handle}, active={SceneManager.GetActiveScene().path})。"; return false; }
        error = null; return true;
    }
    private bool Validate(SceneId id, bool allowVisit, out string error)
    {
        if (!isActiveAndEnabled) error = "场景管理器尚未启用。";
        else if (IsLoading) error = "场景操作正在进行，请勿重复请求。";
        else if (HasFaultedVisit) error = "请先重试清理失败的场景会话。";
        else if (!allowVisit && visits.Count > 0) error = "请先返回，或使用当前会话的替换接口。";
        else if (catalog == null || !catalog.TryGetPath(id, out var path)) error = $"SceneCatalog 未正确登记：{id}。";
        else if (!Application.CanStreamedLevelBeLoaded(path)) error = $"场景未加入当前构建配置：{path}";
        else { error = null; return true; }
        return false;
    }
    public bool CanLoad(SceneId id, out string error) => Validate(id, false, out error);
    /// <summary>Compatibility: true means accepted, not completed.</summary>
    public bool TryLoad(SceneId id, out string error, SceneTransitionMode mode = SceneTransitionMode.Immediate)
    {
        var op = LoadAsync(id, new SceneLoadOptions { Transition = mode });
        error = op.Error;
        op.OnCompleted(r => { if (r.Accepted && !r.Succeeded) Debug.LogError(r.Error); });
        return op.Accepted;
    }
    public SceneOperation LoadAsync(SceneId id, SceneLoadOptions options = null)
    {
        var settings = options?.Copy() ?? new SceneLoadOptions();
        if (!ValidOptions(settings, out var error) || !Validate(id, false, out error)) return Reject(error);
        if (settings.LoadMode == LoadSceneMode.Additive && TryGetLoadedScene(id, out _)) return Reject("目标场景已经加载。");
        return Begin(op => LoadRoutine(op, id, settings), settings);
    }
    public SceneOperation EnterVisitAsync(SceneId id, ISceneVisitLifecycle lifecycle, object context = null,
        SceneTransitionMode transition = SceneTransitionMode.Immediate)
    {
        if (lifecycle == null) return Reject("缺少场景生命周期适配器。");
        var settings = new SceneLoadOptions { LoadMode = LoadSceneMode.Additive, Context = context, Transition = transition };
        if (!ValidOptions(settings, out var error) || !Validate(id, true, out error)) return Reject(error);
        if (TryGetVisit(id, out _) || TryGetLoadedScene(id, out _)) return Reject("目标展示场景已经存在。");
        var visit = new Visit { Id = id, Source = SceneManager.GetActiveScene(), Lifecycle = lifecycle, Context = context };
        return Begin(op => EnterRoutine(op, visit), settings, op =>
        {
            // Capture before input/fade can advance the movement state machine.
            visits.Add(visit); failedEntry = visit; op.Visit = visit.Handle;
            visit.Suspension = visit.Lifecycle.SuspendSource(visit.Source);
        });
    }
    public SceneOperation AdoptDirectScene(Scene scene, ISceneVisitLifecycle lifecycle, object context = null)
    {
        if (IsScenePending(scene)) return current;
        if (IsLoading || HasFaultedVisit || lifecycle == null || !scene.IsValid() || !scene.isLoaded
            || catalog == null || !catalog.TryGetId(scene.path, out var id)) return Reject("无法接管直接启动的展示场景。");
        if (TryGetVisit(id, out var handle))
        { var ready = new SceneOperation { Scene = scene, Visit = handle }; ready.Finish(); return ready; }
        return Begin(op => AdoptRoutine(op, id, scene, lifecycle, context), new SceneLoadOptions());
    }
    public SceneOperation ReturnAsync(SceneVisitHandle handle)
    {
        if (!GetTop(handle, out var visit, out var error)) return Reject(error);
        if (!visit.Source.IsValid() || !visit.Source.isLoaded) return ReplaceFromVisitAsync(handle, SceneId.Sample);
        return Begin(op => ReturnRoutine(op, visit), new SceneLoadOptions());
    }
    public SceneOperation ReplaceFromVisitAsync(SceneVisitHandle handle, SceneId id, SceneLoadOptions options = null)
    {
        if (!GetTop(handle, out _, out var error)) return Reject(error);
        var settings = options?.Copy() ?? new SceneLoadOptions();
        settings.LoadMode = LoadSceneMode.Single;
        if (!ValidOptions(settings, out error)) return Reject(error);
        if (catalog == null || !catalog.TryGetPath(id, out var path) || !Application.CanStreamedLevelBeLoaded(path))
            return Reject($"场景 {id} 未正确登记到构建配置。");
        return Begin(op => LoadRoutine(op, id, settings), settings);
    }
    public SceneOperation UnloadAsync(Scene scene)
    {
        if (IsLoading || visits.Count > 0) return Reject("存在场景操作或展示会话，请使用 ReturnAsync。");
        if (!scene.IsValid() || !scene.isLoaded || SceneManager.sceneCount <= 1) return Reject("无法卸载无效场景或最后一个场景。");
        return Begin(op => UnloadCore(op, scene), new SceneLoadOptions());
    }
    private bool GetTop(SceneVisitHandle handle, out Visit visit, out string error)
    {
        visit = visits.Count > 0 ? visits[visits.Count - 1] : null;
        if (IsLoading) error = "场景操作正在进行。";
        else if (visit == null || !handle.IsValid || !visit.Handle.Equals(handle)) error = "会话已失效或不是最上层会话。";
        else { error = null; return true; }
        return false;
    }
    private static bool ValidOptions(SceneLoadOptions options, out string error)
    {
        error = !Enum.IsDefined(typeof(LoadSceneMode), options.LoadMode)
            || !Enum.IsDefined(typeof(SceneTransitionMode), options.Transition) ? "无效的加载或过渡模式。" : null;
        return error == null;
    }
    private static SceneOperation Reject(string error)
    { var op = new SceneOperation(); op.Finish(error ?? "请求被拒绝。"); return op; }
    private SceneOperation Begin(Func<SceneOperation, IEnumerator> body, SceneLoadOptions options, Action<SceneOperation> beforeFade = null)
    {
        var op = new SceneOperation { Accepted = true };
        current = op; singleActivated = false; failedEntry = null;
        HideRecovery(); fade.blocksRaycasts = true;
        StartCoroutine(Execute(op, body, options, beforeFade));
        return op;
    }
    private IEnumerator Execute(SceneOperation op, Func<SceneOperation, IEnumerator> body, SceneLoadOptions options, Action<SceneOperation> beforeFade)
    {
        string failure = null;
        bool prepared = false;
        yield return Guard(Work(), e => failure = e);
        if (failure != null)
        {
            if (failedEntry != null)
            {
                var incomplete = failedEntry;
                string cleanupError = null;
                yield return Guard(CleanupEntry(op, incomplete), e => cleanupError = e);
                if (cleanupError != null)
                {
                    incomplete.Faulted = true;
                    failure += "\n清理失败：" + cleanupError;
                    ShowRecovery(failure, () => ReturnAsync(incomplete.Handle));
                }
            }
            if (prepared && !singleActivated)
            {
                try { options.Rollback?.Invoke(); } catch (Exception e) { failure += "\n业务恢复失败：" + e.Message; }
            }
            if (singleActivated)
            {
                failure += "\n新场景已激活，旧场景不能自动回滚。";
                if (!recovery.activeSelf && visits.Count == 0)
                    ShowRecovery(failure, () => LoadAsync(SceneId.Sample));
            }
            op.Scene = SceneManager.GetActiveScene();
            var remaining = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                remaining.Add(scene.path + (scene == op.Scene ? " [active]" : ""));
            }
            failure += "\n当前驻留场景：" + string.Join(", ", remaining);
        }
        failedEntry = null; pendingPath = replacingPath = null;
        current = null;
        if (!recovery.activeSelf)
        { fade.alpha = 0f; fade.blocksRaycasts = false; transitionInput?.Dispose(); transitionInput = null; }
        op.Finish(failure);

        IEnumerator Work()
        {
            beforeFade?.Invoke(op);
            transitionInput ??= Game.UI.UIManager.Instance?.AcquireTransitionInput();
            if (options.Transition == SceneTransitionMode.Fade)
            { op.Stage = SceneOperationStage.FadingOut; yield return FadeTo(1f); }
            prepared = true;
            options.Prepare?.Invoke();
            yield return body(op);
            if (options.Transition == SceneTransitionMode.Fade)
            { op.Stage = SceneOperationStage.FadingIn; yield return FadeTo(0f); }
        }
    }
    // Drive nested iterators ourselves: errors must return through the same cleanup path.
    private static IEnumerator Guard(IEnumerator routine, Action<string> fail)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(routine);
        try
        {
            while (stack.Count > 0)
            {
                object next = null; bool moved = false; Exception failure = null;
                try { moved = stack.Peek().MoveNext(); if (moved) next = stack.Peek().Current; }
                catch (Exception e) { failure = e; }
                if (failure != null) { fail(failure.ToString()); yield break; }
                if (!moved)
                {
                    try { (stack.Pop() as IDisposable)?.Dispose(); }
                    catch (Exception e) { failure = e; }
                    if (failure != null) { fail(failure.ToString()); yield break; }
                    continue;
                }
                if (next is IEnumerator nested) stack.Push(nested); else yield return next;
            }
        }
        finally
        {
            while (stack.Count > 0)
            { try { (stack.Pop() as IDisposable)?.Dispose(); } catch (Exception e) { fail(e.ToString()); } }
        }
    }
    private IEnumerator LoadCore(SceneOperation op, SceneId id, LoadSceneMode mode, bool setActive)
    {
        if (!catalog.TryGetPath(id, out var path)) throw new InvalidOperationException("场景登记已改变。");
        pendingPath = path;
        if (mode == LoadSceneMode.Single) replacingPath = path;
        op.Stage = SceneOperationStage.Loading;
        var loading = SceneManager.LoadSceneAsync(path, mode);
        if (loading == null) throw new InvalidOperationException("Unity 没有创建加载操作。");
        while (!loading.isDone) { op.Progress = Mathf.Clamp01(loading.progress / .9f) * .8f; yield return null; }
        var scene = SceneManager.GetSceneByPath(path);
        if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("加载结束但目标场景不存在。");
        op.Scene = scene;
        if (setActive && !SetActive(scene, out var error)) throw new InvalidOperationException(error);
    }
    private IEnumerator LoadRoutine(SceneOperation op, SceneId id, SceneLoadOptions options)
    {
        yield return LoadCore(op, id, options.LoadMode, options.SetActive);
        // Raw Additive is a scene operation only. Use EnterVisitAsync to apply a
        // presentation lifecycle, including source suspension and UI initialization.
        if (options.LoadMode == LoadSceneMode.Additive) yield break;
        var entry = FindEntry(op.Scene);
        if (entry != null && entry.VisitLifecycle != null)
        {
            yield return AdoptRoutine(op, id, op.Scene, entry.VisitLifecycle, options.Context);
        }
        else if (entry != null)
        { op.Stage = SceneOperationStage.Initializing; yield return entry.InitializeScene(default, options.Context); }
    }
    private IEnumerator AdoptRoutine(SceneOperation op, SceneId id, Scene target, ISceneVisitLifecycle lifecycle, object context)
    {
        var visit = new Visit { Id = id, Target = target, Lifecycle = lifecycle, Context = context };
        visits.Add(visit); failedEntry = visit;
        op.Scene = target; op.Visit = visit.Handle; pendingPath = target.path;
        visit.Suspension = lifecycle.SuspendSource(default);
        op.Stage = SceneOperationStage.Initializing;
        yield return lifecycle.InitializeTarget(target, visit.Handle, context);
        failedEntry = null;
    }
    private IEnumerator EnterRoutine(SceneOperation op, Visit visit)
    {
        yield return LoadCore(op, visit.Id, LoadSceneMode.Additive, true);
        visit.Target = op.Scene;
        op.Stage = SceneOperationStage.Initializing;
        yield return visit.Lifecycle.InitializeTarget(visit.Target, visit.Handle, visit.Context);
        // Loaded is not ready: only hand over after the controller and required UI finish.
        if (visit.IncomingPresentation == null || !visit.IncomingPresentation.HasDisplayCamera)
            throw new InvalidOperationException("目标展示场景没有可用的屏幕相机。");
        (visit.Suspension as IScenePresentationSuspension)?.HidePresentation();
        visit.IncomingPresentation.Show();
        visit.IncomingPresentation = null;
        failedEntry = null;
    }
    private IEnumerator CleanupEntry(SceneOperation op, Visit visit)
    {
        if (!visit.Target.IsValid() && TryGetLoadedScene(visit.Id, out var found)) visit.Target = found;
        if (visit.Source.IsValid() && visit.Source.isLoaded && !SetActive(visit.Source, out var error))
            throw new InvalidOperationException(error);
        (visit.Suspension as IScenePresentationSuspension)?.ShowPresentation();
        if (visit.Target.IsValid() && visit.Target.isLoaded)
        {
            if (SceneManager.sceneCount <= 1) throw new InvalidOperationException("直接启动场景初始化失败，需要返回 Sample。");
            yield return UnloadCore(op, visit.Target);
        }
        visit.Lifecycle.ReleaseTarget(visit.Target);
        visit.IncomingPresentation = null;
        visit.Suspension?.Resume();
        visits.Remove(visit);
    }
    private IEnumerator ReturnRoutine(SceneOperation op, Visit visit)
    {
        op.Visit = visit.Handle;
        IDisposable targetHold = null; bool completed = false;
        try
        {
            targetHold = visit.Lifecycle.SuspendTarget(visit.Target);
            if (!SetActive(visit.Source, out var error)) throw new InvalidOperationException(error);
            // Source gameplay stays paused, but its camera must cover the asynchronous unload.
            (visit.Suspension as IScenePresentationSuspension)?.ShowPresentation();
            (targetHold as IScenePresentationSuspension)?.HidePresentation();
            if (visit.Target.IsValid() && visit.Target.isLoaded) yield return UnloadCore(op, visit.Target);
            targetHold?.Dispose(); targetHold = null;
            visit.Lifecycle.ReleaseTarget(visit.Target);
            op.Stage = SceneOperationStage.Restoring;
            visit.Suspension?.Resume();
            visits.Remove(visit);
            op.Scene = visit.Source;
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
                    if (visit.Faulted) ShowRecovery("Unable to unload scene. Retry cleanup.", () => ReturnAsync(visit.Handle));
                }
                else if (!completed)
                {
                    visit.Faulted = true;
                    ShowRecovery("Source restoration is incomplete. Retry cleanup.", () => ReturnAsync(visit.Handle));
                }
            }
        }
    }
    private IEnumerator UnloadCore(SceneOperation op, Scene scene)
    {
        op.Stage = SceneOperationStage.Unloading;
        if (SceneManager.sceneCount <= 1) throw new InvalidOperationException("不能卸载最后一个场景。");
#if UNITY_EDITOR
        if (RejectUnloadForVerification?.Invoke(scene) == true)
            throw new InvalidOperationException("Injected backend unload refusal; scene remains loaded.");
#endif
        var unload = SceneManager.UnloadSceneAsync(scene);
        if (unload == null) throw new InvalidOperationException("Unity 没有创建卸载操作。");
        yield return unload;
        if (scene.IsValid() && scene.isLoaded) throw new InvalidOperationException("卸载结束后场景仍存在。");
        op.Scene = scene;
    }
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive && failedEntry != null
            && string.Equals(scene.path, pendingPath, StringComparison.OrdinalIgnoreCase))
        {
            // sceneLoaded runs before Start/first rendering. Stage the incoming view
            // while its initialization yields; the paused source still supplies the image.
            failedEntry.Target = scene;
            failedEntry.IncomingPresentation = new ScenePresentationVisibility(scene);
            failedEntry.IncomingPresentation.Hide();
            return;
        }
        if (mode != LoadSceneMode.Single || !string.Equals(scene.path, replacingPath, StringComparison.OrdinalIgnoreCase)) return;
        singleActivated = true;
        // Before the new scene's Start: Battle must not inherit presentation timeScale=0.
        DiscardVisits();
    }
    private void DiscardVisits()
    {
        for (int i = visits.Count - 1; i >= 0; i--)
        {
            try { visits[i].Lifecycle.ReleaseTarget(visits[i].Target); } catch (Exception e) { Debug.LogException(e); }
            try { visits[i].Suspension?.Discard(); } catch (Exception e) { Debug.LogException(e); }
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
                    if (entry != null) throw new InvalidOperationException("展示场景只能包含一个场景入口。");
                    entry = candidate;
                }
        return entry;
    }
    private IEnumerator FadeTo(float target)
    {
        float start = fade.alpha;
        for (float elapsed = 0; elapsed < fadeDuration; elapsed += Time.unscaledDeltaTime)
        { fade.alpha = Mathf.Lerp(start, target, elapsed / fadeDuration); yield return null; }
        fade.alpha = target;
    }
    private void EnsureFadeOverlay()
    {
        if (fade != null) return;
        var overlay = new GameObject("SceneTransition", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
        overlay.layer = 5; overlay.transform.SetParent(transform, false);
        var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = short.MaxValue;
        fade = overlay.GetComponent<CanvasGroup>(); fade.alpha = 0; fade.blocksRaycasts = false;
        var background = new GameObject("FadeBackground", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(overlay.transform, false);
        var rect = (RectTransform)background.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        background.GetComponent<Image>().color = Color.black;
        recovery = new GameObject("Recovery", typeof(RectTransform)); recovery.transform.SetParent(overlay.transform, false);
        var label = new GameObject("Error", typeof(RectTransform), typeof(Text)); label.transform.SetParent(recovery.transform, false);
        var text = label.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 20; text.alignment = TextAnchor.MiddleCenter; text.rectTransform.sizeDelta = new Vector2(800, 180);
        text.text = "Scene initialization/cleanup failed.\nRetry returning to the source scene.\nSee Console for details.";
        var button = new GameObject("Retry", typeof(RectTransform), typeof(Image), typeof(Button));
        button.transform.SetParent(recovery.transform, false);
        var br = (RectTransform)button.transform; br.sizeDelta = new Vector2(220, 48); br.anchoredPosition = new Vector2(0, -130);
        var caption = new GameObject("Label", typeof(RectTransform), typeof(Text)); caption.transform.SetParent(button.transform, false);
        var ct = caption.GetComponent<Text>(); ct.font = text.font; ct.text = "Retry / Return"; ct.color = Color.black;
        ct.alignment = TextAnchor.MiddleCenter; ct.rectTransform.sizeDelta = br.sizeDelta;
        button.GetComponent<Button>().onClick.AddListener(() => { if (!IsLoading) retry?.Invoke(); });
        recovery.SetActive(false);
    }
    private void ShowRecovery(string error, Action action)
    { Debug.LogError(error); retry = action; recovery.SetActive(true); fade.alpha = 1; fade.blocksRaycasts = true; }
    private void HideRecovery() { retry = null; if (recovery != null) recovery.SetActive(false); }
    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        DiscardVisits(); transitionInput?.Dispose(); transitionInput = null; current?.Finish("场景管理器已销毁。");
        current = null; Instance = null;
    }
}
