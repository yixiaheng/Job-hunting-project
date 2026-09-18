using UnityEngine;

public class FirstSkillDecider : IActionDecider
{
    public BattleAction Decide(BattleUnit caster)
    {
        if (caster.skills == null || caster.skills.Count == 0)
            return null;
        return new BattleAction { skill = caster.skills[0], mainTarget = null };
    }
}
