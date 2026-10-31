using Battle.Logic;
using Game.UI;
using UnityEngine;
using UnityEngine.UI;

/// <summary>战斗操作栏只持有本次打开时传入的战斗入口。</summary>
public sealed class BattleUIController : UIPanel
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button basicButton;
    [SerializeField] private Button skillButton;
    [SerializeField] private Button ultimateButton;

    private BattleManager battle;

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
        battle = null;
        Refresh();
    }

    private void Bind(IUIContext context)
    {
        battle = (context as BattleCommandsUIContext)?.Manager;
        if (battle == null)
            Debug.LogError("战斗操作栏需要 BattleCommandsUIContext。", this);
        Refresh();
    }

    public void Refresh()
    {
        BattleUnit unit = battle != null ? battle.battleFlow?.CurrentUnit : null;
        bool canAct = battle != null && battle.battleFlow != null
            && battle.battleFlow.Result == BattleResult.None && unit != null
            && unit.IsActive && unit.faction == Faction.OurSide;

        if (startButton != null)
            startButton.interactable = battle != null;
        SetAbilityAvailable(basicButton, unit, SkillKind.Basic, canAct);
        SetAbilityAvailable(skillButton, unit, SkillKind.Skill, canAct);
        SetAbilityAvailable(ultimateButton, unit, SkillKind.Ultimate, canAct);
    }

    private static void SetAbilityAvailable(Button button, BattleUnit unit, SkillKind kind, bool canAct)
    {
        if (button == null)
            return;

        bool available = false;
        if (canAct && unit.skills != null)
        {
            foreach (SkillConfig skill in unit.skills)
            {
                if (skill != null && skill.kind == kind)
                {
                    available = true;
                    break;
                }
            }
        }
        button.interactable = available;
    }

    public void OnStartBattle()
    {
        if (IsOpen && battle != null)
            battle.StartBattle();
    }

    public void OnBasic() => Choose(SkillKind.Basic);
    public void OnSkill() => Choose(SkillKind.Skill);
    public void OnUlt() => Choose(SkillKind.Ultimate);

    private void Choose(SkillKind kind)
    {
        if (IsOpen && battle != null && battle.battleFlow != null
            && battle.battleFlow.Result == BattleResult.None
            && battle.battleFlow.CurrentUnit != null
            && battle.battleFlow.CurrentUnit.IsActive
            && battle.battleFlow.CurrentUnit.faction == Faction.OurSide)
            battle.ChooseAbility(kind);
    }
}
