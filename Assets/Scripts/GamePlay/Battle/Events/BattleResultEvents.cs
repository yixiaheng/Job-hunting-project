using System;
using Battle.Logic;

namespace Battle.Events
{
    public readonly struct BattleEndedEvent : IBattleEvent
    {
        public Guid BattleId { get; }
        public BattleResult Result { get; }
        public int TurnCount { get; }

        public BattleEndedEvent(Guid battleId, BattleResult result, int turnCount)
        {
            BattleId = battleId;
            Result = result;
            TurnCount = turnCount;
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
