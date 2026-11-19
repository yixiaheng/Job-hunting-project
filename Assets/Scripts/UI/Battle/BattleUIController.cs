using Battle.Logic;
using Game.UI;
using UnityEngine;
using UnityEngine.UI;
using Battle.Events;
using Game;
using System;
using TMPro;

/// <summary>战斗操作栏只持有本次打开时传入的战斗入口。</summary>
public sealed class BattleUIController : UIPanel
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button basicButton;
    [SerializeField] private Button skillButton;
    [SerializeField] private Button ultimateButton;
    [SerializeField] private TMP_Text skillPointsText;

    private IBattleCommandSource commands;
    private IDisposable selectionSubscription;

    protected override void OnInitialize()
    {
        if (skillPointsText != null)
            return;
        Transform placeholder = transform.Find("Characters");
        if (placeholder == null)
            return;
        var rect = (RectTransform)placeholder;
        rect.anchoredPosition = new Vector2(1088f, -520f);
        rect.sizeDelta = new Vector2(320f, 100f);
        skillPointsText = placeholder.gameObject.AddComponent<TextMeshProUGUI>();
        skillPointsText.raycastTarget = false;
        skillPointsText.alignment = TextAlignmentOptions.Center;
        skillPointsText.fontSize = 42f;
        if (Game.Characters.PlayerRuntime.Instance?.Config?.uiFont != null)
            skillPointsText.font = Game.Characters.PlayerRuntime.Instance.Config.uiFont;
    }

    protected override void OnOpen(IUIContext context)
    {
        Bind(context);
    }

    protected override void OnContextChanged(IUIContext context)
    {
        Bind(context);
    }

    protected override void OnClosed()
    {
        Unsubscribe();
        commands = null;
        Refresh();
    }

    private void Bind(IUIContext context)
    {
        Unsubscribe();
        commands = (context as BattleCommandsUIContext)?.Commands;
        if (commands == null)
            Debug.LogError("战斗操作栏需要 BattleCommandsUIContext。", this);
        Subscribe();
        Refresh();
    }

    private void Subscribe()
    {
        if (selectionSubscription == null && commands != null && isActiveAndEnabled)
            selectionSubscription = GameEvents.Battle.Subscribe<TargetSelectionChangedEvent>(OnSelectionChanged);
    }

    private void OnSelectionChanged(TargetSelectionChangedEvent message)
    {
        if (IsOpen && commands?.Flow != null && message.BattleId == commands.Flow.BattleId)
            Refresh();
    }

    private void Unsubscribe()
    {
        selectionSubscription?.Dispose();
        selectionSubscription = null;
    }

    protected override void OnClosing() => Unsubscribe();
    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();
    private void OnEnable() { if (IsOpen) { Subscribe(); Refresh(); } }

    public void Refresh()
    {
        BattleFlow flow = commands?.Flow;
        BattleUnit unit = flow?.CurrentUnit;
        bool canAct = flow != null
            && flow.Result == BattleResult.None && unit != null
            && unit.IsActive && unit.faction == Faction.OurSide;

        if (startButton != null)
            startButton.interactable = commands != null && !(flow?.IsExecuting ?? false);
        if (skillPointsText != null)
            skillPointsText.text = flow == null
                ? string.Empty
                : $"战技点 {flow.SkillPoints.Current}/{flow.SkillPoints.Maximum}";
        SetAbilityAvailable(basicButton, SkillKind.Basic, canAct);
        SetAbilityAvailable(skillButton, SkillKind.Skill, canAct);
        SetAbilityAvailable(ultimateButton, SkillKind.Ultimate, canAct);
    }

    private void SetAbilityAvailable(Button button, SkillKind kind, bool canAct)
    {
        if (button == null)
            return;

        button.interactable = canAct && commands.Flow.ValidateAction(commands.CreateAbilityAction(kind)).IsValid;
    }

    public void OnStartBattle()
    {
        if (IsOpen && commands != null)
            commands.StartBattle();
    }

    public void OnBasic() => Choose(SkillKind.Basic);
    public void OnSkill() => Choose(SkillKind.Skill);
    public void OnUlt() => Choose(SkillKind.Ultimate);

    private void Choose(SkillKind kind)
    {
        BattleFlow flow = commands?.Flow;
        if (IsOpen && flow != null && flow.Result == BattleResult.None
            && flow.CurrentUnit != null && flow.CurrentUnit.IsActive
            && flow.CurrentUnit.faction == Faction.OurSide)
            commands.ChooseAbility(kind);
    }
}
