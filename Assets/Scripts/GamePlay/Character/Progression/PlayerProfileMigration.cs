using System;
using System.Linq;
using Game.Inventory;
using UnityEngine;

namespace Game.Characters
{
    public static class PlayerProfileMigration
    {
        /// <summary>迁移副本，保留旧文件和所有装备实例 ID，不向已有背包重复发奖。</summary>
        public static PlayerProfileData ImportLegacy(InventoryData legacy, PlayerRuntimeConfig config)
        {
            if (legacy?.equipment == null || legacy.stacks == null)
                throw new ArgumentException("旧背包数据无效。");
            var profile = new PlayerProfileData
            {
                inventory = JsonUtility.FromJson<InventoryData>(JsonUtility.ToJson(legacy))
            };
            var items = config.items.BuildIndex();
            foreach (var instance in profile.inventory.equipment)
            {
                if (instance == null)
                    throw new ArgumentException("旧装备实例为空。");
                if (instance.superimposition == 0)
                    instance.superimposition = 1;
                if (!items.TryGetValue(instance.configId, out var item) || !(item is EquipmentConfig equipment))
                    throw new ArgumentException("旧装备配置缺失：" + instance.configId);
                if (equipment.equipmentKind == EquipmentKind.LightCone)
                    while (instance.ascension >= 0 && instance.ascension < equipment.levelCaps.Length - 1 && instance.level > equipment.levelCaps[instance.ascension])
                        instance.ascension++;
            }

            AddInitialCharacters(profile, config);
            return profile;
        }

        public static void AddInitialCharacters(PlayerProfileData profile, PlayerRuntimeConfig config)
        {
            foreach (string id in config.startingCharacters.Distinct())
            {
                if (config.units.FindById(id) == null)
                    throw new ArgumentException("初始角色未登记：" + id);
                if (!profile.roster.characters.Any(c => c.unitId == id))
                    profile.roster.characters.Add(new CharacterProgress { unitId = id });
            }
        }
    }
}
