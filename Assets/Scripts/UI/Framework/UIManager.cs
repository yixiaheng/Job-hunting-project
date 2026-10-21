using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        public UIRegistry Registry => registry;
        public UIRoot Root => root;
        public UIInputCoordinator Input => inputCoordinator;
        public UIBackgroundController Background => backgroundController;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
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

            registry?.Rebuild();
            RefreshFrameworkState();
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            foreach (UIInstanceRecord instance in instances.Values)
                instance.Panel.StopTransition();

            Instance = null;
        }

        public UIHandle Open(UIId id, IUIContext context = null, UIHandle? parent = null)
        {
            if (!TryGetConfig(id, out UIConfig config))
                return default;

            if (config.prefab == null)
            {
                Debug.LogError($"UIConfig {id} 没有 UIPanel Prefab", config);
                return default;
            }

            UIInstanceRecord instance = GetOrCreate(config);
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
                    instance.DestroyWhenClosed = true;

                    if (instance.Panel.State == UIState.Closed && !instance.Processing)
                        DestroyInstance(instance);
                    else
                        RequestCloseTree(instance);
                }
            }
        }

        private UIInstanceRecord GetOrCreate(UIConfig config)
        {
            if (config.instanceMode == UIInstanceMode.Single
                && singleInstances.TryGetValue(config.id, out UIInstanceRecord existing))
            {
                return existing;
            }

            Transform parent = root != null
                ? root.GetLayerRoot(config.layer)
                : transform;

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

        private void StartProcessing(UIInstanceRecord instance)
        {
            if (instance.Processing)
                return;

            instance.Processing = true;
            StartCoroutine(ProcessInstance(instance));
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
                    instance.Panel.SetCovered(false);
                    yield return StartCoroutine(instance.Panel.OpenRoutine(instance.PendingContext));

                    if (instance.RequestedOpen)
                        break;

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

                yield return StartCoroutine(instance.Panel.CloseRoutine());
                UnregisterOpened(instance);

                if (instance.Config.cacheMode == UICacheMode.DestroyOnClose
                    || instance.DestroyWhenClosed)
                {
                    DestroyInstance(instance);
                    break;
                }

                break;
            }

            instance.Processing = false;
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

        private void UnregisterOpened(UIInstanceRecord instance)
        {
            openedPanels.Remove(instance);
            backgroundController.Release(instance.Handle);
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
        public bool DestroyWhenClosed { get; set; }
        public long OpenOrder { get; set; }
    }
}
