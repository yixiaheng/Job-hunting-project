using System;
using Battle.Logic;
using Battle.SaveData;

namespace Battle.Events
{
    public readonly struct BattleEndedEvent : IBattleEvent
    {
        public Guid BattleId { get; }
        public BattleResult Result { get; }
        public int TurnCount { get; }
        public BattleRewardReceipt Rewards { get; }
        public string RewardError { get; }

        public BattleEndedEvent(Guid battleId, BattleResult result, int turnCount,
            BattleRewardReceipt rewards = null, string rewardError = null)
        {
            BattleId = battleId;
            Result = result;
            TurnCount = turnCount;
            Rewards = rewards;
            RewardError = rewardError;
        }
    }

    public readonly struct BattleRetryRequestedEvent : IBattleEvent
    {
        public Guid BattleId { get; }
        public bool ReformParty { get; }

        public BattleRetryRequestedEvent(Guid battleId, bool reformParty)
        {
            BattleId = battleId;
            ReformParty = reformParty;
        }
    }

    public readonly struct BattleExitRequestedEvent : IBattleEvent
    {
        public Guid BattleId { get; }

        public BattleExitRequestedEvent(Guid battleId)
        {
            BattleId = battleId;
        }
    }
}
