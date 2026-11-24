using System;
using UnityEngine;
using UnityEngine.Timeline;

namespace Battle.Presentation
{
    public enum BattleSkillTemplate { InPlace, MeleeApproach, Ranged, Staged, Support }
    public enum BattleBodyDriver { Timeline, AnimatorController }
    public enum BattleStageSpace { BattlefieldArea, IsolatedSet }
    public enum BattleReturnMode { Run, Backstep, Restore }
    public enum BattleAnchor { Body, Foot, Cast }
    public enum BattleProjectileTiming { FixedTime, FixedSpeed }

    [Serializable]
    public sealed class BattleTravelSettings
    {
        [SerializeField, Min(.01f)] private float preferredSeconds = .4f;
        [SerializeField, Min(.01f)] private float minimumSpeed = 2;
        [SerializeField, Min(.01f)] private float maximumSpeed = 12;
        [SerializeField, Min(.01f)] private float referenceSpeed = 4;
        [SerializeField] private Vector2 playbackLimits = new(.5f, 2f);
        public float Duration(float distance) => distance < .001f ? 0 : distance / Mathf.Clamp(distance / Mathf.Max(.01f, preferredSeconds), Mathf.Max(.01f, minimumSpeed), Mathf.Max(minimumSpeed, maximumSpeed));
        public float Playback(float speed) => Mathf.Clamp(speed / Mathf.Max(.01f, referenceSpeed), Mathf.Max(.01f, playbackLimits.x), Mathf.Max(playbackLimits.x, playbackLimits.y));
    }

    [Serializable]
    public sealed class BattleSkillAnimation
    {
        [SerializeField] private BattleBodyDriver driver;
        [SerializeField] private string approachState = "Base Layer.Approach";
        [SerializeField] private string attackState = "Base Layer.Attack";
        [SerializeField] private string returnState = "Base Layer.Return";
        [SerializeField] private string approachRate = "ApproachPlaybackRate";
        [SerializeField] private string returnRate = "ReturnPlaybackRate";
        [SerializeField, Min(0)] private float blendSeconds = .1f;
        [SerializeField, Min(.01f)] private float controllerAttackSeconds = .8f;
        [SerializeField, Min(0)] private float controllerImpactSeconds = .45f;
        public BattleBodyDriver Driver => driver;
        public string ApproachState => approachState;
        public string AttackState => attackState;
        public string ReturnState => returnState;
        public string ApproachRate => approachRate;
        public string ReturnRate => returnRate;
        public float BlendSeconds => blendSeconds;
        public float AttackSeconds => Mathf.Max(.01f, controllerAttackSeconds);
        public float ImpactSeconds => Mathf.Clamp(controllerImpactSeconds, 0, AttackSeconds);
    }

    [Serializable]
    public sealed class BattleProjectileSettings
    {
        [SerializeField] private bool enabled;
        [SerializeField] private GameObject prefab;
        [SerializeField] private BattleProjectileTiming timing;
        [SerializeField, Min(.01f)] private float seconds = .35f;
        [SerializeField, Min(.01f)] private float speed = 15;
        [SerializeField] private bool followTarget;
        public bool Enabled => enabled;
        public GameObject Prefab => prefab;
        public bool FollowTarget => followTarget;
        public float Duration(float distance) => timing == BattleProjectileTiming.FixedSpeed ? Mathf.Max(.01f, distance / Mathf.Max(.01f, speed)) : Mathf.Max(.01f, seconds);
    }

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
        [Header("技能模板（旧资产默认原地播放）")]
        [SerializeField] private BattleSkillTemplate template;
        [SerializeField] private BattleSkillAnimation animation = new();
        [Header("近战攻击位置：目标 MeleeStand 局部坐标")]
        [SerializeField] private Vector3 standOffset;
        [SerializeField] private Vector3 standEuler;
        [SerializeField] private BattleTravelSettings approach = new();
        [SerializeField] private BattleTravelSettings returning = new();
        [SerializeField] private BattleReturnMode returnMode;
        [Header("命中／生效表现")]
        [SerializeField] private GameObject impactPrefab;
        [SerializeField] private BattleAnchor impactAnchor;
        [SerializeField] private Vector3 impactOffset;
        [SerializeField, Min(.01f)] private float effectLifetime = 1;
        [SerializeField] private BattleProjectileSettings projectile = new();
        [Header("固定演出：相对战场原点")]
        [SerializeField] private BattleStageSpace stageSpace;
        [SerializeField] private Vector3 stageOrigin;
        [SerializeField] private Vector3 isolatedSetOffset = new(0, -1000, 0);
        [SerializeField] private Vector3 stageEuler;
        [SerializeField] private Vector3 casterPosition = new(0, 0, -1);
        [SerializeField] private Vector3 casterEuler;
        [SerializeField] private Vector3 targetPosition;
        [SerializeField] private Vector3 targetEuler = new(0, 180, 0);
        [SerializeField] private Vector3 additionalTargetSpacing = new(1.8f, 0, 0);
        [Header("界面与安全")]
        [SerializeField] private bool hideCommands = true;
        [SerializeField, Min(1)] private float timeoutSeconds = 60;
        public BattleSkillTemplate Template => stagePrefab != null ? BattleSkillTemplate.Staged : template;
        public BattleSkillAnimation Animation => animation;
        public Vector3 StandOffset => standOffset;
        public Vector3 StandEuler => standEuler;
        public BattleTravelSettings Approach => approach;
        public BattleTravelSettings Returning => returning;
        public BattleReturnMode ReturnMode => returnMode;
        public GameObject ImpactPrefab => impactPrefab;
        public BattleAnchor ImpactAnchor => impactAnchor;
        public Vector3 ImpactOffset => impactOffset;
        public float EffectLifetime => Mathf.Max(.01f, effectLifetime);
        public BattleProjectileSettings Projectile => projectile;
        public BattleStageSpace StageSpace => stagePrefab != null ? BattleStageSpace.IsolatedSet : stageSpace;
        public Vector3 StageOrigin => stageOrigin;
        public Vector3 IsolatedSetOffset => isolatedSetOffset;
        public Quaternion StageRotation => Quaternion.Euler(stageEuler);
        public Pose CasterPose => new(casterPosition, Quaternion.Euler(casterEuler));
        public Pose TargetPose => new(targetPosition, Quaternion.Euler(targetEuler));
        public Vector3 AdditionalTargetSpacing => additionalTargetSpacing;
        public bool HideCommands => hideCommands || hideBattleHUD;
        public float TimeoutSeconds => Mathf.Max(1, timeoutSeconds);
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
