using System;
using System.Collections.Generic;

namespace Game.Inventory
{
    //背包服务
    public sealed class InventoryService
    {   
        //背包物品与string名字绑定
        private readonly Dictionary<string, ItemConfig> configs;
        private readonly Dictionary<string, int> stacks = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, EquipmentInstance> equipment = new Dictionary<string, EquipmentInstance>(StringComparer.Ordinal);
        private readonly Dictionary<ItemUseKind, IItemUseHandler> useHandlers = new Dictionary<ItemUseKind, IItemUseHandler>();
        private bool executingUse;

        public event Action Changed;
        public IEquipmentUsageProvider EquipmentUsage { get; set; }

        public InventoryService(ItemCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            configs = catalog.BuildIndex();
        }

        public bool TryGetConfig(string configId, out ItemConfig config)
        {
            config = null;
            return configId != null && configs.TryGetValue(configId, out config);
        }

        public int GetQuantity(string configId)
        {
            if (configId == null) return 0;
            if (stacks.TryGetValue(configId, out int quantity)) return quantity;
            int count = 0;
            foreach (EquipmentInstance instance in equipment.Values)
                if (instance.configId == configId) count++;
            return count;
        }

        public bool TryAdd(string configId, int quantity, out string reason)
        {
            return TryAddRewards(new[] { new ItemAmount(configId, quantity) }, out reason);
        }

        public bool TryAddRewards(IEnumerable<ItemAmount> rewards, out string reason)
        {
            reason = null;
            if (!CanMutate(out reason) || !TryAggregate(rewards, out var amounts, out reason)) return false;
            foreach (KeyValuePair<string, int> amount in amounts)
            {
                if (!configs[amount.Key].IsEquipment && GetQuantity(amount.Key) > int.MaxValue - amount.Value)
                {
                    reason = "物品数量超出上限。";
                    return false;
                }
            }
            foreach (KeyValuePair<string, int> amount in amounts)
            {
                if (configs[amount.Key].IsEquipment)
                {
                    for (int index = 0; index < amount.Value; index++)
                    {
                        var instance = new EquipmentInstance { instanceId = Guid.NewGuid().ToString("N"), configId = amount.Key };
                        equipment.Add(instance.instanceId, instance);
                    }
                }
                else stacks[amount.Key] = GetQuantity(amount.Key) + amount.Value;
            }
            Changed?.Invoke();
            return true;
        }

        public bool TryRemove(string configId, int quantity, out string reason)
        {
            return TryConsume(new[] { new ItemAmount(configId, quantity) }, out reason);
        }

        public bool TryConsume(IEnumerable<ItemAmount> costs, out string reason)
        {
            reason = null;
            if (!CanMutate(out reason) || !TryAggregate(costs, out var amounts, out reason)) return false;
            foreach (KeyValuePair<string, int> amount in amounts)
            {
                if (configs[amount.Key].IsEquipment)
                {
                    reason = "独立装备必须通过实例 ID 移除。";
                    return false;
                }
                if (GetQuantity(amount.Key) < amount.Value)
                {
                    reason = configs[amount.Key].displayName + "数量不足。";
                    return false;
                }
            }
            foreach (KeyValuePair<string, int> amount in amounts) RemoveStack(amount.Key, amount.Value);
            Changed?.Invoke();
            return true;
        }

        public bool TryGetEquipment(string instanceId, out EquipmentInstance instance)
        {
            instance = null;
            if (instanceId == null || !equipment.TryGetValue(instanceId, out var stored)) return false;
            instance = Copy(stored);
            return true;
        }

        public bool TrySetLocked(string instanceId, bool locked, out string reason)
        {
            reason = null;
            if (!CanMutate(out reason)) return false;
            if (instanceId == null || !equipment.TryGetValue(instanceId, out var instance))
            {
                reason = "装备不存在。";
                return false;
            }
            if (instance.isLocked == locked) return true;
            instance.isLocked = locked;
            Changed?.Invoke();
            return true;
        }

