using System;
using System.Collections.Generic;
using Battle.Events;
using Battle.SaveData;
using Game;
using Game.UI;
using UnityEngine;
using UnityEngine.UI;

public enum PartyMode
{
    Replace,
    Sort
}

public class AlliesChooseCanvasUI : UIPanel
{
    [SerializeField] private GameObject allyViewPrefab;
    [SerializeField] private Transform allyViewParent;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button outsideClickButton;

    private readonly Dictionary<AllyView, UnitConfig> views = new();
    // Array indices are real formation slots; null keeps a slot empty.
    private readonly UnitConfig[] pendingSlots = new UnitConfig[BattleFormation.MaxMembers];
    private IDisposable selectionSubscription;
    private IDisposable formationSubscription;
    private BattlePartyManager manager;
    private UnitConfig pendingUnit;
    private PartyMode partyMode;
    private int selectedPosition = -1;
    private bool acceptingSelection;

    protected override void OnInitialize()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(ConfirmSelection);
        if (outsideClickButton != null)
            outsideClickButton.onClick.AddListener(Close);
    }

    protected override void OnOpen(IUIContext context)
    {
        BindContext(context);
    }

    protected override void OnContextChanged(IUIContext context)
    {
        BindContext(context);
    }

    private void BindContext(IUIContext context)
    {
        ClearDraft();
        AlliesChooseUIContext selection = context as AlliesChooseUIContext;
        manager = selection?.Manager;
        if (manager == null || allyViewPrefab == null || allyViewParent == null)
        {
            Debug.LogError("AlliesChoose requires a valid context, card prefab and card parent.", this);
            if (confirmButton != null)
                confirmButton.interactable = false;
            return;
        }

        partyMode = selection.Mode;
        selectedPosition = selection.Position;
        BattleFormation formation = manager.Formation;
        for (int i = 0; i < BattleFormation.MaxMembers; i++)
        {
            FormationMember member = formation.GetMemberAtPosition(i);
            pendingSlots[i] = member != null ? manager.FindConfig(member.unitId) : null;
        }

        if (partyMode == PartyMode.Replace)
            pendingUnit = pendingSlots[selectedPosition];

        RebuildViews();
        selectionSubscription = GameEvents.Battle.Subscribe<AllySlectionEvent>(HandleSelection);
        formationSubscription = GameEvents.Battle.Subscribe<PartyFormationChangedEvent>(HandleFormationChanged);
        acceptingSelection = true;
        RefreshViews();
    }

    private void RebuildViews()
    {
        ClearViews();
        foreach (UnitConfig config in manager.AvailableAllies)
        {
            if (config == null)
                continue;

            GameObject instance = Instantiate(allyViewPrefab, allyViewParent, false);
            AllyView view = instance.GetComponent<AllyView>();
            if (view == null)
            {
                instance.SetActive(false);
                Destroy(instance);
                continue;
            }

            view.Bind(config);
            views.Add(view, config);
        }
    }

    private void HandleSelection(AllySlectionEvent message)
    {
        if (!acceptingSelection || !IsOpen || message.Source == null || !views.TryGetValue(message.Source, out UnitConfig clicked))
            return;

        if (partyMode == PartyMode.Replace)
        {
            pendingUnit = clicked;
        }
        else
        {
            int index = Array.IndexOf(pendingSlots, clicked);
            if (index >= 0)
            {
                pendingSlots[index] = null;
            }
            else
            {
                int emptySlot = Array.IndexOf(pendingSlots, null);
                if (emptySlot >= 0)
                    pendingSlots[emptySlot] = clicked;
            }
        }

        RefreshViews();
    }

    public void ConfirmSelection()
    {
        if (!acceptingSelection || !IsOpen || manager == null)
            return;

        bool changed = partyMode == PartyMode.Replace
            ? pendingUnit != null && manager.AddOrReplace(pendingUnit, selectedPosition)
            : manager.ApplySortedFormation(pendingSlots);

        if (changed)
            Close();
    }

    private void HandleFormationChanged(PartyFormationChangedEvent message)
    {
        if (message.Manager == manager)
            RefreshViews();
    }

    private void RefreshViews()
    {
        if (manager == null)
            return;

        BattleFormation formation = manager.Formation;
        foreach (var pair in views)
        {
            int index = Array.IndexOf(pendingSlots, pair.Value);
            pair.Key.Refresh(
                partyMode == PartyMode.Replace && formation.ContainsUnit(pair.Value.id),
                partyMode == PartyMode.Replace ? pair.Value == pendingUnit : index >= 0,
                partyMode == PartyMode.Sort && index >= 0 ? index + 1 : 0,
                partyMode == PartyMode.Replace);
        }

        if (confirmButton != null)
            confirmButton.interactable = acceptingSelection &&
                (partyMode == PartyMode.Sort || pendingUnit != null);
    }

    public void Close()
    {
        acceptingSelection = false;
        if (Owner != null && Handle.IsValid)
            Owner.Close(Handle);
    }

    protected override void OnClosing()
    {
        ClearDraft();
    }

    protected override void OnClosed()
    {
        ClearViews();
    }

    private void OnDisable()
    {
        ClearDraft();
    }

    private void ClearDraft()
    {
        acceptingSelection = false;
        selectionSubscription?.Dispose();
        formationSubscription?.Dispose();
        selectionSubscription = null;
        formationSubscription = null;
        manager = null;
        selectedPosition = -1;
        pendingUnit = null;
        Array.Clear(pendingSlots, 0, pendingSlots.Length);
    }

    private void ClearViews()
    {
        foreach (AllyView view in views.Keys)
        {
            if (view == null)
                continue;

            view.gameObject.SetActive(false);
            Destroy(view.gameObject);
        }

        views.Clear();
    }

    private void OnDestroy()
    {
        ClearDraft();
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(ConfirmSelection);
        if (outsideClickButton != null)
            outsideClickButton.onClick.RemoveListener(Close);
    }
}
