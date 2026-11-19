using System.Collections.Generic;
using UnityEngine;

/// <summary>确定性的敌人决策：对所有可用技能和主目标评分，选择收益最高的合法行动。</summary>
public sealed class EnemyActionDecider : IActionDecider
{
    public BattleAction Decide(BattleUnit caster, IReadOnlyList<BattleUnit> units)
    {
        if (caster == null || units == null || caster.skills == null)
            return null;

        BattleAction best = null;
        float bestScore = float.NegativeInfinity;
        foreach (SkillConfig skill in caster.skills)
        {
            if (skill == null || skill.effectConfigs == null || skill.effectConfigs.Count == 0
                || skill.energyCost < 0 || skill.energyCost > caster.currentEnergy)
                continue;

            BattleUnit target = SelectMainTarget(caster, units, skill);
            float score = Score(caster, units, skill, target);
            if (score <= bestScore)
                continue;
            bestScore = score;
            best = new BattleAction { caster = caster, skill = skill, mainTarget = target };
        }
        return best;
    }

    private static BattleUnit SelectMainTarget(BattleUnit caster,
        IReadOnlyList<BattleUnit> units, SkillConfig skill)
    {
        if (skill.targetRule == TargetRule.Self || skill.targetRule == TargetRule.AoE)
            return null;

        Faction faction = skill.targetSide == TargetSide.OurSide
            ? caster.faction
            : caster.faction == Faction.OurSide ? Faction.Enemy : Faction.OurSide;
        bool support = IsSupport(skill);
        BattleUnit best = null;
        float bestValue = float.NegativeInfinity;
        foreach (BattleUnit unit in units)
        {
            if (unit == null || !unit.IsActive || unit.faction != faction)
                continue;
            float value = support
                ? 1f - (float)unit.currentHealth / Mathf.Max(1, unit.maxHealth)
                : 1f - (float)unit.currentHealth / Mathf.Max(1, unit.maxHealth);
            if (skill.targetRule == TargetRule.Spread)
                value += .2f * AdjacentCount(units, faction, unit.pos);
            if (best == null || value > bestValue
                || Mathf.Approximately(value, bestValue) && unit.pos < best.pos)
            {
                best = unit;
                bestValue = value;
            }
        }
        return best;
    }

    private static float Score(BattleUnit caster, IReadOnlyList<BattleUnit> units,
        SkillConfig skill, BattleUnit mainTarget)
    {
        float score = skill.kind switch
        {
            SkillKind.Ultimate => 18f,
            SkillKind.Skill => 8f,
            _ => 1f
        };
        foreach (EffectConfig effect in skill.effectConfigs)
        {
            if (effect == null)
                continue;
            TargetSide side = effect.overrideTarget ? effect.targetSide : skill.targetSide;
            TargetRule rule = effect.overrideTarget ? effect.target : skill.targetRule;
            Faction faction = side == TargetSide.OurSide
                ? caster.faction
                : caster.faction == Faction.OurSide ? Faction.Enemy : Faction.OurSide;
            int targets = TargetCount(caster, units, faction, rule, mainTarget);
            switch (effect.effectType)
            {
                case EffectType.Damage:
                    score += targets * (25f + Mathf.Max(0f, effect.value) * 10f);
                    if (mainTarget != null)
                    {
                        int predicted = Mathf.Max(1,
                            Mathf.RoundToInt(caster.GetStat(effect.scaleStat) * effect.value)
                            - mainTarget.GetStat(StatType.Defence));
                        if (predicted >= mainTarget.currentHealth)
                            score += 45f;
                    }
                    break;
                case EffectType.Heal:
                    score += MissingHealth(units, faction, rule, mainTarget) * .08f;
                    break;
                case EffectType.AddShield:
                    score += targets * 14f;
                    break;
                case EffectType.ApplyBuff:
                    score += targets * (effect.buff != null && effect.buff.isDebuff ? 16f : 12f);
                    break;
                case EffectType.GainEnergy:
                case EffectType.GainSkillPoint:
                    score += targets * 8f;
                    break;
                case EffectType.AdvanceAction:
                    score += targets * 10f;
                    break;
                case EffectType.RemoveBuff:
                case EffectType.RemoveDeBuff:
                    score += targets * 6f;
                    break;
            }
        }
        return score;
    }

    private static bool IsSupport(SkillConfig skill)
    {
        foreach (EffectConfig effect in skill.effectConfigs)
            if (effect != null && effect.effectType != EffectType.Damage)
                return true;
        return false;
    }

    private static int AdjacentCount(IReadOnlyList<BattleUnit> units, Faction faction, int position)
    {
        int count = 0;
        foreach (BattleUnit unit in units)
            if (unit != null && unit.IsActive && unit.faction == faction
                && Mathf.Abs(unit.pos - position) == 1)
                count++;
        return count;
    }

    private static int TargetCount(BattleUnit caster, IReadOnlyList<BattleUnit> units,
        Faction faction, TargetRule rule, BattleUnit mainTarget)
    {
        if (rule == TargetRule.Self)
            return caster.IsActive ? 1 : 0;
        if (rule == TargetRule.Single)
            return mainTarget != null ? 1 : AliveCount(units, faction) > 0 ? 1 : 0;
        if (rule == TargetRule.Spread)
            return mainTarget == null ? 0 : 1 + AdjacentCount(units, faction, mainTarget.pos);
        return AliveCount(units, faction);
    }

    private static int MissingHealth(IReadOnlyList<BattleUnit> units, Faction faction,
        TargetRule rule, BattleUnit mainTarget)
    {
        if (rule == TargetRule.Single || rule == TargetRule.Spread)
            return mainTarget == null ? 0 : Mathf.Max(0, mainTarget.maxHealth - mainTarget.currentHealth);
        int missing = 0;
        foreach (BattleUnit unit in units)
            if (unit != null && unit.IsActive && unit.faction == faction)
                missing += Mathf.Max(0, unit.maxHealth - unit.currentHealth);
        return missing;
    }

    private static int AliveCount(IReadOnlyList<BattleUnit> units, Faction faction)
    {
        int count = 0;
        foreach (BattleUnit unit in units)
            if (unit != null && unit.IsActive && unit.faction == faction)
                count++;
        return count;
    }
}
