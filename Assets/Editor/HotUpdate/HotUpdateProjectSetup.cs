using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class HotUpdateProjectSetup
{
    public const string StartupScenePath = "Assets/Scenes/HotUpdateStartup.unity";
    public const string ProgressPrefabPath =
        "Assets/Prefabs/UIPrefabs/Overlay/HotUpdateProgressPanel.prefab";
    public const string ProgressConfigPath =
        "Assets/Config/UI/Overlay/HotUpdateProgressUIConfig.asset";
    public const string RegistryPath = "Assets/Config/UI/UIRegistry.asset";
    public const string SettingsPath =
        "Assets/Resources/Bootstrap/HotUpdateSettings.asset";
    private const string FontPath = "Assets/UI/Fonts/InventoryChinese SDF.asset";

    [MenuItem("Tools/Hot Update/Apply Project Setup", priority = 101)]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("只能在 Edit Mode 应用热更新项目设置。");

        EnsureFolders();
        GameObject prefab = CreateProgressPrefab();
        UIConfig config = CreateOrUpdateProgressConfig(
            prefab.GetComponent<HotUpdateProgressPanel>());
        RegisterConfig(config);
        CreateOrUpdateStartupScene();
        ConfigureBuildSettings();
        ConfigureRuntimeSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "热更新项目设置已应用：启动场景、Overlay 进度面板、UIConfig、"
            + "UIRegistry、Build Settings 与运行时配置已同步。");
    }

    private static void EnsureFolders()
    {
        Directory.CreateDirectory("Assets/Prefabs/UIPrefabs/Overlay");
        Directory.CreateDirectory("Assets/Config/UI/Overlay");
        Directory.CreateDirectory("Assets/Scenes");
        Directory.CreateDirectory("UserSettings");
        AssetDatabase.Refresh();
    }

    private static GameObject CreateProgressPrefab()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
            throw new FileNotFoundException("找不到热更新界面使用的中文 TMP 字体。", FontPath);

        GameObject root = new GameObject(
            "HotUpdateProgressPanel",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(Image),
            typeof(HotUpdateProgressPanel));
        try
        {
            RectTransform rootRect = (RectTransform)root.transform;
            Stretch(rootRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image backdrop = root.GetComponent<Image>();
            backdrop.color = new Color(0.025f, 0.035f, 0.055f, 0.985f);
            backdrop.raycastTarget = true;

            TMP_Text title = CreateText(
                rootRect,
                "Phase",
                font,
                "正在准备资源",
                36f,
                FontStyles.Bold,
                new Color(0.94f, 0.96f, 0.99f),
                new Vector2(0.16f, 0.68f),
                new Vector2(0.84f, 0.76f));
            TMP_Text version = CreateText(
                rootRect,
                "SourceAndVersion",
                font,
                "来源：本地    版本：待确定",
                20f,
                FontStyles.Normal,
                new Color(0.58f, 0.78f, 0.96f),
                new Vector2(0.16f, 0.61f),
                new Vector2(0.84f, 0.67f));
            TMP_Text file = CreateText(
                rootRect,
                "CurrentFile",
                font,
                "正在初始化",
                22f,
                FontStyles.Normal,
                new Color(0.82f, 0.86f, 0.92f),
                new Vector2(0.20f, 0.50f),
                new Vector2(0.80f, 0.56f));

            RectTransform sliderRect = CreateRect(
                rootRect,
                "ProgressBar",
                new Vector2(0.20f, 0.44f),
                new Vector2(0.80f, 0.475f));
            Image sliderBackground = sliderRect.gameObject.AddComponent<Image>();
            sliderBackground.color = new Color(0.12f, 0.15f, 0.20f, 1f);
            Slider slider = sliderRect.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.direction = Slider.Direction.LeftToRight;
            slider.transition = Selectable.Transition.None;
            slider.interactable = false;
            slider.targetGraphic = sliderBackground;

            RectTransform fillRect = CreateRect(
                sliderRect,
                "Fill",
                Vector2.zero,
                Vector2.one,
                new Vector2(4f, 4f),
                new Vector2(-4f, -4f));
            Image fill = fillRect.gameObject.AddComponent<Image>();
            fill.color = new Color(0.18f, 0.72f, 0.92f, 1f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            slider.fillRect = fillRect;

            TMP_Text bytes = CreateText(
                rootRect,
                "Bytes",
                font,
                string.Empty,
                18f,
                FontStyles.Normal,
                new Color(0.64f, 0.70f, 0.79f),
                new Vector2(0.20f, 0.375f),
                new Vector2(0.68f, 0.425f),
                TextAlignmentOptions.Left);
            TMP_Text percent = CreateText(
                rootRect,
                "Percent",
                font,
                string.Empty,
                20f,
                FontStyles.Bold,
                new Color(0.94f, 0.96f, 0.99f),
                new Vector2(0.68f, 0.375f),
                new Vector2(0.80f, 0.425f),
                TextAlignmentOptions.Right);

            Button retry = CreateButton(
                rootRect,
                font,
                "重试",
                new Vector2(0.43f, 0.25f),
                new Vector2(0.57f, 0.32f));
            retry.gameObject.SetActive(false);

            SerializedObject serialized = new SerializedObject(
                root.GetComponent<HotUpdateProgressPanel>());
            serialized.FindProperty("phaseLabel").objectReferenceValue = title;
            serialized.FindProperty("versionLabel").objectReferenceValue = version;
            serialized.FindProperty("fileLabel").objectReferenceValue = file;
            serialized.FindProperty("bytesLabel").objectReferenceValue = bytes;
            serialized.FindProperty("percentLabel").objectReferenceValue = percent;
            serialized.FindProperty("progressBar").objectReferenceValue = slider;
            serialized.FindProperty("retryButton").objectReferenceValue = retry;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(root, ProgressPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static UIConfig CreateOrUpdateProgressConfig(HotUpdateProgressPanel prefab)
    {
        if (prefab == null)
            throw new InvalidOperationException("热更新进度预制体缺少 HotUpdateProgressPanel。");

        UIConfig config = AssetDatabase.LoadAssetAtPath<UIConfig>(ProgressConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<UIConfig>();
            config.name = "HotUpdateProgressUIConfig";
            AssetDatabase.CreateAsset(config, ProgressConfigPath);
        }

        config.id = UIId.HotUpdateProgress;
        config.prefab = prefab;
        config.layer = UILayer.Overlay;
        config.space = UISpace.Screen;
        config.instanceMode = UIInstanceMode.Single;
        config.cacheMode = UICacheMode.Keep;
        config.lifetime = UILifetime.Global;
        config.suspendWithPresentation = false;
        config.handlesBack = false;
        config.hideLowerWindows = false;
        config.showCursor = true;
        config.blockMovement = true;
        config.blockCamera = true;
        config.modal = true;
        config.closeWhenClickMask = false;
        config.blurBackground = false;
        EditorUtility.SetDirty(config);
        return config;
    }

    private static void RegisterConfig(UIConfig config)
    {
        UIRegistry registry = AssetDatabase.LoadAssetAtPath<UIRegistry>(RegistryPath);
        if (registry == null)
            throw new FileNotFoundException("找不到 UIRegistry。", RegistryPath);

        SerializedObject serialized = new SerializedObject(registry);
        SerializedProperty configs = serialized.FindProperty("configs");
        bool found = false;
        for (int index = 0; index < configs.arraySize; index++)
        {
            SerializedProperty element = configs.GetArrayElementAtIndex(index);
            if (element.objectReferenceValue == config)
            {
                found = true;
                break;
            }
        }

        if (!found)
        {
            configs.InsertArrayElementAtIndex(configs.arraySize);
            configs.GetArrayElementAtIndex(configs.arraySize - 1).objectReferenceValue = config;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        registry.Rebuild();
        EditorUtility.SetDirty(registry);
    }

    private static void CreateOrUpdateStartupScene()
    {
        Scene scene = SceneManager.GetSceneByPath(StartupScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (wasLoaded && scene.isDirty)
            throw new InvalidOperationException("HotUpdateStartup 场景有未保存修改，请先保存。");

        if (!wasLoaded)
        {
            scene = File.Exists(StartupScenePath)
                ? EditorSceneManager.OpenScene(StartupScenePath, OpenSceneMode.Additive)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        }

        try
        {
            HotUpdateStartupController controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HotUpdateStartupController>(true))
                .FirstOrDefault();
            if (controller == null)
            {
                GameObject root = new GameObject("HotUpdateStartupController");
                SceneManager.MoveGameObjectToScene(root, scene);
                root.AddComponent<HotUpdateStartupController>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, StartupScenePath))
                throw new IOException("保存热更新启动场景失败。");
        }
        finally
        {
            if (!wasLoaded && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void ConfigureBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(StartupScenePath, true)
        };
        scenes.AddRange(
            EditorBuildSettings.scenes.Where(
                scene => !string.Equals(
                    scene.path,
                    StartupScenePath,
                    StringComparison.OrdinalIgnoreCase)));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void ConfigureRuntimeSettings()
    {
        HotUpdateSettings settings = AssetDatabase.LoadAssetAtPath<HotUpdateSettings>(SettingsPath);
        if (settings == null)
            throw new FileNotFoundException("找不到 HotUpdateSettings。", SettingsPath);

        if (string.IsNullOrWhiteSpace(settings.productionBaseUrl))
            settings.tryRemoteUpdate = false;
        settings.currentVersionPath = "current/version.txt";
        settings.releasesPath = "releases";
        settings.manifestName = "ABCompareInfo.txt";
        settings.requestTimeoutSeconds = 30;
        settings.bundleFolder = "AssetBundles";
        settings.copyStreamingAssetsOnFirstLaunch = true;
        settings.luaEntry = "Main";
        settings.loadLuaFromAssetBundlesInEditor = false;
        EditorUtility.SetDirty(settings);
    }

    private static TMP_Text CreateText(
        RectTransform parent,
        string name,
        TMP_FontAsset font,
        string text,
        float fontSize,
        FontStyles style,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        RectTransform rect = CreateRect(parent, name, anchorMin, anchorMax);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    private static Button CreateButton(
        RectTransform parent,
        TMP_FontAsset font,
        string caption,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        RectTransform rect = CreateRect(parent, "RetryButton", anchorMin, anchorMax);
        Image background = rect.gameObject.AddComponent<Image>();
        background.color = new Color(0.15f, 0.55f, 0.78f, 1f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.22f, 0.68f, 0.92f, 1f);
        colors.pressedColor = new Color(0.10f, 0.42f, 0.64f, 1f);
        button.colors = colors;

        TMP_Text label = CreateText(
            rect,
            "Label",
            font,
            caption,
            22f,
            FontStyles.Bold,
            Color.white,
            Vector2.zero,
            Vector2.one);
        label.raycastTarget = false;
        return button;
    }

    private static RectTransform CreateRect(
        RectTransform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2? offsetMin = null,
        Vector2? offsetMax = null)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)gameObject.transform;
        rect.SetParent(parent, false);
        Stretch(
            rect,
            anchorMin,
            anchorMax,
            offsetMin ?? Vector2.zero,
            offsetMax ?? Vector2.zero);
        return rect;
    }

    private static void Stretch(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
    }
}
