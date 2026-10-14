using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// 物品仓库，ScriptObject存储
    /// </summary>
    [CreateAssetMenu(fileName = "ItemCatalog", menuName = "Inventory/Catalog")]
    public class ItemCatalog : ScriptableObject
    {
        public List<ItemConfig> items = new List<ItemConfig>();

        public Dictionary<string, ItemConfig> BuildIndex()
        {
            if (items == null) throw new InvalidOperationException("物品目录不能为空。");
            var result = new Dictionary<string, ItemConfig>(StringComparer.Ordinal);
            foreach (ItemConfig config in items)
            {
                if (config == null || string.IsNullOrWhiteSpace(config.id))
                    throw new InvalidOperationException("物品目录包含空配置或空 ID。");
                if (!result.TryAdd(config.id, config))
                    throw new InvalidOperationException("重复的物品 ID：" + config.id);
                if (config.IsEquipment != (config.category == ItemCategory.Equipment))
                    throw new InvalidOperationException("装备必须使用 EquipmentConfig：" + config.id);
            }
            return result;
        }
    }
}
