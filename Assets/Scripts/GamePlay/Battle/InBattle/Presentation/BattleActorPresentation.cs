using System;
using UnityEngine;

namespace Battle.Presentation
{
    /// <summary>Authored anchors and body actions. Never owns combat rules.</summary>
    [DisallowMultipleComponent]
    public sealed class BattleActorPresentation : MonoBehaviour
    {
        [SerializeField] private Animator bodyAnimator;
        [SerializeField] private Transform meleeStand;
        [SerializeField] private Transform hit;
        [SerializeField] private Transform foot;
        [SerializeField] private Transform cast;
        [SerializeField] private AnimationClip lightHit;
        [SerializeField] private AnimationClip death;
        [SerializeField] private BattleSkillOverride[] skills = Array.Empty<BattleSkillOverride>();
        public Animator Animator => bodyAnimator != null ? bodyAnimator : GetComponentInChildren<Animator>(true);
        public Transform MeleeStand => meleeStand;
        public AnimationClip LightHit => lightHit;
        public AnimationClip Death => death;
        public Transform Anchor(BattleAnchor kind) => kind == BattleAnchor.Foot ? foot != null ? foot : transform
            : kind == BattleAnchor.Cast ? cast != null ? cast : hit != null ? hit : transform : hit != null ? hit : transform;
        public BattleSequenceDefinition Resolve(int id)
        { foreach (var entry in skills) if (entry.SkillId == id && entry.Sequence != null) return entry.Sequence; return null; }
        public void ConfigureAnchors(Transform stand, Transform body, Transform ground, Transform emission)
        { meleeStand = stand; hit = body; foot = ground; cast = emission; }
        private void OnDrawGizmosSelected()
        {
            if (meleeStand != null) { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(meleeStand.position, .15f); Gizmos.DrawRay(meleeStand.position, meleeStand.forward); }
            if (hit != null) { Gizmos.color = Color.red; Gizmos.DrawWireSphere(hit.position, .12f); }
        }
    }
    [Serializable]
    public sealed class BattleSkillOverride
    {
        [SerializeField] private int skillId;
        [SerializeField] private BattleSequenceDefinition sequence;
        public int SkillId => skillId;
        public BattleSequenceDefinition Sequence => sequence;
    }
}
