using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[CustomEditor(typeof(SceneCatalog))]
public sealed class SceneCatalogEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("用稳定的 SceneId 登记场景资源。路径自动生成，重命名或移动场景后会重新同步。", MessageType.Info);
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();

        SceneCatalog catalog = (SceneCatalog)target;
        SceneCatalogEditorUtility.Synchronize(catalog);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("运行时路径（自动生成）", EditorStyles.boldLabel);
        if (catalog.Entries != null)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                foreach (SceneCatalog.SceneEntry entry in catalog.Entries)
                {
                    if (entry != null)
                        EditorGUILayout.TextField(entry.Id.ToString(), entry.Path ?? string.Empty);
                }
            }
        }

        List<string> errors = SceneCatalogEditorUtility.Validate(catalog);
        foreach (string error in errors)
            EditorGUILayout.HelpBox(error, MessageType.Error);
        if (errors.Count == 0)
            EditorGUILayout.HelpBox("登记完整，所有场景均已加入当前构建配置。", MessageType.Info);

        if (GUILayout.Button("同步并校验场景登记"))
        {
            AssetDatabase.SaveAssetIfDirty(catalog);
            SceneCatalogEditorUtility.ValidateRuntimeCatalog(true);
        }
    }
}

public static class SceneCatalogEditorUtility
{
    public const string AssetPath = "Assets/Resources/" + SceneCatalog.ResourcePath + ".asset";

    /// <summary>资源引用由 Unity 的 GUID 跟踪；运行时只保留同步后的场景路径。</summary>
    public static bool Synchronize(SceneCatalog catalog)
    {
        if (catalog == null)
            return false;

        SerializedObject serialized = new SerializedObject(catalog);
        SerializedProperty entries = serialized.FindProperty("entries");
        bool changed = false;
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            UnityEngine.Object scene = entry.FindPropertyRelative("scene").objectReferenceValue;
            SerializedProperty path = entry.FindPropertyRelative("scenePath");
            string currentPath = scene != null ? AssetDatabase.GetAssetPath(scene) : string.Empty;
            if (path.stringValue == currentPath)
                continue;

            path.stringValue = currentPath;
            changed = true;
        }

        if (changed)
        {
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        return changed;
    }

    public static List<string> Validate(SceneCatalog catalog)
    {
        List<string> errors = new List<string>();
        if (catalog == null)
        {
            errors.Add($"缺少运行时登记表：{AssetPath}");
            return errors;
        }

        HashSet<string> enabledPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Unity 6 的该属性会读取活动 Build Profile 覆盖后的场景列表。
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
                enabledPaths.Add(scene.path);
        }

        HashSet<SceneId> ids = new HashSet<SceneId>();
        HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (catalog.Entries != null)
        {
            foreach (SceneCatalog.SceneEntry entry in catalog.Entries)
            {
                if (entry == null)
                {
                    errors.Add("登记表存在空条目。");
                    continue;
                }

                if (entry.Id == SceneId.None || !Enum.IsDefined(typeof(SceneId), entry.Id))
                    errors.Add($"无效的场景 ID：{entry.Id}。");
                if (!ids.Add(entry.Id))
                    errors.Add($"场景 ID 重复登记：{entry.Id}。");

                if (string.IsNullOrWhiteSpace(entry.Path)
                    || AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.Path) == null)
                {
                    errors.Add($"{entry.Id} 未指定有效的场景资源。");
                    continue;
                }

                if (!paths.Add(entry.Path))
                    errors.Add($"同一场景被多次登记：{entry.Path}");
                if (!enabledPaths.Contains(entry.Path))
                    errors.Add($"{entry.Id} 未加入当前 Build Profile 的启用场景列表：{entry.Path}");
            }
        }

        foreach (SceneId id in Enum.GetValues(typeof(SceneId)))
        {
            if (id != SceneId.None && !ids.Contains(id))
                errors.Add($"场景 ID 尚未登记：{id}。");
        }

        return errors;
    }

    public static bool ValidateRuntimeCatalog(bool logSuccess = false)
    {
        SceneCatalog catalog = AssetDatabase.LoadAssetAtPath<SceneCatalog>(AssetPath);
        Synchronize(catalog);
        if (catalog != null)
            AssetDatabase.SaveAssetIfDirty(catalog);

        List<string> errors = Validate(catalog);
        foreach (string error in errors)
            Debug.LogError($"[SceneCatalog] {error}", catalog);
        if (errors.Count == 0 && logSuccess)
            Debug.Log("[SceneCatalog] 路径同步及场景登记校验通过。", catalog);

        return errors.Count == 0;
    }

    [MenuItem("Tools/Scenes/同步并校验 SceneCatalog")]
    private static void ValidateFromMenu()
    {
        ValidateRuntimeCatalog(true);
    }
}

[InitializeOnLoad]
public static class SceneCatalogPlayModeGuard
{
    static SceneCatalogPlayModeGuard()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.delayCall += SynchronizeRuntimeCatalog;
    }

    private static void HandlePlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode
            && !SceneCatalogEditorUtility.ValidateRuntimeCatalog())
            EditorApplication.isPlaying = false;
    }

    internal static void SynchronizeRuntimeCatalog()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        SceneCatalog catalog = AssetDatabase.LoadAssetAtPath<SceneCatalog>(SceneCatalogEditorUtility.AssetPath);
        if (SceneCatalogEditorUtility.Synchronize(catalog))
            AssetDatabase.SaveAssetIfDirty(catalog);
    }
}

public sealed class SceneCatalogBuildGuard : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (!SceneCatalogEditorUtility.ValidateRuntimeCatalog())
            throw new BuildFailedException("SceneCatalog 登记无效，请修正 Console 中的错误后重新打包。");
    }
}

public sealed class SceneCatalogAssetPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
        string[] movedAssets, string[] movedFromAssetPaths)
    {
        if (!ContainsScene(importedAssets) && !ContainsScene(deletedAssets)
            && !ContainsScene(movedAssets) && !ContainsScene(movedFromAssetPaths))
            return;

        EditorApplication.delayCall -= SceneCatalogPlayModeGuard.SynchronizeRuntimeCatalog;
        EditorApplication.delayCall += SceneCatalogPlayModeGuard.SynchronizeRuntimeCatalog;
    }

    private static bool ContainsScene(string[] paths)
    {
        foreach (string path in paths)
        {
            if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
