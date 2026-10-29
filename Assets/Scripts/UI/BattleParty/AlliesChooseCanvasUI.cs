using System;
using System.Collections.Generic;
using Battle.Events;
using Battle.SaveData;
using Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum PartyMode
{
    Replace,
    Sort
}

[RequireComponent(typeof(CanvasGroup))]
public class AlliesChooseCanvasUI : MonoBehaviour
{
    [SerializeField] private GameObject allyViewPrefab;
    [SerializeField] private Transform allyViewParent;
    [SerializeField] private Button confirmButton;

    private readonly Dictionary<AllyView, UnitConfig> views = new();
    // Array indices are real formation slots; null keeps a slot empty.
    private readonly UnitConfig[] pendingSlots = new UnitConfig[BattleFormation.MaxMembers];
    private readonly List<RaycastResult> pointerHits = new();
    private PointerEventData pointerEventData;
    private EventSystem pointerEventSystem;
    private CanvasGroup canvasGroup;
    private IDisposable selectionSubscription;
    private IDisposable formationSubscription;
    private BattlePartyManager manager;
    private UnitConfig pendingUnit;
    private PartyMode partyMode;
    private int selectedPosition = -1;
    private bool isOpen;
    private int openedFrame = -1;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (confirmButton != null)
            confirmButton.onClick.AddListener(ConfirmSelection);
        SetVisible(false);
    }

    public void Open(PartyMode mode, int position = -1)
    {
        if (mode == PartyMode.Replace && (position < 0 || position >= BattleFormation.MaxMembers))
            return;

        manager = BattlePartyManager.Instance;
        if (manager == null || allyViewPrefab == null || allyViewParent == null)
        {
            Debug.LogError("Assign the party manager, card prefab and card parent.", this);
            return;
        }

        partyMode = mode;
        selectedPosition = mode == PartyMode.Replace ? position : -1;
        pendingUnit = null;
        Array.Clear(pendingSlots, 0, pendingSlots.Length);

        BattleFormation formation = manager.Formation;
        for (int i = 0; i < BattleFormation.MaxMembers; i++)
        {
            FormationMember member = formation.GetMemberAtPosition(i);
            UnitConfig config = member != null ? manager.FindConfig(member.unitId) : null;
            pendingSlots[i] = config;
        }

        if (mode == PartyMode.Replace)
        {
            FormationMember member = formation.GetMemberAtPosition(position);
            pendingUnit = member != null ? manager.FindConfig(member.unitId) : null;
        }

        RebuildViews();
        selectionSubscription?.Dispose();
        formationSubscription?.Dispose();
        selectionSubscription = GameEvents.Battle.Subscribe<AllySlectionEvent>(HandleSelection);
        formationSubscription = GameEvents.Battle.Subscribe<PartyFormationChangedEvent>(HandleFormationChanged);
        isOpen = true;
        openedFrame = Time.frameCount;
        gameObject.SetActive(true);
        SetVisible(true);
        RefreshViews();
    }

    private void RebuildViews()
    {
        foreach (AllyView view in views.Keys)
            if (view != null)
                Destroy(view.gameObject);
        views.Clear();

        foreach (UnitConfig config in manager.AvailableAllies)
        {
            if (config == null)
                continue;

            GameObject instance = Instantiate(allyViewPrefab, allyViewParent);
            AllyView view = instance.GetComponent<AllyView>();
            if (view == null)
            {
                Destroy(instance);
                continue;
            }

            view.Bind(config);
            views.Add(view, config);
        }
    }

    private void HandleSelection(AllySlectionEvent message)
    {
        if (!isOpen || message.Source == null || !views.ContainsKey(message.Source))
            return;

        UnitConfig clicked = views[message.Source];
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
        if (!isOpen || manager == null)
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
            confirmButton.interactable = isOpen &&
                (partyMode == PartyMode.Sort || pendingUnit != null);
    }

    public void Close()
    {
        isOpen = false;
        selectedPosition = -1;
        pendingUnit = null;
        Array.Clear(pendingSlots, 0, pendingSlots.Length);
        selectionSubscription?.Dispose();
        formationSubscription?.Dispose();
        selectionSubscription = null;
        formationSubscription = null;
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private void OnDisable() => Close();

    private void Update()
    {
        // Ignore the click that opened this panel, regardless of script execution order.
        if (!isOpen || Time.frameCount == openedFrame || EventSystem.current == null)
            return;

        if (!TryGetPointerDown(out Vector2 screenPosition))
            return;

        if (!IsPointerInsideSelectionPanel(screenPosition))
            Close();
    }

    private static bool TryGetPointerDown(out Vector2 screenPosition)
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            screenPosition = mouse.position.ReadValue();
            return true;
        }

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
        {
            screenPosition = touchscreen.primaryTouch.position.ReadValue();
            return true;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }
#endif

        screenPosition = default;
        return false;
    }

    private bool IsPointerInsideSelectionPanel(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (pointerEventData == null || pointerEventSystem != eventSystem)
        {
            pointerEventSystem = eventSystem;
            pointerEventData = new PointerEventData(eventSystem);
        }

        pointerEventData.Reset();
        pointerEventData.position = screenPosition;
        pointerHits.Clear();
        eventSystem.RaycastAll(pointerEventData, pointerHits);

        // Check the frontmost UI hit only. A covered panel must not count as clicked.
        foreach (RaycastResult hit in pointerHits)
        {
            if (hit.gameObject == null || !(hit.module is GraphicRaycaster))
                continue;

            Transform target = hit.gameObject.transform;
            return target == transform || target.IsChildOf(transform);
        }

        return false;
    }

    private void OnDestroy()
    {
        Close();
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(ConfirmSelection);
    }
}
