using System;
using System.Collections.Generic;
using Battle.Logic;
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
    private UIHandle actionTimelineHandle;
    public bool IsReady => uiManager.TryGetPanel(alliesStatusHandle, out BattleAlliesStatusControll allies)
        && allies.State == UIState.Open && !allies.IsPresentationSuspended
        && uiManager.TryGetPanel(enemiesStatusHandle, out BattleEnemiesStatusPanel enemies)
        && enemies.State == UIState.Open && !enemies.IsPresentationSuspended
        && uiManager.TryGetPanel(commandsHandle, out BattleUIController commands)
        && commands.State == UIState.Open && !commands.IsPresentationSuspended
        && uiManager.TryGetPanel(actionTimelineHandle, out BattleActionTimelinePanel timeline)
        && timeline.State == UIState.Open && !timeline.IsPresentationSuspended;

    public void SetVisible(bool visible)
    {
        if (uiManager.TryGetPanel(alliesStatusHandle, out BattleAlliesStatusControll allies)) allies.SetCinematicHidden(!visible);
        if (uiManager.TryGetPanel(enemiesStatusHandle, out BattleEnemiesStatusPanel enemies)) enemies.SetCinematicHidden(!visible);
        if (uiManager.TryGetPanel(commandsHandle, out BattleUIController commands)) commands.SetCinematicHidden(!visible);
        if (uiManager.TryGetPanel(actionTimelineHandle, out BattleActionTimelinePanel timeline)) timeline.SetCinematicHidden(!visible);
    }
    public void SetCommandsVisible(bool visible)
    { if (uiManager.TryGetPanel(commandsHandle, out BattleUIController commands)) commands.SetCinematicHidden(!visible); }

    public BattleUIPresenter(UIManager uiManager, int ownerSceneHandle)
    {
        this.uiManager = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
        this.ownerSceneHandle = ownerSceneHandle;
    }

    public void Open(IReadOnlyList<BattleUnit> allies, IReadOnlyList<BattleUnit> enemies,
        IBattleActorLookup actorLookup, Camera battleCamera, IBattleCommandSource commands,
        IBattleActionOrderSource actionOrderSource)
    {
        Close();
        alliesStatusHandle = uiManager.Open(UIId.BattleAlliesStatus,
            new BattleAlliesUIContext(allies, commands), ownerSceneHandle: ownerSceneHandle);
        enemiesStatusHandle = uiManager.Open(UIId.BattleEnemiesStatus,
            new BattleEnemiesUIContext(enemies, actorLookup, battleCamera), ownerSceneHandle: ownerSceneHandle);
        commandsHandle = uiManager.Open(UIId.BattleCommands,
            new BattleCommandsUIContext(commands), ownerSceneHandle: ownerSceneHandle);
        actionTimelineHandle = uiManager.Open(UIId.BattleActionTimeline,
            new BattleActionTimelineUIContext(actionOrderSource), ownerSceneHandle: ownerSceneHandle);
    }

    public void Refresh(BattleUnit currentUnit)
    {
        if (uiManager.TryGetPanel(alliesStatusHandle, out BattleAlliesStatusControll alliesPanel))
            alliesPanel.Refresh(currentUnit);
        if (uiManager.TryGetPanel(enemiesStatusHandle, out BattleEnemiesStatusPanel enemiesPanel))
            enemiesPanel.Refresh(currentUnit);
        if (uiManager.TryGetPanel(commandsHandle, out BattleUIController commandsPanel))
            commandsPanel.Refresh();
        if (uiManager.TryGetPanel(actionTimelineHandle, out BattleActionTimelinePanel timelinePanel))
            timelinePanel.Refresh(currentUnit);
    }

    public void HoldActionTimeline(BattleUnit currentUnit)
    {
        if (uiManager.TryGetPanel(actionTimelineHandle, out BattleActionTimelinePanel panel))
            panel.Hold(currentUnit);
    }

    public void AdvanceAndHoldActionTimeline(BattleUnit currentUnit)
    {
        if (uiManager.TryGetPanel(actionTimelineHandle, out BattleActionTimelinePanel panel))
            panel.AdvanceAndHold(currentUnit);
    }

    public void ReleaseActionTimeline(BattleUnit currentUnit, bool animate = true)
    {
        if (uiManager.TryGetPanel(actionTimelineHandle, out BattleActionTimelinePanel panel))
            panel.ReleaseAndRefresh(currentUnit, animate);
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
        if (actionTimelineHandle.IsValid)
            uiManager.Close(actionTimelineHandle);
        alliesStatusHandle = default;
        enemiesStatusHandle = default;
        commandsHandle = default;
        actionTimelineHandle = default;
    }

    public void Dispose() => Close();
}
