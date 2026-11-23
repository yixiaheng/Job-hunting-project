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
        public bool Busy => routine != null;
        public event Action<bool> HUDVisibilityChanged;
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
        private readonly List<(Renderer renderer, bool enabled)> hiddenRenderers = new();
        private readonly List<(Transform transform, Vector3 position, Quaternion rotation, Vector3 scale)> poses = new();
        private readonly List<(Animator animator, float speed)> animatorSpeeds = new();
        private readonly List<(Light light, int mask)> lightMasks = new();
        private readonly HashSet<BattleUnit> deathPresented = new();
        private BattleUnit presentationCaster;
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
                onFailure?.Invoke();
                DisposeRoutine();
            }
            if (session != generation) return;
            routine = null; var done = completion; completion = null; onFailure = null;
            RestoreSequence(); presentationCaster = null;
            cameraDirector?.SetBase(flow?.CurrentUnit);
            done?.Invoke();
        }
        private IEnumerator ActionSequence(ActionResolution resolution)
        {
            var record = resolution?.Record;
            if (record == null) yield break;
            presentationCaster = record.Caster;
            cameraDirector?.SetBase(record.Caster);
            cameraToken = cameraDirector != null ? cameraDirector.Acquire() : 0;
            var applied = new HashSet<int>();
            void Apply(int index)
            {
                if (index < 0) { for (int i = 0; i < record.Effects.Count; i++) Apply(i); return; }
                if (index >= record.Effects.Count || !applied.Add(index)) return;
                Display.Apply(record.Effects[index]); EffectPresented?.Invoke(record.Effects[index]);
            }
            BattleSequenceDefinition definition = null;
            foreach (var entry in skills ?? Array.Empty<BattleSkillSequence>())
                if (record.Skill != null && entry.skillId == record.Skill.id && entry.casterFaction == record.Caster.faction)
                { definition = entry.sequence; break; }
            var targets = record.Effects.Where(e => e.Target != null && e.Target != record.Caster)
                .Select(e => e.Target).Distinct().ToList();
            // Validation may auto-select a replacement for a missing/dead/wrong-side requested target.
            // Aim at the recorded recipients, including a recipient killed by this action.
            BattleUnit visualTarget = record.MainTarget != null && targets.Contains(record.MainTarget)
                ? record.MainTarget : targets.FirstOrDefault() ?? record.Caster;
            if(cameraDirector != null) cameraDirector.SequenceTargets = targets;
            if (record.Caster?.faction == Faction.Enemy)
            {
                cameraDirector?.SetContext(new BattleCameraContext(record.Caster, visualTarget, targets, BattleShotMode.EnemyAction));
                cameraDirector?.FrameCurrentContext();
            }
            if (definition != null && definition.timeline != null && timelineDirector != null)
                yield return PlayTimeline(definition, record.Caster, visualTarget,
                    marker => Apply(marker.ResolveIndex(record)));
            else
            {
                yield return Wait(Profile != null ? Profile.fallbackWindup : 0.3f);
                Apply(-1);
                yield return Wait(Profile != null ? Profile.fallbackRecovery : 0.25f);
            }
            Apply(-1); Display.Synchronize();
            RestoreSequence(false);
            battlefield?.ReturnHome(record.Caster);
            while (battlefield != null && battlefield.IsMoving) yield return null;
            var deaths = flow.Units.Where(u => !u.IsActive && !deathPresented.Contains(u)).ToList();
            if (deaths.Count > 0)
            {
                yield return EventSequenceInternal("Death", deaths);
                foreach (var unit in deaths) FinishDeath(unit);
            }
        }
        private void FinishDeath(BattleUnit unit)
        {
            deathPresented.Add(unit); Display.SetVisible(unit, false);
            if (actors.TryGetActor(unit, out var actor)) actor.gameObject.SetActive(false);
        }
        private IEnumerator EventSequence(string eventId, IReadOnlyList<BattleUnit> subjects)
        {
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
            if (definition.stagePrefab != null)
            {
                if (!definition.stagePrefab.IsSafe(out var error)) throw new InvalidOperationException(error);
                stage = Instantiate(definition.stagePrefab, new Vector3(0, -10000f, 0), Quaternion.identity);
                SceneManager.MoveGameObjectToScene(stage.gameObject, gameObject.scene);
                if (casterTransform != null) casterTransform = BattleStage.CloneVisual(casterTransform,
                    stage.casterAnchor != null ? stage.casterAnchor : stage.transform).transform;
                if (targetTransform != null) targetTransform = BattleStage.CloneVisual(targetTransform,
                    stage.targetAnchor != null ? stage.targetAnchor : stage.transform).transform;
                BattleStage.SetLayer(stage.transform, stage.stageLayer);
                if (cameraDirector != null && cameraDirector.outputCamera != null)
                {
                    originalMask = cameraDirector.outputCamera.cullingMask;
                    cameraDirector.outputCamera.cullingMask = 1 << stage.stageLayer;
                }
                foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (light.gameObject.scene != gameObject.scene) continue;
                    lightMasks.Add((light, light.cullingMask));
                    light.cullingMask = light.transform.IsChildOf(stage.transform)
                        ? 1 << stage.stageLayer : light.cullingMask & ~(1 << stage.stageLayer);
                }
                foreach (var unit in flow.Units)
                    if (actors.TryGetActor(unit, out var actor)) foreach (var renderer in actor.GetComponentsInChildren<Renderer>(true))
                    { hiddenRenderers.Add((renderer, renderer.enabled)); renderer.enabled = false; }
            }
            if (definition.hideBattleHUD || stage != null) HUDVisibilityChanged?.Invoke(false);
            if (cameraDirector != null)
            {
                cameraDirector.SequenceOrigin = stage != null ? stage.transform : null;
                cameraDirector.SequenceCaster = casterTransform; cameraDirector.SequenceTarget = targetTransform;
            }
            timelineDirector.timeUpdateMode = DirectorUpdateMode.Manual;
            timelineDirector.extrapolationMode = DirectorWrapMode.Hold;
            timelineDirector.playableAsset = definition.timeline;
            foreach (var output in definition.timeline.outputs)
            {
                UnityEngine.Object binding = null;
                if (output.sourceObject is BattleCameraShotTrack) binding = cameraDirector;
                else if (output.sourceObject is AnimationTrack)
                {
                    Transform actor = output.streamName == definition.casterTrack ? casterTransform
                        : output.streamName == definition.targetTrack ? targetTransform : null;
                    if (actor != null) binding = actor.GetComponentInChildren<Animator>();
                    if (binding is Animator animator)
                    { animatorSpeeds.Add((animator, animator.speed)); animator.speed = 0f; }
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
        private void CapturePose(Transform root)
        {
            if (root == null) return;
            foreach (var item in root.GetComponentsInChildren<Transform>(true))
                poses.Add((item, item.localPosition, item.localRotation, item.localScale));
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
            foreach (var entry in hiddenRenderers) if (entry.renderer != null) entry.renderer.enabled = entry.enabled;
            hiddenRenderers.Clear();
            foreach (var entry in lightMasks) if (entry.light != null) entry.light.cullingMask = entry.mask;
            lightMasks.Clear();
            if (stage != null)
            {
                if (cameraDirector != null && cameraDirector.outputCamera != null) cameraDirector.outputCamera.cullingMask = originalMask;
                stage.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(stage.gameObject); else DestroyImmediate(stage.gameObject);
                stage = null;
            }
            HUDVisibilityChanged?.Invoke(true);
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
            Display?.Dispose(); Display = null; flow = null; actors = null;
            deathPresented.Clear(); presentationCaster = null;
        }
        private void OnDisable() => Cancel();
    }
}
