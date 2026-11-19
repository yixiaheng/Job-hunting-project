using System.Collections.Generic;

namespace Battle.Logic
{
    public enum BattleEffectRecordType
    {
        Damage,
        Heal,
        Shield,
        Energy,
        SkillPoint,
        BuffApplied,
        BuffRemoved,
        ActionAdvanced,
        FollowUp
    }

    public readonly struct BattleEffectRecord
    {
        public BattleEffectRecordType Type { get; }
        public BattleUnit Source { get; }
        public BattleUnit Target { get; }
        public int Amount { get; }
        public int Before { get; }
        public int After { get; }
        public string Key { get; }

        public BattleEffectRecord(BattleEffectRecordType type, BattleUnit source,
            BattleUnit target, int amount = 0, int before = 0, int after = 0, string key = null)
        {
            Type = type;
            Source = source;
            Target = target;
            Amount = amount;
            Before = before;
            After = after;
            Key = key;
        }
    }

    public sealed class BattleActionRecord
    {
        public int Turn { get; internal set; }
        public double StartedAt { get; internal set; }
        public double EndedAt { get; internal set; }
        public BattleUnit Caster { get; internal set; }
        public SkillConfig Skill { get; internal set; }
        public BattleUnit MainTarget { get; internal set; }
        public int SkillPointsBefore { get; internal set; }
        public int SkillPointsAfter { get; internal set; }
        public int EnergyBefore { get; internal set; }
        public int EnergyAfter { get; internal set; }
        public IReadOnlyList<BattleEffectRecord> Effects => effects;
        internal readonly List<BattleEffectRecord> effects = new();
    }
}
