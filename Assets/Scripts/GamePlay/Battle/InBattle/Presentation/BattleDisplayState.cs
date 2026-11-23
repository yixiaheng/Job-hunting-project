using System;
using System.Collections.Generic;
using Battle.Events;
using Battle.Logic;
using UnityEngine;

namespace Battle.Presentation
{
    /// <summary>Read-only HUD facade. Never writes authoritative combat health.</summary>
    public sealed class BattleDisplayState : IDisposable
    {
        private static readonly Dictionary<BattleUnit, BattleDisplayState> owners = new();
        private readonly Dictionary<BattleUnit, int> health = new();
        private readonly HashSet<BattleUnit> hidden = new();
        public static event Action<HealthChangedEvent> HealthChanged;
        public BattlePresentationClock Clock { get; }
        public BattleDisplayState(BattlePresentationClock clock) => Clock = clock;
        public static bool IsManaged(BattleUnit unit) => unit != null && owners.ContainsKey(unit);
        public static int Health(BattleUnit unit) => unit == null ? 0
            : owners.TryGetValue(unit, out var owner) ? owner.health[unit] : unit.currentHealth;
        public static bool Visible(BattleUnit unit) => unit != null
            && (owners.TryGetValue(unit, out var owner) ? !owner.hidden.Contains(unit) : unit.IsActive);
        public static float Delta(BattleUnit unit) => unit != null && owners.TryGetValue(unit, out var owner)
            ? owner.Clock.Delta : UnityEngine.Time.deltaTime;
        public void Register(IEnumerable<BattleUnit> units)
        {
            foreach (var unit in units)
            {
                if (unit == null || health.ContainsKey(unit)) continue;
                owners[unit] = this;
                health[unit] = unit.currentHealth;
                if (!unit.IsActive) hidden.Add(unit);
            }
        }
        public void Apply(BattleEffectRecord effect)
        {
            if (effect.Type == BattleEffectRecordType.Damage || effect.Type == BattleEffectRecordType.Heal)
                SetHealth(effect.Target, effect.After);
        }
        public void Synchronize()
        {
            foreach (var unit in new List<BattleUnit>(health.Keys)) SetHealth(unit, unit.currentHealth);
        }
        private void SetHealth(BattleUnit unit, int value)
        {
            if (unit == null || !health.TryGetValue(unit, out int before)) return;
            value = Mathf.Clamp(value, 0, unit.maxHealth);
            health[unit] = value;
            if (before != value) HealthChanged?.Invoke(new HealthChangedEvent(unit, before, value, unit.maxHealth));
        }
        public void SetVisible(BattleUnit unit, bool visible)
        {
            if (visible) hidden.Remove(unit); else hidden.Add(unit);
        }
        public void Dispose()
        {
            foreach (var unit in health.Keys)
                if (owners.TryGetValue(unit, out var owner) && owner == this) owners.Remove(unit);
            health.Clear(); hidden.Clear();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { owners.Clear(); HealthChanged = null; }
    }
}