        public bool TryRemoveEquipment(string instanceId, out string reason)
        {
            reason = null;
            if (!CanMutate(out reason)) return false;
            if (instanceId == null || !equipment.TryGetValue(instanceId, out var instance))
            {
                reason = "装备不存在。";
                return false;
            }
            if (instance.isLocked || EquipmentUsage != null && EquipmentUsage.IsEquipped(instanceId))
            {
                reason = "锁定或已穿戴的装备不能移除。";
                return false;
            }
            equipment.Remove(instanceId);
            Changed?.Invoke();
            return true;
        }

        public bool TrySetEquipmentProgress(string instanceId, int level, int experience, int ascension, out string reason)
        {
            reason = null;
            if (!CanMutate(out reason)) return false;
            if (instanceId == null || !equipment.TryGetValue(instanceId, out var instance))
            {
                reason = "装备不存在。";
                return false;
            }
            var config = (EquipmentConfig)configs[instance.configId];
            if (level < 1 || level > config.maxLevel || experience < 0 || ascension < 0)
            {
                reason = "装备养成数据无效。";
                return false;
            }
            instance.level = level;
            instance.experience = experience;
            instance.ascension = ascension;
            Changed?.Invoke();
            return true;
        }

        public void RegisterUseHandler(ItemUseKind kind, IItemUseHandler handler)
        {
            if (handler == null) useHandlers.Remove(kind);
            else useHandlers[kind] = handler;
        }

        public bool CanUse(string configId, int quantity, string targetId, out string reason)
        {
            reason = null;
            if (!TryGetConfig(configId, out var config) || config.IsEquipment || config.useKind == ItemUseKind.None)
            {
                reason = "该物品不能直接使用。";
                return false;
            }
            if (quantity <= 0 || GetQuantity(configId) < quantity)
            {
                reason = "物品数量不足或数量无效。";
                return false;
            }
            if (!useHandlers.TryGetValue(config.useKind, out var handler))
            {
                reason = "对应的使用或养成系统尚未接入。";
                return false;
            }
            if (config.requiresTarget && string.IsNullOrWhiteSpace(targetId))
            {
                reason = "请先选择目标角色或装备。";
                return false;
            }
            return handler.CanUse(new ItemUseRequest(config, quantity, targetId), out reason);
        }

        public bool TryUse(string configId, int quantity, string targetId, out string reason)
        {
            reason = null;
            if (!CanMutate(out reason)) return false;
            executingUse = true;
            try
            {
                if (!CanUse(configId, quantity, targetId, out reason)) return false;
                ItemConfig config = configs[configId];
                if (!useHandlers[config.useKind].TryUse(new ItemUseRequest(config, quantity, targetId), out reason))
                    return false;
                RemoveStack(configId, quantity);
            }
            finally { executingUse = false; }
            Changed?.Invoke();
            return true;
        }

        public List<InventoryEntry> GetEntries(ItemCategory? category = null, EquipmentKind? kind = null, InventorySort sort = InventorySort.Rarity)
        {
            var entries = new List<InventoryEntry>();
            foreach (KeyValuePair<string, int> stack in stacks)
            {
                ItemConfig config = configs[stack.Key];
                if ((!category.HasValue || config.category == category.Value) && !kind.HasValue)
                    entries.Add(new InventoryEntry(config, stack.Value));
            }
            foreach (EquipmentInstance instance in equipment.Values)
            {
                var config = (EquipmentConfig)configs[instance.configId];
                if ((!category.HasValue || config.category == category.Value) && (!kind.HasValue || config.equipmentKind == kind.Value))
                    entries.Add(new InventoryEntry(config, 1, Copy(instance)));
            }
            entries.Sort((left, right) =>
            {
                int comparison = sort == InventorySort.Quantity ? right.Quantity.CompareTo(left.Quantity)
                    : sort == InventorySort.Rarity ? right.Config.rarity.CompareTo(left.Config.rarity) : 0;
                if (comparison == 0) comparison = StringComparer.Ordinal.Compare(left.Config.displayName, right.Config.displayName);
                return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(left.Key, right.Key);
            });
            return entries;
        }

