using System;
using System.Collections.Generic;
using Game.UI;
using UnityEngine;

/// <summary>拥有本场三个战斗面板的打开、刷新与关闭。</summary>
public sealed class BattleUIPresenter : IDisposable
{
    private readonly UIManager uiManager;
    private readonly int ownerSceneHandle;
    private UIHandle alliesStatusHandle;
    private UIHandle enemiesStatusHandle;
    private UIHandle commandsHandle;

    public BattleUIPresenter(UIManager uiManager, int ownerSceneHandle)
    {
        this.uiManager = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
        this.ownerSceneHandle = ownerSceneHandle;
    }

    public void Open(IReadOnlyList<BattleUnit> allies, IReadOnlyList<BattleUnit> enemies,
        IBattleActorLookup actorLookup, Camera battleCamera, IBattleCommandSource commands)
    {
        Close();
        alliesStatusHandle = uiManager.Open(UIId.BattleAlliesStatus,
            new BattleAlliesUIContext(allies), ownerSceneHandle: ownerSceneHandle);
        enemiesStatusHandle = uiManager.Open(UIId.BattleEnemiesStatus,
            new BattleEnemiesUIContext(enemies, actorLookup, battleCamera), ownerSceneHandle: ownerSceneHandle);
        commandsHandle = uiManager.Open(UIId.BattleCommands,
            new BattleCommandsUIContext(commands), ownerSceneHandle: ownerSceneHandle);
    }

    public void Refresh(BattleUnit currentUnit)
    {
        if (uiManager.TryGetPanel(alliesStatusHandle, out BattleAlliesStatusControll alliesPanel))
            alliesPanel.Refresh(currentUnit);
        if (uiManager.TryGetPanel(enemiesStatusHandle, out BattleEnemiesStatusPanel enemiesPanel))
            enemiesPanel.Refresh(currentUnit);
        if (uiManager.TryGetPanel(commandsHandle, out BattleUIController commandsPanel))
            commandsPanel.Refresh();
    }

    public void ReplaceEnemies(IReadOnlyList<BattleUnit> previous,
        IReadOnlyList<BattleUnit> next)
    {
        if (!uiManager.TryGetPanel(enemiesStatusHandle, out BattleEnemiesStatusPanel panel))
            return;
        if (previous != null)
            foreach (BattleUnit unit in previous)
                panel.RemoveUnit(unit);
        if (next != null)
            foreach (BattleUnit unit in next)
                panel.AddUnit(unit);
    }

    public void Close()
    {
        if (uiManager == null)
        {
            alliesStatusHandle = default;
            enemiesStatusHandle = default;
            commandsHandle = default;
            return;
        }

        if (alliesStatusHandle.IsValid)
            uiManager.Close(alliesStatusHandle);
        if (enemiesStatusHandle.IsValid)
            uiManager.Close(enemiesStatusHandle);
        if (commandsHandle.IsValid)
            uiManager.Close(commandsHandle);
        alliesStatusHandle = default;
        enemiesStatusHandle = default;
        commandsHandle = default;
    }

    public void Dispose() => Close();
}
