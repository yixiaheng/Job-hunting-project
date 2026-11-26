using System.Collections.Generic;
using Game.UI;
using UnityEngine;


public sealed class BattleAlliesUIContext : IUIContext
{
    public IReadOnlyList<BattleUnit> Units { get; }
    public IBattleCommandSource Commands { get; }

    public BattleAlliesUIContext(IReadOnlyList<BattleUnit> units,
        IBattleCommandSource commands = null)
    {
        Units = units;
        Commands = commands;
    }
}

public sealed class BattleEnemiesUIContext : IUIContext
{
    public IReadOnlyList<BattleUnit> Units { get; }
    public Camera BattleCamera { get; }
    public IBattleActorLookup ActorLookup { get; }

    public BattleEnemiesUIContext(IReadOnlyList<BattleUnit> units,
        IBattleActorLookup actorLookup, Camera battleCamera = null)
    {
        Units = units;
        ActorLookup = actorLookup;
        BattleCamera = battleCamera;
    }
}
