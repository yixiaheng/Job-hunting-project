using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Only visual/audio components; does not release gameplay, UI or time ownership.</summary>
internal sealed class ScenePresentationVisibility
{
    private readonly List<(Behaviour component, bool enabled)> components = new();
    private bool hidden;
    public ScenePresentationVisibility(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                Capture(camera);
            foreach (var light in root.GetComponentsInChildren<Light>(true))
                Capture(light);
            foreach (var listener in root.GetComponentsInChildren<AudioListener>(true))
                Capture(listener);
        }
    }

    private void Capture(Behaviour component) => components.Add((component, component.enabled));
    public bool HasDisplayCamera
    {
        get
        {
            foreach (var item in components)
                if (item.enabled && item.component is Camera camera && camera != null && camera.gameObject.activeInHierarchy && camera.targetTexture == null)
                    return true;
            return false;
        }
    }

    public void Hide()
    {
        if (hidden)
            return;
        hidden = true;
        foreach (var item in components)
            if (item.component != null)
                item.component.enabled = false;
    }

    public void Show()
    {
        if (!hidden)
            return;
        hidden = false;
        foreach (var item in components)
            if (item.component != null)
                item.component.enabled = item.enabled;
    }
}
