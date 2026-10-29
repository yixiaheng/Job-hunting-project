using Battle.Events;
using Battle.Logic;
using Game;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameOverCanvasUI : UIPanel
{
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text turnsText;
    [SerializeField] private Toggle reformPartyToggle;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button retryButton;

    private BattleResultUIContext resultContext;
    private bool busy;

    protected override void OnInitialize()
    {
        exitButton.onClick.AddListener(ExitBattle);
        retryButton.onClick.AddListener(RetryBattle);
    }

    protected override void OnOpen(IUIContext context) => Bind(context);

    protected override void OnContextChanged(IUIContext context) => Bind(context);

    private void Bind(IUIContext context)
    {
        resultContext = context as BattleResultUIContext;
        reformPartyToggle.SetIsOnWithoutNotify(false);
        SetBusy(resultContext == null);
        resultText.text = resultContext?.Result switch
        {
            BattleResult.AllyWin => "挑战成功",
            BattleResult.EnemyWin => "挑战失败",
            BattleResult.Draw => "战斗结束：平局",
            _ => "战斗结束"
        };
        turnsText.text = resultContext == null
            ? string.Empty
            : $"行动次数：{resultContext.TurnCount}";
    }

    private void RetryBattle()
    {
        if (!IsOpen || busy || resultContext == null)
            return;

        SetBusy(true);
        GameEvents.Battle.Publish(new BattleRetryRequestedEvent(
            resultContext.BattleId, reformPartyToggle.isOn));
    }

    private void ExitBattle()
    {
        if (!IsOpen || busy || resultContext == null)
            return;

        SetBusy(true);
        GameEvents.Battle.Publish(new BattleExitRequestedEvent(resultContext.BattleId));
    }

    public void SetBusy(bool value)
    {
        busy = value;
        exitButton.interactable = !value;
        retryButton.interactable = !value;
        reformPartyToggle.interactable = !value;
    }

    public override bool CanCloseFromBack() => false;

    protected override void OnClosed()
    {
        resultContext = null;
    }

    private void OnDestroy()
    {
        if (exitButton != null)
            exitButton.onClick.RemoveListener(ExitBattle);
        if (retryButton != null)
            retryButton.onClick.RemoveListener(RetryBattle);
    }
}
