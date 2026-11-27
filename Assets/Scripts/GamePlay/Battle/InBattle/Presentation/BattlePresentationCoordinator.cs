using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Battle.Logic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.SceneManagement;

namespace Battle.Presentation
{
    /// <summary>Owns the complete visual action, including impacts, deaths and restoration.</summary>
    [DefaultExecutionOrder(-40)]
    public sealed class BattlePresentationCoordinator : MonoBehaviour
    {
        public BattleShotDirector cameraDirector;
        public PlayableDirector timelineDirector;
        public BattleSkillSequence[] skills = Array.Empty<BattleSkillSequence>();
        public BattleEventSequence[] events = Array.Empty<BattleEventSequence>();
        public BattlePresentationClock Clock { get; private set; } = new();
        public BattleDisplayState Display { get; private set; }
        public BattleActionDamageDisplay ActionDamage { get; } = new();
        public bool Busy => routine != null;
        public event Action<bool> HUDVisibilityChanged;
        public event Action<bool> CommandsVisibilityChanged;
        public event Action<BattleEffectRecord> EffectPresented;
        private BattlefieldDirector battlefield;
        public void ConfigureBattlefield(BattlefieldDirector value) => battlefield = value;
        private BattleFlow flow;
        private IBattleActorLookup actors;
        private Stack<IEnumerator> routine;
        private Action completion;
        private Action onFailure;
        private int generation;
        private int cameraToken;
        private BattleStage stage;
        private int originalMask;
        [Flags]
        private enum VisibilityScope { Action = 1, Stage = 2 }
        private readonly Dictionary<Renderer, (bool enabled, VisibilityScope scopes)> hiddenRenderers = new();
        private readonly List<(Transform transform, Vector3 position, Quaternion rotation, Vector3 scale)> poses = new();
        private readonly List<(Animator animator, float speed)> animatorSpeeds = new();
        private readonly List<(Light light, int mask)> lightMasks = new();
        private readonly HashSet<BattleUnit> deathPresented = new();
        private BattleUnit presentationCaster;
        private BattleActionPresentationSession actionSession;
        private readonly Dictionary<BattleUnitActor, bool> worldVisibility = new();
        private bool stagePositions, finishing;
        private bool cuePresented;
        private Transform stageOrigin;
        public BattleActionPresentationSession Session => actionSession;
        public string CurrentPhase => actionSession?.Phase.ToString() ?? "Idle";
        public bool HasPresentationOwnership => actionSession != null;
        public BattleUnit PresentationCaster => presentationCaster;

        public void Bind(BattleFlow battle, IBattleActorLookup lookup)
        {
            Cancel(); flow = battle; actors = lookup; Clock = new BattlePresentationClock();
            battlefield?.Bind(Clock);
            Display = new BattleDisplayState(Clock); Display.Register(flow.Units);
            cameraDirector?.Bind(actors, flow.Units, Clock);
        }
        public void Register(IEnumerable<BattleUnit> units) => Display?.Register(units);
        public void Present(ActionResolution resolution, Action completed)
        {
            Run(ActionSequence(resolution), completed, () =>
            {
                Display.Synchronize();
                foreach (var unit in flow.Units) if (!unit.IsActive) FinishDeath(unit);
            });
        }
        public void PresentEvent(string eventId, IReadOnlyList<BattleUnit> subjects, Action completed)
            => Run(EventSequence(eventId, subjects ?? Array.Empty<BattleUnit>()), completed);
        private void Run(IEnumerator sequence, Action completed, Action failure = null)
        {
            if (Busy) throw new InvalidOperationException("Presentation must be submitted through the serial queue.");
            routine = new Stack<IEnumerator>(); routine.Push(sequence); completion = completed; onFailure = failure;
        }
        private void Update()
            => Tick(UnityEngine.Time.unscaledDeltaTime);

