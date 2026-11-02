using System;
using System.Linq;
using Game.Inventory;
using TapTap21.SaveSystem.dyh;
using UnityEngine;

namespace Game.Characters
{
    /// <summary>玩家局外数据的全局入口；与 UI、配队场景的生命周期无关。</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class PlayerRuntime : MonoBehaviour
    {
        public static PlayerRuntime Instance { get; private set; }
        public PlayerRuntimeConfig Config { get; private set; }
        public InventoryService Inventory { get; private set; }
        public CharacterProgressionService Characters { get; private set; }
        public bool IsReady { get; private set; }
        public string LastMessage { get; private set; }

        private PlayerProfileStore store;
        private bool dirty;
        private float retryAt;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime()
        {
            Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void EnsureCreated()
        {
            if (Instance == null)
                new GameObject("PlayerRuntime").AddComponent<PlayerRuntime>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Config = Resources.Load<PlayerRuntimeConfig>("Bootstrap/PlayerRuntimeConfig");
            try
            {
                if (Config == null || Config.units == null || Config.items == null)
                    throw new InvalidOperationException("缺少玩家运行时配置或角色/物品目录。");
                Inventory = new InventoryService(Config.items);
                store = new PlayerProfileStore(Config.saveSlot);
                var loaded = store.Load();
                PlayerProfileData profile;
                bool migrated = false;
                if (loaded.Success)
                {
                    profile = loaded.Data;
                    if (profile == null || profile.formatVersion != 1 || profile.roster == null || profile.inventory == null)
                        throw new InvalidOperationException("玩家进度文件格式无效，原文件不会被覆盖。");
                }
                else if (loaded.Status == SaveStatus.NotFound)
                {
                    profile = new PlayerProfileData();
                    var legacy = SaveSystem.Load(InventorySaveDefinitions.Inventory, Config.saveSlot);
                    if (legacy.Success)
                    {
                        profile = PlayerProfileMigration.ImportLegacy(legacy.Data, Config);
                    }
                    else if (legacy.Status == SaveStatus.NotFound)
                    {
                        var starting = new InventoryService(Config.items, false);
                        if (Config.startingItems.Count > 0 && !starting.TryAddRewards(Config.startingItems, out string error))
                            throw new InvalidOperationException(error);
                        profile.inventory = starting.CreateSnapshot();
                    }
                    else
                        throw new InvalidOperationException("旧背包读取失败：" + legacy.Message);
                    PlayerProfileMigration.AddInitialCharacters(profile, Config);
                    migrated = true;
                }
                else
                    throw new InvalidOperationException("玩家进度读取失败：" + loaded.Message);
                if (!Inventory.TryRestore(profile.inventory, out string reason, false))
                    throw new InvalidOperationException(reason);
                Characters = new CharacterProgressionService(Config.units, Config.items, Inventory, profile.roster, store);
                if (migrated && !store.TrySave(profile, out reason))
                    throw new InvalidOperationException(reason);
                IsReady = true;
                if (loaded.UsedBackup)
                {
                    Characters.CanMutate = false;
                    Inventory.IsReadOnly = true;
                    Debug.LogWarning("玩家进度从成组备份读取，目前只读，损坏的原文件不会被覆盖。", this);
                }

                LastMessage = loaded.UsedBackup ? "玩家进度已成组从备份恢复。" : migrated ? "玩家数据初始化/迁移完成。" : "玩家数据已读取。";
                Inventory.Changed += OnInventoryChanged;
                gameObject.AddComponent<InventoryManager>();
                gameObject.AddComponent<CharacterNavigationBridge>();
            }
            catch (Exception exception)
            {
                IsReady = false;
                if (Characters != null)
                    Characters.CanMutate = false;
                if (Inventory != null)
                    Inventory.IsReadOnly = true;
                LastMessage = exception.Message;
                Debug.LogError("玩家数据初始化失败；已禁止覆盖存档：" + LastMessage, this);
            }
        }

        private void OnInventoryChanged()
        {
            dirty = true;
        }

        public bool Save(out string reason)
        {
            reason = LastMessage;
            if (!IsReady || Characters == null || store == null || Inventory.IsReadOnly)
                return false;
            if (!store.TrySave(new PlayerProfileData { roster = Characters.CreateSnapshot(), inventory = Inventory.CreateSnapshot() }, out reason))
            {
                LastMessage = "保存失败：" + reason;
                retryAt = Time.unscaledTime + 5f;
                return false;
            }

            dirty = false;
            LastMessage = "玩家进度已保存。";
            return true;
        }

        private void LateUpdate()
        {
            if (dirty && IsReady && Time.unscaledTime >= retryAt)
                Save(out _);
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause && dirty)
                Save(out _);
        }

        private void OnApplicationQuit()
        {
            if (dirty)
                Save(out _);
        }

        private void OnDestroy()
        {
            if (Inventory != null)
                Inventory.Changed -= OnInventoryChanged;
            if (Instance == this)
                Instance = null;
        }

        public bool TryUseGrowthItem(string material, int quantity, string target, out string reason)
        {
            reason = "玩家数据尚未初始化。";
            if (!IsReady || !Inventory.TryGetConfig(material, out ItemConfig config))
                return false;
            bool equipment = config.useKind == ItemUseKind.EquipmentExperience || config.useKind == ItemUseKind.EquipmentAscension;
            string id = equipment ? Characters.EquipmentOwner(target) ?? Characters.OwnedCharacters.FirstOrDefault()?.id : target;
            var request = new CharacterRequest
            {
                unitId = id,
                equipmentId = equipment ? target : null,
                materialId = material,
                quantity = quantity
            };
            switch (config.useKind)
            {
                case ItemUseKind.CharacterExperience:
                    request.operation = CharacterOperation.Level;
                    break;
                case ItemUseKind.CharacterAscension:
                    request.operation = CharacterOperation.Ascend;
                    break;
                case ItemUseKind.EquipmentExperience:
                    request.operation = CharacterOperation.EquipmentLevel;
                    break;
                case ItemUseKind.EquipmentAscension:
                    request.operation = CharacterOperation.EquipmentAscend;
                    break;
                default:
                    reason = "不是养成材料。";
                    return false;
            }

            CharacterPreview preview = Characters.Preview(request);
            if (!preview.Success)
            {
                reason = preview.Message;
                return false;
            }

            return Characters.TryCommit(preview, out reason);
        }
    }
}
