using System.Collections.Generic;
using UnityEngine;

namespace Battle.Presentation
{
    public sealed class BattlefieldDirector : MonoBehaviour
    {
        [SerializeField] private BattlefieldLayout layout;
        [SerializeField] private Transform battlefieldRoot;
        private readonly Dictionary<BattleUnit, BattleUnitActor> actors = new();
        private readonly Dictionary<BattleUnit, BattleSlotPose> slots = new();
        private readonly List<Motion> motions = new();
        private BattlePresentationClock clock;
        private BattleUnit ready;
        private sealed class Motion
        {
            public Transform root;
            public Vector3 from, to;
            public Quaternion fromRotation, toRotation;
            public float elapsed, duration;
        }
        public BattlefieldLayout Layout => layout;
        public Transform Root => battlefieldRoot != null ? battlefieldRoot : transform;
        public bool IsMoving => motions.Count != 0;
        public void Configure(BattlefieldLayout value, Transform origin) { layout = value; battlefieldRoot = origin; }
        public void Bind(BattlePresentationClock value) => clock = value;
        public Pose GetSpawnPose(Faction side, int index, int count, string preset = null)
        {
            var slot = layout.GetSlot(side, index, count, preset);
            return new Pose(Root.TransformPoint(slot.position), Root.rotation * Quaternion.Euler(slot.euler));
        }
        public void Register(BattleUnitActor actor, int capacity, string preset = null)
        {
            var unit = actor.Unit;
            actors[unit] = actor;
            slots[unit] = layout.GetSlot(unit.faction, unit.pos, capacity, preset).Copy();
            ApplyHome(unit);
        }
        public Vector3 ReadyPosition(BattleUnit unit)
        {
            if (unit != null && slots.TryGetValue(unit, out var slot))
                return Root.TransformPoint(slot.position + (unit.faction == Faction.OurSide ? slot.readyOffset : Vector3.zero));
            return Root.position;
        }
        public Vector3 CameraOffset(BattleUnit unit) => unit != null && slots.TryGetValue(unit, out var slot) ? slot.cameraOffset : Vector3.zero;
        public void SetReady(BattleUnit unit, bool immediate = false)
        {
            if (ready == unit) return;
            if (ready != null) Move(ready, false, immediate);
            ready = unit != null && unit.faction == Faction.OurSide ? unit : null;
            if (ready != null) Move(ready, true, immediate);
        }
        public void ReturnHome(BattleUnit unit, bool immediate = false)
        { Move(unit, false, immediate); if (ready == unit) ready = null; }
        private void Move(BattleUnit unit, bool preparing, bool immediate)
        {
            if (unit == null || !actors.TryGetValue(unit, out var actor) || actor == null || !slots.TryGetValue(unit, out var slot)) return;
            var root = actor.PresentationRoot;
            motions.RemoveAll(m => m.root == root);
            Vector3 position = Root.TransformPoint(slot.position + (preparing ? slot.readyOffset : Vector3.zero));
            Quaternion rotation = Root.rotation * Quaternion.Euler(slot.euler + (preparing ? slot.readyEuler : Vector3.zero));
            if (immediate || layout.ReadySeconds <= 0) { root.SetPositionAndRotation(position, rotation); return; }
            motions.Add(new Motion { root = root, from = root.position, to = position, fromRotation = root.rotation,
                toRotation = rotation, duration = layout.ReadySeconds });
        }
        private void ApplyHome(BattleUnit unit)
        {
            if (!actors.TryGetValue(unit, out var actor) || actor == null) return;
            var slot = slots[unit];
            var identity = actor.PresentationRoot != actor.transform ? actor.PresentationRoot.parent : actor.transform;
            identity.SetPositionAndRotation(Root.TransformPoint(slot.position), Root.rotation * Quaternion.Euler(slot.euler));
            Move(unit, false, true);
        }
        public void ApplyLayout(BattlefieldLayout value, int enemyCapacity, string preset = null, BattleUnit previewActor = null)
        {
            layout = value; motions.Clear(); ready = null;
            foreach (var entry in actors)
            {
                slots[entry.Key] = layout.GetSlot(entry.Key.faction, entry.Key.pos,
                    entry.Key.faction == Faction.OurSide ? 4 : enemyCapacity, preset).Copy();
                ApplyHome(entry.Key);
            }
            SetReady(previewActor, true);
        }
        public void Unregister(BattleUnit unit) { if (actors.TryGetValue(unit, out var actor) && actor != null) motions.RemoveAll(m => m.root == actor.PresentationRoot); actors.Remove(unit); slots.Remove(unit); if (ready == unit) ready = null; }
        public void Clear() { motions.Clear(); actors.Clear(); slots.Clear(); ready = null; clock = null; }
        private void LateUpdate()
        {
            float delta = clock?.Delta ?? 0;
            if (delta <= 0) return;
            for (int i = motions.Count - 1; i >= 0; i--)
            {
                var m = motions[i];
                if (m.root == null) { motions.RemoveAt(i); continue; }
                m.elapsed += delta; float t = Mathf.Clamp01(m.elapsed / m.duration); t = t * t * (3 - 2 * t);
                m.root.SetPositionAndRotation(Vector3.Lerp(m.from, m.to, t), Quaternion.Slerp(m.fromRotation, m.toRotation, t));
                if (m.elapsed >= m.duration) motions.RemoveAt(i);
            }
        }
    }
}
