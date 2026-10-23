using UnityEngine;

namespace Battle.Logic
{
    public sealed class BuffService
    {
        public bool Execute(BattleUnit target, EffectConfig effect)
        {
            if (target == null || effect == null || !target.IsActive || effect.buff == null)
                return false;

            BuffConfig config = effect.buff;
            int maxStack = Mathf.Max(1, config.maxStack);
            int addStacks = Mathf.Max(1, effect.stack);

            foreach (BuffInstance instance in target.buffs)
            {
                if (instance.config == null || instance.config.id != config.id) continue;
                instance.stacks = Mathf.Min(maxStack, instance.stacks + addStacks);
                instance.remainingTurns = config.durationTimes;
                Debug.Log($"    Buff {Name(target)} 获得【{config.Name}】 {instance.stacks}层/{maxStack}  持续{instance.remainingTurns}回合  {StatText(target, config.stat)}");
                return true;
            }

            var created = new BuffInstance
            {
                config = config,
                stacks = Mathf.Min(maxStack, addStacks),
                remainingTurns = config.durationTimes
            };
            target.buffs.Add(created);
            Debug.Log($"    Buff {Name(target)} 获得【{config.Name}】 {created.stacks}层/{maxStack}  持续{created.remainingTurns}回合  {StatText(target, config.stat)}");
            return true;
        }

        private static string Name(BattleUnit unit)
        {
            if (unit == null) return "?";
            if (!string.IsNullOrEmpty(unit.displayName)) return unit.displayName;
            return unit.faction == Faction.OurSide ? $"我方{unit.pos}" : $"敌方{unit.pos}";
        }

        private static string StatText(BattleUnit unit, StatType stat)
        {
            return stat switch
            {
                StatType.Defence => $"防御{unit.defence}→{unit.GetStat(stat)}",
                StatType.Attack => $"攻击{unit.attack}→{unit.GetStat(stat)}",
                StatType.Speed => $"速度{unit.speed}→{unit.GetStat(stat)}",
                _ => string.Empty
            };
        }
    }
}
