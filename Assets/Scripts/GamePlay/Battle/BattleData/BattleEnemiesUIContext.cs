using System.Collections.Generic;
using Game.UI;
using UnityEngine;

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
