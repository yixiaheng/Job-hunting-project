using System;
using Game.Events;

namespace Game.Inventory.Events
{
    public interface IInventoryEvent { }

    public sealed class InventoryEventBus : EventBus<IInventoryEvent>
    {
        public InventoryEventBus(Action<Exception, Type, Delegate> reportException = null,
            Action<Type, int> reportPublish = null) : base(reportException, reportPublish) { }
    }

    public readonly struct InventoryChangedEvent : IInventoryEvent
    {
        public InventoryService Inventory { get; }

        public InventoryChangedEvent(InventoryService inventory) { Inventory = inventory; }
    }
}
