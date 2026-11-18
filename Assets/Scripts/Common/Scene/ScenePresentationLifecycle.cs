using System;
using System.Collections;
using Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Presentation policy shared by character/party adapters; the loader has no gameplay dependency.</summary>
public sealed class ScenePresentationLifecycle<T> : ISceneVisitLifecycle where T : MonoBehaviour, ISceneEntryPoint
{
    private readonly int previewMask;
    public ScenePresentationLifecycle(int previewMask)
    {
        this.previewMask = previewMask;
    }

    public ISceneSuspension SuspendSource(Scene source) => new ScenePresentationScope(source, true, previewMask);
    public IDisposable SuspendTarget(Scene target) => new ScenePresentationScope(target, false);
    public void ReleaseTarget(Scene target)
    {
        // Scene.handle remains usable after unloading; never substitute the current scene.
        if (target.handle != 0)
            UIManager.Instance?.ReleaseSceneUI(target.handle);
    }

    public IEnumerator InitializeTarget(Scene target, SceneVisitHandle visit, object context)
    {
        if (!(GameSceneManager.FindEntry(target) is T entry))
            throw new InvalidOperationException($"场景缺少有效的 {typeof(T).Name}。");
        yield return entry.InitializeScene(visit, context);
    }

    public static IEnumerator WaitForPanel(UIManager ui, UIHandle handle)
    {
        float until = Time.realtimeSinceStartup + 15f;
        while (ui != null && ui.TryGetPanel<UIPanel>(handle, out var panel))
        {
            if (panel.State == UIState.Open)
                yield break;
            if (panel.State == UIState.Closed || panel.State == UIState.Destroyed)
                break;
            if (Time.realtimeSinceStartup >= until)
                throw new TimeoutException("场景 UI 初始化超时。");
            yield return null;
        }

        throw new InvalidOperationException("场景 UI 未成功打开。");
    }
}
