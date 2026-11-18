using System;
using Game.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Updates registered UI wiring in place. Does not rebuild layouts or touch the activity draft.</summary>
public static class UIFrameworkSetup
{
    private const string Prefabs = "Assets/Prefabs/UIPrefabs/";
    private const string RegistryPath = "Assets/Config/UI/UIRegistry.asset";

    [MenuItem("Tools/UI/Apply Framework Integration")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Apply UI resource changes in Edit Mode.");

        CreateTransitionPanel();
        var registry = AssetDatabase.LoadAssetAtPath<UIRegistry>(RegistryPath);
        var entries = new SerializedObject(registry).FindProperty("configs");
        for (int index = 0; index < entries.arraySize; index++)
        {
            var config = entries.GetArrayElementAtIndex(index).objectReferenceValue as UIConfig;
            if (config == null || config.prefab == null)
                throw new InvalidOperationException("UIRegistry contains an empty configuration or prefab.");
            string path = AssetDatabase.GetAssetPath(config.prefab);
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = ConfigurePanel(contents, config.layer, config.space);
                if (config.id == UIId.ExplorationHUD)
                {
                    ConfigureExplorationHUD(contents);
                    changed = true;
                }
                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        const string oldBar = Prefabs + "HUD/Battle/BattleEnemyBar.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(oldBar) != null)
        {
            string error = AssetDatabase.MoveAsset(oldBar, Prefabs + "Widgets/Battle/BattleEnemyBar.prefab");
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException(error);
        }
        AssetDatabase.SaveAssets();
        registry.Rebuild();
        Debug.Log("UI framework integration applied to registered panels; activity draft unchanged.");
    }

    public static bool ConfigurePanel(GameObject root, UILayer layer, UISpace space = UISpace.Screen)
    {
        if (space == UISpace.World)
            return false;

        bool changed = false;

        // Screen panels inherit the framework's layer order. Preserve the existing layout/scaler.
        foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
        {
            if (!canvas.overrideSorting)
                continue;
            canvas.overrideSorting = false;
            changed = true;
        }

        if ((layer == UILayer.Window || layer == UILayer.Popup) && root.GetComponent<UITransition>() == null)
        {
            root.AddComponent<UIFadeTransition>();
            changed = true;
        }

        foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
        {
            if ((rect.name == "OutsideClickMask" || rect.name == "Mask")
                && rect.GetComponent<Graphic>() != null && rect.GetComponent<UIMaskClose>() == null)
            {
                rect.gameObject.AddComponent<UIMaskClose>();
                changed = true;
            }
        }
        return changed;
    }

    public static void ConfigureExplorationHUD(GameObject root)
    {
        foreach (var entry in root.GetComponentsInChildren<PartyEntryButton>(true))
            EnsureVisibility(entry.gameObject);
        foreach (var entry in root.GetComponentsInChildren<Game.Characters.CharacterEntryButton>(true))
            EnsureVisibility(entry.gameObject);

        var settings = root.transform.Find("SettingButton/Button");
        if (settings == null)
            throw new InvalidOperationException("ExplorationHUD is missing SettingButton/Button.");
        EnsureVisibility(settings.gameObject);
        var opener = settings.GetComponent<UIOpenButton>() ?? settings.gameObject.AddComponent<UIOpenButton>();
        var data = new SerializedObject(opener);
        data.FindProperty("id").intValue = (int)UIId.Settings;
        data.ApplyModifiedPropertiesWithoutUndo();
        var button = settings.GetComponent<Button>();
        for (int index = button.onClick.GetPersistentEventCount() - 1; index >= 0; index--)
            UnityEventTools.RemovePersistentListener(button.onClick, index);
        UnityEventTools.AddPersistentListener(button.onClick, opener.Open);
    }

    private static void EnsureVisibility(GameObject target)
    {
        if (target.GetComponent<UIHUDEntryVisibility>() == null)
            target.AddComponent<UIHUDEntryVisibility>();
    }

    private static void CreateTransitionPanel()
    {
        EnsureFolder(Prefabs + "Overlay");
        EnsureFolder("Assets/Config/UI/Overlay");
        const string prefabPath = Prefabs + "Overlay/SceneTransitionPanel.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            var root = new GameObject("SceneTransitionPanel", typeof(RectTransform), typeof(CanvasGroup),
                typeof(Image), typeof(SceneTransitionPanel));
            root.layer = 5;
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = Color.clear;
            root.SetActive(false);
            prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        const string configPath = "Assets/Config/UI/Overlay/SceneTransitionUIConfig.asset";
        var config = AssetDatabase.LoadAssetAtPath<UIConfig>(configPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<UIConfig>();
            AssetDatabase.CreateAsset(config, configPath);
        }
        config.id = UIId.SceneTransition;
        config.prefab = prefab.GetComponent<SceneTransitionPanel>();
        config.layer = UILayer.Overlay;
        config.lifetime = UILifetime.Global;
        config.cacheMode = UICacheMode.Keep;
        config.suspendWithPresentation = false;
        config.handlesBack = false;
        config.modal = true;
        config.blockMovement = config.blockCamera = config.showCursor = true;
        EditorUtility.SetDirty(config);

        var registry = AssetDatabase.LoadAssetAtPath<UIRegistry>(RegistryPath);
        var data = new SerializedObject(registry);
        var entries = data.FindProperty("configs");
        for (int index = 0; index < entries.arraySize; index++)
        {
            if (entries.GetArrayElementAtIndex(index).objectReferenceValue == config)
                return;
        }
        entries.InsertArrayElementAtIndex(entries.arraySize);
        entries.GetArrayElementAtIndex(entries.arraySize - 1).objectReferenceValue = config;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        int separator = path.LastIndexOf('/');
        string parent = path.Substring(0, separator);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
    }
}
