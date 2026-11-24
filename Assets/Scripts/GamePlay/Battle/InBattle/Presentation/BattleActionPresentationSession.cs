using System;
using System.Collections.Generic;
using System.Linq;
using Battle.Logic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Battle.Presentation
{
    public enum BattlePresentationPhase { Preparing, Approach, Main, Waiting, Return, Death, Exiting, Completed }

    /// <summary>Resources owned by one visual action; disposing never rewinds combat data.</summary>
    public sealed class BattleActionPresentationSession : IDisposable
    {
        private sealed class ActorState
        {
            public BattleUnitActor Actor;
            public Pose Start, Home;
            public int Token;
            public BattleActorAnimationDriver Animation;
            public readonly List<(Transform node, Vector3 position, Quaternion rotation, Vector3 scale)> Local = new();
        }
        private sealed class Flight
        {
            public GameObject Object;
            public Vector3 Start, End;
            public BattleUnitActor Follow;
            public float Elapsed, Duration;
            public Action Arrived;
        }
        private sealed class Effect
        {
            public GameObject Object;
            public ParticleSystem[] Particles;
            public Animator[] Animators;
            public float Time, Lifetime;
            public bool Started;
        }
        private readonly Dictionary<BattleUnit, ActorState> actors = new();
        private readonly Dictionary<Transform, int> layers = new();
        private readonly List<Flight> flights = new();
        private readonly List<Effect> effects = new();
        private readonly BattlefieldDirector battlefield;
        private readonly Scene scene;
        private bool disposed;
        public ActionResolution Resolution { get; }
        public BattleSequenceDefinition Definition { get; }
        public BattlePresentationPhase Phase { get; set; }
        public float Elapsed { get; private set; }
        public readonly HashSet<int> Applied = new();
        public readonly HashSet<int> Reserved = new();
        public readonly List<string> EventLog = new();
        public bool HasBlocking => flights.Count > 0 || actors.Values.Any(a => a.Animation.Reacting);
        public bool HasFlights => flights.Count > 0;
        public BattleActionPresentationSession(ActionResolution resolution, BattleSequenceDefinition definition, BattlefieldDirector field, Scene owner)
        { Resolution = resolution; Definition = definition; battlefield = field; scene = owner; }
        public void Own(BattleUnit unit, BattleUnitActor actor)
        {
            if (unit == null || actor == null || actors.ContainsKey(unit)) return;
            var root = actor.PresentationRoot;
            var state = new ActorState { Actor = actor, Start = new Pose(root.position, root.rotation),
                Home = battlefield != null ? battlefield.HomePose(unit) : new Pose(root.position, root.rotation),
                Token = battlefield != null ? battlefield.Acquire(unit) : 0, Animation = new BattleActorAnimationDriver(actor) };
            foreach (var t in actor.VisualRoot.GetComponentsInChildren<Transform>(true)) state.Local.Add((t, t.localPosition, t.localRotation, t.localScale));
            actors.Add(unit, state);
        }
        public BattleActorAnimationDriver Animation(BattleUnit unit) => unit != null && actors.TryGetValue(unit, out var state) ? state.Animation : null;
        public Pose Home(BattleUnit unit) => actors[unit].Home;
        public void RestoreLocal()
        {
            foreach (var actor in actors.Values)
                foreach (var pose in actor.Local) if (pose.node != null)
                    pose.node.SetLocalPositionAndRotation(pose.position, pose.rotation);
            foreach (var actor in actors.Values) foreach (var pose in actor.Local) if (pose.node != null) pose.node.localScale = pose.scale;
        }
        public void RestorePositions(bool homeCaster)
        {
            foreach (var pair in actors)
            {
                if (pair.Value.Actor == null) continue;
                var pose = homeCaster && pair.Key == Resolution?.Record?.Caster ? pair.Value.Home : pair.Value.Start;
                pair.Value.Actor.PresentationRoot.SetPositionAndRotation(pose.position, pose.rotation);
            }
        }
        public void SetLayer(Transform root, int layer)
        { foreach (var node in root.GetComponentsInChildren<Transform>(true)) { if (!layers.ContainsKey(node)) layers.Add(node, node.gameObject.layer); node.gameObject.layer = layer; } }
        public void RestoreLayers()
        { foreach (var pair in layers) if (pair.Key != null) pair.Key.gameObject.layer = pair.Value; layers.Clear(); }
        public void Log(string message)
        { if (EventLog.Count > 200) EventLog.RemoveAt(0); EventLog.Add($"{Elapsed:F3} {Phase}: {message}"); }
        public static Vector3 Point(BattleUnitActor actor, BattleAnchor kind, Vector3 offset)
        {
            var anchor = actor.Presentation != null ? actor.Presentation.Anchor(kind) : null;
            return anchor != null ? anchor.position + anchor.rotation * offset
                : (kind == BattleAnchor.Foot ? actor.PresentationRoot.position : actor.FocusPosition) + actor.PresentationRoot.rotation * offset;
        }
        public void Impact(BattleUnitActor actor)
        {
            if (Definition == null || actor == null || Definition.ImpactPrefab == null) return;
            var go = Object.Instantiate(Definition.ImpactPrefab, Point(actor, Definition.ImpactAnchor, Definition.ImpactOffset), actor.PresentationRoot.rotation);
            SceneManager.MoveGameObjectToScene(go, scene);
            BattleStage.SetLayer(go.transform, actor.gameObject.layer);
            TrackEffect(go, Definition.EffectLifetime);
        }
        private void TrackEffect(GameObject go, float lifetime)
        {
            var item = new Effect { Object = go, Lifetime = lifetime, Particles = go.GetComponentsInChildren<ParticleSystem>(true), Animators = go.GetComponentsInChildren<Animator>(true) };
            foreach (var particle in item.Particles) { particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); var main = particle.main; main.playOnAwake = false; }
            foreach (var animator in item.Animators) { animator.speed = 0; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
            effects.Add(item);
        }
        public void Launch(BattleUnitActor caster, BattleUnitActor target, Action arrived)
        {
            var settings = Definition.Projectile;
            Vector3 start = Point(caster, BattleAnchor.Cast, Vector3.zero), end = Point(target, BattleAnchor.Body, Definition.ImpactOffset);
            GameObject go = settings.Prefab != null ? Object.Instantiate(settings.Prefab) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Battle projectile"; SceneManager.MoveGameObjectToScene(go, scene);
            BattleStage.SetLayer(go.transform, caster.gameObject.layer);
            if (settings.Prefab == null) { go.transform.localScale = Vector3.one * .15f; var collider = go.GetComponent<Collider>(); if (collider != null) collider.enabled = false; }
            go.transform.position = start;
            flights.Add(new Flight { Object = go, Start = start, End = end, Follow = settings.FollowTarget ? target : null, Duration = settings.Duration(Vector3.Distance(start, end)), Arrived = arrived });
            TrackEffect(go, float.PositiveInfinity);
        }
        public void Tick(float delta)
        {
            if (disposed || delta <= 0) return;
            Elapsed += delta;
            foreach (var actor in actors.Values) actor.Animation.Tick(delta);
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var effect = effects[i]; effect.Time += delta;
                if (effect.Object == null || effect.Time >= effect.Lifetime) { Destroy(effect.Object); effects.RemoveAt(i); continue; }
                foreach (var particle in effect.Particles) if (particle != null) particle.Simulate(delta, false, !effect.Started, false);
                effect.Started = true;
                foreach (var animator in effect.Animators) if (animator != null) { animator.speed = 1; animator.Update(delta); animator.speed = 0; }
            }
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                var flight = flights[i]; flight.Elapsed += delta;
                if (flight.Follow != null) flight.End = Point(flight.Follow, BattleAnchor.Body, Definition.ImpactOffset);
                float t = Mathf.Clamp01(flight.Elapsed / flight.Duration);
                if (flight.Object != null)
                { flight.Object.transform.position = Vector3.Lerp(flight.Start, flight.End, t); var direction = flight.End - flight.Start; if (direction.sqrMagnitude > .0001f) flight.Object.transform.rotation = Quaternion.LookRotation(direction); }
                if (t < 1) continue;
                Destroy(flight.Object); flights.RemoveAt(i); flight.Arrived?.Invoke();
            }
        }
        public void CancelPending()
        { foreach (var flight in flights) Destroy(flight.Object); flights.Clear(); Reserved.Clear(); }
        public void Dispose()
        {
            if (disposed) return; disposed = true; CancelPending();
            foreach (var effect in effects) Destroy(effect.Object); effects.Clear();
            RestoreLayers();
            foreach (var pair in actors)
            {
                try { pair.Value.Animation.Dispose(); }
                catch (Exception error) { Debug.LogException(error); }
                finally { battlefield?.Release(pair.Key, pair.Value.Token); }
            }
            RestoreLocal(); Phase = BattlePresentationPhase.Completed;
        }
        private static void Destroy(GameObject go)
        { if (go == null) return; go.SetActive(false); if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go); }
    }
}
