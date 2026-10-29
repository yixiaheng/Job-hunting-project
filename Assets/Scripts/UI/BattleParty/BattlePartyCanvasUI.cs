using System;
using Battle.Events;
using Battle.SaveData;
using Game;
using Game.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class BattlePartyCanvasUI : UIPanel
{
    [SerializeField] private Transform[] alliesPosition;
    [SerializeField] private Button[] chooseButtons;
    [SerializeField] private Button sortButton;
    [SerializeField] private AlliesChooseCanvasUI alliesChooseCanvasUI;

    private readonly GameObject[] models = new GameObject[BattleFormation.MaxMembers];
    private readonly UnitConfig[] shownConfigs = new UnitConfig[BattleFormation.MaxMembers];
    private UnityAction[] buttonActions;
    private IDisposable formationSubscription;

    // The current party screen is scene-owned; UIManager-created panels use its hooks.
    private void Start()
    {
        if (sortButton == null)
            sortButton = FindButton("QuickSort");

        if (sortButton != null)
            sortButton.onClick.AddListener(SortFormation);

        if (Config == null)
        {
            ButtonsSet();
            OnOpened();
        }
    }

    protected override void OnInitialize()
    {
        ButtonsSet();
    }

    protected override void OnOpened()
    {
        formationSubscription?.Dispose();
        formationSubscription = GameEvents.Battle.Subscribe<PartyFormationChangedEvent>(HandleFormationChanged);
        RefreshFormation();
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

        if (alliesChooseCanvasUI != null)
            alliesChooseCanvasUI.Close();
    }

    public void ButtonsSet()
    {
        RemoveButtonListeners();

        if (chooseButtons == null)
            return;

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

    public void AddOrReplaceAlly(int position)
    {
        if (alliesChooseCanvasUI != null)
            alliesChooseCanvasUI.Open(PartyMode.Replace, position);
    }

    public void SortFormation()
    {
        if (alliesChooseCanvasUI != null)
            alliesChooseCanvasUI.Open(PartyMode.Sort);
    }


    //监听Formation的变化
    private void HandleFormationChanged(PartyFormationChangedEvent message)
    {
        if (message.Manager == BattlePartyManager.Instance)
            RefreshFormation();
    }

    public void RefreshFormation()
    {
        BattlePartyManager manager = BattlePartyManager.Instance;

        if (manager == null)
            return;

        BattleFormation formation = manager.Formation;

        for (int position = 0; position < BattleFormation.MaxMembers; position++)
        {
            FormationMember member = formation.GetMemberAtPosition(position);
            UnitConfig config = member != null ? manager.FindConfig(member.unitId) : null;

            if (shownConfigs[position] == config && (config == null || models[position] != null))
                continue;

            if (models[position] != null)
            {
                models[position].SetActive(false);
                Destroy(models[position]);
            }

            models[position] = null;
            shownConfigs[position] = null;

            if (config == null || config.prefab == null || alliesPosition == null
                || position >= alliesPosition.Length || alliesPosition[position] == null)
                continue;

            models[position] = Instantiate(config.prefab, alliesPosition[position], false);
            models[position].transform.localPosition = Vector3.zero;
            models[position].transform.localRotation = Quaternion.identity;
            shownConfigs[position] = config;
        }
    }

    private void RemoveButtonListeners()
    {
        if (buttonActions == null || chooseButtons == null)
            return;

        for (int i = 0; i < buttonActions.Length && i < chooseButtons.Length; i++)
        {
            if (chooseButtons[i] != null && buttonActions[i] != null)
                chooseButtons[i].onClick.RemoveListener(buttonActions[i]);
        }
    }

    private void OnDestroy()
    {
        StopListening();
        RemoveButtonListeners();

        if (sortButton != null)
            sortButton.onClick.RemoveListener(SortFormation);

        foreach (GameObject model in models)
        {
            if (model != null)
                Destroy(model);
        }
    }

    private Button FindButton(string objectName)
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            if (button.name == objectName)
                return button;
        }

        return null;
    }
}
