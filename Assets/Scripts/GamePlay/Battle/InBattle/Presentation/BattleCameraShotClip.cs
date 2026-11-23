using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Battle.Presentation
{
    [Serializable]
    public sealed class BattleCameraKey
    {
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("time"), InspectorName("time")] [Min(0)] private float timeValue;
        public float time { get => timeValue; set => timeValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("position"), InspectorName("position")] private Vector3 positionValue;
        public Vector3 position { get => positionValue; set => positionValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("euler"), InspectorName("euler")] private Vector3 eulerValue;
        public Vector3 euler { get => eulerValue; set => eulerValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("fov"), InspectorName("fov")] [Range(5,120)] private float fovValue = 45;
        public float fov { get => fovValue; set => fovValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("cut"), InspectorName("cut")] private bool cutValue;
        public bool cut { get => cutValue; set => cutValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("ease"), InspectorName("ease")] private AnimationCurve easeValue = AnimationCurve.EaseInOut(0,0,1,1);
        public AnimationCurve ease { get => easeValue; set => easeValue = value; }
    }
    public enum BattleShotSpace { Battlefield, Caster, Target, TargetGroup }
    [Serializable]
    public sealed class BattleCameraShotBehaviour : PlayableBehaviour
    {
        public BattleShotSpace space = BattleShotSpace.Caster;
        public Vector3 fromPosition = new(1.8f, 1.7f, -3.5f);
        public Vector3 toPosition = new(1.3f, 1.6f, -3f);
        public Vector3 fromEuler = new(10f, 0f, 0f);
        public Vector3 toEuler = new(10f, 0f, 0f);
        public AnimationCurve rotationX = AnimationCurve.Linear(0, 0, 1, 0);
        public AnimationCurve rotationY = AnimationCurve.Linear(0, 0, 1, 0);
        public AnimationCurve rotationZ = AnimationCurve.Linear(0, 0, 1, 0);
        public bool lookAtTarget = true;
        public Vector3 aimOffset = new(0f, 1.1f, 0f);
        public AnimationCurve travel = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Tooltip("Local trajectory offsets; keys use normalized clip time.")]
        public AnimationCurve pathX = AnimationCurve.Linear(0, 0, 1, 0);
        public AnimationCurve pathY = AnimationCurve.Linear(0, 0, 1, 0);
        public AnimationCurve pathZ = AnimationCurve.Linear(0, 0, 1, 0);
        public AnimationCurve fieldOfView = AnimationCurve.Linear(0, 48, 1, 42);
        public AnimationCurve shake = AnimationCurve.Linear(0, 0, 1, 0);
        public float shakeFrequency = 25f;
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("rotationShake")] private Vector3 rotationShakeValue;
        public Vector3 rotationShake { get => rotationShakeValue; set => rotationShakeValue = value; }

        [Tooltip("Freeze reference on first evaluated frame, or follow its animated transform.")]
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("followAnchor")] private bool followAnchorValue;
        public bool followAnchor { get => followAnchorValue; set => followAnchorValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("fitTargetGroup")] private bool fitTargetGroupValue;
        public bool fitTargetGroup { get => fitTargetGroupValue; set => fitTargetGroupValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("useAnchorRotation")] private bool useAnchorRotationValue;
        public bool useAnchorRotation { get => useAnchorRotationValue; set => useAnchorRotationValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("keys")] private BattleCameraKey[] keysValue = Array.Empty<BattleCameraKey>();
        public BattleCameraKey[] keys { get => keysValue; set => keysValue = value; }
        private bool captured;
        private Pose frozen;
        public override void OnPlayableCreate(Playable playable) { captured = false; }
        public void ResetReference() => captured = false;
        public BattleCameraPose Evaluate(BattleShotDirector director, float normalizedTime, float seconds)
        {
            Transform anchor = space == BattleShotSpace.Caster ? director.SequenceCaster
                : space == BattleShotSpace.Target ? director.SequenceTarget : director.SequenceOrigin;
            Vector3 origin = space == BattleShotSpace.TargetGroup ? director.SequenceTargetCenter : anchor != null ? anchor.position : director.BattlefieldOrigin;
            Quaternion basis = useAnchorRotation && anchor != null ? anchor.rotation
                : director.SequenceOrigin != null ? director.SequenceOrigin.rotation : director.BattlefieldRotation;
            if (!captured) { frozen = new Pose(origin, basis); captured = true; }
            if (!followAnchor) { origin = frozen.position; basis = frozen.rotation; }
            float t = travel.Evaluate(normalizedTime);
            Vector3 offset = Vector3.LerpUnclamped(fromPosition, toPosition, t)
                + new Vector3(pathX.Evaluate(normalizedTime), pathY.Evaluate(normalizedTime), pathZ.Evaluate(normalizedTime));
            Quaternion localRotation = Quaternion.SlerpUnclamped(Quaternion.Euler(fromEuler), Quaternion.Euler(toEuler), t)
                * Quaternion.Euler(rotationX.Evaluate(normalizedTime), rotationY.Evaluate(normalizedTime), rotationZ.Evaluate(normalizedTime));
            float fov = fieldOfView.Evaluate(normalizedTime);
            if (keys != null && keys.Length > 0)
            {
                BattleCameraKey before = null, after = null;
                foreach (var key in keys)
                {
                    if (key == null) continue;
                    if (key.time <= seconds && (before == null || key.time > before.time)) before = key;
                    if (key.time > seconds && (after == null || key.time < after.time)) after = key;
                }
                before ??= after; after ??= before;
                if (before != null)
                {
                    float blend = before == after || after.cut ? 0 : Mathf.Clamp01((seconds-before.time)/(after.time-before.time));
                    blend = before.ease != null ? before.ease.Evaluate(blend) : blend;
                    offset = Vector3.LerpUnclamped(before.position, after.position, blend);
                    localRotation = Quaternion.SlerpUnclamped(Quaternion.Euler(before.euler), Quaternion.Euler(after.euler), blend);
                    fov = Mathf.LerpUnclamped(before.fov, after.fov, blend);
                }
            }
            float amplitude = shake.Evaluate(normalizedTime);
            offset += new Vector3(Mathf.Sin(seconds * shakeFrequency * 6.283f), Mathf.Cos(seconds * shakeFrequency * 7.97f), 0) * amplitude;
            Vector3 pos = origin + basis * offset;
            Quaternion rotation = basis * localRotation;
            if (lookAtTarget && director.SequenceTarget != null)
            {
                Vector3 direction = (space == BattleShotSpace.TargetGroup ? director.SequenceTargetCenter : director.SequenceTarget.position + aimOffset) - pos;
                if (direction.sqrMagnitude > .0001f) rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
            rotation *= Quaternion.Euler(Vector3.Scale(rotationShake, new Vector3(Mathf.Sin(seconds*shakeFrequency*6.283f),Mathf.Cos(seconds*shakeFrequency*7.97f),Mathf.Sin(seconds*shakeFrequency*5.13f))) * amplitude);
            var pose = new BattleCameraPose(pos, rotation, fov);
            return fitTargetGroup ? director.FitSequence(pose) : pose;
        }
        public void Sample(BattleShotDirector director, float normalizedTime, float weight)
        {
            var pose = Evaluate(director, normalizedTime, normalizedTime);
            director.SetSequencePose(pose.Position, pose.Rotation, pose.Fov);
        }

    }
    [Serializable]
    public sealed class BattleCameraShotClip : PlayableAsset, ITimelineClipAsset
    {
        public BattleCameraShotBehaviour shot = new();
        public ClipCaps clipCaps => ClipCaps.Blending;
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
            => ScriptPlayable<BattleCameraShotBehaviour>.Create(graph, shot);
    }
}
