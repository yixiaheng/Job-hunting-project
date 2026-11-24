using UnityEngine;
using UnityEngine.Timeline;
using Battle.Logic;

namespace Battle.Presentation
{
    public enum BattleCueKind { LegacyIndex, ImpactGroup, CueOnly, LaunchGroup, FlushRemaining }
    /// <summary>Executed by the sequence player, including when one frame crosses multiple markers.</summary>
    public sealed class BattleImpactMarker : Marker
    {
        [Tooltip("Zero-based resolution effect index. -1 applies every remaining effect.")]
        public int effectIndex = -1;
        [Tooltip("Index among damage/heal records; resource records do not shift the chosen impact. -1 still applies all remaining effects.")]
        public bool healthEffectsOnly = true;
        [SerializeField] private BattleCueKind kind;
        [SerializeField, Tooltip("EffectConfig index; -1 selects all primary recipients and unconsumed effects.")] private int effectGroup = -1;
        public BattleCueKind Kind => kind;
        public int EffectGroup => effectGroup;

        public int ResolveIndex(BattleActionRecord record)
        {
            if (effectIndex < 0 || !healthEffectsOnly) return effectIndex;
            int ordinal = 0;
            for (int i = 0; i < record.Effects.Count; i++)
                if (record.Effects[i].Type == BattleEffectRecordType.Damage || record.Effects[i].Type == BattleEffectRecordType.Heal || record.Effects[i].Type == BattleEffectRecordType.FollowUp)
                    if (ordinal++ == effectIndex) return i;
            return int.MaxValue;
        }
    }
}
