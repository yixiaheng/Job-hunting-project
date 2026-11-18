using System;
using Game.Characters;
using UnityEngine;

namespace Game.Inventory
{
    /// <summary>背包业务门面：绑定统一玩家运行时并转发使用、装备和养成意图。</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }
        public InventoryService Service { get; private set; }
        public string LastMessage { get; private set; }

        public event Action<ItemUseRequest> UseRequested;
        public event Action<string> EquipRequested;
        public event Action<string> ProgressionRequested;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime() => Instance = null;
        private void Awake()
        {
            PlayerRuntime.EnsureCreated();
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            Service = PlayerRuntime.Instance.Inventory;
            LastMessage = PlayerRuntime.Instance.LastMessage;
        }

        public bool SaveInventory()
        {
            var runtime = PlayerRuntime.Instance;
            if (runtime == null)
            {
                LastMessage = "玩家数据尚未初始化。";
                return false;
            }

            bool saved = runtime.Save(out string reason);
            LastMessage = saved ? "玩家档案已保存。" : reason;
            return saved;
        }

        public bool TryUse(string configId, int quantity, string targetId, out string reason)
        {
            if (PlayerRuntime.Instance != null && Service != null && Service.TryGetConfig(configId, out var item) && (item.useKind == ItemUseKind.CharacterExperience || item.useKind == ItemUseKind.CharacterAscension || item.useKind == ItemUseKind.EquipmentExperience || item.useKind == ItemUseKind.EquipmentAscension))
                return PlayerRuntime.Instance.TryUseGrowthItem(configId, quantity, targetId, out reason);
            reason = "背包尚未初始化。";
            if (Service == null)
                return false;
            bool success = Service.TryUse(configId, quantity, targetId, out reason);
            if (success)
                SaveInventory();
            return success;
        }

        public bool RequestUse(string configId, int quantity, out string reason)
        {
            reason = "对应的使用界面尚未接入。";
            if (Service == null || !Service.TryGetConfig(configId, out var config))
                return false;
            if (config.IsEquipment || config.useKind == ItemUseKind.None)
            {
                reason = "该物品不能直接使用。";
                return false;
            }

            if (quantity <= 0 || quantity > Service.GetQuantity(configId))
            {
                reason = "使用数量无效。";
                return false;
            }

            if (config.useKind == ItemUseKind.CharacterExperience || config.useKind == ItemUseKind.CharacterAscension || config.useKind == ItemUseKind.EquipmentExperience || config.useKind == ItemUseKind.EquipmentAscension)
            {
                bool opened = RequestProgression(configId);
                reason = opened ? null : "当前无法打开养成界面。";
                return opened;
            }

            if (UseRequested == null)
                return false;
            UseRequested.Invoke(new ItemUseRequest(config, quantity, null));
            reason = null;
            return true;
        }

        public bool RequestEquip(string instanceId)
        {
            if (Service == null || !Service.TryGetEquipment(instanceId, out _) || EquipRequested == null)
                return false;
            EquipRequested.Invoke(instanceId);
            return true;
        }

        public bool RequestProgression(string itemKey)
        {
            if (Service == null || ProgressionRequested == null)
                return false;
            bool isEquipment = Service.TryGetEquipment(itemKey, out _);
            bool isMaterial = Service.TryGetConfig(itemKey, out var config) && config.category == ItemCategory.Material && Service.GetQuantity(itemKey) > 0;
            if (!isEquipment && !isMaterial)
                return false;
            ProgressionRequested.Invoke(itemKey);
            return true;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