        public InventoryData CreateSnapshot()
        {
            var data = new InventoryData();
            foreach (KeyValuePair<string, int> stack in stacks)
                data.stacks.Add(new ItemStack { configId = stack.Key, quantity = stack.Value });
            foreach (EquipmentInstance instance in equipment.Values) data.equipment.Add(Copy(instance));
            return data;
        }

        public bool TryRestore(InventoryData data, out string reason)
        {
            reason = null;
            if (!CanMutate(out reason)) return false;
            if (data == null || data.stacks == null || data.equipment == null)
            {
                reason = "背包存档数据不完整。";
                return false;
            }
            var restoredStacks = new Dictionary<string, int>(StringComparer.Ordinal);
            var restoredEquipment = new Dictionary<string, EquipmentInstance>(StringComparer.Ordinal);
            foreach (ItemStack stack in data.stacks)
            {
                if (stack == null || !TryGetConfig(stack.configId, out var config) || config.IsEquipment
                    || stack.quantity <= 0 || !restoredStacks.TryAdd(stack.configId, stack.quantity))
                {
                    reason = "背包存档含有无效或重复的堆叠物品。";
                    return false;
                }
            }
            foreach (EquipmentInstance instance in data.equipment)
            {
                if (instance == null || string.IsNullOrWhiteSpace(instance.instanceId)
                    || !TryGetConfig(instance.configId, out var config) || !(config is EquipmentConfig equipmentConfig)
                    || instance.level < 1 || instance.level > equipmentConfig.maxLevel || instance.experience < 0
                    || instance.ascension < 0 || !restoredEquipment.TryAdd(instance.instanceId, Copy(instance)))
                {
                    reason = "背包存档含有无效或重复的装备实例。";
                    return false;
                }
            }
            stacks.Clear();
            equipment.Clear();
            foreach (KeyValuePair<string, int> stack in restoredStacks) stacks.Add(stack.Key, stack.Value);
            foreach (KeyValuePair<string, EquipmentInstance> instance in restoredEquipment) equipment.Add(instance.Key, instance.Value);
            Changed?.Invoke();
            return true;
        }

        private bool TryAggregate(IEnumerable<ItemAmount> items, out Dictionary<string, int> amounts, out string reason)
        {
            amounts = new Dictionary<string, int>(StringComparer.Ordinal);
            reason = null;
            if (items == null)
            {
                reason = "物品清单不能为空。";
                return false;
            }
            foreach (ItemAmount item in items)
            {
                if (item == null || item.quantity <= 0 || !TryGetConfig(item.configId, out _))
                {
                    reason = "物品配置不存在或数量无效。";
                    return false;
                }
                amounts.TryGetValue(item.configId, out int current);
                if (current > int.MaxValue - item.quantity)
                {
                    reason = "物品数量超出上限。";
                    return false;
                }
                amounts[item.configId] = current + item.quantity;
            }
            if (amounts.Count > 0) return true;
            reason = "物品清单不能为空。";
            return false;
        }

        private bool CanMutate(out string reason)
        {
            reason = executingUse ? "物品使用过程中不能再次修改背包。" : null;
            return !executingUse;
        }

        private void RemoveStack(string configId, int quantity)
        {
            int remaining = stacks[configId] - quantity;
            if (remaining == 0) stacks.Remove(configId);
            else stacks[configId] = remaining;
        }

        private static EquipmentInstance Copy(EquipmentInstance instance)
        {
            return new EquipmentInstance
            {
                instanceId = instance.instanceId, configId = instance.configId, level = instance.level,
                experience = instance.experience, ascension = instance.ascension, isLocked = instance.isLocked
            };
        }
    }
}