        public void Tick(float unscaledDelta)
        {
            var scenes = GameSceneManager.Instance;
            Clock.Tick(unscaledDelta,
                scenes != null && (scenes.IsLoading || scenes.IsSceneSuspended(gameObject.scene)));
            if (routine == null || Clock.Delta <= 0f) return;
            int session = generation;
            try
            {
                actionSession?.Tick(Clock.Delta);
                if (actionSession != null && actionSession.Elapsed > (actionSession.Definition != null ? actionSession.Definition.TimeoutSeconds : 60))
                    throw new TimeoutException("Battle action presentation exceeded its active-time limit.");
                while (routine != null && routine.Count > 0)
                {
                    var current = routine.Peek();
                    if (!current.MoveNext()) { (routine.Pop() as IDisposable)?.Dispose(); continue; }
                    if (current.Current is IEnumerator child) { routine.Push(child); continue; }
                    return;
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                actionSession?.CancelPending();
                if (actionSession?.Resolution?.Record != null) FlushAction();
                onFailure?.Invoke();
                DisposeRoutine();
            }
            if (session != generation) return;
            routine = null; var done = completion; completion = null; onFailure = null;
            RestoreSequence(); presentationCaster = null;
            EndActionSession(true);
            cameraDirector?.SetBase(flow?.CurrentUnit);
            done?.Invoke();
        }
        private IEnumerator ActionSequence(ActionResolution resolution)
        {
            var record = resolution?.Record;
            if (record == null) yield break;
            ActionDamage.Begin(record);
            actors.TryGetActor(record.Caster, out var casterActor);
            var definition = casterActor != null && casterActor.Presentation != null ? casterActor.Presentation.Resolve(record.Skill.id) : null;
            if (definition == null) foreach (var entry in skills ?? Array.Empty<BattleSkillSequence>())
                if (record.Skill != null && entry.skillId == record.Skill.id && entry.casterFaction == record.Caster.faction)
                { definition = entry.sequence; break; }
            actionSession = new BattleActionPresentationSession(resolution, definition, battlefield, gameObject.scene);
            cuePresented = false;
            var targets = record.PrimaryTargets.Count > 0 ? record.PrimaryTargets.ToList() : record.Effects.Where(e => e.Target != null && e.Target != record.Caster).Select(e => e.Target).Distinct().ToList();
            BattleUnit visualTarget = record.MainTarget != null && targets.Contains(record.MainTarget) ? record.MainTarget : targets.FirstOrDefault() ?? record.Caster;
            OwnActor(record.Caster);
            foreach (var target in targets) OwnActor(target);
            foreach (var effectTargets in record.ResolvedTargets) foreach (var target in effectTargets.Targets) OwnActor(target);
            presentationCaster = record.Caster;
            cameraDirector?.SetBase(record.Caster);
            cameraToken = cameraDirector != null ? cameraDirector.Acquire() : 0;
            if (record.Caster?.faction == Faction.OurSide
                && record.Skill?.targetSide == TargetSide.OtherSide)
                HideOtherAllies(record.Caster);
            CommandsVisibilityChanged?.Invoke(definition == null || !definition.HideCommands);
            if(cameraDirector != null) cameraDirector.SequenceTargets = targets;
            if (record.Caster?.faction == Faction.Enemy)
            {
                cameraDirector?.SetContext(new BattleCameraContext(record.Caster, visualTarget, targets, BattleShotMode.EnemyAction));
                cameraDirector?.FrameCurrentContext();
            }
            else if (definition != null && cameraDirector != null && (definition.Template is BattleSkillTemplate.Support or BattleSkillTemplate.Ranged))
            {
                var mode = definition.Template == BattleSkillTemplate.Support ? BattleShotMode.AllySupport
                    : record.Skill.targetRule == TargetRule.AoE ? BattleShotMode.EnemyGroup : BattleShotMode.EnemySingle;
                cameraDirector.SetContext(new BattleCameraContext(record.Caster, visualTarget, targets, mode));
                cameraDirector.FrameCurrentContext();
                if (definition.timeline == null || !definition.timeline.GetRootTracks().OfType<BattleCameraShotTrack>().Any())
                    cameraDirector.FrameGroup(targets.Concat(new[] { record.Caster }).Distinct().ToList());
            }
            if (definition != null && definition.Template == BattleSkillTemplate.MeleeApproach && casterActor != null
                && actors.TryGetActor(visualTarget, out var meleeTarget))
            {
                var stand = meleeTarget.Presentation != null ? meleeTarget.Presentation.MeleeStand : null;
                if (stand != null)
                {
                    var pose = new Pose(stand.position + stand.rotation * definition.StandOffset, stand.rotation * Quaternion.Euler(definition.StandEuler));
                    yield return Travel(record.Caster, pose, definition.Approach, false);
                }
                else Debug.LogWarning($"{meleeTarget.name}: MeleeStand missing; playing in place.", meleeTarget);
            }
            actionSession.Phase = BattlePresentationPhase.Main;
            if (definition != null && definition.Animation.Driver == BattleBodyDriver.AnimatorController)
                actionSession.Animation(record.Caster)?.State(definition.Animation.AttackState, null, 1, definition.Animation.BlendSeconds);
            if (definition != null && definition.timeline != null && timelineDirector != null)
                yield return PlayTimeline(definition, record.Caster, visualTarget,
                    HandleMarker);
            else
            {
                if (definition != null && definition.Template == BattleSkillTemplate.Staged)
                {
                    SetupStage(definition, record.Caster, visualTarget);
                    if (cameraDirector != null) { cameraDirector.SequenceOrigin = stageOrigin; cameraDirector.FrameGroup(targets.Concat(new[] { record.Caster }).Distinct().ToList()); }
                }
                float windup = definition != null ? definition.Animation.ImpactSeconds : Profile != null ? Profile.fallbackWindup : .3f;
                yield return Wait(windup);
                PresentGroup(-1, definition != null && definition.Template == BattleSkillTemplate.Ranged && definition.Projectile.Enabled);
                yield return Wait(definition != null ? Mathf.Max(0, definition.Animation.AttackSeconds - windup) : Profile != null ? Profile.fallbackRecovery : .25f);
            }
            // Authored timelines without impact markers still get a safe final cue, including full-health healing.
            if (!cuePresented && actionSession.Applied.Count == 0 && !actionSession.HasFlights) PresentGroup(-1, definition != null && definition.Template == BattleSkillTemplate.Ranged && definition.Projectile.Enabled);
            actionSession.Phase = BattlePresentationPhase.Waiting;
            while (actionSession.HasBlocking) yield return null;
            FlushAction();
            while (actionSession.HasBlocking) yield return null;
            Display.Synchronize();
            RestoreSequence(false);
            actionSession.RestoreLocal();
            if (definition != null && definition.Template == BattleSkillTemplate.MeleeApproach && casterActor != null)
                yield return Travel(record.Caster, actionSession.Home(record.Caster), definition.Returning, true);
            else if (casterActor != null)
                yield return Travel(record.Caster, actionSession.Home(record.Caster), definition != null ? definition.Returning : new BattleTravelSettings(), true);
            actionSession.Phase = BattlePresentationPhase.Death;
            var deaths = flow.Units.Where(u => !u.IsActive && !deathPresented.Contains(u)).ToList();
            if (deaths.Count > 0)
            {
                foreach (var unit in deaths) if (actors.TryGetActor(unit, out var dying) && dying.Presentation != null)
                    actionSession.Animation(unit)?.PlayReaction(dying.Presentation.Death);
                while (actionSession.HasBlocking) yield return null;
                yield return EventSequenceInternal("Death", deaths);
                foreach (var unit in deaths) FinishDeath(unit);
            }
        }
        private void FinishDeath(BattleUnit unit)
        {
            deathPresented.Add(unit); Display.SetVisible(unit, false);
            if (actors.TryGetActor(unit, out var actor)) actor.gameObject.SetActive(false);
        }
        private void OwnActor(BattleUnit unit)
        { if (unit != null && actors.TryGetActor(unit, out var actor)) actionSession.Own(unit, actor); }
        private void SetWorldVisible(BattleUnitActor actor, bool visible)
        { if (!worldVisibility.ContainsKey(actor)) worldVisibility.Add(actor, actor.WorldPresentationVisible); actor.SetWorldPresentationVisible(visible); }
        private void EndActionSession(bool homeCaster)
        {
            ActionDamage.End();
            if (actionSession != null) { actionSession.RestorePositions(homeCaster); actionSession.Dispose(); actionSession = null; }
            foreach (var pair in worldVisibility) if (pair.Key != null) pair.Key.SetWorldPresentationVisible(pair.Value);
            worldVisibility.Clear();
        }
        private IEnumerator Travel(BattleUnit unit, Pose destination, BattleTravelSettings settings, bool returning)
        {
            if (!actors.TryGetActor(unit, out var actor)) yield break;
            if (!Finite(destination.position)) throw new InvalidOperationException("Invalid attack position.");
            actionSession.Phase = returning ? BattlePresentationPhase.Return : BattlePresentationPhase.Approach;
            var definition = actionSession.Definition;
            var root = actor.PresentationRoot; var start = new Pose(root.position, root.rotation);
            float distance = Vector3.Distance(start.position, destination.position);
            if (distance > 100) throw new InvalidOperationException("Attack position exceeds 100m presentation bounds.");
            float duration = settings.Duration(distance);
            var animation = actionSession.Animation(unit);
            var body = definition != null ? definition.Animation : null;
            if (returning && definition != null && definition.ReturnMode == BattleReturnMode.Restore) duration = 0;
            bool running = duration > .001f && body != null && animation != null && animation.State(returning ? body.ReturnState : body.ApproachState,
                returning ? body.ReturnRate : body.ApproachRate, settings.Playback(distance / duration), body.BlendSeconds);
            var direction = destination.position - start.position; direction.y = 0;
            Quaternion facing = direction.sqrMagnitude > .0001f ? Quaternion.LookRotation(direction) : start.rotation;
            if (returning && definition != null && definition.ReturnMode == BattleReturnMode.Backstep) facing = start.rotation;
            for (float elapsed = 0; elapsed < duration; elapsed += Clock.Delta)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                root.SetPositionAndRotation(Vector3.Lerp(start.position, destination.position, t), Quaternion.Slerp(start.rotation, facing, Mathf.Clamp01(elapsed / .1f)));
                if (cameraDirector != null) cameraDirector.FrameGroup(new[] { unit, actionSession.Resolution?.Record?.MainTarget ?? unit });
                yield return null;
            }
            root.SetPositionAndRotation(destination.position, destination.rotation);
            animation?.ClearRate();
            actionSession.Log($"{(returning ? "return" : "approach")} {distance:F2}m, {duration:F2}s, controller={running}");
        }
        private static bool Finite(Vector3 point) => !float.IsNaN(point.x) && !float.IsInfinity(point.x)
            && !float.IsNaN(point.y) && !float.IsInfinity(point.y) && !float.IsNaN(point.z) && !float.IsInfinity(point.z);
        private void ApplyEntry(int index, HashSet<BattleUnit> reacted)
        {
            var record = actionSession.Resolution.Record;
            if (index < 0 || index >= record.Effects.Count || actionSession.Reserved.Contains(index) || !actionSession.Applied.Add(index)) return;
            var effect = record.Effects[index]; Display.Apply(effect); ActionDamage.Present(index, effect); EffectPresented?.Invoke(effect);
            if (effect.Type is BattleEffectRecordType.Damage or BattleEffectRecordType.FollowUp)
            {
                if (effect.Target != null && reacted.Add(effect.Target) && actors.TryGetActor(effect.Target, out var actor))
                { actionSession.Animation(effect.Target)?.PlayReaction(actor.Presentation != null ? actor.Presentation.LightHit : null); actionSession.Impact(actor); }
            }
            actionSession.Log($"effect {index}, group {effect.EffectGroup}, {effect.Target?.displayName}");
        }
        private List<BattleUnit> GroupTargets(int group)
        {
            var record = actionSession.Resolution.Record;
            return (group < 0 ? record.PrimaryTargets : record.ResolvedTargets.Where(g => g.Group == group).SelectMany(g => g.Targets)).Distinct().ToList();
        }
        private void PresentGroup(int group, bool launch, bool cueOnly = false)
        {
            var session = actionSession;
            if (session?.Resolution?.Record == null) return;
            cuePresented = true;
            var record = session.Resolution.Record;
            var recipients = GroupTargets(group);
            var indices = Enumerable.Range(0, record.Effects.Count).Where(i => group < 0 || record.Effects[i].EffectGroup == group).ToList();
            if (launch && session.Definition != null && session.Definition.Projectile.Enabled && actors.TryGetActor(record.Caster, out var caster))
            {
                foreach (var unit in recipients)
                {
                    if (!actors.TryGetActor(unit, out var target)) continue;
                    var owned = indices.Where(i => record.Effects[i].Target == unit && !session.Applied.Contains(i) && !session.Reserved.Contains(i)).ToList();
                    if (owned.Count == 0 && !cueOnly) continue;
                    foreach (int index in owned) session.Reserved.Add(index);
                    session.Launch(caster, target, () =>
                    {
                        if (actionSession != session) return;
                        var reacted = new HashSet<BattleUnit>();
                        foreach (int index in owned) { session.Reserved.Remove(index); ApplyEntry(index, reacted); }
                        if (!reacted.Contains(unit)) session.Impact(target);
                    });
                }
                return;
            }
            var reactions = new HashSet<BattleUnit>();
            if (!cueOnly) foreach (int index in indices) ApplyEntry(index, reactions);
            foreach (var unit in recipients) if (!reactions.Contains(unit) && actors.TryGetActor(unit, out var actor)) session.Impact(actor);
        }
        private void HandleMarker(BattleImpactMarker marker)
        {
            var record = actionSession?.Resolution?.Record; if (record == null) return;
            switch (marker.Kind)
            {
                case BattleCueKind.LegacyIndex:
                    int index = marker.ResolveIndex(record);
                    if (actionSession.Definition != null && actionSession.Definition.Template == BattleSkillTemplate.Ranged && actionSession.Definition.Projectile.Enabled)
                    {
                        if (index >= 0 && index < record.Effects.Count) PresentGroup(record.Effects[index].EffectGroup, true);
                        else if (index < 0 && !cuePresented) PresentGroup(-1, true);
                        break;
                    }
                    if (index < 0) { var reacted = new HashSet<BattleUnit>(); foreach (int i in Enumerable.Range(0, record.Effects.Count)) ApplyEntry(i, reacted); }
                    else ApplyEntry(index, new HashSet<BattleUnit>());
                    break;
                case BattleCueKind.ImpactGroup: PresentGroup(marker.EffectGroup, false); break;
                case BattleCueKind.CueOnly: PresentGroup(marker.EffectGroup, false, true); break;
                case BattleCueKind.LaunchGroup: PresentGroup(marker.EffectGroup, true); break;
                case BattleCueKind.FlushRemaining: FlushAction(); break;
            }
        }
        private void FlushAction()
        {
            if (actionSession?.Resolution?.Record == null) return;
            var reacted = new HashSet<BattleUnit>();
            for (int i = 0; i < actionSession.Resolution.Record.Effects.Count; i++) ApplyEntry(i, reacted);
        }
        public void SkipCurrent()
        {
            if (!Busy || finishing) return;
            finishing = true;
            try
            {
                generation++; actionSession?.CancelPending(); FlushAction(); Display?.Synchronize();
                if (flow != null) foreach (var unit in flow.Units) if (!unit.IsActive) FinishDeath(unit);
                DisposeRoutine(); RestoreSequence(); EndActionSession(true); presentationCaster = null;
                cameraDirector?.SetBase(flow?.CurrentUnit);
                var done = completion; completion = null; onFailure = null; done?.Invoke();
            }
            finally { finishing = false; }
        }
        private void SetupStage(BattleSequenceDefinition definition, BattleUnit caster, BattleUnit target)
        {
            if (stageOrigin != null) throw new InvalidOperationException("Nested stage presentation is not supported.");
            Vector3 origin = battlefield != null ? battlefield.Root.TransformPoint(definition.StageOrigin) : definition.StageOrigin;
            Quaternion rotation = (battlefield != null ? battlefield.Root.rotation : Quaternion.identity) * definition.StageRotation;
            if (definition.StageSpace == BattleStageSpace.IsolatedSet)
            {
                if (definition.stagePrefab == null || !definition.stagePrefab.IsSafe(out var error)) throw new InvalidOperationException("IsolatedSet requires a safe BattleStage prefab.");
                stage = Instantiate(definition.stagePrefab, origin + rotation * definition.IsolatedSetOffset, rotation);
                SceneManager.MoveGameObjectToScene(stage.gameObject, gameObject.scene); stageOrigin = stage.transform;
            }
            else
            {
                var go = new GameObject("Skill stage origin"); SceneManager.MoveGameObjectToScene(go, gameObject.scene);
                stageOrigin = go.transform; stageOrigin.SetPositionAndRotation(origin, rotation);
            }
            var participants = cameraDirector != null ? cameraDirector.SequenceTargets.ToList() : new List<BattleUnit> { target };
            if (!participants.Contains(target)) participants.Insert(0, target);
            if (!participants.Contains(caster)) participants.Insert(0, caster);
            int extra = 0;
            foreach (var unit in participants.Distinct())
            {
                if (!actors.TryGetActor(unit, out var actor)) continue;
                OwnActor(unit);
                var pose = unit == caster ? definition.CasterPose : definition.TargetPose;
                bool additional = unit != caster && unit != target;
                var anchor = stage != null ? unit == caster ? stage.casterAnchor : unit == target ? stage.targetAnchor : stage.AdditionalTargetAnchor(extra) : null;
                Vector3 offset = additional ? definition.AdditionalTargetSpacing * (++extra) : Vector3.zero;
                actor.PresentationRoot.SetPositionAndRotation(anchor != null ? anchor.position : stageOrigin.TransformPoint(pose.position + offset), anchor != null ? anchor.rotation : stageOrigin.rotation * pose.rotation);
                if (stage != null) actionSession.SetLayer(actor.PresentationRoot, stage.stageLayer);
            }
            stagePositions = true;
            foreach (var unit in flow.Units)
            {
                if (participants.Contains(unit) || !actors.TryGetActor(unit, out var actor)) continue;
                SetWorldVisible(actor, false);
                foreach (var renderer in actor.VisualRoot.GetComponentsInChildren<Renderer>(true)) HideRenderer(renderer, VisibilityScope.Stage);
            }
            if (stage == null) return;
            BattleStage.SetLayer(stage.transform, stage.stageLayer);
            if (cameraDirector != null && cameraDirector.outputCamera != null)
            { originalMask = cameraDirector.outputCamera.cullingMask; cameraDirector.outputCamera.cullingMask = 1 << stage.stageLayer; }
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.gameObject.scene != gameObject.scene) continue;
                lightMasks.Add((light, light.cullingMask));
                light.cullingMask = light.transform.IsChildOf(stage.transform) ? 1 << stage.stageLayer : light.cullingMask & ~(1 << stage.stageLayer);
            }
        }
        private IEnumerator EventSequence(string eventId, IReadOnlyList<BattleUnit> subjects)
        {
            actionSession = new BattleActionPresentationSession(null, null, battlefield, gameObject.scene);
            foreach (var unit in subjects) OwnActor(unit);
            cameraToken = cameraDirector != null ? cameraDirector.Acquire() : 0;
            if (eventId == "Revive") foreach (var unit in subjects)
            {
                deathPresented.Remove(unit); Display.SetVisible(unit, true);
                if (actors.TryGetActor(unit, out var actor)) actor.gameObject.SetActive(true);
            }
            yield return EventSequenceInternal(eventId, subjects);
            Display.Synchronize();
        }
        private IEnumerator EventSequenceInternal(string eventId, IReadOnlyList<BattleUnit> subjects)
        {
            if (subjects == null || subjects.Count == 0) yield break;
            if(cameraDirector != null) cameraDirector.SequenceTargets = subjects;
            cameraDirector?.FrameGroup(subjects);
            BattleSequenceDefinition definition = null;
            foreach (var entry in events ?? Array.Empty<BattleEventSequence>())
                if (entry.eventId == eventId) { definition = entry.sequence; break; }
            if (definition != null && definition.timeline != null && timelineDirector != null)
                yield return PlayTimeline(definition, subjects[0], subjects[subjects.Count - 1], _ => { });
            else yield return Wait(eventId == "Death" ? Profile != null ? Profile.deathSeconds : 0.65f
                : Profile != null ? Profile.entranceSeconds : 0.8f);
        }
        private BattleCameraProfile Profile => cameraDirector != null ? cameraDirector.profile : null;
        private IEnumerator Wait(float seconds)
        { for (float elapsed = 0; elapsed < seconds; elapsed += Clock.Delta) yield return null; }

        private IEnumerator PlayTimeline(BattleSequenceDefinition definition, BattleUnit caster, BattleUnit target, Action<BattleImpactMarker> impact)
        {
            Transform casterTransform = actors.TryGetActor(caster, out var casterActor) ? casterActor.VisualRoot : null;
            Transform targetTransform = actors.TryGetActor(target, out var targetActor) ? targetActor.VisualRoot : null;
            CapturePose(casterTransform);
            if (targetTransform != casterTransform) CapturePose(targetTransform);
            if (definition.Template == BattleSkillTemplate.Staged)
            {
                SetupStage(definition, caster, target);
            }
            CommandsVisibilityChanged?.Invoke(!definition.HideCommands);
            if (cameraDirector != null)
            {
                cameraDirector.SequenceOrigin = stageOrigin;
                cameraDirector.SequenceCaster = casterTransform; cameraDirector.SequenceTarget = targetTransform;
            }
            timelineDirector.timeUpdateMode = DirectorUpdateMode.Manual;
            timelineDirector.extrapolationMode = DirectorWrapMode.Hold;
            timelineDirector.playableAsset = definition.timeline;
            foreach (var output in definition.timeline.outputs)
            {
                UnityEngine.Object binding = null;
                if (output.sourceObject is BattleCameraShotTrack) binding = cameraDirector;
                else if (output.sourceObject is BattleStagePoseTrack) binding = this;
                else if (output.sourceObject is AnimationTrack animationTrack && (animationTrack.GetClips().Any() || animationTrack.infiniteClip != null))
                {
                    Transform actor = output.streamName == definition.casterTrack ? casterTransform
                        : output.streamName == definition.targetTrack ? targetTransform : null;
                    bool casterTrack = output.streamName == definition.casterTrack;
                    if (actor != null && !(casterTrack && definition.Animation.Driver == BattleBodyDriver.AnimatorController)
                        && !(target == caster && !casterTrack))
                        binding = actionSession?.Animation(casterTrack ? caster : target)?.Animator ?? actor.GetComponentInChildren<Animator>(true);
                    if (binding is Animator animator)
                    {
                        if (!animatorSpeeds.Any(entry => entry.animator == animator)) animatorSpeeds.Add((animator, animator.speed));
                        animator.speed = 0f;
                        var driver = actionSession?.Animation(casterTrack ? caster : target);
                        if (driver != null) driver.BeginTimeline();
                    }
                }
                timelineDirector.SetGenericBinding(output.sourceObject, binding);
            }
            var markers = CollectMarkers(definition.timeline);
            int next = 0;
            double duration = definition.timeline.duration;
            if (double.IsNaN(duration) || double.IsInfinity(duration) || duration < 0) duration = 0;
            timelineDirector.Play(); timelineDirector.Pause();
            double time = 0;
            while (true)
            {
                timelineDirector.time = time; timelineDirector.Evaluate();
                actionSession?.Animation(caster)?.BlendTimeline((float)time, definition.Animation.BlendSeconds);
                while (next < markers.Count && markers[next].time <= time)
                    impact(markers[next++]);
                if (time >= duration) break;
                yield return null;
                time = Math.Min(duration, time + Clock.Delta);
            }
        }
        public static List<BattleImpactMarker> CollectMarkers(TimelineAsset timeline)
        {
            var markers = new List<BattleImpactMarker>();
            void Read(TrackAsset track)
            {
                if (track == null || track.mutedInHierarchy) return;
                markers.AddRange(track.GetMarkers().OfType<BattleImpactMarker>());
                foreach (var child in track.GetChildTracks()) Read(child);
            }
            Read(timeline.markerTrack);
            foreach (var track in timeline.GetRootTracks()) Read(track);
            return markers.Distinct().OrderBy(m => m.time).ToList();
        }
        public void SetStagePose(BattleStageRole role, Pose localPose)
        {
            if (stageOrigin == null || actionSession == null || cameraDirector == null) return;
            var visual = role == BattleStageRole.Caster ? cameraDirector.SequenceCaster : cameraDirector.SequenceTarget;
            if (visual == null) return;
            var actor = visual.GetComponentInParent<BattleUnitActor>();
            if (actor != null) actor.PresentationRoot.SetPositionAndRotation(stageOrigin.TransformPoint(localPose.position), stageOrigin.rotation * localPose.rotation);
        }
        private void CapturePose(Transform root)
        {
            if (root == null) return;
            foreach (var item in root.GetComponentsInChildren<Transform>(true))
                poses.Add((item, item.localPosition, item.localRotation, item.localScale));
        }
        private void HideOtherAllies(BattleUnit caster)
        {
            if (caster == null || flow == null || actors == null) return;
            foreach (var unit in flow.Units)
            {
                if (unit == caster || unit.faction != Faction.OurSide) continue;
                if (!actors.TryGetActor(unit, out var actor)) continue;
                SetWorldVisible(actor, false);
                foreach (var renderer in actor.VisualRoot.GetComponentsInChildren<Renderer>(true))
                    HideRenderer(renderer, VisibilityScope.Action);
            }
        }
        private void HideRenderer(Renderer renderer, VisibilityScope scope)
        {
            if (renderer == null) return;
            if (hiddenRenderers.TryGetValue(renderer, out var state))
                hiddenRenderers[renderer] = (state.enabled, state.scopes | scope);
            else
                hiddenRenderers.Add(renderer, (renderer.enabled, scope));
            renderer.enabled = false;
        }
        private void RestoreVisibility(VisibilityScope scope)
        {
            if (hiddenRenderers.Count == 0) return;
            var renderers = hiddenRenderers.Keys.ToArray();
            foreach (var renderer in renderers)
            {
                var state = hiddenRenderers[renderer];
                var remaining = state.scopes & ~scope;
                if (remaining != 0)
                {
                    hiddenRenderers[renderer] = (state.enabled, remaining);
                    if (renderer != null) renderer.enabled = false;
                    continue;
                }
                if (renderer != null) renderer.enabled = state.enabled;
                hiddenRenderers.Remove(renderer);
            }
        }
        private void RestoreSequence(bool releaseCamera = true)
        {
            if (timelineDirector != null)
            {
                timelineDirector.Stop();
                if (timelineDirector.playableAsset != null)
                    foreach (var output in timelineDirector.playableAsset.outputs) timelineDirector.ClearGenericBinding(output.sourceObject);
                timelineDirector.playableAsset = null;
            }
            foreach (var entry in poses) if (entry.transform != null)
            { entry.transform.localPosition = entry.position; entry.transform.localRotation = entry.rotation; entry.transform.localScale = entry.scale; }
            poses.Clear();
            foreach (var entry in animatorSpeeds) if (entry.animator != null) entry.animator.speed = entry.speed;
            animatorSpeeds.Clear();
            if (actionSession != null && flow != null) foreach (var unit in flow.Units)
            { var animation = actionSession.Animation(unit); if (animation != null) animation.TimelineOwned = false; }
            RestoreVisibility(VisibilityScope.Stage);
            if (releaseCamera) RestoreVisibility(VisibilityScope.Action);
            foreach (var entry in lightMasks) if (entry.light != null) entry.light.cullingMask = entry.mask;
            lightMasks.Clear();
            if (stagePositions)
            {
                actionSession?.RestorePositions(false); actionSession?.RestoreLayers(); stagePositions = false;
                // Return the owned camera to the battlefield before restoring its culling mask.
                // Otherwise the normal blend would start from an isolated set below the battlefield.
                cameraDirector?.FrameCurrentContext();
            }
            if (stage != null)
            {
                if (cameraDirector != null && cameraDirector.outputCamera != null) cameraDirector.outputCamera.cullingMask = originalMask;
                stageOrigin = null;
                stage.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(stage.gameObject); else DestroyImmediate(stage.gameObject);
                stage = null;
            }
            if (stageOrigin != null)
            { var originObject = stageOrigin.gameObject; stageOrigin = null; if (Application.isPlaying) Destroy(originObject); else DestroyImmediate(originObject); }
            HUDVisibilityChanged?.Invoke(true);
            if (releaseCamera) CommandsVisibilityChanged?.Invoke(true);
            if (releaseCamera && cameraDirector != null) cameraDirector.Release(cameraToken);
        }
        private void DisposeRoutine()
        {
            if (routine == null) return;
            while (routine.Count > 0) (routine.Pop() as IDisposable)?.Dispose();
            routine = null;
        }
        public void Cancel()
        {
            generation++; completion = null; onFailure = null;
            DisposeRoutine(); RestoreSequence(); cameraDirector?.Cancel();
            EndActionSession(false);
            Display?.Dispose(); Display = null; flow = null; actors = null;
            deathPresented.Clear(); presentationCaster = null;
        }
        private void OnDisable() => Cancel();
    }
}
