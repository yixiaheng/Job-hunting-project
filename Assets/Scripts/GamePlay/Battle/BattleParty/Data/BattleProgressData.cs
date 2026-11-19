using System;
using System.Collections.Generic;
using Game.Inventory;

namespace Battle.SaveData
{
    [Serializable]
    public sealed class BattleCompletionRecord
    {
        public string encounterId;
        public int clearCount;
        public int bestTurnCount;
        public string lastClearedAtUtc;
        public List<ItemAmount> lastRewards = new();

        public BattleCompletionRecord Copy()
        {
            var copy = new BattleCompletionRecord
            {
                encounterId = encounterId,
                clearCount = clearCount,
                bestTurnCount = bestTurnCount,
                lastClearedAtUtc = lastClearedAtUtc
            };
            if (lastRewards != null)
                foreach (ItemAmount item in lastRewards)
                    if (item != null)
                        copy.lastRewards.Add(new ItemAmount(item.configId, item.quantity));
            return copy;
        }
    }

    [Serializable]
    public sealed class BattleProgressData
    {
        public long revision;
        public BattleFormation formation = new();
        public List<BattleCompletionRecord> completions = new();

        public BattleProgressData Copy()
        {
            var copy = new BattleProgressData { revision = revision };
            if (formation != null)
                foreach (FormationMember member in formation.members)
                    if (member != null)
                        copy.formation.TryAdd(member.unitId, member.position);
            if (completions != null)
                foreach (BattleCompletionRecord record in completions)
                    if (record != null)
                        copy.completions.Add(record.Copy());
            return copy;
        }

        public BattleCompletionRecord Find(string encounterId)
        {
            if (completions == null || string.IsNullOrWhiteSpace(encounterId))
                return null;
            return completions.Find(record => record != null
                && string.Equals(record.encounterId, encounterId, StringComparison.Ordinal));
        }
    }

    public readonly struct BattleRewardLine
    {
        public string ConfigId { get; }
        public string DisplayName { get; }
        public int Quantity { get; }

        public BattleRewardLine(string configId, string displayName, int quantity)
        {
            ConfigId = configId;
            DisplayName = displayName;
            Quantity = quantity;
        }
    }

    public sealed class BattleRewardReceipt
    {
        private readonly List<BattleRewardLine> rewards = new();
        public IReadOnlyList<BattleRewardLine> Rewards => rewards;
        public bool HasRewards => rewards.Count > 0;
        internal void Add(BattleRewardLine reward) => rewards.Add(reward);
    }
}
