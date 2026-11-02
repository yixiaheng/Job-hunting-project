using System;
using System.Collections.Generic;
using System.Linq;
using Game.Inventory;
using UnityEngine;

namespace Game.Characters
{
    /// <summary>全部培养指令先在副本预演，保存成功后才一次性发布新状态。</summary>
    public sealed class CharacterProgressionService : IEquipmentUsageProvider
    {
        private readonly UnitCatalog catalog;
        private readonly ItemCatalog items;
        private readonly IPlayerProfileStore store;
        private CharacterRosterData roster;
        private bool committing;
        public InventoryService Inventory { get; }
        public long Revision => roster.revision;
        public bool CanMutate { get; set; } = true;
        public IReadOnlyList<UnitConfig> OwnedCharacters => catalog.Units.Where(c => c != null && Find(c.id) != null).ToArray();

        public CharacterProgressionService(UnitCatalog catalog, ItemCatalog items, InventoryService inventory, CharacterRosterData roster, IPlayerProfileStore store)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.items = items ?? throw new ArgumentNullException(nameof(items));
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            if (!ValidateRoster(roster, inventory, out string reason))
                throw new ArgumentException(reason);
            this.roster = roster.Copy();
            Inventory.EquipmentUsage = this;
        }

        private CharacterProgress Find(string id) => roster.characters.Find(c => c.unitId == id);
        public bool Owns(string id) => Find(id) != null;
        public UnitConfig Config(string id) => catalog.FindById(id);
        public CharacterProgress GetCharacter(string id) => Find(id)?.Copy();
        public CharacterRosterData CreateSnapshot() => roster.Copy();
        public CharacterStats Stats(string id) => Find(id)is CharacterProgress progress ? CharacterStatCalculator.Calculate(Config(id), progress, Inventory, out _) : default;
        public string EquipmentOwner(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                return null;
            return roster.characters.Find(c => Array.IndexOf(c.equipment, instanceId) >= 0)?.unitId;
        }

        public bool IsEquipped(string instanceId) => EquipmentOwner(instanceId) != null;
        public string GetOwnerName(string instanceId) => Config(EquipmentOwner(instanceId))?.displayName;
        public CharacterPreview Preview(CharacterRequest request)
        {
            var result = new CharacterPreview();
            if (!CanMutate || committing)
                return Fail(result, "角色数据尚不可修改。");
            if (!Inventory.CanMutate(out string inventoryReason))
                return Fail(result, inventoryReason);
            if (request == null || !Enum.IsDefined(typeof(CharacterOperation), request.operation))
                return Fail(result, "培养请求无效。");
            result.request = request.Copy();
            result.revision = Revision;
            result.inventoryRevision = Inventory.Revision;
            try
            {
                result.roster = roster.Copy();
                var stage = new InventoryService(items, false);
                if (!stage.TryRestore(Inventory.CreateSnapshot(), out string reason, false))
                    return Fail(result, reason);
                CharacterProgress progress = result.roster.characters.Find(c => c.unitId == request.unitId);
                UnitConfig config = Config(request.unitId);
                if (config == null || config.growth == null)
                    return Fail(result, "角色或成长配置不存在。");
                if (progress == null && request.operation != CharacterOperation.Acquire)
                    return Fail(result, "尚未拥有该角色。");
                if (progress != null)
                    result.Before = CharacterStatCalculator.Calculate(config, progress, stage, out _);
                result.affected.Add(config.id);
                bool success;
                switch (request.operation)
                {
                    case CharacterOperation.Level:
                        success = Level(result, stage, progress, config.growth, request);
                        break;
                    case CharacterOperation.Ascend:
                        success = Ascend(result, stage, progress, config.growth);
                        break;
                    case CharacterOperation.Skill:
                        success = Skill(result, stage, progress, config, request.skill);
                        break;
                    case CharacterOperation.Rank:
                        success = Rank(result, stage, progress, config.growth);
                        break;
                    case CharacterOperation.Equip:
                        success = Equip(result, stage, progress, config, request);
                        break;
                    case CharacterOperation.Unequip:
                        success = Enum.IsDefined(typeof(EquipmentSlot), request.slot) && !string.IsNullOrEmpty(progress.equipment[(int)request.slot]);
                        if (success)
                        {
                            progress.equipment[(int)request.slot] = null;
                            result.Description = "卸下装备";
                        }
                        else
                            result.Message = "该槽位没有装备。";
                        break;
                    case CharacterOperation.EquipmentLevel:
                    case CharacterOperation.EquipmentAscend:
                    case CharacterOperation.Merge:
                        success = Equipment(result, stage, request);
                        break;
                    case CharacterOperation.Acquire:
                        if (progress == null)
                        {
                            progress = new CharacterProgress
                            {
                                unitId = config.id
                            };
                            result.roster.characters.Add(progress);
                            result.Description = "获得角色：" + config.displayName;
                            success = true;
                        }
                        else
                        {
                            success = stage.TryAdd(config.growth.shardItem, config.growth.duplicateShards, out reason);
                            result.Message = reason;
                            result.Description = "重复角色转换为专属碎片";
                        }

                        break;
                    default:
                        success = false;
                        break;
                }

                if (!success)
                    return Fail(result, result.Message ?? "无法执行这项操作。");
                result.inventory = stage.CreateSnapshot();
                if (!ValidateRoster(result.roster, stage, out reason))
                    return Fail(result, reason);
                result.After = CharacterStatCalculator.Calculate(config, progress, stage, out _);
                result.Success = true;
                return result;
            }
            catch (Exception exception)when (exception is ArgumentException || exception is OverflowException || exception is IndexOutOfRangeException)
            {
                return Fail(result, "培养配置或数量无效：" + exception.Message);
            }
        }

