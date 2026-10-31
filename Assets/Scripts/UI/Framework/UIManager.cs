using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game.UI
{
    //[DefaultExecutionOrder(-100)] 是 Unity 中的一个特性（Attribute）
    //用来指定该脚本相对于其他 MonoBehaviour 脚本的执行优先级。
    //数值 -100 意味着它比大多数默认脚本（默认值为 0）更早执行。
    [DefaultExecutionOrder(-100)]
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Framework References")]
        [SerializeField] private UIRegistry registry;   
        [SerializeField] private UIRoot root;
        [SerializeField] private UIBackgroundController backgroundController;
        [SerializeField] private UIBlurManager blurService;
        [SerializeField] private bool persistAcrossScenes = true;

        //没有被销毁的窗口实例
        private readonly Dictionary<int, UIInstanceRecord> instances = new();
        //单个窗口实例
        private readonly Dictionary<UIId, UIInstanceRecord> singleInstances = new();
        //当前打开的窗口，以及先后顺序
        private readonly List<UIInstanceRecord> openedPanels = new();


        private UIInputCoordinator inputCoordinator = new();
        private int nextInstanceId = 1;
        private long openOrder;
        private bool cursorOverridden;
        private bool previousCursorVisible;
        private CursorLockMode previousCursorLock;
        private Transform worldRoot;

        public UIRegistry Registry => registry;
        public UIRoot Root => root;
        public UIInputCoordinator Input => inputCoordinator;
        public UIBackgroundController Background => backgroundController;
        public UIBlurManager Blur => blurService;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                gameObject.SetActive(false);
                if (root != null && root != Instance.root)
                    root.gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (persistAcrossScenes)
                DontDestroyOnLoad(gameObject);

            if (root != null && root.transform.parent != transform)
                root.transform.SetParent(transform, false);
            
            if (backgroundController == null)
                backgroundController = gameObject.AddComponent<UIBackgroundController>();

            if (blurService == null)
                blurService = GetComponentInChildren<UIBlurManager>(true);
            if (blurService != null)
                blurService.Initialize(backgroundController);

            registry?.Rebuild();
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            RefreshFrameworkState();
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Back();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                Back();
#endif

            if (inputCoordinator.Current.ShowCursor)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            ReleaseSceneUI(scene.handle);
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            StopAllCoroutines();
            UIInstanceRecord[] copy = new List<UIInstanceRecord>(instances.Values).ToArray();
            foreach (UIInstanceRecord instance in copy)
            {
                if (instance.Panel != null)
                    instance.Panel.ForceCloseImmediate();
                UnregisterOpened(instance);
            }

            instances.Clear();
            singleInstances.Clear();
            RefreshFrameworkState();

            Instance = null;
        }

        /// <summary>
        /// 打开UI
        /// </summary>
        /// <param name="id"> 哪个界面 </param>
        /// <param name="context"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public UIHandle Open(UIId id, IUIContext context = null, UIHandle? parent = null)
        {
            //查找UIConfig
            if (!TryGetConfig(id, out UIConfig config))
                return default;

            if (config.prefab == null)
            {
                Debug.LogError($"UIConfig {id} 没有 UIPanel Prefab", config);
                return default;
            }

            //有就取出，没有就创建
            UIInstanceRecord instance = GetOrCreate(config);
            //设置父节点
            SetParent(instance, parent);
            
            instance.RequestedOpen = true;
            instance.PendingContext = context;

            if (instance.Panel.State == UIState.Open)
            {
                instance.Panel.UpdateContext(context);
                BringToFront(instance);
            }

            StartProcessing(instance);
            return instance.Handle;
        }

        //关闭UI
        public bool Close(UIHandle handle)
        {
            if (!TryGetInstance(handle, out UIInstanceRecord instance))
                return false;

            RequestCloseTree(instance);
            return true;
        }

        public bool Close(UIId id)
        {
            if (!singleInstances.TryGetValue(id, out UIInstanceRecord instance))
                return false;

            return Close(instance.Handle);
        }

        public bool IsOpen(UIId id)
        {
            for (int index = 0; index < openedPanels.Count; index++)
            {
                if (openedPanels[index].Config.id == id)
                    return true;
            }

            return false;
        }

        public bool TryGetPanel<T>(UIHandle handle, out T panel) where T : UIPanel
        {
            if (TryGetInstance(handle, out UIInstanceRecord instance)
                && instance.Panel is T typedPanel)
            {
                panel = typedPanel;
                return true;
            }

            panel = null;
            return false;
        }

        public bool Back()
        {
            for (int index = openedPanels.Count - 1; index >= 0; index--)
            {
                UIInstanceRecord instance = openedPanels[index];
                if (!instance.Config.handlesBack || !instance.Panel.CanCloseFromBack())
                    continue;

                Close(instance.Handle);
                return true;
            }

            return false;
        }

        public void CloseAll()
        {
            UIInstanceRecord[] copy = openedPanels.ToArray();
            for (int index = copy.Length - 1; index >= 0; index--)
                RequestCloseTree(copy[index]);
        }

        public void ReleaseSceneUI(int sceneHandle = -1)
        {
            int targetSceneHandle = sceneHandle < 0
                ? SceneManager.GetActiveScene().handle
                : sceneHandle;

            UIInstanceRecord[] copy = new List<UIInstanceRecord>(instances.Values).ToArray();
            for (int index = 0; index < copy.Length; index++)
            {
                UIInstanceRecord instance = copy[index];
                if (instance.Config.lifetime == UILifetime.Scene
                    && instance.OwnerSceneHandle == targetSceneHandle)
                {
                    ReleaseInstanceImmediately(instance);
                }
            }

            RefreshFrameworkState();
        }

        private void ReleaseInstanceImmediately(UIInstanceRecord instance)
        {
            if (!instances.ContainsKey(instance.Handle.Value))
                return;

            foreach (UIInstanceRecord child in instance.Children.ToArray())
                ReleaseInstanceImmediately(child);

            if (instance.Routine != null)
                StopCoroutine(instance.Routine);

            instance.Processing = false;
            instance.RequestedOpen = false;
            instance.Panel.ForceCloseImmediate();
            UnregisterOpened(instance);
            DestroyInstance(instance);
        }

        private UIInstanceRecord GetOrCreate(UIConfig config)
        {
            if (config.instanceMode == UIInstanceMode.Single
                && singleInstances.TryGetValue(config.id, out UIInstanceRecord existing))
            {
                return existing;
            }

            Transform parent = GetPanelRoot(config);

            UIPanel panel = Instantiate(config.prefab, parent, false);
            panel.gameObject.SetActive(false);

            UIHandle handle = new(nextInstanceId++);
            UIInstanceRecord instance = new(handle, config, panel, SceneManager.GetActiveScene().handle);
            instances.Add(handle.Value, instance);

            if (config.instanceMode == UIInstanceMode.Single)
                singleInstances.Add(config.id, instance);

            panel.Initialize(this, handle, config);
            return instance;
        }

        private Transform GetPanelRoot(UIConfig config)
        {
            if (config.space == UISpace.Screen)
                return root != null ? root.GetLayerRoot(config.layer) : transform;

            if (worldRoot == null)
            {
                worldRoot = new GameObject("WorldUIRoot").transform;
                worldRoot.SetParent(transform, false);
                worldRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                worldRoot.localScale = Vector3.one;
            }
            return worldRoot;
        }

        private void StartProcessing(UIInstanceRecord instance)
        {
            if (instance.Processing)
                return;

            instance.Processing = true;
            Coroutine routine = StartCoroutine(ProcessInstance(instance));
            instance.Routine = instance.Processing ? routine : null;
        }

        private IEnumerator ProcessInstance(UIInstanceRecord instance)
        {
            while (true)
            {
                if (instance.RequestedOpen)
                {
                    if (instance.Panel.State == UIState.Open)
                        break;

                    RegisterOpened(instance);
                    yield return instance.Panel.OpenRoutine(instance.PendingContext);

                    if (instance.RequestedOpen)
                    {
                        // 打开动画期间也可能收到下一行对话或新的交互目标。
                        if (!ReferenceEquals(instance.Panel.Context, instance.PendingContext))
                            instance.Panel.UpdateContext(instance.PendingContext);
                        break;
                    }

                    continue;
                }

                if (instance.Panel.State == UIState.Closed)
                    break;

                UIInstanceRecord[] children = instance.Children.ToArray();
                for (int index = 0; index < children.Length; index++)
                    RequestCloseTree(children[index]);

                bool childIsStillClosing = false;
                for (int index = 0; index < children.Length; index++)
                {
                    UIInstanceRecord child = children[index];
                    if (instances.ContainsKey(child.Handle.Value)
                        && (child.Processing || child.Panel.State != UIState.Closed))
                    {
                        childIsStillClosing = true;
                        break;
                    }
                }

                if (childIsStillClosing)
                {
                    yield return null;
                    continue;
                }

                yield return instance.Panel.CloseRoutine();
                UnregisterOpened(instance, !instance.RequestedOpen);

                if (instance.RequestedOpen)
                    continue;

                if (instance.Config.cacheMode == UICacheMode.DestroyOnClose)
                {
                    DestroyInstance(instance);
                    break;
                }

                break;
            }

            instance.Processing = false;
            instance.Routine = null;
            RefreshFrameworkState();
        }

        private void RegisterOpened(UIInstanceRecord instance)
        {
            if (openedPanels.Contains(instance))
                return;

            openedPanels.Add(instance);
            instance.OpenOrder = ++openOrder;
            backgroundController.Acquire(instance.Handle, instance.Config);
            BringToFront(instance);
            RefreshFrameworkState();
        }

        private void UnregisterOpened(UIInstanceRecord instance, bool detachFromParent = true)
        {
            openedPanels.Remove(instance);
            backgroundController.Release(instance.Handle);
            if (detachFromParent)
                RemoveFromParent(instance);
        }

        private void RequestCloseTree(UIInstanceRecord instance)
        {
            UIInstanceRecord[] children = instance.Children.ToArray();
            for (int index = 0; index < children.Length; index++)
                RequestCloseTree(children[index]);

            instance.RequestedOpen = false;
            StartProcessing(instance);
        }

        private void SetParent(UIInstanceRecord instance, UIHandle? parentHandle)
        {
            RemoveFromParent(instance);

            if (!parentHandle.HasValue || !parentHandle.Value.IsValid)
                return;

            if (!TryGetInstance(parentHandle.Value, out UIInstanceRecord parent))
            {
                Debug.LogWarning($"找不到 UI 父窗口：{parentHandle.Value}");
                return;
            }

            instance.Parent = parent;
            if (!parent.Children.Contains(instance))
                parent.Children.Add(instance);
        }

        private void RemoveFromParent(UIInstanceRecord instance)
        {
            if (instance.Parent == null)
                return;

            instance.Parent.Children.Remove(instance);
            instance.Parent = null;
        }

        private void BringToFront(UIInstanceRecord instance)
        {
            instance.Panel.transform.SetAsLastSibling();

            if (openedPanels.Remove(instance))
                openedPanels.Add(instance);

            instance.OpenOrder = ++openOrder;
        }

        private void RefreshFrameworkState()
        {
            RefreshCoveredState();
            inputCoordinator.Refresh(openedPanels);
            RefreshCursorState();
        }

        private void RefreshCursorState()
        {
            if (inputCoordinator.Current.ShowCursor)
            {
                if (!cursorOverridden)
                {
                    previousCursorVisible = Cursor.visible;
                    previousCursorLock = Cursor.lockState;
                    cursorOverridden = true;
                }

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (cursorOverridden)
            {
                Cursor.lockState = previousCursorLock;
                Cursor.visible = previousCursorVisible;
                cursorOverridden = false;
            }
        }

        private void RefreshCoveredState()
        {
            for (int lowerIndex = 0; lowerIndex < openedPanels.Count; lowerIndex++)
            {
                bool covered = false;
                for (int higherIndex = lowerIndex + 1; higherIndex < openedPanels.Count; higherIndex++)
                {
                    UIConfig higherConfig = openedPanels[higherIndex].Config;
                    if (higherConfig.modal || higherConfig.hideLowerWindows)
                    {
                        covered = true;
                        break;
                    }
                }

                openedPanels[lowerIndex].Panel.SetCovered(covered);
            }
        }

        private void RemoveInstance(UIInstanceRecord instance)
        {
            instances.Remove(instance.Handle.Value);
            if (singleInstances.TryGetValue(instance.Config.id, out UIInstanceRecord single)
                && single == instance)
            {
                singleInstances.Remove(instance.Config.id);
            }

            openedPanels.Remove(instance);
            RemoveFromParent(instance);
        }

        private void DestroyInstance(UIInstanceRecord instance)
        {
            RemoveInstance(instance);
            instance.Panel.MarkDestroyed();
            Destroy(instance.Panel.gameObject);
        }

        private bool TryGetConfig(UIId id, out UIConfig config)
        {
            if (registry == null)
            {
                Debug.LogError("UIManager 没有配置 UIRegistry", this);
                config = null;
                return false;
            }

            if (registry.TryGet(id, out config))
                return true;

            Debug.LogError($"UIRegistry 中找不到 UIId：{id}", this);
            return false;
        }

        private bool TryGetInstance(UIHandle handle, out UIInstanceRecord instance)
        {
            if (handle.IsValid && instances.TryGetValue(handle.Value, out instance))
                return true;

            instance = null;
            return false;
        }
    }

    /// <summary>
    /// UI实例的记录，内部有基本单元的各类信息
    /// </summary>
    internal sealed class UIInstanceRecord
    {
        public UIInstanceRecord(UIHandle handle, UIConfig config, UIPanel panel, int ownerSceneHandle)
        {
            Handle = handle;
            Config = config;
            Panel = panel;
            OwnerSceneHandle = ownerSceneHandle;
        }

        public UIHandle Handle { get; }
        public UIConfig Config { get; }
        public UIPanel Panel { get; }
        public int OwnerSceneHandle { get; }
        public UIInstanceRecord Parent { get; set; }
        public List<UIInstanceRecord> Children { get; } = new();
        public IUIContext PendingContext { get; set; }
        public bool RequestedOpen { get; set; }
        public bool Processing { get; set; }
        public Coroutine Routine { get; set; }
        public long OpenOrder { get; set; }
    }
}
