using System;
using System.Linq;
using Game.Inventory;

namespace Game.Characters
{
    /// <summary>没有完整档案时创建一次初始数据，不读取或修改历史背包文件。</summary>
    public static class PlayerProfileFactory
    {
        public static PlayerProfileData Create(PlayerRuntimeConfig config)
        {
            if (config == null || config.items == null || config.units == null)
                throw new ArgumentException("缺少玩家初始配置。");

            var inventory = new InventoryService(config.items, false);
            if (config.startingItems.Count > 0 && !inventory.TryAddRewards(config.startingItems, out string reason))
                throw new ArgumentException(reason);

            var profile = new PlayerProfileData { inventory = inventory.CreateSnapshot() };
            foreach (string id in config.startingCharacters.Distinct())
            {
                if (config.units.FindById(id) == null)
                    throw new ArgumentException("初始角色未登记：" + id);
                profile.roster.characters.Add(new CharacterProgress { unitId = id });
            }
            return profile;
        }
    }
}
