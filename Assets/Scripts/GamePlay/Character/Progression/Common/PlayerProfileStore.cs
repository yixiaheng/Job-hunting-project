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
        public int formatVersion = 3;
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
        public static readonly SaveDefinition<PlayerProfileData> Definition = SaveSystem.Register<PlayerProfileData>("player_profile", "player_profile.json", 3);
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
            SaveMigration.Register(Definition, 2, 3, MigrateSharedGrowth);
        }

        /// <summary>保留等级/经验/技能/装备；只转换旧突破阶段索引。不覆盖磁盘存档。</summary>
        public static PlayerProfileData MigrateSharedGrowth(PlayerProfileData source)
        {
            if (source == null || source.roster == null)
                throw new InvalidOperationException("旧角色存档缺少角色列表。");
            var profile = source.Copy();
            int[] oldCaps = { 20, 40, 60, 90, 100 };
            var table = CharacterGrowthTable.Shared;
            foreach (var character in profile.roster.characters)
            {
                if (character.ascension < 0 || character.ascension >= oldCaps.Length
                    || character.level < 1 || character.level > oldCaps[character.ascension]
                    || character.level > table.MaxLevel)
                    throw new InvalidOperationException("角色 " + character.unitId + " 的旧等级无法无损迁移到80级规则；保留原存档，请先人工迁移。");
                int stage = 0;
                // 技能上限由加载后的CharacterProgressionService按对应角色配置校验，不在存档迁移硬编码。
                // 同级边界：旧上限已超过当前等级时，保留已解锁的下一阶段。
                while (stage < table.StageCount - 1
                    && (character.level > table.GetStage(stage).MaxLevel
                        || character.level == table.GetStage(stage).MaxLevel && oldCaps[character.ascension] > character.level))
                    stage++;
                if (!table.IsValidProgress(character.level, stage)
                    || character.level == table.GetStage(stage).MaxLevel && character.experience != 0)
                    throw new InvalidOperationException("角色 " + character.unitId + " 的旧技能等级或经验与新突破阶段不兼容；不降低进度，请先人工迁移。");
                character.ascension = stage;
            }
            profile.formatVersion = 3;
            return profile;
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
