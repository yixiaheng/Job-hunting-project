using UnityEngine;
using Game.UI;

public class BattleAlliesStatusControll : UIPanel
{
    [SerializeField] private BattleUnitView[] allyViews;

    protected override void OnOpen(IUIContext context)
    {
        Bind(context);
    }

    protected override void OnContextChanged(IUIContext context)
    {
        Bind(context);
    }

    protected override void OnClosing()
    {
        Unbind();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void Bind(IUIContext context)
    {
        Unbind();

        if (context is not BattleAlliesUIContext battleContext || battleContext.Units == null)
        {
            Debug.LogError("我方状态栏需要 BattleAlliesUIContext 和本场战斗的我方单位。", this);
            return;
        }

        if (allyViews == null)
            return;

        foreach (BattleUnit unit in battleContext.Units)
        {
            if (unit == null || unit.faction != Faction.OurSide)
                continue;

            if (unit.pos < 0 || unit.pos >= allyViews.Length || allyViews[unit.pos] == null)
            {
                Debug.LogWarning($"我方状态栏没有配置站位 {unit.pos}。", this);
                continue;
            }

            BattleUnitView view = allyViews[unit.pos];
            if (view.unit != null)
            {
                Debug.LogWarning($"我方状态栏站位 {unit.pos} 重复，保留先绑定的角色。", this);
                continue;
            }

            view.Bind(unit);
        }
    }

    public void Refresh(BattleUnit currentUnit)
    {
        if (allyViews == null)
            return;

        foreach (BattleUnitView view in allyViews)
        {
            if (view == null)
                continue;

            view.Refresh();
            view.SetActing(view.unit != null && view.unit == currentUnit);
        }
    }

    private void Unbind()
    {
        if (allyViews == null)
            return;

        foreach (BattleUnitView view in allyViews)
        {
            if (view != null)
                view.Bind(null);
        }
    }
}
