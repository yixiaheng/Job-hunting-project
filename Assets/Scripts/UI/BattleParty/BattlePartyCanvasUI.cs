using System;
using Game.UI;
using UnityEngine;
using UnityEngine.UI;

public class BattlePartyCanvasUI : UIPanel
{
    [SerializeField] private Transform[] alliesPosition;
    [SerializeField] private Button[] chooseButtons;
    private CanvasGroup canvasGroup;

    protected override void OnInitialize()
    {
        canvasGroup = GetComponentInParent<CanvasGroup>();
        
    }

    protected override void OnOpened()
    {
        
    }

    protected override void OnClosed()
    {
        
    }

}
