using System;
using System.Collections.Generic;
using Battle.Logic;
using Battle.SaveData;
using Game.Events;
using UnityEngine;

namespace Battle.Events
{
    public interface IBattleEvent { }

    public sealed class BattleEventBus : EventBus<IBattleEvent>
    {
        public BattleEventBus(
            Action<Exception, Type, Delegate> reportException = null,
            Action<Type, int> reportPublish = null) 
            : base(reportException, reportPublish)
        {
            
        }
    }
    public readonly struct AllySlectionEvent : IBattleEvent
    {
        public UnitConfig Unit{ get; }
        public AllySlectionEvent(UnitConfig unit)
        {
            Unit = unit;
        }
    }
    
    /// <summary>
    /// 血量变化
    /// </summary>
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

    /// <summary>
    /// 选中目标变化
    /// </summary>
    public readonly struct TargetSelectionChangedEvent : IBattleEvent
    {
        public Guid BattleId { get; }
        public BattleUnit PreviousTarget { get; }
        public BattleUnit CurrentTarget { get; }

        public TargetSelectionChangedEvent(
            Guid battleId,
            BattleUnit previousTarget,
            BattleUnit currentTarget)
        {
            BattleId = battleId;
            PreviousTarget = previousTarget;
            CurrentTarget = currentTarget;
        }
    }
}
