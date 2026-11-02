using System;
using Game.Inventory;
using TapTap21.SaveSystem.dyh;

namespace Game.Characters
{
    /// <summary>角色和背包放在同一个带备份的原子文件中，避免跨文件恢复出半套进度。</summary>
    [Serializable]
    public sealed class PlayerProfileData
    {
        public int formatVersion = 1;
        public CharacterRosterData roster = new CharacterRosterData();
        public InventoryData inventory = new InventoryData();
    }

    public interface IPlayerProfileStore
    {
        bool TrySave(PlayerProfileData profile, out string reason);
    }

    public sealed class PlayerProfileStore : IPlayerProfileStore
    {
        public static readonly SaveDefinition<PlayerProfileData> Definition = SaveSystem.Register<PlayerProfileData>("player_profile", "player_profile.json", 1);
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
