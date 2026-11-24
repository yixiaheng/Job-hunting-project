using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Battle.Presentation
{
    public enum BattleStageRole { Caster, MainTarget }
    [Serializable]
    public sealed class BattleStagePoseBehaviour : PlayableBehaviour
    {
        [SerializeField] private BattleStageRole role;
        [SerializeField] private Vector3 fromPosition;
        [SerializeField] private Vector3 toPosition;
        [SerializeField] private Vector3 fromEuler;
        [SerializeField] private Vector3 toEuler;
        [SerializeField] private AnimationCurve progress = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private AnimationCurve height = AnimationCurve.Linear(0, 0, 1, 0);
        [SerializeField] private AnimationCurve lateral = AnimationCurve.Linear(0, 0, 1, 0);
        [SerializeField] private AnimationCurve forward = AnimationCurve.Linear(0, 0, 1, 0);
        public BattleStageRole Role => role;
        public Pose Evaluate(float t) => new(Vector3.LerpUnclamped(fromPosition, toPosition, progress.Evaluate(t)) + new Vector3(lateral.Evaluate(t), height.Evaluate(t), forward.Evaluate(t)), Quaternion.Slerp(Quaternion.Euler(fromEuler), Quaternion.Euler(toEuler), progress.Evaluate(t)));
    }
    public sealed class BattleStagePoseClip : PlayableAsset, ITimelineClipAsset
    {
        [SerializeField] private BattleStagePoseBehaviour pose = new();
        public ClipCaps clipCaps => ClipCaps.Blending;
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) => ScriptPlayable<BattleStagePoseBehaviour>.Create(graph, pose);
    }
}
