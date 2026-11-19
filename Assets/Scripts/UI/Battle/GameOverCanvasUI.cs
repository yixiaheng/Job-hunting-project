using Battle.Events;
using Battle.Logic;
using Game;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Text;

public sealed class GameOverCanvasUI : UIPanel
{
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text turnsText;
    [SerializeField] private Toggle reformPartyToggle;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private TMP_Text rewardsText;

    private BattleResultUIContext resultContext;
    private bool busy;

    protected override void OnInitialize()
    {
        exitButton.onClick.AddListener(ExitBattle);
        retryButton.onClick.AddListener(RetryBattle);
        if (rewardsText == null && turnsText != null)
        {
            GameObject copy = Instantiate(turnsText.gameObject, turnsText.transform.parent);
            copy.name = "RewardsText";
            rewardsText = copy.GetComponent<TMP_Text>();
            RectTransform rect = (RectTransform)copy.transform;
            rect.anchoredPosition += new Vector2(0f, -80f);
        }
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
        if (rewardsText != null)
        {
            var text = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(resultContext?.RewardError))
                text.Append("奖励结算失败：").Append(resultContext.RewardError);
            else if (resultContext?.Rewards?.HasRewards == true)
            {
                text.Append("获得奖励：");
                foreach (var reward in resultContext.Rewards.Rewards)
                    text.Append('\n').Append(reward.DisplayName).Append(" x").Append(reward.Quantity);
            }
            rewardsText.text = text.ToString();
        }
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
