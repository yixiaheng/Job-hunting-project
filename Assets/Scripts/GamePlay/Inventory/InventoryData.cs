using System;
using System.Collections.Generic;

namespace Game.Inventory
{
    /// <summary>
    /// 物品堆叠
    /// </summary>
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

    /// <summary>
    /// 仓库数据
    /// </summary>
    [Serializable]
    public class InventoryData
    {
        //物品堆叠的列表
        public List<ItemStack> stacks = new List<ItemStack>();
        //装备实例的列表
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
