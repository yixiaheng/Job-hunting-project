using System.Collections.Generic;
using Battle.Logic;
using Sirenix.OdinInspector;
using UnityEngine;

public class ActionQueueSmokeTest : MonoBehaviour
{
    [Button]
    void TestQueue()
    {
        var basic = ScriptableObject.CreateInstance<SkillConfig>();
        basic.id = 1;
        basic.Name = "Basic";
        basic.kind = SkillKind.Basic;
        basic.energyCost = 0;
        basic.energyGain = 20;
        basic.targetRule = TargetRule.Single;
        basic.targetSide = TargetSide.OtherSide;
        basic.effectConfigs = new List<EffectConfig>
        {
            new EffectConfig { effectType = EffectType.Damage, value = 1f, scaleStat = StatType.Attack },
        };

        var aoe = ScriptableObject.CreateInstance<SkillConfig>();
        aoe.id = 2;
        aoe.Name = "AoE";
        aoe.kind = SkillKind.Skill;
        aoe.energyCost = 0;
        aoe.energyGain = 10;
        aoe.targetRule = TargetRule.AoE;
        aoe.targetSide = TargetSide.OtherSide;
        aoe.effectConfigs = new List<EffectConfig>
        {
            new EffectConfig { effectType = EffectType.Damage, value = 0.8f, scaleStat = StatType.Attack },
        };

        var heal = ScriptableObject.CreateInstance<SkillConfig>();
        heal.id = 3;
        heal.Name = "Heal";
        heal.kind = SkillKind.Skill;
        heal.energyCost = 0;
        heal.energyGain = 10;
        heal.targetRule = TargetRule.Single;
        heal.targetSide = TargetSide.OurSide;
        heal.effectConfigs = new List<EffectConfig>
        {
            new EffectConfig { effectType = EffectType.Heal, value = 0.3f, scaleStat = StatType.Attack },
        };

        var units = new[]
        {
            new BattleUnit { faction = Faction.OurSide, pos = 0, attack = 20, defence = 5, speed = 100, currentHealth = 80, tieBreaker = 0 },
            new BattleUnit { faction = Faction.OurSide, pos = 1, attack = 18, defence = 6, speed = 150, currentHealth = 70, tieBreaker = 1 },
            new BattleUnit { faction = Faction.OurSide, pos = 2, attack = 22, defence = 4, speed = 120, currentHealth = 75, tieBreaker = 2 },
            new BattleUnit { faction = Faction.Enemy,   pos = 0, attack = 16, defence = 5, speed = 110, currentHealth = 60, tieBreaker = 3 },
            new BattleUnit { faction = Faction.Enemy,   pos = 1, attack = 19, defence = 3, speed = 90,  currentHealth = 65, tieBreaker = 4 },
            new BattleUnit { faction = Faction.Enemy,   pos = 2, attack = 15, defence = 7, speed = 130, currentHealth = 55, tieBreaker = 5 },
            new BattleUnit { faction = Faction.Enemy,   pos = 3, attack = 12, defence = 2, speed = 140, currentHealth = 55, tieBreaker = 6 },
            new BattleUnit { faction = Faction.Enemy,   pos = 4, attack = 12, defence = 2, speed = 100, currentHealth = 55, tieBreaker = 7 },
        };

        foreach (var u in units)
        {
            if (u.faction == Faction.Enemy)
                u.skills.Add(aoe);
            else
                u.skills.Add(basic);
        }

        var result = new BattleFlow(units, new FirstSkillDecider()).Run();
        Debug.Log($"结果: {result}");
    }
}
