using System.Collections.Generic;
using Battle.Events;
using Battle.Logic;
using Game;
using UnityEngine;

/// <summary>
/// 战斗时单元数据，不需要SO资产存储
/// </summary>
public class BattleUnit
{
    public string displayName;
    public ElementType element;
    public PathType path;
    public Faction faction;
    public int pos;
    public int maxHealth;
    public int currentHealth;
    public System.Guid BattleId { get; internal set; }
    public int shield;
    public bool IsActive => currentHealth > 0;
    public int attack;
    public int defence;
    public int speed;
    public int currentEnergy;
    public int maxEnergy = 100;
    public double nextActionTime;   // 下次行动的时刻（绝对时间戳）

    public int tieBreaker;
    public GameObject gameObject;

    public List<SkillConfig> skills = new ();

    public List<BuffInstance> buffs = new();

    public int TakeDamage(int amount)
    {
        DamageApplication application = ApplyDamage(amount);
        return application.HealthDamage;
    }

    public int Heal(int amount)
    {
        if (amount <= 0 || !IsActive) return 0;
        int before = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        int actual = currentHealth - before;
        if (actual > 0)
        {
            Game.GameEvents.Battle.Publish(new HealthChangedEvent(
                this, before, currentHealth, maxHealth));
        }
        return actual;
    }

    public DamageApplication ApplyDamage(int amount)
    {
        int previousHealth = currentHealth;
        int safeAmount = Mathf.Max(0, amount);
        int shieldDamage = Mathf.Min(shield, safeAmount);
        shield -= shieldDamage;
        int healthDamage = Mathf.Min(currentHealth, safeAmount - shieldDamage);
        currentHealth -= healthDamage;

        if (healthDamage > 0)
        {
            Game.GameEvents.Battle.Publish(new HealthChangedEvent(
                this, previousHealth, currentHealth, maxHealth));
        }

        return new DamageApplication(shieldDamage, healthDamage, previousHealth, currentHealth);
    }

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
