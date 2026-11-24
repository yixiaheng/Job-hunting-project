using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Serialization;

namespace Battle.Presentation
{
    [DefaultExecutionOrder(100)]
    public sealed class BattleShotDirector : MonoBehaviour
    {
        [SerializeField, FormerlySerializedAs("outputCamera")] private Camera output;
        [SerializeField, FormerlySerializedAs("brain")] private CinemachineBrain cameraBrain;
        [SerializeField, FormerlySerializedAs("normalCamera")] private CinemachineCamera baseCamera;
        [SerializeField, FormerlySerializedAs("cinematicCamera")] private CinemachineCamera sequenceCamera;
        [SerializeField, FormerlySerializedAs("profile")] private BattleCameraProfile defaultProfile;
        public Camera outputCamera { get => output; set => output = value; }
        public CinemachineBrain brain { get => cameraBrain; set => cameraBrain = value; }
        public CinemachineCamera normalCamera { get => baseCamera; set => baseCamera = value; }
        public CinemachineCamera cinematicCamera { get => sequenceCamera; set => sequenceCamera = value; }
        public BattleCameraProfile profile { get => defaultProfile; set => defaultProfile = value; }
        private readonly Dictionary<Renderer,bool> framingVisibility = new();
        private void RestoreFramingVisibility()
        { foreach(var entry in framingVisibility)if(entry.Key!=null)entry.Key.enabled=entry.Value;framingVisibility.Clear(); }
        private void ApplyFramingVisibility(BattleShotSettings settings)
        {
            RestoreFramingVisibility();
            if(settings==null || !settings.hideOtherAllies || context?.Target == null
                || context.Target.faction != Faction.Enemy || units==null || actors==null)return;
            foreach(var unit in units)if(unit.faction==Faction.OurSide && unit!=AnchorUnit && actors.TryGetActor(unit,out var actor))
                foreach(var renderer in actor.VisualRoot.GetComponentsInChildren<Renderer>(true))
                { framingVisibility[renderer]=renderer.enabled;renderer.enabled=false; }
        }
        private IBattleActorLookup actors;
        private IReadOnlyList<BattleUnit> units;
        private BattlePresentationClock clock;
        private BattlefieldDirector battlefield;
        private BattleCameraProfile encounterProfile, draft;
        private BattleCameraContext context, actualContext;
        private BattleCameraPose start, destination;
        private AnimationCurve easing;
        private float elapsed, duration, lastAspect;
        private int owner, generation;
        private bool dirty, transitioning, initialized;
        private Vector3 fallbackForward = Vector3.forward;
        private Action restoreEditorCameras;
        public bool IsTransitioning => transitioning;
        public bool IsEditing => draft != null;
        public bool IsBound => actors != null && clock != null;
        public bool HasExclusiveOwner => owner != 0;
        public BattleUnit SelectedTarget => context?.Target;
        public BattleUnit AnchorUnit => context?.Actor;
        public BattleCameraContext Context => context;
        public Vector3 AnchorPosition => Position(AnchorUnit, Center(Faction.OurSide), true);
        public Vector3 ShotAnchor => context?.Mode == BattleShotMode.Overview ? BattlefieldOrigin
            : context?.Mode is BattleShotMode.AllySupport or BattleShotMode.EnemyAction ? Center(Faction.OurSide) : AnchorPosition;
        public Quaternion ShotRotation
        {
            get
            {
                var settings = ResolveSettings();
                if (settings == null || !settings.alignToFormation || context?.Mode is BattleShotMode.Self or BattleShotMode.AllySupport or BattleShotMode.EnemyAction or BattleShotMode.Overview) return BattlefieldRotation;
                var direction = Center(Faction.Enemy) - AnchorPosition; direction.y = 0;
                return direction.sqrMagnitude > .001f ? Quaternion.LookRotation(direction) : BattlefieldRotation;
            }
        }
        public Vector3 SlotCameraOffset => battlefield != null ? battlefield.CameraOffset(AnchorUnit) : Vector3.zero;
        public Quaternion BattlefieldRotation => battlefield != null ? battlefield.Root.rotation : Quaternion.LookRotation(fallbackForward);
        public Vector3 BattlefieldOrigin => battlefield != null ? battlefield.Root.position : (Center(Faction.OurSide) + Center(Faction.Enemy)) * .5f;
        public IReadOnlyList<BattleUnit> SequenceTargets { get; set; } = Array.Empty<BattleUnit>();
        public Vector3 SequenceTargetCenter
        { get { var list = new List<Bounds>(); foreach(var unit in SequenceTargets) AddBounds(list,unit); if(list.Count==0)return SequenceTarget!=null?SequenceTarget.position:BattlefieldOrigin; var bounds=list[0]; foreach(var item in list)bounds.Encapsulate(item); return bounds.center; } }
        public BattleCameraPose FitSequence(BattleCameraPose pose)
        {
            var bounds = new List<Bounds>(); foreach(var unit in SequenceTargets)AddBounds(bounds,unit);
            return new BattleCameraPose(Fit(pose.Position,pose.Rotation,pose.Fov,bounds,new Rect(.12f,.22f,.76f,.65f),outputCamera!=null?outputCamera.aspect:16f/9f),pose.Rotation,pose.Fov);
        }
        public void FrameCurrentContext()
        { var settings=ResolveSettings();if(settings!=null&&HasExclusiveOwner){var pose=Solve(settings);SetSequencePose(pose.Position,pose.Rotation,pose.Fov);} }
        public Transform SequenceOrigin { get; set; }
        public Transform SequenceCaster { get; set; }
        public Transform SequenceTarget { get; set; }
        public BattleCameraProfile ActiveProfile => ActorProfile ?? encounterProfile ?? profile;
        public string SettingsSource { get; private set; }
        public int OutputWriteCount { get; private set; }
        private BattleCameraProfile ActorProfile => actors != null && actors.TryGetActor(AnchorUnit, out var actor) ? actor.CameraProfile : null;
        public void ConfigureBattlefield(BattlefieldDirector value, BattleCameraProfile encounter = null)
        { battlefield = value; encounterProfile = encounter; }
        private void Awake()
        {
            foreach (var camera in new[] { normalCamera, cinematicCamera })
            {
                if (camera == null) continue;
                var pos = camera.transform.localPosition; var rot = camera.transform.localRotation;
                var lens = camera.Lens; var priority = camera.Priority;
                restoreEditorCameras += () => { if (camera != null) { camera.transform.SetLocalPositionAndRotation(pos, rot); camera.Lens = lens; camera.Priority = priority; } };
            }
        }
        public void PrepareForEditorExit() { Cancel(); restoreEditorCameras?.Invoke(); }
        public void Bind(IBattleActorLookup lookup, IReadOnlyList<BattleUnit> battleUnits, BattlePresentationClock presentationClock)
        {
            actors = lookup; units = battleUnits; clock = presentationClock;
            generation++; owner = 0; initialized = false; draft = null;
            if (brain != null) { brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate; brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0); }
            if (normalCamera != null) normalCamera.Priority = 10;
            if (cinematicCamera != null) cinematicCamera.Priority = 0;
            var axis = Center(Faction.Enemy) - Center(Faction.OurSide); axis.y = 0;
            fallbackForward = axis.sqrMagnitude > .01f ? axis.normalized : Vector3.forward;
            lastAspect = outputCamera != null ? outputCamera.aspect : 16f/9f;
            SetBase(null);
        }
        public void SetContext(BattleCameraContext value, bool immediate = false)
        { if (IsEditing) { actualContext = value; return; } context = value; Rebuild(immediate); }
        public void SetBase(BattleUnit current)
        {
            BattleUnit anchor = current?.faction == Faction.OurSide && current.IsActive ? current : AnchorUnit;
            if (anchor == null || !anchor.IsActive)
            { anchor = null; if (units != null) foreach (var unit in units) if (unit.faction == Faction.OurSide && unit.IsActive) { anchor = unit; break; } }
            SetContext(new BattleCameraContext(anchor, null, null, BattleShotMode.EnemySingle));
        }
        public void SetSelection(BattleUnit target) => SetContext(new BattleCameraContext(AnchorUnit, target,
            target != null ? new[] { target } : null, context?.Mode ?? BattleShotMode.EnemySingle));
        public void RefreshConfiguration(bool immediate = true) => Rebuild(immediate);
        private BattleShotSettings ResolveSettings()
        {
            var mode = context?.Mode ?? BattleShotMode.Overview;
            if (mode == BattleShotMode.EnemySingle && actors != null && actors.TryGetActor(SelectedTarget, out var target)
                && target.FramingBounds.size.y > 4f) mode = BattleShotMode.Boss;
            BattleShotSettings result = null; SettingsSource = "Default";
            foreach (var pair in new[] { (profile, "Default"), (encounterProfile, "Encounter"), (ActorProfile, "Actor"), (draft, "Draft") })
            {
                var found = pair.Item1 != null ? pair.Item1.Find(mode) : null;
                if (found != null) { result = found; SettingsSource = pair.Item2 + ": " + pair.Item1.name; }
            }
            return result ?? new BattleShotSettings { mode = mode, fitTargets = mode != BattleShotMode.EnemySingle, hideOtherAllies = mode is BattleShotMode.EnemySingle or BattleShotMode.EnemySpread or BattleShotMode.EnemyGroup or BattleShotMode.Boss };
        }
        private void Rebuild(bool immediate = false)
        {
            if (!IsBound || normalCamera == null || profile == null) return;
            var settings = ResolveSettings();
            if(!HasExclusiveOwner) ApplyFramingVisibility(settings);
            destination = Solve(settings);
            easing = settings?.easing;
            duration = !initialized || immediate || IsEditing ? 0 : settings?.seconds ?? (SelectedTarget != null ? profile.selectionBlendSeconds : profile.blendSeconds);
            start = Read(normalCamera); elapsed = 0; initialized = true; transitioning = duration > 0;
            if (!HasExclusiveOwner && !transitioning) Write(normalCamera, destination);
        }
        private BattleCameraPose Solve(BattleShotSettings settings)
        {
            var mode = context?.Mode ?? BattleShotMode.Overview;
            Vector3 allies = Center(Faction.OurSide), enemies = Center(Faction.Enemy), anchor = AnchorPosition;
            Vector3 target = Position(SelectedTarget, enemies, false, true);
            Vector3 aim = Vector3.Lerp(enemies + Vector3.up, target, settings.targetWeight);
            bool selected = SelectedTarget != null;
            var bounds = new List<Bounds>();
            if (mode is BattleShotMode.AllySupport or BattleShotMode.Self)
            {
                anchor = mode == BattleShotMode.Self ? AnchorPosition : allies; aim = anchor + Vector3.up;
                foreach (var unit in units) if (unit.faction == Faction.OurSide && (mode != BattleShotMode.Self || unit == AnchorUnit)) AddBounds(bounds, unit);
                selected = false;
            }
            else if (mode is BattleShotMode.Overview or BattleShotMode.EnemyAction)
            {
                anchor = mode == BattleShotMode.Overview ? BattlefieldOrigin : allies; aim = anchor + Vector3.up;
                foreach (var unit in units) if (mode == BattleShotMode.Overview || unit.faction == Faction.OurSide) AddBounds(bounds, unit);
            }
            else
            {
                if (context != null) foreach (var unit in context.Targets) AddBounds(bounds, unit);
                if (bounds.Count == 0) foreach (var unit in units) if (unit.faction == Faction.Enemy) AddBounds(bounds, unit);
            }
            Vector3 pos = anchor + ShotRotation * (settings.position + (selected ? settings.selectionPosition : Vector3.zero)
                + (battlefield != null ? battlefield.CameraOffset(AnchorUnit) : Vector3.zero));
            aim += ShotRotation * settings.aimOffset;
            Quaternion rotation = settings.manualRotation ? ShotRotation * Quaternion.Euler(settings.rotation) : Look(pos, aim);
            if (selected) rotation *= Quaternion.Euler(settings.selectionRotation);
            float fov = Mathf.Clamp(settings.fov + (selected ? settings.selectionFov : 0), 5, 120);
            if (settings.fitTargets) pos = Fit(pos, rotation, fov, bounds, settings.safeArea, outputCamera != null ? outputCamera.aspect : 16f / 9f);
            if (settings.lockActorViewport && AnchorUnit != null && mode is BattleShotMode.EnemySingle or BattleShotMode.EnemySpread or BattleShotMode.EnemyGroup or BattleShotMode.Boss)
                pos = LockToViewport(pos, rotation, fov, Position(AnchorUnit, anchor, false, true), settings.actorViewport,
                    outputCamera != null ? outputCamera.aspect : 16f / 9f, settings.fitTargets ? bounds : null, settings.safeArea);
            return new BattleCameraPose(pos, rotation, fov);
        }
        public static Vector3 LockToViewport(Vector3 position, Quaternion rotation, float fov, Vector3 subject,
            Vector2 viewport, float aspect, IReadOnlyList<Bounds> fitBounds = null, Rect safeArea = default)
        {
            aspect = Mathf.Max(.1f, aspect);
            viewport = new Vector2(Mathf.Clamp01(viewport.x), Mathf.Clamp01(viewport.y));
            float vertical = Mathf.Tan(Mathf.Clamp(fov, 5, 120) * Mathf.Deg2Rad * .5f);
            float slopeX = (viewport.x * 2 - 1) * vertical * aspect;
            float slopeY = (viewport.y * 2 - 1) * vertical;
            float depth = Mathf.Max(.05f, (Quaternion.Inverse(rotation) * (subject - position)).z);
            Vector3 AtDepth(float value) => subject - rotation * new Vector3(slopeX * value, slopeY * value, value);
            bool FitsAt(float value)
            {
                if (fitBounds == null || fitBounds.Count == 0) return true;
                Vector3 candidate = AtDepth(value); Quaternion inverse = Quaternion.Inverse(rotation);
                foreach (var bounds in fitBounds) for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = inverse * (corner - candidate);
                    if (p.z <= .01f) return false;
                    float x = .5f + p.x / (2 * p.z * vertical * aspect);
                    float y = .5f + p.y / (2 * p.z * vertical);
                    if (x < safeArea.xMin || x > safeArea.xMax || y < safeArea.yMin || y > safeArea.yMax) return false;
                }
                return true;
            }
            if (fitBounds != null && fitBounds.Count > 0 && !FitsAt(depth))
            {
                float low = depth, high = depth;
                for (int i = 0; i < 24 && !FitsAt(high); i++) high *= 1.35f;
                if (FitsAt(high))
                {
                    for (int i = 0; i < 24; i++)
                    { float middle = (low + high) * .5f; if (FitsAt(middle)) high = middle; else low = middle; }
                    depth = high;
                }
            }
            return AtDepth(depth);
        }
        public static Vector3 Fit(Vector3 position, Quaternion rotation, float fov, IReadOnlyList<Bounds> bounds, Rect safe, float aspect)
        {
            float vertical = Mathf.Tan(fov * Mathf.Deg2Rad * .5f), horizontal = vertical * Mathf.Max(.1f, aspect), retreat = 0;
            Quaternion inverse = Quaternion.Inverse(rotation);
            foreach (var b in bounds) for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = inverse * (corner - position);
                float xLimit = p.x < 0 ? 1 - 2 * safe.xMin : 2 * safe.xMax - 1;
                float yLimit = p.y < 0 ? 1 - 2 * safe.yMin : 2 * safe.yMax - 1;
                float needed = Mathf.Max(Mathf.Abs(p.x) / (horizontal * Mathf.Max(.05f, xLimit)), Mathf.Abs(p.y) / (vertical * Mathf.Max(.05f, yLimit)));
                retreat = Mathf.Max(retreat, needed - p.z);
            }
            return position - rotation * Vector3.forward * Mathf.Max(0, retreat);
        }
        private void AddBounds(List<Bounds> list, BattleUnit unit)
        { if (BattleDisplayState.Visible(unit) && actors.TryGetActor(unit, out var actor)) list.Add(actor.FramingBounds); }
        public bool PreviewConfiguration(BattleCameraProfile value, bool selectedState) => PreviewContext(value,
            context == null ? null : new BattleCameraContext(context.Actor, selectedState ? context.Target : null, context.Targets, context.Mode));
        public bool PreviewContext(BattleCameraProfile value, BattleCameraContext preview, bool selectedState = true)
        {
            if (!IsBound || HasExclusiveOwner || value == null) return false;
            if (!IsEditing) actualContext = context;
            draft = value; context = preview; Rebuild(true); FlushOutput(); return true;
        }
        public void EndConfigurationPreview()
        { draft = null; context = actualContext ?? context; actualContext = null; if (IsBound) { Rebuild(true); FlushOutput(); } }
        public int Acquire()
        {
            if (HasExclusiveOwner) throw new InvalidOperationException("Battle camera already owned by a sequence.");
            // Selection framing belongs to the normal camera. The presentation coordinator
            // owns visibility once a cinematic starts.
            RestoreFramingVisibility();
            owner = ++generation; transitioning = false;
            if (cinematicCamera != null) { Write(cinematicCamera, outputCamera != null ? new BattleCameraPose(outputCamera.transform.position, outputCamera.transform.rotation, outputCamera.fieldOfView) : Read(normalCamera)); cinematicCamera.Priority = 20; }
            return owner;
        }
        public bool Release(int token)
        {
            if (owner == 0 || token != owner) return false;
            if (normalCamera != null && cinematicCamera != null) Write(normalCamera, Read(cinematicCamera));
            owner = 0; if (cinematicCamera != null) cinematicCamera.Priority = 0;
            SequenceOrigin = SequenceCaster = SequenceTarget = null; SequenceTargets = Array.Empty<BattleUnit>(); Rebuild(); dirty = true; return true;
        }
        public void SetSequencePose(Vector3 pos, Quaternion rotation, float fov)
        { if (HasExclusiveOwner && cinematicCamera != null) Write(cinematicCamera, new BattleCameraPose(pos, rotation, fov)); }
        public void FrameGroup(IReadOnlyList<BattleUnit> subjects)
        {
            if (!HasExclusiveOwner || subjects == null || subjects.Count == 0 || profile == null) return;
            var bounds = new List<Bounds>(); foreach (var unit in subjects) AddBounds(bounds, unit);
            if (bounds.Count == 0) return;
            var all = bounds[0]; foreach (var b in bounds) all.Encapsulate(b);
            Vector3 pos = all.center + BattlefieldRotation * new Vector3(2, 2, -6);
            var rot = Look(pos, all.center); pos = Fit(pos, rot, 45, bounds, new Rect(.12f,.2f,.76f,.68f), outputCamera != null ? outputCamera.aspect : 16f/9f);
            SetSequencePose(pos, rot, 45);
        }
        public void Cancel()
        {
            if (HasExclusiveOwner) Release(owner);
            RestoreFramingVisibility();
            generation++; owner = 0; actors = null; units = null; clock = null; draft = null;
            context = actualContext = null; transitioning = initialized = dirty = false;
        }
        private Vector3 Center(Faction side)
        {
            Vector3 sum = Vector3.zero; int count = 0;
            if (units != null && actors != null) foreach (var unit in units)
                if (unit.faction == side && BattleDisplayState.Visible(unit) && actors.TryGetActor(unit, out var actor)) { sum += actor.PresentationRoot.position; count++; }
            return count > 0 ? sum / count : battlefield != null ? battlefield.Root.position : Vector3.zero;
        }
        private Vector3 Position(BattleUnit unit, Vector3 fallback, bool prepared = false, bool focus = false)
        {
            if (unit != null && prepared && battlefield != null) return battlefield.ReadyPosition(unit);
            return actors != null && actors.TryGetActor(unit, out var actor) ? focus ? actor.FocusPosition : actor.PresentationRoot.position : fallback;
        }
        private static Quaternion Look(Vector3 pos, Vector3 aim) => (aim - pos).sqrMagnitude > .0001f ? Quaternion.LookRotation(aim - pos) : Quaternion.identity;
        private static BattleCameraPose Read(CinemachineCamera camera) => camera != null ? new BattleCameraPose(camera.transform.position, camera.transform.rotation, camera.Lens.FieldOfView) : new BattleCameraPose(Vector3.zero, Quaternion.identity, 45);
        private void Write(CinemachineCamera camera, BattleCameraPose pose)
        { camera.transform.SetPositionAndRotation(pose.Position, pose.Rotation); var lens = camera.Lens; lens.FieldOfView = pose.Fov; camera.Lens = lens; dirty = true; OutputWriteCount++; }
        public void FlushOutput()
        { if (!dirty || brain == null || clock == null) return; brain.ManualUpdate(Time.frameCount, clock.Delta); dirty = false; }
        private void LateUpdate()
        {
            if (!IsBound) return;
            float aspect = outputCamera != null ? outputCamera.aspect : 16f/9f;
            if (Mathf.Abs(aspect-lastAspect) > .001f) { lastAspect = aspect; Rebuild(true); }
            if (transitioning && !HasExclusiveOwner && !IsEditing && clock.Delta > 0)
            {
                elapsed += clock.Delta; float t = Mathf.Clamp01(elapsed / duration);
                Write(normalCamera, BattleCameraPose.Blend(start, destination, easing != null ? easing.Evaluate(t) : t*t*(3-2*t)));
                if (t >= 1) { Write(normalCamera, destination); transitioning = false; }
            }
            FlushOutput();
        }
    }
}
