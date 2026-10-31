using System.Collections.Generic;
using Game.UI;
using UnityEngine;

/// <summary>场景只登记需要自动打开的界面 ID，实例与销毁由 UIManager 负责。</summary>
[DefaultExecutionOrder(-25)]
public sealed class SceneUIPresenter : MonoBehaviour
{
    [SerializeField] private UIId[] panels = System.Array.Empty<UIId>();

    private readonly List<UIHandle> handles = new List<UIHandle>();
    private UIManager manager;

    private void OnEnable()
    {
        manager = UIManager.Instance;
        if (manager == null)
        {
            Debug.LogError("场景 UI 入口需要先初始化 UIManager。", this);
            return;
        }

        foreach (UIId id in panels)
        {
            UIHandle handle = manager.Open(id);
            if (handle.IsValid)
                handles.Add(handle);
        }
    }

    private void OnDisable()
    {
        if (manager != null)
        {
            foreach (UIHandle handle in handles)
                manager.Close(handle);
        }
        handles.Clear();
        manager = null;
    }
}
