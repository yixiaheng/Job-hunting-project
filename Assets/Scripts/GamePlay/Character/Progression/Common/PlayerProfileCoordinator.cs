using System;
using Game.Inventory;

namespace Game.Characters
{
    /// <summary>养成只提交自己修改的数据，不依赖完整档案和存储实现。</summary>
    public interface IPlayerProfileCommitter
    {
        bool TrySave(CharacterRosterData roster, InventoryData inventory, long inventoryRevision, out string reason);
    }

    /// <summary>完整档案的唯一组装入口；成功写盘后才记录快照和修订号。</summary>
    public sealed class PlayerProfileCoordinator : IPlayerProfileCommitter
    {
        private readonly IPlayerProfileStore store;
        private readonly bool readOnly;
        private PlayerProfileData savedProfile;
        private long savedInventoryRevision;
        private bool hasSaved;

        public PlayerProfileCoordinator(IPlayerProfileStore store, PlayerProfileData profile,
            long inventoryRevision, bool hasSaved = true, bool readOnly = false)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            savedProfile = (profile ?? throw new ArgumentNullException(nameof(profile))).Copy();
            savedInventoryRevision = inventoryRevision;
            this.hasSaved = hasSaved;
            this.readOnly = readOnly;
        }

        public bool HasChanges(long rosterRevision, long inventoryRevision)
        {
            return !hasSaved || savedProfile.roster.revision != rosterRevision
                || savedInventoryRevision != inventoryRevision;
        }

        public bool TrySave(CharacterRosterData roster, InventoryData inventory, long inventoryRevision, out string reason)
        {
            reason = null;
            if (readOnly)
            {
                reason = "档案处于只读状态，原文件不会被覆盖。";
                return false;
            }
            if (roster == null || inventory == null)
            {
                reason = "提交的角色或背包数据为空。";
                return false;
            }
            if (!HasChanges(roster.revision, inventoryRevision))
                return true;

            // 从完整快照出发，只替换本次参与修改的模块，避免业务漏掉其他模块。
            var candidate = savedProfile.Copy();
            candidate.roster = roster.Copy();
            candidate.inventory = inventory.Copy();
            try
            {
                if (!store.TrySave(candidate.Copy(), out reason))
                    return false;
            }
            catch (Exception exception)
            {
                reason = "保存失败：" + exception.Message;
                return false;
            }

            savedProfile = candidate;
            savedInventoryRevision = inventoryRevision;
            hasSaved = true;
            return true;
        }
    }
}