        public bool TryCommit(CharacterPreview preview, out string reason)
        {
            reason = null;
            if (preview == null || !preview.Success)
            {
                reason = preview?.Message ?? "请先预览。";
                return false;
            }

            if (preview.revision != Revision || preview.inventoryRevision != Inventory.Revision)
            {
                reason = "角色或背包已变化，请重新预览并确认。";
                return false;
            }

            CharacterPreview verified = Preview(preview.request);
            if (!verified.Success)
            {
                reason = verified.Message;
                return false;
            }

            committing = true;
            try
            {
                verified.roster.revision = checked(Revision + 1);
                var profile = new PlayerProfileData
                {
                    roster = verified.roster.Copy(),
                    inventory = verified.inventory
                };
                try
                {
                    if (!store.TrySave(profile, out reason))
                        return false;
                }
                catch (Exception exception)
                {
                    reason = "保存失败：" + exception.Message;
                    return false;
                }

                roster = verified.roster.Copy();
                if (!Inventory.TryRestore(verified.inventory, out reason, false))
                    throw new InvalidOperationException(reason);
                Inventory.PublishCommitted();
                Game.GameEvents.Character.Publish(new CharacterChangedEvent(verified.request.unitId, verified.request.operation, Revision, verified.affected.Distinct().ToArray()));
                return true;
            }
            finally
            {
                committing = false;
            }
        }

