using System;
using Game.Inventory;
using TapTap21.SaveSystem.dyh;
using Battle.SaveData;

namespace Game.Characters
{
    /// <summary>角色和背包放在同一个带备份的原子文件中，避免跨文件恢复出半套进度。</summary>
    [Serializable]
    public sealed class PlayerProfileData
    {
        public int formatVersion = 2;
        public CharacterRosterData roster = new CharacterRosterData();
        public InventoryData inventory = new InventoryData();
        public BattleProgressData battle = new BattleProgressData();

        // 完整档案的拷贝规则集中在这里；新增模块时同步补入此处。
        public PlayerProfileData Copy()
        {
            return new PlayerProfileData
            {
                formatVersion = formatVersion,
                roster = roster.Copy(),
                inventory = inventory.Copy(),
                battle = battle?.Copy() ?? new BattleProgressData()
            };
        }
    }

    public interface IPlayerProfileStore
    {
        bool TrySave(PlayerProfileData profile, out string reason);
    }

    public sealed class PlayerProfileStore : IPlayerProfileStore
    {
        public static readonly SaveDefinition<PlayerProfileData> Definition = SaveSystem.Register<PlayerProfileData>("player_profile", "player_profile.json", 2);
        static PlayerProfileStore()
        {
            SaveMigration.Register(Definition, 1, 2, profile =>
            {
                profile ??= new PlayerProfileData();
                profile.formatVersion = 2;
                profile.roster ??= new CharacterRosterData();
                profile.inventory ??= new InventoryData();
                profile.battle ??= new BattleProgressData();
                return profile;
            });
        }
        private readonly string slot;
        public PlayerProfileStore(string slot)
        {
            this.slot = slot;
        }

        public LoadResult<PlayerProfileData> Load() => SaveSystem.Load(Definition, slot);
        public bool TrySave(PlayerProfileData profile, out string reason)
        {
            SaveResult saved = SaveSystem.Save(Definition, slot, profile);
            reason = saved.Success ? null : saved.Message;
            return saved.Success;
        }
    }
}
