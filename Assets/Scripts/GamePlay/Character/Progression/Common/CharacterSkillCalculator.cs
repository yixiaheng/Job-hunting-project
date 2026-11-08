using System.Text;

namespace Game.Characters
{
    /// <summary>界面预览与战斗快照共用同一个按效果键匹配的技能成长计算。</summary>
    public static class CharacterSkillCalculator
    {
        public static float Factor(CharacterGrowthConfig growth, CharacterProgress progress, SkillConfig skill, EffectConfig effect)
        {
            if (growth == null || !effect.growsWithSkill)
                return 1f;
            var modifier = growth.effectGrowth?.Find(item => item.effectKey == effect.key);
            if (modifier == null)
                return 1f;
            float value = 1 + modifier.perSkillLevel * (progress.SkillLevel(skill.kind) - 1);
            return value + (progress.rank >= modifier.requiredRank ? modifier.rankBonus : 0f);
        }

        public static string Describe(CharacterGrowthConfig growth, CharacterProgress progress, SkillConfig skill)
        {
            var text = new StringBuilder();
            if (skill?.effectConfigs == null)
                return string.Empty;
            foreach (var effect in skill.effectConfigs)
            {
                if (effect == null)
                    continue;
                float factor = Factor(growth, progress, skill, effect);
                if (text.Length > 0)
                    text.AppendLine();
                text.Append(effect.effectType switch
                {
                    EffectType.Damage => "伤害倍率",
                    EffectType.Heal => "治疗倍率",
                    EffectType.AddShield => "护盾倍率",
                    EffectType.ApplyBuff => effect.buff != null ? effect.buff.Name : "状态效果",
                    EffectType.GainEnergy => "恢复能量",
                    EffectType.AdvanceAction => "行动提前",
                    EffectType.RemoveBuff => "驱散增益",
                    EffectType.RemoveDeBuff => "净化减益",
                    _ => effect.effectType.ToString()});
                if (effect.effectType == EffectType.ApplyBuff && effect.buff != null)
                    text.Append(' ').Append((effect.buff.value * effect.buffValueMultiplier * factor).ToString("P1"));
                else if (effect.effectType != EffectType.RemoveBuff && effect.effectType != EffectType.RemoveDeBuff)
                    text.Append(' ').Append(effect.effectType == EffectType.GainEnergy ? effect.value.ToString("F0") : (effect.value * factor).ToString("P1"));
            }

            return text.ToString();
        }
    }
}
