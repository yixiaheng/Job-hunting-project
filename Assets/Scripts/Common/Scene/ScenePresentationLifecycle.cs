using System;
using System.Collections;
using System.Collections.Generic;
using Game.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>Presentation policy shared by character/party adapters; the loader has no gameplay dependency.</summary>
public sealed class ScenePresentationLifecycle<T> : ISceneVisitLifecycle where T : MonoBehaviour, ISceneEntryPoint
{
    public ISceneSuspension SuspendSource(Scene source) => new ScenePresentationScope(source, true);
    public IDisposable SuspendTarget(Scene target) => new ScenePresentationScope(target, false);
    public void ReleaseTarget(Scene target)
    {
        // Scene.handle remains usable after unloading; never substitute the current scene.
        if (target.handle != 0) UIManager.Instance?.ReleaseSceneUI(target.handle);
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
            if (panel.State == UIState.Open) yield break;
            if (panel.State == UIState.Closed || panel.State == UIState.Destroyed) break;
            if (Time.realtimeSinceStartup >= until) throw new TimeoutException("场景 UI 初始化超时。");
            yield return null;
        }
        throw new InvalidOperationException("场景 UI 未成功打开。");
    }
}

/// <summary>Captures only this scene's components; Resume/Discard are idempotent and nested-safe.</summary>
internal sealed class ScenePresentationScope : IScenePresentationSuspension, IDisposable
{
    private readonly float time;
    private readonly bool ownsTime;
    private readonly List<(Behaviour component, bool enabled)> held = new();
    private readonly List<(PlayerInputHandler player, List<InputAction> enabled)> inputs = new();
    private readonly List<PlayerController> controllers = new();
    private readonly List<(Camera camera, int mask)> cameraMasks = new();
    private readonly List<(Animator animator, bool keepState)> animatorStates = new();
    private ScenePresentationVisibility presentation;
    private IDisposable ui;
    private bool released;

    public ScenePresentationScope(Scene source, bool pauseTime)
    {
        time = Time.timeScale;
        ownsTime = pauseTime;
        try
        {
            if (source.IsValid() && source.isLoaded)
            {
                // Keep the source rendering until the destination has finished initializing.
                presentation = new ScenePresentationVisibility(source);
                // Acquire gameplay restrictions before UI suspension releases any existing UI restrictions.
                foreach (var root in source.GetRootGameObjects())
                {
                    foreach (var controller in root.GetComponentsInChildren<PlayerController>(true))
                    { controllers.Add(controller); controller.SetScenePaused(this, true); }
                    foreach (var player in root.GetComponentsInChildren<PlayerInputHandler>(true))
                    {
                        var enabledActions = new List<InputAction>();
                        if (player.inputActions != null)
                        {
                            foreach (var action in player.inputActions.asset) if (action.enabled) enabledActions.Add(action);
                            inputs.Add((player, enabledActions));
                            player.SetInputRestriction(this, true, true, false);
                            player.inputActions.Disable();
                        }
                    }
                    foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                        cameraMasks.Add((camera, camera.cullingMask));
                    foreach (var animator in root.GetComponentsInChildren<Animator>(true))
                    {
                        animatorStates.Add((animator, animator.keepAnimatorStateOnDisable));
                        animator.keepAnimatorStateOnDisable = true;
                        Hold(animator);
                    }
                }
                ui = pauseTime ? UIManager.Instance?.SuspendPresentation() : UIManager.Instance?.SuspendScenePresentation(source.handle);
            }
            if (ownsTime) Time.timeScale = 0f;
        }
        catch { Resume(); throw; }
    }
    private void Hold(Behaviour component)
    { held.Add((component, component.enabled)); component.enabled = false; }
    public void HidePresentation()
    {
        if (released) return;
        foreach (var item in cameraMasks)
            if (item.camera != null) item.camera.cullingMask &= ~((1 << 8) | (1 << 9));
        presentation?.Hide();
    }
    public void ShowPresentation()
    {
        if (released) return;
        foreach (var item in cameraMasks) if (item.camera != null) item.camera.cullingMask = item.mask;
        presentation?.Show();
    }
    public void Resume() => Release(true);
    public void Discard() => Release(false);
    public void Dispose() => Resume();
    private void Release(bool restoreObjects)
    {
        if (released) return;
        if (restoreObjects) ShowPresentation();
        released = true;
        // Restore UI first so existing backpack/dialogue restrictions are present before our restriction is removed.
        ui?.Dispose(); ui = null;
        foreach (var input in inputs)
        {
            if (input.player == null) continue;
            input.player.ClearInputRestriction(this);
            if (restoreObjects && input.player.isActiveAndEnabled && input.player.inputActions != null)
                foreach (var action in input.enabled) action.Enable();
        }
        foreach (var controller in controllers) if (controller != null) controller.SetScenePaused(this, false);
        if (restoreObjects)
        {
            foreach (var item in held) if (item.component != null) item.component.enabled = item.enabled;
            foreach (var item in animatorStates) if (item.animator != null) item.animator.keepAnimatorStateOnDisable = item.keepState;
        }
        if (ownsTime) Time.timeScale = time;
        held.Clear(); inputs.Clear(); controllers.Clear(); cameraMasks.Clear(); animatorStates.Clear();
        presentation = null;
    }
}

/// <summary>Only visual/audio components; does not release gameplay, UI or time ownership.</summary>
internal sealed class ScenePresentationVisibility
{
    private readonly List<(Behaviour component, bool enabled)> components = new();
    private bool hidden;

    public ScenePresentationVisibility(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var camera in root.GetComponentsInChildren<Camera>(true)) Capture(camera);
            foreach (var light in root.GetComponentsInChildren<Light>(true)) Capture(light);
            foreach (var listener in root.GetComponentsInChildren<AudioListener>(true)) Capture(listener);
        }
    }
    private void Capture(Behaviour component) => components.Add((component, component.enabled));
    public bool HasDisplayCamera
    {
        get
        {
            foreach (var item in components)
                if (item.enabled && item.component is Camera camera && camera != null
                    && camera.gameObject.activeInHierarchy && camera.targetTexture == null) return true;
            return false;
        }
    }
    public void Hide()
    {
        if (hidden) return;
        hidden = true;
        foreach (var item in components) if (item.component != null) item.component.enabled = false;
    }
    public void Show()
    {
        if (!hidden) return;
        hidden = false;
        foreach (var item in components) if (item.component != null) item.component.enabled = item.enabled;
    }
}
