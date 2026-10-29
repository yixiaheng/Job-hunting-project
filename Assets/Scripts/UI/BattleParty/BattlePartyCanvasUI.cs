using System;
using Battle.Events;
using Battle.SaveData;
using Game;
using Game.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class BattlePartyCanvasUI : UIPanel
{
    [SerializeField] private Button[] chooseButtons;
    [SerializeField] private Button sortButton;
    [SerializeField] private Button startBattleButton;

    private UnityAction[] buttonActions;
    private BattlePartyManager manager;
    private Action startBattle;
    private IDisposable formationSubscription;

    protected override void OnInitialize()
    {
        if (chooseButtons != null)
        {
            buttonActions = new UnityAction[chooseButtons.Length];
            for (int i = 0; i < chooseButtons.Length && i < BattleFormation.MaxMembers; i++)
            {
                if (chooseButtons[i] == null)
                    continue;

                int position = i;
                buttonActions[i] = () => AddOrReplaceAlly(position);
                chooseButtons[i].onClick.AddListener(buttonActions[i]);
            }
        }

        if (sortButton != null)
            sortButton.onClick.AddListener(SortFormation);
        if (startBattleButton != null)
            startBattleButton.onClick.AddListener(StartBattle);
    }

    protected override void OnOpen(IUIContext context)
    {
        BindContext(context);
    }

    protected override void OnContextChanged(IUIContext context)
    {
        Owner.Close(UIId.AlliesChoose);
        BindContext(context);
    }

    private void BindContext(IUIContext context)
    {
        StopListening();
        BattlePartyUIContext partyContext = context as BattlePartyUIContext;
        manager = partyContext?.Manager;
        startBattle = partyContext?.StartBattle;
        if (manager == null)
            Debug.LogError("BattleParty requires a BattlePartyUIContext with a scene party manager.", this);

        formationSubscription = GameEvents.Battle.Subscribe<PartyFormationChangedEvent>(HandleFormationChanged);
        RefreshStartBattleButton();
    }

    private void HandleFormationChanged(PartyFormationChangedEvent message)
    {
        if (message.Manager == manager)
            RefreshStartBattleButton();
    }

    private void RefreshStartBattleButton()
    {
        if (startBattleButton != null)
            startBattleButton.interactable = manager != null && startBattle != null
                && manager.Formation.members.Count > 0;
    }

    public void StartBattle()
    {
        if (IsOpen && manager != null && manager.Formation.members.Count > 0)
            startBattle?.Invoke();
    }

    public void AddOrReplaceAlly(int position)
    {
        if (!IsOpen || manager == null || position < 0 || position >= BattleFormation.MaxMembers)
            return;

        Owner.Open(UIId.AlliesChoose, new AlliesChooseUIContext(manager, PartyMode.Replace, position), Handle);
    }

    public void SortFormation()
    {
        if (IsOpen && manager != null)
            Owner.Open(UIId.AlliesChoose, new AlliesChooseUIContext(manager, PartyMode.Sort), Handle);
    }

    protected override void OnClosed()
    {
        StopListening();
        startBattle = null;
        manager = null;
    }

    protected override void OnClosing()
    {
        StopListening();
    }

    private void OnDisable()
    {
        StopListening();
    }

    private void StopListening()
    {
        formationSubscription?.Dispose();
        formationSubscription = null;
    }

    private void OnDestroy()
    {
        StopListening();
        if (buttonActions != null && chooseButtons != null)
        {
            for (int i = 0; i < buttonActions.Length && i < chooseButtons.Length; i++)
            {
                if (chooseButtons[i] != null && buttonActions[i] != null)
                    chooseButtons[i].onClick.RemoveListener(buttonActions[i]);
            }
        }

        if (sortButton != null)
            sortButton.onClick.RemoveListener(SortFormation);
        if (startBattleButton != null)
            startBattleButton.onClick.RemoveListener(StartBattle);
    }
}
