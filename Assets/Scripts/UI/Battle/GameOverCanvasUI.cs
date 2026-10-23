using Battle.Logic;
using UnityEngine;

public class GameOverCanvasUI : MonoBehaviour
{
    private CanvasGroup canvasGroup;
    private BattleFlow subscribedFlow;
    private BattleManager battleManager;

    private void Awake() 
    {
        canvasGroup = GetComponent<CanvasGroup>();
        CanvasVisibility(false);
    }
    void OnEnable()
    {
        battleManager = BattleManager.Instance;
        if (battleManager == null)
            return;
        battleManager.BattleStarted += BindFlow;
        BindFlow(battleManager.battleFlow);
    }

    private void BindFlow(BattleFlow flow)
    {
        if (subscribedFlow != null)
            subscribedFlow.GameOverEvent -= OnGameOver;
        subscribedFlow = flow;
        CanvasVisibility(false);
        if (subscribedFlow != null)
            subscribedFlow.GameOverEvent += OnGameOver;
    }

    void OnDisable() 
    {
        if (battleManager != null)
            battleManager.BattleStarted -= BindFlow;
        battleManager = null;
        if (subscribedFlow != null)
            subscribedFlow.GameOverEvent -= OnGameOver;
        subscribedFlow = null;
    }

    void OnGameOver(BattleResult result)
    {
        Debug.Log("GameOver");
        CanvasVisibility(true);
    }

    public void CanvasVisibility(bool vity)
    {
        if(vity == true)
        {
            canvasGroup.alpha = 1;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        else
        {
            canvasGroup.alpha = 0;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
