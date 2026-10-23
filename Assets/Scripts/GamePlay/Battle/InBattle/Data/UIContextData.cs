using System.Collections.Generic;
using Game.UI;
using UnityEngine;


public sealed class BattleAlliesUIContext : IUIContext
{
    public IReadOnlyList<BattleUnit> Units { get; }

    public BattleAlliesUIContext(IReadOnlyList<BattleUnit> units)
    {
        Units = units;
    }
}

public sealed class BattleEnemiesUIContext : IUIContext
{
    public IReadOnlyList<BattleUnit> Units { get; }
    public Camera BattleCamera { get; }

    public BattleEnemiesUIContext(IReadOnlyList<BattleUnit> units, Camera battleCamera = null)
    {
        Units = units;
        BattleCamera = battleCamera;
    }
}
