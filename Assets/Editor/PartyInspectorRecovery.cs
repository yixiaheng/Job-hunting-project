using UnityEditor;

// Scene/prefab reimports may leave locked Inspectors targeting deleted objects.
[InitializeOnLoad]
internal static class PartyInspectorRecovery
{
    private const string RecoveryKey = "PartyInspectorRecovery.Completed";

    static PartyInspectorRecovery()
    {
        if (SessionState.GetBool(RecoveryKey, false))
            return;

        EditorApplication.delayCall += Recover;
    }

    [MenuItem("Tools/Party/Refresh Inspector References")]
    private static void Recover()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        SessionState.SetBool(RecoveryKey, true);
        Selection.objects = new UnityEngine.Object[0];

        ActiveEditorTracker.sharedTracker.isLocked = false;
        ActiveEditorTracker.sharedTracker.ForceRebuild();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }
}
