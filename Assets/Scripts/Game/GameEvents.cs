using System;
using Battle.Events;
using Game.Inventory.Events;
using UnityEngine;

namespace Game
{
    public static class GameEvents
    {
        public static BattleEventBus Battle { get; } 
                = new BattleEventBus(ReportException, ReportPublish);
        public static InventoryEventBus Inventory { get; } 
                = new InventoryEventBus(ReportException, ReportPublish);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            ClearForNewSession();
            Battle.EnableLogging = false;
            Inventory.EnableLogging = false;
        }

        public static void ClearForNewSession()
        {
            Battle.Clear();
            Inventory.Clear();
        }

        private static void ReportException(Exception exception, Type eventType, Delegate handler)
        {
            Debug.LogError($"事件监听异常 [{eventType.FullName}] {handler.Method.DeclaringType?.FullName}.{handler.Method.Name}\n{exception}");
        }

        private static void ReportPublish(Type eventType, int subscriberCount)
        {
            Debug.Log($"事件发布 [{eventType.FullName}] 监听数：{subscriberCount}");
        }
    }
}
