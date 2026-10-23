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
    public int maxHealth;
    public int attack;
    public int defence;
    public int speed;
    public int startEnergy;
    public GameObject prefab;
    public List<SkillConfig> skills = new();
}
