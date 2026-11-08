using System;
using System.IO;
using System.Linq;
using Game.Characters;
using Game.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Explicit setup/verification commands only; importing this file does not rewrite assets.</summary>
[InitializeOnLoad]
public static class SceneFlowSetup
{
    public const string DirectoryPath = "Temp/SceneFlow";
    private const string CommandPath = DirectoryPath + "/command.txt";
    static SceneFlowSetup() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(CommandPath)) return;
        string command;
        try { command = File.ReadAllText(CommandPath).Trim(); }
        catch (IOException) { return; } // External writer has not finished publishing the command yet.
        if (EditorApplication.isPlayingOrWillChangePlaymode && command != "status" && command != "stop-verification") return;
        File.Delete(CommandPath);
        try
        {
            if (command == "setup") ConfigureAssets();
            else if (command == "stop-verification") { SceneFlowVerification.Stop(); EditorApplication.isPlaying = false; }
            else if (command == "verify") { SceneFlowVerification.Begin(); return; }
            else if (command == "verify-direct-character") { SceneFlowVerification.BeginCharacter(); return; }
            else if (command == "verify-direct-party") { SceneFlowVerification.BeginParty(); return; }
            else if (command == "verify-direct-battle") { SceneFlowVerification.BeginBattle(); return; }
            else if (command != "status") throw new ArgumentException("Unknown scene flow command: " + command);
            File.WriteAllText(DirectoryPath + "/command-result.txt", "OK " + command + " " + DateTime.Now.ToString("O")
                + " playing=" + EditorApplication.isPlaying + " active=" + SceneManager.GetActiveScene().path
                + " dirty=" + SceneManager.GetActiveScene().isDirty + " paused=" + EditorApplication.isPaused
                + " frame=" + Time.frameCount + " test=" + SceneFlowVerification.Status
                + "\nSample asset GUID=" + AssetDatabase.AssetPathToGUID("Assets/Scenes/SampleScene.unity")
                + "\nBuild scene properties=" + string.Join(",", typeof(EditorBuildSettingsScene).GetProperties().Select(p => p.Name)));
        }
        catch (Exception e) { File.WriteAllText(DirectoryPath + "/command-result.txt", e.ToString()); Debug.LogException(e); }
    }

    [MenuItem("Tools/Scenes/Configure Unified Scene Flow")]
    public static void ConfigureAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layer = tags.FindProperty("layers").GetArrayElementAtIndex(9);
        if (!string.IsNullOrEmpty(layer.stringValue) && layer.stringValue != "PartyPreview")
            throw new InvalidOperationException("Layer 9 is already used; refusing to overwrite it.");
        layer.stringValue = "PartyPreview";
        tags.ApplyModifiedPropertiesWithoutUndo();
        // Re-resolve current GUIDs from paths instead of regenerating or changing scene assets.
        var sceneList = EditorBuildSettings.scenes.Select(s => new EditorBuildSettingsScene(s.path, s.enabled)
        { guid = AssetDatabase.GUIDFromAssetPath(s.path) }).ToArray();
        // Unity may optimize away an assignment when only GUIDs changed.
        EditorBuildSettings.scenes = Array.Empty<EditorBuildSettingsScene>();
        EditorBuildSettings.scenes = sceneList;
        InstallPartyEntry();
        InstallPartyBack();
        ConfigurePartyScene();
        if (!SceneCatalogEditorUtility.ValidateRuntimeCatalog()) throw new InvalidOperationException("Scene catalog validation failed.");
    }

    private static void InstallPartyEntry()
    {
        const string path = "Assets/Prefabs/UIPrefabs/HUD/Exploration/ExplorationHUD.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var old = root.GetComponentsInChildren<LoadScene>(true).SingleOrDefault(c => c.sceneId == SceneId.Party);
            if (old == null)
            {
                if (root.GetComponentInChildren<PartyEntryButton>(true) == null) throw new InvalidOperationException("Party HUD entry not found.");
                return;
            }
            var button = old.GetComponent<Button>();
            if (button == null) throw new InvalidOperationException("Party entry has no Button.");
            var entry = old.gameObject.AddComponent<PartyEntryButton>();
            var settings = new SerializedObject(entry);
            settings.FindProperty("transition").enumValueIndex = (int)old.mode;
            settings.ApplyModifiedPropertiesWithoutUndo();
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                if (button.onClick.GetPersistentTarget(i) == old) UnityEventTools.RemovePersistentListener(button.onClick, i);
            UnityEventTools.AddPersistentListener(button.onClick, entry.Open);
            UnityEngine.Object.DestroyImmediate(old);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void InstallPartyBack()
    {
        const string path = "Assets/Prefabs/UIPrefabs/Window/Party/BattlePartyPanel.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.transform.Find("SceneReturn") != null) return;
            var panel = root.GetComponent<BattlePartyCanvasUI>();
            var button = CharacterUIWidgets.Button(root.transform, "返回");
            button.name = "SceneReturn";
            UnityEngine.Object.DestroyImmediate(button.GetComponent<LayoutElement>());
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(.02f, .91f); rect.anchorMax = new Vector2(.14f, .98f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            UnityEventTools.AddPersistentListener(button.onClick, panel.Return);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ConfigurePartyScene()
    {
        const string path = "Assets/Scenes/PartyScreen.unity";
        var source = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (!opened && scene.isDirty) throw new InvalidOperationException("PartyScreen has unsaved edits; cannot overwrite them.");
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.gameObject.layer = 9;
                foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                { camera.cullingMask = (1 << 9) | (1 << 5); camera.clearFlags = CameraClearFlags.SolidColor; }
                foreach (var light in root.GetComponentsInChildren<Light>(true)) light.cullingMask = 1 << 9;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (source.IsValid() && source.isLoaded) SceneManager.SetActiveScene(source);
        }
    }
}
