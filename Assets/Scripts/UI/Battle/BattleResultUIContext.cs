using System;
using Battle.Logic;
using Game.UI;

public sealed class BattleResultUIContext : IUIContext
{
    public Guid BattleId { get; }
    public BattleResult Result { get; }
    public int TurnCount { get; }

    public BattleResultUIContext(Guid battleId, BattleResult result, int turnCount)
    {
        BattleId = battleId;
        Result = result;
        TurnCount = turnCount;
    }
}
