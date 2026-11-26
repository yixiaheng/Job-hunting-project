using System;
using System.IO;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class BattleActionTimelineSetup
{
    private const string RequestPath = "Temp/BattleActionTimelineSetup/request.txt";
    private const string WidgetFolder = "Assets/Prefabs/UIPrefabs/Widgets/Battle";
    private const string PanelFolder = "Assets/Prefabs/UIPrefabs/HUD/Battle";
    private const string ConfigFolder = "Assets/Config/UI/HUD";
    private const string ItemPath = WidgetFolder + "/BattleActionTimelineItem.prefab";
    private const string PanelPath = PanelFolder + "/BattleActionTimelinePanel.prefab";
    private const string ConfigPath = ConfigFolder + "/BattleActionTimelineUIConfig.asset";
    private const string RegistryPath = "Assets/Config/UI/UIRegistry.asset";

    static BattleActionTimelineSetup() => EditorApplication.update += Poll;

    [MenuItem("Tools/Battle/Create Missing Action Timeline HUD")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Create the action timeline HUD in Edit Mode.");

        EnsureFolder(WidgetFolder);
        EnsureFolder(PanelFolder);
        EnsureFolder(ConfigFolder);

        GameObject itemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ItemPath);
        if (itemPrefab == null)
            itemPrefab = CreateItemPrefab();

        GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
        if (panelPrefab == null)
            panelPrefab = CreatePanelPrefab(itemPrefab.GetComponent<BattleActionTimelineItem>());
        else
            RepairLegacyViewport();

        UIConfig config = AssetDatabase.LoadAssetAtPath<UIConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<UIConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }
        config.id = UIId.BattleActionTimeline;
        config.prefab = panelPrefab.GetComponent<BattleActionTimelinePanel>();
        config.layer = UILayer.HUD;
        config.space = UISpace.Screen;
        config.instanceMode = UIInstanceMode.Single;
        config.cacheMode = UICacheMode.DestroyOnClose;
        config.lifetime = UILifetime.Scene;
        config.suspendWithPresentation = true;
        config.handlesBack = false;
        config.hideLowerWindows = false;
        config.showCursor = false;
        config.blockMovement = false;
        config.blockCamera = false;
        config.modal = false;
        config.closeWhenClickMask = false;
        config.blurBackground = false;
        EditorUtility.SetDirty(config);

        Register(config);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Battle action timeline HUD is ready: " + PanelPath);
    }

    private static GameObject CreateItemPrefab()
    {
        GameObject root = UIObject("BattleActionTimelineItem", null);
        var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(270f, 148f);
        root.AddComponent<CanvasGroup>();
        BattleActionTimelineItem item = root.AddComponent<BattleActionTimelineItem>();

        Image connectorLine = ImageObject("ConnectorLine", root.transform,
            new Color(0.2f, 0.75f, 0.95f, 0.72f));
        Place(connectorLine.rectTransform, new Vector2(10f, -74f), new Vector2(45f, 4f));
        Image connector = ImageObject("Connector", root.transform,
            new Color(0.65f, 0.94f, 1f, 1f));
        Place(connector.rectTransform, new Vector2(10f, -74f), new Vector2(18f, 18f));
        var connectorOutline = connector.gameObject.AddComponent<Outline>();
        connectorOutline.effectColor = new Color(0.06f, 0.2f, 0.28f, 0.95f);
        connectorOutline.effectDistance = new Vector2(3f, -3f);

        Image current = ImageObject("CurrentHighlight", root.transform,
            new Color(1f, 0.91f, 0.48f, 1f));
        Place(current.rectTransform, new Vector2(126f, -74f), new Vector2(158f, 146f));
        Image frame = ImageObject("FactionFrame", root.transform,
            new Color(0.14f, 0.83f, 1f, 1f));
        Place(frame.rectTransform, new Vector2(126f, -74f), new Vector2(150f, 138f));
        Image portrait = ImageObject("Portrait", root.transform,
            new Color(0.08f, 0.11f, 0.16f, 0.96f));
        Place(portrait.rectTransform, new Vector2(126f, -74f), new Vector2(138f, 126f));

        TMP_Text fallback = TextObject("Fallback", root.transform, 54f, TextAlignmentOptions.Center);
        Place(fallback.rectTransform, new Vector2(126f, -74f), new Vector2(132f, 118f));
        fallback.color = new Color(0.78f, 0.87f, 0.94f, 0.9f);

        Image valuePlate = ImageObject("ActionValuePlate", root.transform,
            new Color(0.02f, 0.035f, 0.055f, 0.92f));
        Place(valuePlate.rectTransform, new Vector2(181f, -119f), new Vector2(60f, 38f));
        TMP_Text value = TextObject("ActionValue", valuePlate.transform, 27f, TextAlignmentOptions.Center);
        Stretch(value.rectTransform);
        value.color = Color.white;
        var shadow = value.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(2f, -2f);

        current.gameObject.SetActive(false);
        var serialized = new SerializedObject(item);
        serialized.FindProperty("portrait").objectReferenceValue = portrait;
        serialized.FindProperty("factionFrame").objectReferenceValue = frame;
        serialized.FindProperty("currentHighlight").objectReferenceValue = current;
        serialized.FindProperty("actionValueText").objectReferenceValue = value;
        serialized.FindProperty("fallbackText").objectReferenceValue = fallback;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ItemPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreatePanelPrefab(BattleActionTimelineItem itemPrefab)
    {
        GameObject root = UIObject("BattleActionTimelinePanel", null);
        Stretch(root.GetComponent<RectTransform>());
        root.AddComponent<CanvasGroup>();
        BattleActionTimelinePanel panel = root.AddComponent<BattleActionTimelinePanel>();

        Image rail = ImageObject("DecorativeRail", root.transform,
            new Color(0.18f, 0.76f, 0.96f, 0.55f));
        AnchorTopLeft(rail.rectTransform, new Vector2(40f, -180f), new Vector2(7f, 1420f));
        Image topMarker = ImageObject("TopMarker", root.transform,
            new Color(0.65f, 0.94f, 1f, 0.95f));
        PlaceTopLeft(topMarker.rectTransform, new Vector2(40f, -180f), new Vector2(24f, 24f));
        var markerOutline = topMarker.gameObject.AddComponent<Outline>();
        markerOutline.effectColor = new Color(0.04f, 0.16f, 0.23f, 0.95f);
        markerOutline.effectDistance = new Vector2(4f, -4f);

        GameObject viewportObject = UIObject("Viewport", root.transform);
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        AnchorTopLeft(viewport, new Vector2(28f, -180f), new Vector2(280f, 1420f));
        viewport.pivot = new Vector2(0f, 1f);
        viewport.anchoredPosition = new Vector2(28f, -180f);
        viewportObject.AddComponent<RectMask2D>();

        GameObject itemsObject = UIObject("ItemsRoot", viewport.transform);
        RectTransform itemsRoot = itemsObject.GetComponent<RectTransform>();
        itemsRoot.anchorMin = itemsRoot.anchorMax = new Vector2(0f, 1f);
        itemsRoot.pivot = new Vector2(0f, 1f);
        itemsRoot.anchoredPosition = Vector2.zero;
        itemsRoot.sizeDelta = new Vector2(270f, 1420f);

        var serialized = new SerializedObject(panel);
        serialized.FindProperty("viewport").objectReferenceValue = viewport;
        serialized.FindProperty("itemsRoot").objectReferenceValue = itemsRoot;
        serialized.FindProperty("itemPrefab").objectReferenceValue = itemPrefab;
        serialized.FindProperty("itemHeight").floatValue = 148f;
        serialized.FindProperty("spacing").floatValue = 12f;
        serialized.FindProperty("fallbackViewportHeight").floatValue = 1420f;
        serialized.FindProperty("bottomFadeHeight").floatValue = 240f;
        serialized.FindProperty("transitionDuration").floatValue = 0.22f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static void RepairLegacyViewport()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PanelPath);
        try
        {
            RectTransform viewport = contents.transform.Find("Viewport") as RectTransform;
            if (viewport == null || !Mathf.Approximately(viewport.pivot.x, 0.5f)
                || !Mathf.Approximately(viewport.anchoredPosition.x, 28f))
                return;
            viewport.pivot = new Vector2(0f, 1f);
            viewport.anchoredPosition = new Vector2(28f, -180f);
            PrefabUtility.SaveAsPrefabAsset(contents, PanelPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void Register(UIConfig config)
    {
        UIRegistry registry = AssetDatabase.LoadAssetAtPath<UIRegistry>(RegistryPath);
        if (registry == null)
            throw new InvalidOperationException("UIRegistry is missing: " + RegistryPath);
        var serialized = new SerializedObject(registry);
        SerializedProperty entries = serialized.FindProperty("configs");
        for (int index = 0; index < entries.arraySize; index++)
            if (entries.GetArrayElementAtIndex(index).objectReferenceValue == config)
                return;
        entries.InsertArrayElementAtIndex(entries.arraySize);
        entries.GetArrayElementAtIndex(entries.arraySize - 1).objectReferenceValue = config;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(registry);
    }

    private static GameObject UIObject(string name, Transform parent)
    {
        var result = new GameObject(name, typeof(RectTransform));
        result.layer = 5;
        if (parent != null) result.transform.SetParent(parent, false);
        return result;
    }

    private static Image ImageObject(string name, Transform parent, Color color)
    {
        GameObject result = UIObject(name, parent);
        Image image = result.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text TextObject(string name, Transform parent, float size,
        TextAlignmentOptions alignment)
    {
        GameObject result = UIObject(name, parent);
        var text = result.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void PlaceTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(position.x, position.y);
        rect.sizeDelta = size;
    }

    private static void AnchorTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
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

    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RequestPath))
            return;
        try
        {
            File.Delete(RequestPath);
            Build();
        }
        catch (Exception error)
        {
            Debug.LogException(error);
        }
    }
}