        public bool ValidateRoster(CharacterRosterData data, InventoryService inventory, out string reason)
        {
            reason = null;
            if (data?.characters == null || data.revision < 0)
            {
                reason = "角色存档不完整。";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var equipped = new HashSet<string>(StringComparer.Ordinal);
            foreach (CharacterProgress progress in data.characters)
            {
                UnitConfig config = progress == null ? null : catalog.FindById(progress.unitId);
                CharacterGrowthConfig growth = config?.growth;
                if (growth == null || !ids.Add(progress.unitId) || progress.ascension < 0 || progress.ascension >= growth.levelCaps.Length || progress.level < 1 || progress.level > growth.levelCaps[progress.ascension] || progress.experience < 0 || progress.rank < 0 || progress.rank > growth.shardCosts.Length || progress.equipment?.Length != 7 || progress.basicLevel < 1 || progress.skillLevel < 1 || progress.ultimateLevel < 1 || progress.basicLevel > growth.skillCaps[progress.ascension] || progress.skillLevel > growth.skillCaps[progress.ascension] || progress.ultimateLevel > growth.skillCaps[progress.ascension] || progress.experience >= growth.NextExperience(progress.level) || progress.level == growth.levelCaps[progress.ascension] && progress.experience != 0)
                {
                    reason = "角色存档含无效、重复或超出上限的进度。";
                    return false;
                }

                for (int slot = 0; slot < progress.equipment.Length; slot++)
                {
                    string id = progress.equipment[slot];
                    if (string.IsNullOrEmpty(id))
                        continue;
                    if (!equipped.Add(id) || !inventory.TryGetEquipment(id, out EquipmentInstance instance) || !inventory.TryGetConfig(instance.configId, out ItemConfig item) || !(item is EquipmentConfig equipment) || !Matches(config, equipment, (EquipmentSlot)slot))
                    {
                        reason = "角色装备缺失、重复穿戴或不符合命途/部位限制。";
                        return false;
                    }
                }
            }

            return true;
        }

        public static bool Matches(UnitConfig unit, EquipmentConfig equipment, EquipmentSlot slot) => slot == EquipmentSlot.LightCone ? equipment.equipmentKind == EquipmentKind.LightCone && equipment.path == unit.path : equipment.equipmentKind == EquipmentKind.Instrument && equipment.slot == slot;
        private static CharacterPreview Fail(CharacterPreview preview, string reason)
        {
            preview.Success = false;
            preview.Message = reason;
            return preview;
        }

        private static bool Consume(CharacterPreview preview, InventoryService stage, string id, int count)
        {
            if (!stage.TryRemove(id, count, out string reason))
            {
                preview.Message = reason;
                return false;
            }

            preview.costs.Add(new ItemAmount(id, count));
            return true;
        }

        private static bool Level(CharacterPreview preview, InventoryService stage, CharacterProgress progress, CharacterGrowthConfig growth, CharacterRequest request)
        {
            int cap = growth.levelCaps[progress.ascension];
            string material = string.IsNullOrEmpty(request.materialId) ? growth.characterExperience : request.materialId;
            if (!Experience(preview, stage, material, request.quantity, ItemUseKind.CharacterExperience, progress.level, progress.experience, cap, growth.NextExperience, out int level, out int exp))
                return false;
            preview.Description = $"角色等级 {progress.level} → {level}，经验 {progress.experience} → {exp}";
            progress.level = level;
            progress.experience = exp;
            return true;
        }

        private static bool Experience(CharacterPreview preview, InventoryService stage, string material, int requested, ItemUseKind kind, int currentLevel, int currentExperience, int cap, Func<int, int> next, out int level, out int experience)
        {
            level = currentLevel;
            experience = currentExperience;
            if (level >= cap)
            {
                preview.Message = "已到当前等级上限，请突破或选择其他目标。";
                return false;
            }

            if (requested <= 0 || !stage.TryGetConfig(material, out ItemConfig config) || config.IsEquipment || config.useKind != kind)
            {
                preview.Message = "经验材料或数量无效。";
                return false;
            }

            int value = Mathf.RoundToInt(config.effectValue);
            if (value <= 0)
            {
                preview.Message = "经验材料的经验值必须大于零。";
                return false;
            }

            long required = -currentExperience;
            for (int i = currentLevel; i < cap; i++)
                required = checked(required + next(i));
            int count = (int)Math.Min(requested, (required + value - 1) / value);
            if (!Consume(preview, stage, material, count))
                return false;
            long remaining = checked((long)count * value + experience);
            while (level < cap && remaining >= next(level))
            {
                remaining -= next(level);
                level++;
            }

            preview.WastedExperience = level == cap ? checked((int)remaining) : 0;
            experience = level == cap ? 0 : checked((int)remaining);
            return true;
        }

        private static bool Ascend(CharacterPreview preview, InventoryService stage, CharacterProgress progress, CharacterGrowthConfig growth)
        {
            if (progress.ascension >= growth.levelCaps.Length - 1 || progress.level != growth.levelCaps[progress.ascension])
            {
                preview.Message = "需要达到当前等级上限，且仍有下一突破阶段。";
                return false;
            }

            if (!Consume(preview, stage, growth.characterAscension, growth.ascensionCosts[progress.ascension]))
                return false;
            progress.ascension++;
            preview.Description = "突破后等级上限：" + growth.levelCaps[progress.ascension];
            return true;
        }

        private static bool Skill(CharacterPreview preview, InventoryService stage, CharacterProgress progress, UnitConfig config, SkillKind kind)
        {
            var growth = config.growth;
            if (!Enum.IsDefined(typeof(SkillKind), kind))
            {
                preview.Message = "技能类型无效。";
                return false;
            }

            int before = progress.SkillLevel(kind);
            var skill = config.skills.Find(value => value != null && value.kind == kind);
            string beforeEffects = CharacterSkillCalculator.Describe(growth, progress, skill);
            if (before >= growth.skillCaps[progress.ascension])
            {
                preview.Message = "技能已达到当前突破阶段上限。";
                return false;
            }

            if (!Consume(preview, stage, growth.skillMaterial, checked(growth.skillCostPerLevel * (before + 1))))
                return false;
            if (kind == SkillKind.Basic)
                progress.basicLevel++;
            else if (kind == SkillKind.Skill)
                progress.skillLevel++;
            else
                progress.ultimateLevel++;
            preview.Description = $"技能等级 {before} → {before + 1}";
            preview.Description += "\n升级前\n" + beforeEffects + "\n升级后\n" + CharacterSkillCalculator.Describe(growth, progress, skill);
            return true;
        }

        private static bool Rank(CharacterPreview preview, InventoryService stage, CharacterProgress progress, CharacterGrowthConfig growth)
        {
            if (progress.rank >= growth.shardCosts.Length)
            {
                preview.Message = "角色已达到五阶。";
                return false;
            }

            if (!Consume(preview, stage, growth.shardItem, growth.shardCosts[progress.rank]))
                return false;
            progress.rank++;
            preview.Description = "角色升至 " + progress.rank + " 阶";
            return true;
        }

        private bool Equip(CharacterPreview preview, InventoryService stage, CharacterProgress progress, UnitConfig unit, CharacterRequest request)
        {
            if (!Enum.IsDefined(typeof(EquipmentSlot), request.slot) || !stage.TryGetEquipment(request.equipmentId, out EquipmentInstance instance) || !stage.TryGetConfig(instance.configId, out ItemConfig item) || !(item is EquipmentConfig equipment) || !Matches(unit, equipment, request.slot))
            {
                preview.Message = "装备不存在，或命途/部位不匹配。";
                return false;
            }

            if (progress.equipment[(int)request.slot] == instance.instanceId)
            {
                preview.Message = "已穿戴这件装备。";
                return false;
            }

            string owner = EquipmentOwner(instance.instanceId);
            if (owner != null && owner != progress.unitId)
            {
                if (!request.allowTransfer)
                {
                    preview.Message = "装备已由其他角色穿戴，需要确认转移。";
                    return false;
                }

                CharacterProgress previous = preview.roster.characters.Find(c => c.unitId == owner);
                previous.equipment[(int)request.slot] = null;
                preview.affected.Add(owner);
                preview.Description = "从「" + Config(owner).displayName + "」转移装备；原拥有者将卸下。\n";
            }

            progress.equipment[(int)request.slot] = instance.instanceId;
            preview.Description += "穿戴「" + equipment.displayName + "」，当前被替换装备将回到未穿戴状态。";
            return true;
        }

        private bool Equipment(CharacterPreview preview, InventoryService stage, CharacterRequest request)
        {
            if (!stage.TryGetEquipment(request.equipmentId, out EquipmentInstance instance) || !stage.TryGetConfig(instance.configId, out ItemConfig item) || !(item is EquipmentConfig config))
            {
                preview.Message = "装备实例不存在。";
                return false;
            }

            string owner = EquipmentOwner(instance.instanceId);
            if (owner != null)
                preview.affected.Add(owner);
            if (request.operation == CharacterOperation.Merge)
            {
                if (config.equipmentKind != EquipmentKind.LightCone || instance.instanceId == request.materialEquipmentId || !stage.TryGetEquipment(request.materialEquipmentId, out EquipmentInstance donor) || donor.configId != instance.configId || donor.isLocked || IsEquipped(donor.instanceId))
                {
                    preview.Message = "只能合并同款、未锁定、未穿戴的其他光锥。";
                    return false;
                }

                if (instance.superimposition + donor.superimposition > config.maxSuperimposition)
                {
                    preview.Message = "合并会超过被动等级上限。";
                    return false;
                }

                if (!stage.TryRemoveEquipment(donor.instanceId, out string reason))
                {
                    preview.Message = reason;
                    return false;
                }

                instance.superimposition += donor.superimposition;
                preview.Description = $"光锥被动提升至 {instance.superimposition} 级。\n消耗实例 {donor.instanceId}\n等级 {donor.level}、突破 {donor.ascension}、被动 {donor.superimposition}；其培养进度不返还。";
            }
            else if (request.operation == CharacterOperation.EquipmentAscend)
            {
                if (config.equipmentKind != EquipmentKind.LightCone || instance.ascension >= config.levelCaps.Length - 1 || instance.level != config.levelCaps[instance.ascension])
                {
                    preview.Message = "光锥未到阶段上限，或已经满突破。";
                    return false;
                }

                if (!Consume(preview, stage, config.ascensionMaterial, config.ascensionCosts[instance.ascension]))
                    return false;
                instance.ascension++;
                preview.Description = "光锥突破，上限 " + config.levelCaps[instance.ascension];
            }
            else
            {
                bool relic = config.equipmentKind == EquipmentKind.Instrument;
                int cap = relic ? config.maxLevel : config.levelCaps[Mathf.Clamp(instance.ascension, 0, config.levelCaps.Length - 1)];
                string material = string.IsNullOrEmpty(request.materialId) ? config.experienceMaterial : request.materialId;
                if (!Experience(preview, stage, material, request.quantity, ItemUseKind.EquipmentExperience, instance.level, instance.experience, cap, l => checked(config.experienceBase + config.experiencePerLevel * l), out int level, out int exp))
                    return false;
                preview.Description = $"{(relic ? "遗器强化" : "光锥等级")} {instance.level} → {level}，经验 {instance.experience} → {exp}";
                instance.level = level;
                instance.experience = exp;
            }

            InventoryData snapshot = stage.CreateSnapshot();
            int index = snapshot.equipment.FindIndex(e => e.instanceId == instance.instanceId);
            snapshot.equipment[index] = instance;
            if (!stage.TryRestore(snapshot, out string restoreReason, false))
            {
                preview.Message = restoreReason;
                return false;
            }

            return true;
        }
    }
}
