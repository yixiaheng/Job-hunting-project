using System;
using Battle.Logic;
using Game.Events;
using UnityEngine;

namespace Battle.Events
{
    public interface IBattleEvent { }

    public sealed class BattleEventBus : EventBus<IBattleEvent>
    {
        public BattleEventBus(Action<Exception, Type, Delegate> reportException = null,
            Action<Type, int> reportPublish = null) : base(reportException, reportPublish) { }
    }

    public readonly struct HealthChangedEvent : IBattleEvent
    {
        public BattleUnit Unit { get; }
        public Guid BattleId{ get;}
        public int PreviousHealth { get; }
        public int CurrentHealth { get; }
        public int MaxHealth { get; }

        public HealthChangedEvent(BattleUnit unit, int previousHealth, int currentHealth, int maxHealth)
        {
            Unit = unit;
            BattleId = Unit.BattleId;
            PreviousHealth = previousHealth;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
        }
    }

}
