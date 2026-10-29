using System;
using Battle.SaveData;
using Game.UI;

public sealed class BattlePartyUIContext : IUIContext
{
    public BattlePartyManager Manager { get; }
    public Action StartBattle { get; }

    public BattlePartyUIContext(BattlePartyManager manager, Action startBattle = null)
    {
        Manager = manager != null ? manager : throw new ArgumentNullException(nameof(manager));
        StartBattle = startBattle;
    }
}

public sealed class AlliesChooseUIContext : IUIContext
{
    public BattlePartyManager Manager { get; }
    public PartyMode Mode { get; }
    public int Position { get; }

    public AlliesChooseUIContext(BattlePartyManager manager, PartyMode mode, int position = -1)
    {
        if (mode == PartyMode.Replace && (position < 0 || position >= BattleFormation.MaxMembers))
            throw new ArgumentOutOfRangeException(nameof(position));

        Manager = manager != null ? manager : throw new ArgumentNullException(nameof(manager));
        Mode = mode;
        Position = mode == PartyMode.Replace ? position : -1;
    }
}
