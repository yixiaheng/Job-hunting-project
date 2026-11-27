using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 局外Unit数据
/// </summary>
[CreateAssetMenu(fileName = "UnitConfig", menuName = "Battle/UnitConfig")]
public class UnitConfig : ScriptableObject
{
    public string id;
    public string displayName;
    public Sprite icon;
    public Game.Characters.CharacterGrowthConfig growth;
    public ElementType element;
    public PathType path;
    [SerializeField, InspectorName("基础属性配置")]
    private Game.Characters.UnitBaseStatsConfig baseStats;
    public Game.Characters.UnitBaseStatsConfig BaseStats => baseStats != null ? baseStats
        : throw new System.InvalidOperationException("单位缺少基础属性配置：" + id);

    public GameObject prefab;
    public List<SkillConfig> skills = new();
    public SkillConfig talentAction;
}
