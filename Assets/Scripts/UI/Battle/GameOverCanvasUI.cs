using Battle.Logic;
using Unity.VisualScripting;
using UnityEngine;

public class GameOverCanvasUI : MonoBehaviour
{
    private CanvasGroup canvasGroup;

    private void Awake() 
    {
        canvasGroup = GetComponent<CanvasGroup>();
        CanvasVisibility(false);
    }
    void Start()
    {
        BattleManager.Instance.battleFlow.GameOverEvent += OnGameOver;
    }
    void OnEnable() 
    {
        
    }   
    void OnDisable() 
    {
        BattleManager.Instance.battleFlow.GameOverEvent -= OnGameOver;
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
