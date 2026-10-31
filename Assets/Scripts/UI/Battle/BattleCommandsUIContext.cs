using System;
using Game.UI;

/// <summary>将本场战斗的操作入口交给战斗操作栏。</summary>
public sealed class BattleCommandsUIContext : IUIContext
{
    public BattleManager Manager { get; }

    public BattleCommandsUIContext(BattleManager manager)
    {
        Manager = manager != null ? manager : throw new ArgumentNullException(nameof(manager));
    }
}
