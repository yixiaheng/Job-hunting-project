using System;
using UnityEngine;
using UnityEngine.Timeline;

namespace Battle.Presentation
{
    [CreateAssetMenu(menuName = "Battle/Presentation/Sequence")]
    public sealed class BattleSequenceDefinition : ScriptableObject
    {
        public TimelineAsset timeline;
        [Tooltip("Optional visual-only stage. Its BattleStage supplies caster/target anchors.")]
        public BattleStage stagePrefab;
        public bool hideBattleHUD;
        [Tooltip("AnimationTrack names bound to the acting model and main target Animator.")]
        public string casterTrack = "Caster";
        public string targetTrack = "Target";
    }
    [Serializable]
    public struct BattleSkillSequence
    {
        public int skillId;
        public Faction casterFaction;
        public BattleSequenceDefinition sequence;
    }
    [Serializable]
    public struct BattleEventSequence
    {
        public string eventId;
        public BattleSequenceDefinition sequence;
    }
}
