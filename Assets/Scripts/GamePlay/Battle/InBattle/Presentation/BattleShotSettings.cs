using System;
using System.Collections.Generic;
using UnityEngine;

namespace Battle.Presentation
{
    public enum BattleShotMode { EnemySingle, EnemySpread, EnemyGroup, AllySupport, Self, EnemyAction, Overview, Boss }
    [Serializable]
    public sealed class BattleShotSettings
    {
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("mode"), InspectorName("mode")] private BattleShotMode modeValue;
        public BattleShotMode mode { get => modeValue; set => modeValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("enabled"), InspectorName("enabled")] private bool enabledValue = true;
        public bool enabled { get => enabledValue; set => enabledValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("position"), InspectorName("position")] private Vector3 positionValue = new(2.1f, 1.9f, -2.4f);
        public Vector3 position { get => positionValue; set => positionValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("alignToFormation"), InspectorName("alignToFormation")] private bool alignToFormationValue = true;
        public bool alignToFormation { get => alignToFormationValue; set => alignToFormationValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("hideOtherAllies"), InspectorName("hideOtherAllies")] private bool hideOtherAlliesValue = true;
        public bool hideOtherAllies { get => hideOtherAlliesValue; set => hideOtherAlliesValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("rotation"), InspectorName("rotation")] private Vector3 rotationValue = new(8, -18, 0);
        public Vector3 rotation { get => rotationValue; set => rotationValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("manualRotation"), InspectorName("manualRotation")] private bool manualRotationValue;
        public bool manualRotation { get => manualRotationValue; set => manualRotationValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("aimOffset"), InspectorName("aimOffset")] private Vector3 aimOffsetValue;
        public Vector3 aimOffset { get => aimOffsetValue; set => aimOffsetValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("targetWeight"), InspectorName("targetWeight")] [Range(0, 1)] private float targetWeightValue = .5f;
        public float targetWeight { get => targetWeightValue; set => targetWeightValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("fov"), InspectorName("fov")] [Range(10, 100)] private float fovValue = 45;
        public float fov { get => fovValue; set => fovValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("selectionPosition"), InspectorName("selectionPosition")] private Vector3 selectionPositionValue;
        public Vector3 selectionPosition { get => selectionPositionValue; set => selectionPositionValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("selectionRotation"), InspectorName("selectionRotation")] private Vector3 selectionRotationValue;
        public Vector3 selectionRotation { get => selectionRotationValue; set => selectionRotationValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("selectionFov"), InspectorName("selectionFov")] private float selectionFovValue;
        public float selectionFov { get => selectionFovValue; set => selectionFovValue = value; }
        [SerializeField, InspectorName("锁定行动者屏幕位置")] private bool lockActorViewportValue;
        public bool lockActorViewport { get => lockActorViewportValue; set => lockActorViewportValue = value; }
        [SerializeField, InspectorName("行动者屏幕位置 (0-1)")] private Vector2 actorViewportValue = new(.22f, .28f);
        public Vector2 actorViewport { get => actorViewportValue; set => actorViewportValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("seconds"), InspectorName("seconds")] [Min(0)] private float secondsValue = .2f;
        public float seconds { get => secondsValue; set => secondsValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("easing"), InspectorName("easing")] private AnimationCurve easingValue = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public AnimationCurve easing { get => easingValue; set => easingValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("fitTargets"), InspectorName("fitTargets")] private bool fitTargetsValue = true;
        public bool fitTargets { get => fitTargetsValue; set => fitTargetsValue = value; }
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("safeArea"), InspectorName("safeArea")] private Rect safeAreaValue = new(.12f, .22f, .78f, .65f);
        public Rect safeArea { get => safeAreaValue; set => safeAreaValue = value; }
        public BattleShotSettings Copy() => JsonUtility.FromJson<BattleShotSettings>(JsonUtility.ToJson(this));
    }
    public readonly struct BattleCameraPose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly float Fov;
        public BattleCameraPose(Vector3 position, Quaternion rotation, float fov)
        { Position = position; Rotation = rotation; Fov = Mathf.Clamp(fov, 5, 120); }
        public static BattleCameraPose Blend(BattleCameraPose a, BattleCameraPose b, float t) => new(
            Vector3.LerpUnclamped(a.Position, b.Position, t), Quaternion.SlerpUnclamped(a.Rotation, b.Rotation, t), Mathf.LerpUnclamped(a.Fov, b.Fov, t));
    }
    public sealed class BattleCameraContext
    {
        public BattleUnit Actor { get; }
        public BattleUnit Target { get; }
        public IReadOnlyList<BattleUnit> Targets { get; }
        public BattleShotMode Mode { get; }
        public BattleCameraContext(BattleUnit actor, BattleUnit target, IReadOnlyList<BattleUnit> targets, BattleShotMode mode)
        { Actor = actor; Target = target; Targets = targets ?? Array.Empty<BattleUnit>(); Mode = mode; }
        public static BattleCameraContext FromSelection(Battle.Logic.BattleSelectionState state)
        {
            var skill = state.Skill;
            var mode = skill == null ? BattleShotMode.Overview : skill.targetRule == TargetRule.Self ? BattleShotMode.Self
                : skill.targetSide == TargetSide.OurSide ? BattleShotMode.AllySupport
                : skill.targetRule == TargetRule.AoE ? BattleShotMode.EnemyGroup
                : skill.targetRule == TargetRule.Spread ? BattleShotMode.EnemySpread : BattleShotMode.EnemySingle;
            return new BattleCameraContext(state.Actor, state.Target, state.Preview?.Targets, mode);
        }
    }
}
