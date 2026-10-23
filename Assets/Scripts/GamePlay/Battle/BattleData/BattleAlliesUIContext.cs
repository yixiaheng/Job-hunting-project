using System.Collections.Generic;
using Game.UI;

public sealed class BattleAlliesUIContext : IUIContext
{
    public IReadOnlyList<BattleUnit> Units { get; }

    public BattleAlliesUIContext(IReadOnlyList<BattleUnit> units)
    {
        Units = units;
    }
}