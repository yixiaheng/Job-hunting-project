using System;
using System.Collections.Generic;
using Game.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>Captures only this scene's components; Resume/Discard are idempotent and nested-safe.</summary>
internal sealed class ScenePresentationScope : IScenePresentationSuspension, IDisposable
{
    private readonly float previousTimeScale;
    private readonly bool ownsTime;
    private readonly List<(Behaviour component, bool enabled)> pausedComponents = new();
    private readonly List<(PlayerInputHandler player, List<InputAction> enabled)> inputs = new();
    private readonly List<PlayerController> controllers = new();
    private readonly List<(Camera camera, int mask)> cameraMasks = new();
    private readonly List<(Light light, int mask)> lightMasks = new();
    private readonly List<(Animator animator, bool keepState)> animatorStates = new();
    private ScenePresentationVisibility presentation;
    private IDisposable existingUI;
    private IDisposable sceneUI;
    private bool isReleased;
    public ScenePresentationScope(Scene source, bool pauseTime, int excludedMask = 0)
    {
        previousTimeScale = Time.timeScale;
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
                    {
                        controllers.Add(controller);
                        controller.SetScenePaused(this, true);
                    }

                    foreach (var player in root.GetComponentsInChildren<PlayerInputHandler>(true))
                    {
                        var enabledActions = new List<InputAction>();
                        if (player.inputActions != null)
                        {
                            foreach (var action in player.inputActions.asset)
                                if (action.enabled)
                                    enabledActions.Add(action);
                            inputs.Add((player, enabledActions));
                            player.SetInputRestriction(this, true, true, false);
                            player.inputActions.Disable();
                        }
                    }

                    foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                    {
                        cameraMasks.Add((camera, camera.cullingMask));
                        camera.cullingMask &= ~excludedMask;
                    }

                    foreach (var light in root.GetComponentsInChildren<Light>(true))
                    {
                        lightMasks.Add((light, light.cullingMask));
                        light.cullingMask &= ~excludedMask;
                    }

                    foreach (var animator in root.GetComponentsInChildren<Animator>(true))
                    {
                        animatorStates.Add((animator, animator.keepAnimatorStateOnDisable));
                        animator.keepAnimatorStateOnDisable = true;
                        Hold(animator);
                    }
                }

                // 全局快照包含已有的常驻背包；场景租约也挂起来源随后创建的面板。
                if (pauseTime)
                    existingUI = UIManager.Instance?.SuspendPresentation();
                sceneUI = UIManager.Instance?.SuspendScenePresentation(source.handle);
            }

            if (ownsTime)
                Time.timeScale = 0f;
        }
        catch
        {
            Resume();
            throw;
        }
    }

    private void Hold(Behaviour component)
    {
        pausedComponents.Add((component, component.enabled));
        component.enabled = false;
    }

    public void HidePresentation()
    {
        if (isReleased)
            return;
        presentation?.Hide();
    }

    public void ShowPresentation()
    {
        if (isReleased)
            return;
        presentation?.Show();
    }

    public void Resume() => Release(true);
    public void Discard() => Release(false);
    public void Dispose() => Resume();
    private void Release(bool restoreObjects)
    {
        if (isReleased)
            return;
        if (restoreObjects)
        {
            // 目标卸载完成后才恢复 Mask，返回期间不会透出目标模型。
            foreach (var item in cameraMasks)
                if (item.camera != null)
                    item.camera.cullingMask = item.mask;
            foreach (var item in lightMasks)
                if (item.light != null)
                    item.light.cullingMask = item.mask;
            ShowPresentation();
        }

        isReleased = true;
        // Restore UI first so existing backpack/dialogue restrictions are present before our restriction is removed.
        sceneUI?.Dispose();
        sceneUI = null;
        existingUI?.Dispose();
        existingUI = null;
        foreach (var input in inputs)
        {
            if (input.player == null)
                continue;
            input.player.ClearInputRestriction(this);
            if (restoreObjects && input.player.isActiveAndEnabled && input.player.inputActions != null)
                foreach (var action in input.enabled)
                    action.Enable();
        }

        foreach (var controller in controllers)
            if (controller != null)
                controller.SetScenePaused(this, false);
        if (restoreObjects)
        {
            foreach (var item in pausedComponents)
                if (item.component != null)
                    item.component.enabled = item.enabled;
            foreach (var item in animatorStates)
                if (item.animator != null)
                    item.animator.keepAnimatorStateOnDisable = item.keepState;
        }

        if (ownsTime)
            Time.timeScale = previousTimeScale;
        pausedComponents.Clear();
        inputs.Clear();
        controllers.Clear();
        cameraMasks.Clear();
        lightMasks.Clear();
        animatorStates.Clear();
        presentation = null;
    }
}
