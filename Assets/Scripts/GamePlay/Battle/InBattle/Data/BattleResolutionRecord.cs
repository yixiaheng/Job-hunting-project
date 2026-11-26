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
        public int EffectGroup { get; }
        public int ShieldBefore { get; }
        public int ShieldAfter { get; }

        public BattleEffectRecord(BattleEffectRecordType type, BattleUnit source,
            BattleUnit target, int amount = 0, int before = 0, int after = 0, string key = null,
            int effectGroup = -1, int shieldBefore = -1, int shieldAfter = -1)
        {
            Type = type;
            Source = source;
            Target = target;
            Amount = amount;
            Before = before;
            After = after;
            Key = key;
            EffectGroup = effectGroup; ShieldBefore = shieldBefore; ShieldAfter = shieldAfter;
        }
        internal BattleEffectRecord InGroup(int group) => new(Type, Source, Target, Amount, Before, After, Key, group, ShieldBefore, ShieldAfter);
    }

    public sealed class BattleResolvedEffectTargets
    {
        public int Group { get; }
        public IReadOnlyList<BattleUnit> Targets { get; }
        public BattleResolvedEffectTargets(int group, IEnumerable<BattleUnit> targets)
        { Group = group; Targets = new List<BattleUnit>(targets).AsReadOnly(); }
    }

    public sealed class BattleActionRecord
    {
        public long RequestId { get; internal set; }
        public BattleActionOrigin Origin { get; internal set; }
        public int ActionSequence { get; internal set; }
        public int NormalTurnIndex { get; internal set; }
        public bool ConsumedNormalTurn { get; internal set; }
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
        public IReadOnlyList<BattleUnit> PrimaryTargets { get; internal set; } = System.Array.Empty<BattleUnit>();
        public IReadOnlyList<BattleResolvedEffectTargets> ResolvedTargets => resolvedTargets;
        internal readonly List<BattleResolvedEffectTargets> resolvedTargets = new();
        internal readonly List<BattleEffectRecord> effects = new();
    }
}
