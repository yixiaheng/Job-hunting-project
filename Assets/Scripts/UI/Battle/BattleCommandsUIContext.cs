using System;
using Battle.Logic;
using Game.UI;

public interface IBattleCommandSource
{
    BattleFlow Flow { get; }
    void StartBattle();
    BattleAction CreateAbilityAction(SkillKind kind);
    void ChooseAbility(SkillKind kind);
}

/// <summary>将本场战斗的窄操作入口交给战斗操作栏。</summary>
public sealed class BattleCommandsUIContext : IUIContext
{
    public IBattleCommandSource Commands { get; }

    public BattleCommandsUIContext(IBattleCommandSource commands)
    {
        Commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }
}
