using System;
using System.Collections.Generic;

namespace Game.Inventory
{
    [Serializable]
    public class ItemStack
    {
        //物品id
        public string configId;
        //物品数量
        public int quantity;
    }

    [Serializable]
    public class EquipmentInstance
    {
        public string instanceId;
        public string configId;
        public int level = 1;
        public int experience;
        public int ascension;
        public bool isLocked;
    }

    [Serializable]
    public class InventoryData
    {
        public List<ItemStack> stacks = new List<ItemStack>();
        public List<EquipmentInstance> equipment = new List<EquipmentInstance>();
    }

    [Serializable]
    public class ItemAmount
    {
        public string configId;
        public int quantity = 1;

        public ItemAmount() { }

        public ItemAmount(string configId, int quantity)
        {
            this.configId = configId;
            this.quantity = quantity;
        }
    }

    public sealed class InventoryEntry
    {
        public ItemConfig Config { get; }
        public int Quantity { get; }
        public EquipmentInstance Equipment { get; }
        public string Key => Equipment == null ? Config.id : Equipment.instanceId;

        public InventoryEntry(ItemConfig config, int quantity, EquipmentInstance equipment = null)
        {
            Config = config;
            Quantity = quantity;
            Equipment = equipment;
        }
    }
}
