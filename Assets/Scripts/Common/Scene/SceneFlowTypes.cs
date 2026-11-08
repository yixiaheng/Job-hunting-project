using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SceneOperationStage { Accepted, FadingOut, Loading, Initializing, Unloading, Restoring, FadingIn, Completed, Failed }

/// <summary>A session identity, not an index into the navigation stack.</summary>
public readonly struct SceneVisitHandle : IEquatable<SceneVisitHandle>
{
    internal readonly Guid Value;
    internal SceneVisitHandle(Guid value) { Value = value; }
    public bool IsValid => Value != Guid.Empty;
    public bool Equals(SceneVisitHandle other) => Value == other.Value;
    public override bool Equals(object obj) => obj is SceneVisitHandle other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
}

/// <summary>Yield this result, or register OnCompleted even after completion.</summary>
public sealed class SceneOperation : CustomYieldInstruction
{
    public bool Accepted { get; internal set; }
    public bool IsDone { get; private set; }
    public bool Succeeded { get; private set; }
    public string Error { get; private set; }
    public SceneOperationStage Stage { get; internal set; } = SceneOperationStage.Accepted;
    public float Progress { get; internal set; }
    public Scene Scene { get; internal set; }
    public SceneVisitHandle Visit { get; internal set; }
    public override bool keepWaiting => !IsDone;
    private Action<SceneOperation> completed;

    public void OnCompleted(Action<SceneOperation> callback)
    {
        if (callback == null) return;
        if (IsDone) Invoke(callback); else completed += callback;
    }
    internal void Finish(string error = null)
    {
        if (IsDone) return;
        Error = error;
        Succeeded = error == null;
        Stage = Succeeded ? SceneOperationStage.Completed : SceneOperationStage.Failed;
        if (Succeeded) Progress = 1f;
        IsDone = true;
        var listeners = completed;
        completed = null;
        if (listeners != null)
            foreach (Action<SceneOperation> callback in listeners.GetInvocationList()) Invoke(callback);
    }
    private void Invoke(Action<SceneOperation> callback)
    {
        try { callback(this); } catch (Exception e) { Debug.LogException(e); }
    }
}

public sealed class SceneLoadOptions
{
    public LoadSceneMode LoadMode = LoadSceneMode.Single;
    public SceneTransitionMode Transition = SceneTransitionMode.Immediate;
    public bool SetActive = true;
    public object Context;
    // Called by the persistent manager, never by a coroutine on an outgoing scene object.
    public Action Prepare;
    public Action Rollback;
    internal SceneLoadOptions Copy() => (SceneLoadOptions)MemberwiseClone();
}

public interface ISceneSuspension
{
    void Resume();
    // Release global state/ownership without reviving outgoing scene objects.
    void Discard();
}

/// <summary>Rendering can hand over while gameplay and UI remain suspended.</summary>
public interface IScenePresentationSuspension : ISceneSuspension
{
    void HidePresentation();
    void ShowPresentation();
}

public interface ISceneVisitLifecycle
{
    ISceneSuspension SuspendSource(Scene source);
    IEnumerator InitializeTarget(Scene target, SceneVisitHandle visit, object context);
    IDisposable SuspendTarget(Scene target);
    void ReleaseTarget(Scene target);
}

/// <summary>Scene-owned entry point. Start must defer while the manager initializes this scene.</summary>
public interface ISceneEntryPoint
{
    ISceneVisitLifecycle VisitLifecycle { get; }
    IEnumerator InitializeScene(SceneVisitHandle visit, object context);
}
