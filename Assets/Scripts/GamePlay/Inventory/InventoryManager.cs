using System;
using System.Collections.Generic;
using TapTap21.SaveSystem.dyh;
using UnityEngine;

namespace Game.Inventory
{
    [DefaultExecutionOrder(-100)]
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }
        //物品目录
        [SerializeField] private ItemCatalog catalog;
        //永久跨场景
        [SerializeField] private bool persistAcrossScenes = true;
        //存储存档，直接默认slot_0
        //SaveSystem中额外提供了不同存档说法，所以使用需要注意
        [SerializeField] private string saveSlot = "slot_0";
        //物品栏存储，每一个ItemAmount存储着物品id与数量
        [SerializeField] private List<ItemAmount> startingItems = new List<ItemAmount>();

        //dirty 脏，这里是别的意思，被修改过
        private bool dirty;
        private bool canSave;
        private float nextSaveTime;
        public InventoryService Service { get; private set; }
        public string LastMessage { get; private set; }
        public event Action<ItemUseRequest> UseRequested;
        public event Action<string> EquipRequested;
        public event Action<string> ProgressionRequested;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (persistAcrossScenes) DontDestroyOnLoad(gameObject);
            try
            {
                Service = new InventoryService(catalog);
                LoadInventory();
                Service.Changed += HandleChanged;
            }
            catch (Exception exception)
            {
                LastMessage = exception.Message;
                Debug.LogError("背包初始化失败：" + exception.Message, this);
            }
        }

        public bool LoadInventory()
        {
            if (Service == null) return false;
            canSave = false;
            dirty = false;
            if (!SaveSlotId.TryFrom(saveSlot, out var slot)) return Fail("背包存档槽位无效。");
            var loaded = SaveSystem.Load(InventorySaveDefinitions.Inventory, slot);
            if (loaded.Success)
            {
                if (!Service.TryRestore(loaded.Data, out string reason)) return Fail(reason);
                dirty = false;
                canSave = true;
                LastMessage = loaded.UsedBackup ? "背包已从备份恢复。" : "背包已读取。";
                return true;
            }
            if (loaded.Status != SaveStatus.NotFound) return Fail("背包读取失败：" + loaded.Message);
            if (!Service.TryRestore(new InventoryData(), out string restoreError)) return Fail(restoreError);
            if (startingItems != null && startingItems.Count > 0 && !Service.TryAddRewards(startingItems, out string error))
                return Fail(error);
            canSave = true;
            dirty = true;
            return SaveInventory();
        }

        public bool SaveInventory()
        {
            if (Service == null || !canSave) return Fail("背包未成功读取，暂不覆盖存档。");
            var saved = SaveSystem.Save(InventorySaveDefinitions.Inventory, saveSlot, Service.CreateSnapshot());
            if (!saved.Success)
            {
                nextSaveTime = Time.unscaledTime + 5f;
                return Fail("背包保存失败：" + saved.Message);
            }
            dirty = false;
            LastMessage = "背包已保存。";
            return true;
        }

        public bool TryUse(string configId, int quantity, string targetId, out string reason)
        {
            reason = "背包尚未初始化。";
            if (Service == null) return false;
            bool success = Service.TryUse(configId, quantity, targetId, out reason);
            if (success) SaveInventory();
            return success;
        }

        public bool RequestUse(string configId, int quantity, out string reason)
        {
            reason = "对应的使用界面尚未接入。";
            if (Service == null || !Service.TryGetConfig(configId, out var config)) return false;
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
            if (UseRequested == null) return false;
            UseRequested.Invoke(new ItemUseRequest(config, quantity, null));
            reason = null;
            return true;
        }

        public bool RequestEquip(string instanceId)
        {
            if (Service == null || !Service.TryGetEquipment(instanceId, out _) || EquipRequested == null) return false;
            EquipRequested.Invoke(instanceId);
            return true;
        }

        public bool RequestProgression(string itemKey)
        {
            if (Service == null || ProgressionRequested == null) return false;
            bool isEquipment = Service.TryGetEquipment(itemKey, out _);
            bool isMaterial = Service.TryGetConfig(itemKey, out var config)
                && config.category == ItemCategory.Material && Service.GetQuantity(itemKey) > 0;
            if (!isEquipment && !isMaterial) return false;
            ProgressionRequested.Invoke(itemKey);
            return true;
        }

        private void HandleChanged() { dirty = true; }
        private void LateUpdate() { if (dirty && canSave && Time.unscaledTime >= nextSaveTime) SaveInventory(); }
        private void OnApplicationPause(bool paused) { if (paused && dirty && canSave) SaveInventory(); }
        private void OnApplicationQuit() { if (dirty && canSave) SaveInventory(); }

        private bool Fail(string message)
        {
            LastMessage = message;
            Debug.LogWarning(message, this);
            return false;
        }

        private void OnDestroy()
        {
            if (Service != null) Service.Changed -= HandleChanged;
            if (Instance == this) Instance = null;
        }
    }
}
