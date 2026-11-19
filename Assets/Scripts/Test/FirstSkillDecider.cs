using System.Collections.Generic;
using UnityEngine;

public class FirstSkillDecider : IActionDecider
{
    public BattleAction Decide(BattleUnit caster, IReadOnlyList<BattleUnit> units)
    {
        if (caster == null || caster.skills == null || caster.skills.Count == 0)
            return null;

        SkillConfig skillConfig = SelectSkill(caster, units);
        if (skillConfig == null) return null;
        BattleUnit target = SelectBattleUnit(caster, units, skillConfig);

        return new BattleAction{caster = caster, skill = skillConfig, mainTarget = target};
    }

    public SkillConfig SelectSkill(BattleUnit caster, IReadOnlyList<BattleUnit> units)
    {
        if (caster?.skills == null) return null;
        //优先终结技
        foreach(var skill in caster.skills)
        {
            if(skill != null && skill.kind == SkillKind.Ultimate && caster.currentEnergy >= skill.energyCost)
            {
                return skill;
            }
        }
        foreach(var skill in caster.skills)
        {
            if(skill != null && skill.kind != SkillKind.Ultimate && caster.currentEnergy >= skill.energyCost)
            {
                return skill;
            }
        }
        return null;
    }
    public BattleUnit SelectBattleUnit(BattleUnit caster, IReadOnlyList<BattleUnit> units, SkillConfig skillConfig)
    {
        if (caster == null || skillConfig == null || units == null) return null;
        BattleUnit unit = null;
        Faction targetFaction;
        if(skillConfig.targetSide == TargetSide.OurSide)
            targetFaction = caster.faction;
        else
            targetFaction = caster.faction == Faction.OurSide ? Faction.Enemy : Faction.OurSide;

        foreach(var u in units)
        {
            if(u != null && u.IsActive && u.faction == targetFaction)
            {
                if(unit == null)
                    unit = u;
                if(u.currentHealth < unit.currentHealth)
                    unit = u;
            }
        }
        return unit;
    }
}
