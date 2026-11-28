using System;
using System.Linq;
using System.Collections.Generic;
using Battle.SaveData;
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
        public BattleProgressData BattleProgress { get; private set; }
        public bool IsReady { get; private set; }
        public string LastMessage { get; private set; }

        public PlayerProfileCoordinator Profiles { get; private set; }
        private bool HasUnsavedChanges => IsReady && !Inventory.IsReadOnly
            && Profiles.HasChanges(Characters.Revision, Inventory.Revision,
                BattleProgress?.revision ?? 0);
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
                var store = new PlayerProfileStore(Config.saveSlot);
                var loaded = store.Load();
                PlayerProfileData profile;
                bool created = false;
                if (loaded.Success)
                {
                    profile = loaded.Data;
                    if (profile == null || profile.formatVersion != 3 || profile.roster == null
                        || profile.inventory == null || profile.battle == null)
                        throw new InvalidOperationException("玩家进度文件格式无效，原文件不会被覆盖。");
                }
                else if (loaded.Status == SaveStatus.NotFound)
                {
                    profile = PlayerProfileFactory.Create(Config);
                    created = true;
                }
                else
                    throw new InvalidOperationException("玩家进度读取失败：" + loaded.Message);
                if (!Inventory.TryRestore(profile.inventory, out string reason, false))
                    throw new InvalidOperationException(reason);
                Profiles = new PlayerProfileCoordinator(store, profile, Inventory.Revision, !created, loaded.UsedBackup);
                BattleProgress = profile.battle.Copy();
                Characters = new CharacterProgressionService(Config.units, Config.items, Inventory, profile.roster, Profiles);
                if (created && !Profiles.TrySave(profile.roster, profile.inventory, Inventory.Revision, out reason))
                    throw new InvalidOperationException(reason);
                IsReady = true;
                if (loaded.UsedBackup)
                {
                    Characters.CanMutate = false;
                    Inventory.IsReadOnly = true;
                    Debug.LogWarning("玩家进度从成组备份读取，目前只读，损坏的原文件不会被覆盖。", this);
                }

                if (loaded.UsedBackup)
                    LastMessage = "玩家进度已成组从备份恢复。";
                else
                    LastMessage = created ? "玩家初始数据已创建。" : "玩家数据已读取。";
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

        public bool Save(out string reason)
        {
            reason = LastMessage;
            if (!IsReady || Characters == null || Profiles == null || Inventory.IsReadOnly)
                return false;
            if (!Profiles.HasChanges(Characters.Revision, Inventory.Revision,
                BattleProgress?.revision ?? 0))
            {
                reason = null;
                return true;
            }
            if (!Profiles.TrySave(Characters.CreateSnapshot(), Inventory.CreateSnapshot(),
                Inventory.Revision, BattleProgress ?? new BattleProgressData(),
                BattleProgress?.revision ?? 0, out reason))
            {
                LastMessage = "保存失败：" + reason;
                retryAt = Time.unscaledTime + 5f;
                return false;
            }

            retryAt = 0f;
            LastMessage = "玩家进度已保存。";
            return true;
        }

        public BattleFormation GetFormation()
        {
            return BattleProgress?.formation != null
                ? CopyFormation(BattleProgress.formation)
                : new BattleFormation();
        }

        public bool TrySetFormation(BattleFormation formation, out string reason)
        {
            reason = "玩家数据尚未初始化。";
            if (!IsReady || Inventory.IsReadOnly || formation == null)
                return false;

            BattleFormation verified;
            try { verified = CopyFormation(formation); }
            catch (Exception exception) { reason = exception.Message; return false; }
            foreach (FormationMember member in verified.members)
            {
                if (!Characters.Owns(member.unitId))
                {
                    reason = "配队包含未拥有角色：" + member.unitId;
                    return false;
                }
            }

            BattleProgressData candidate = (BattleProgress ?? new BattleProgressData()).Copy();
            candidate.formation = verified;
            candidate.revision++;
            if (!Profiles.TrySave(Characters.CreateSnapshot(), Inventory.CreateSnapshot(),
                Inventory.Revision, candidate, candidate.revision, out reason))
                return false;
            BattleProgress = candidate;
            return true;
        }

        public bool TryGrantBattleRewards(string encounterId, int turnCount,
            IEnumerable<ItemAmount> rewards, out BattleRewardReceipt receipt, out string reason)
        {
            receipt = new BattleRewardReceipt();
            reason = "玩家数据尚未初始化。";
            if (!IsReady || Inventory.IsReadOnly || string.IsNullOrWhiteSpace(encounterId))
                return false;

            var normalized = new List<ItemAmount>();
            if (rewards != null)
                foreach (ItemAmount reward in rewards)
                    if (reward != null)
                        normalized.Add(new ItemAmount(reward.configId, reward.quantity));

            InventoryData nextInventory = Inventory.CreateSnapshot();
            long nextInventoryRevision = Inventory.Revision;
            if (normalized.Count > 0)
            {
                var stage = new InventoryService(Config.items, false);
                if (!stage.TryRestore(nextInventory, out reason, false)
                    || !stage.TryAddRewards(normalized, out reason))
                    return false;
                nextInventory = stage.CreateSnapshot();
                nextInventoryRevision++;
            }

            BattleProgressData candidate = (BattleProgress ?? new BattleProgressData()).Copy();
            BattleCompletionRecord record = candidate.Find(encounterId);
            if (record == null)
            {
                record = new BattleCompletionRecord { encounterId = encounterId };
                candidate.completions.Add(record);
            }
            record.clearCount++;
            if (turnCount > 0 && (record.bestTurnCount <= 0 || turnCount < record.bestTurnCount))
                record.bestTurnCount = turnCount;
            record.lastClearedAtUtc = DateTime.UtcNow.ToString("O");
            record.lastRewards = normalized.Select(item =>
                new ItemAmount(item.configId, item.quantity)).ToList();
            candidate.revision++;

            if (!Profiles.TrySave(Characters.CreateSnapshot(), nextInventory,
                nextInventoryRevision, candidate, candidate.revision, out reason))
                return false;
            if (normalized.Count > 0
                && !Inventory.TryCommitSnapshot(nextInventory, nextInventoryRevision, out reason))
                return false;
            BattleProgress = candidate;

            foreach (ItemAmount reward in normalized)
            {
                Inventory.TryGetConfig(reward.configId, out ItemConfig config);
                receipt.Add(new BattleRewardLine(reward.configId,
                    config != null ? config.displayName : reward.configId, reward.quantity));
            }
            reason = null;
            return true;
        }

        private static BattleFormation CopyFormation(BattleFormation source)
        {
            var copy = new BattleFormation();
            if (source?.members == null)
                return copy;
            foreach (FormationMember member in source.members)
                if (member == null || !copy.TryAdd(member.unitId, member.position))
                    throw new ArgumentException("配队数据无效。", nameof(source));
            return copy;
        }

        private void LateUpdate()
        {
            if (HasUnsavedChanges && Time.unscaledTime >= retryAt)
                Save(out _);
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause && HasUnsavedChanges)
                Save(out _);
        }

        private void OnApplicationQuit()
        {
            if (HasUnsavedChanges)
                Save(out _);
        }

        private void OnDestroy()
        {
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
