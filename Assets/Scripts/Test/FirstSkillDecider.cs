using System.Collections.Generic;
using UnityEngine;

public class FirstSkillDecider : IActionDecider
{
    public BattleAction Decide(BattleUnit caster, IReadOnlyList<BattleUnit> units)
    {
        if (caster.skills == null || caster.skills.Count == 0)
            return null;

        SkillConfig skillConfig = SelectSkill(caster, units);
        BattleUnit target = SelectBattleUnit(caster, units, skillConfig);

        return new BattleAction{skill = skillConfig, mainTarget = target};
    }

    public SkillConfig SelectSkill(BattleUnit caster, IReadOnlyList<BattleUnit> units)
    {
        //优先终结技
        foreach(var skill in caster.skills)
        {
            if(skill.kind == SkillKind.Ultimate && caster.currentEnergy >= skill.energyCost)
            {
                return skill;
            }
        }
        foreach(var skill in caster.skills)
        {
            if(skill.kind != SkillKind.Ultimate)
            {
                return skill;
            }
        }
        return null;
    }
    public BattleUnit SelectBattleUnit(BattleUnit caster, IReadOnlyList<BattleUnit> units, SkillConfig skillConfig)
    {
        BattleUnit unit = null;
        Faction targetFaction;
        if(skillConfig.targetSide == TargetSide.OurSide)
            targetFaction = caster.faction;
        else
            targetFaction = caster.faction == Faction.OurSide ? Faction.Enemy : Faction.OurSide;

        foreach(var u in units)
        {
            if(u.faction == targetFaction)
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
