using UnityEditor;

/// <summary>Compatibility entry for the original upload menu.</summary>
public static class UpLoadAB
{
    [MenuItem("Tools/Hot Update/Upload AssetBundles", priority = 121)]
    private static void Upload()
    {
        HotUpdatePublisherWindow.Open();
    }
}
