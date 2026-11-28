using System;
using Game.Characters;
using UnityEditor;
using UnityEngine;

/// <summary>每个单位拥有自己的基础属性子资源；与主体共用文件以保持导入备份原子性。</summary>
public static class UnitBaseStatsAuthoring
{
    public static UnitBaseStatsConfig Ensure(UnitConfig unit)
    {
        var serialized = new SerializedObject(unit);
        var property = serialized.FindProperty("baseStats");
        var stats = property.objectReferenceValue as UnitBaseStatsConfig;
        string path = AssetDatabase.GetAssetPath(unit);
        if (stats == null && !string.IsNullOrEmpty(path))
            foreach (var child in AssetDatabase.LoadAllAssetsAtPath(path))
                if (child is UnitBaseStatsConfig candidate)
                {
                    if (stats != null) throw new InvalidOperationException("存在多个基础属性子资源，请手动指定：" + path);
                    stats = candidate;
                }
        if (stats == null)
        {
            stats = ScriptableObject.CreateInstance<UnitBaseStatsConfig>();
            stats.name = "BaseStats";
        }
        if (!string.IsNullOrEmpty(path))
        {
            if (!AssetDatabase.Contains(stats)) AssetDatabase.AddObjectToAsset(stats, unit);
            else if (AssetDatabase.GetAssetPath(stats) != path)
                throw new InvalidOperationException("基础属性必须是当前单位独占的子资源：" + path);
        }
        // 必须在子资源获得持久fileID之后赋值，否则首次保存引用可能成为fileID:0。
        serialized.Update();
        property = serialized.FindProperty("baseStats");
        property.objectReferenceValue = stats;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return stats;
    }

    public static bool HasDirtyObjects(UnityEngine.Object asset)
    {
        if (EditorUtility.IsDirty(asset)) return true;
        string path = AssetDatabase.GetAssetPath(asset);
        foreach (var child in AssetDatabase.LoadAllAssetsAtPath(path))
            if (child != null && EditorUtility.IsDirty(child)) return true;
        return false;
    }

    // 编辑器生成器/验证统一经过此入口；运行时无可写属性接口。
    public static void Set(UnitConfig unit, string field, object value)
    {
        var stats = Ensure(unit);
        typeof(UnitBaseStatsConfig).GetField(field,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(stats, value);
        EditorUtility.SetDirty(stats);
    }
}

[CustomEditor(typeof(UnitConfig))]
public sealed class UnitConfigEditor : Editor
{
    private Editor statsEditor;
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var reference = serializedObject.FindProperty("baseStats").objectReferenceValue;
        if (reference == null)
        {
            if (GUILayout.Button("创建独立基础属性配置"))
            {
                UnitBaseStatsAuthoring.Ensure((UnitConfig)target);
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssetIfDirty(target);
            }
            return;
        }
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("以下为1级基础值。HP/攻击/防御根据存档等级与全局突破表计算；速度、双暴、元素和击破属性不自然成长。Excel下次导入会覆盖这些基础值。", MessageType.Info);
        CreateCachedEditor(reference, null, ref statsEditor);
        statsEditor.OnInspectorGUI();
        if (GUILayout.Button("查看共用成长表")) Selection.activeObject = CharacterGrowthTable.Shared;
    }
    private void OnDisable() { if (statsEditor != null) DestroyImmediate(statsEditor); }
}
