using System;
using Battle.Logic;
using Game.UI;
using Battle.SaveData;

public sealed class BattleResultUIContext : IUIContext
{
    public Guid BattleId { get; }
    public BattleResult Result { get; }
    public int TurnCount { get; }
    public BattleRewardReceipt Rewards { get; }
    public string RewardError { get; }

    public BattleResultUIContext(Guid battleId, BattleResult result, int turnCount,
        BattleRewardReceipt rewards = null, string rewardError = null)
    {
        BattleId = battleId;
        Result = result;
        TurnCount = turnCount;
        Rewards = rewards;
        RewardError = rewardError;
    }
}
