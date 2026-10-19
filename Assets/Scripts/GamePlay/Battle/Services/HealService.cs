using UnityEngine;

namespace Battle.Logic
{
    public sealed class HealService
    {
        public int Execute(BattleUnit caster, BattleUnit target, EffectConfig effect)
        {
            if (caster == null) throw new System.ArgumentNullException(nameof(caster));
            if (target == null) throw new System.ArgumentNullException(nameof(target));
            if (effect == null) throw new System.ArgumentNullException(nameof(effect));
            if (!target.IsActive) return 0;

            int amount = Mathf.Max(1, Mathf.RoundToInt(caster.GetStat(effect.scaleStat) * effect.value));
            int before = target.currentHealth;
            int actual = target.Heal(amount);
            Debug.Log($"    治疗 {Name(target)}  +{actual}  HP {before}→{target.currentHealth}/{target.maxHealth}");
            return actual;
        }

        private static string Name(BattleUnit unit)
        {
            if (unit == null) return "?";
            if (!string.IsNullOrEmpty(unit.displayName)) return unit.displayName;
            return unit.faction == Faction.OurSide ? $"我方{unit.pos}" : $"敌方{unit.pos}";
        }
    }
}
