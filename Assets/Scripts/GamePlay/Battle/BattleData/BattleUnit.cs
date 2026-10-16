using System.Collections.Generic;
using Battle.Events;
using Game;
using UnityEngine;

/// <summary>
/// 战斗时单元数据，不需要SO资产存储
/// </summary>
public class BattleUnit
{
    public string displayName;
    public Faction faction;
    public int pos;
    public int maxHealth;
    public int currentHealth;
    public int shield;
    public bool IsActive => currentHealth > 0;
    public int attack;
    public int defence;
    public int speed;
    public int currentEnergy;
    public double nextActionTime;   // 下次行动的时刻（绝对时间戳）

    public int tieBreaker;
    public GameObject gameObject;

    public List<SkillConfig> skills = new ();

    public List<BuffInstance> buffs = new();

    public int GetStat(StatType stat)
    {
        int baseStat = stat switch
        {
            StatType.Defence => defence,
            StatType.Speed => speed,
            StatType.Health => currentHealth,
            StatType.Attack => attack,
            _ => attack
        };

        float mod = 0f;
        foreach (var b in buffs)
        {
            if (b.config != null && b.config.stat == stat)
                mod += b.config.value * b.stacks;
        }
        return Mathf.Max(0, Mathf.RoundToInt(baseStat * (1f + mod)));
    }
    

}
