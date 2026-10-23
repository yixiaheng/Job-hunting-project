using UnityEngine;

namespace Battle.Logic
{
    public sealed class EnergyService
    {
        public int Execute(BattleUnit target, EffectConfig effect)
        {
            if (target == null) throw new System.ArgumentNullException(nameof(target));
            if (effect == null) throw new System.ArgumentNullException(nameof(effect));
            if (!target.IsActive) return 0;

            int before = target.currentEnergy;
            target.currentEnergy += Mathf.RoundToInt(effect.value);
            int actual = target.currentEnergy - before;
            Debug.Log($"    {Name(target)} 能量 {before}→{target.currentEnergy}");
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
