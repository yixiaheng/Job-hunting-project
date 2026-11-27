using System;
using System.IO;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class BattleActionDamageSetup
{
    public const string PanelPath = "Assets/Prefabs/UIPrefabs/HUD/Battle/BattleActionDamagePanel.prefab";
    public const string ConfigPath = "Assets/Config/UI/HUD/BattleActionDamageUIConfig.asset";
    private const string Request = "Temp/BattleActionDamage/request.txt";
    static BattleActionDamageSetup() => EditorApplication.update += Poll;

    [MenuItem("Tools/Battle/Create Missing Action Damage HUD")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before creating HUD assets.");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
        if (prefab == null)
        {
            GameObject root = Rect("BattleActionDamagePanel", null, Vector2.zero, Vector2.zero);
            try
            {
                var rect = (RectTransform)root.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                var panel = root.AddComponent<BattleActionDamagePanel>();
                var content = Rect("DamageContent", root.transform, new Vector2(-90, -150), new Vector2(600, 170));
                var label = Text("Label", content.transform, new Vector2(-18, -4), new Vector2(560, 45), 28, "TOTAL DAMAGE", new Color(.85f, .82f, .7f));
                var value = Text("DamageNumber", content.transform, new Vector2(-18, -52), new Vector2(560, 100), 72, "", new Color(1f, .89f, .52f));
                value.enableAutoSizing = true; value.fontSizeMin = 28; value.fontSizeMax = 72;
                var line = Rect("AccentLine", content.transform, new Vector2(-18, -158), new Vector2(160, 3)).AddComponent<Image>();
                line.color = new Color(1f, .89f, .52f, .7f); line.raycastTarget = false;
                var serialized = new SerializedObject(panel);
                serialized.FindProperty("content").objectReferenceValue = content;
                serialized.FindProperty("damageText").objectReferenceValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                content.SetActive(false);
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        var config = AssetDatabase.LoadAssetAtPath<UIConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<UIConfig>();
            config.id = UIId.BattleActionDamage;
            config.prefab = prefab.GetComponent<BattleActionDamagePanel>();
            config.layer = UILayer.HUD; config.space = UISpace.Screen;
            config.instanceMode = UIInstanceMode.Single; config.cacheMode = UICacheMode.DestroyOnClose;
            config.lifetime = UILifetime.Scene; config.handlesBack = false;
            AssetDatabase.CreateAsset(config, ConfigPath);
        }
        var registry = AssetDatabase.LoadAssetAtPath<UIRegistry>("Assets/Config/UI/UIRegistry.asset");
        var so = new SerializedObject(registry); var entries = so.FindProperty("configs");
        bool registered = false;
        for (int i = 0; i < entries.arraySize; i++) if (entries.GetArrayElementAtIndex(i).objectReferenceValue == config) registered = true;
        if (!registered)
        {
            entries.InsertArrayElementAtIndex(entries.arraySize);
            entries.GetArrayElementAtIndex(entries.arraySize - 1).objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(registry);
        }
        AssetDatabase.SaveAssetIfDirty(config);
    }
    private static GameObject Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        if (parent != null) go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.anchoredPosition = position; rect.sizeDelta = size;
        return go;
    }
    private static TMP_Text Text(string name, Transform parent, Vector2 position, Vector2 size, float fontSize, string value, Color color)
    {
        var text = Rect(name, parent, position, size).AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset; text.fontSize = fontSize; text.text = value;
        text.color = color; text.alignment = TextAlignmentOptions.MidlineRight;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.raycastTarget = false;
        return text;
    }
    private static void Poll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Request)) return;
        string command = File.ReadAllText(Request).Trim(); File.Delete(Request);
        try { if (command == "setup") Build(); else if (command == "test") BattleActionDamageVerification.Run(); }
        catch (Exception error) { File.WriteAllText("Temp/BattleActionDamage/error.txt", error.ToString()); Debug.LogException(error); }
    }
}
