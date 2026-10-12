using System.Collections.Generic;
using Battle.Logic;
using Sirenix.OdinInspector;
using UnityEngine;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance{get; private set;}

    public MouseRayCasting rayCasting;

    //Todo:不拖拽，从BattleLaunchData;
    public UnitConfig[] allies;
    public UnitConfig[] enemies;

    //角色显示自己拖拽即可
    public BattleUnitView[] allyViews;
    public BattleUnitView[] enemyViews;

    //pos由BattleManager中自己设置
    public Transform[] allyPos;
    public Transform[] enemyPos;
    public BattleFlow battleFlow{get; private set;}
    readonly Dictionary<BattleUnit, BattleUnitView> views = new();

    private void Awake() 
    {
        if(Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;    

        //从BattleLauchData中读取数据
        //BattleDataInit();

        StartBattle();
    }


    public void StartBattle()
    {
        var units = new List<BattleUnit>();
        views.Clear();
        int tie = 0;

        SpawnUnits(allies, Faction.OurSide, allyPos, allyViews, units, ref tie);
        SpawnUnits(enemies, Faction.Enemy, enemyPos, enemyViews, units, ref tie);

        if (units.Count == 0)
        {
            Debug.LogError("没有单位：把 UnitConfig 拖到 Allies / Enemies");
            return;
        }

        battleFlow = new BattleFlow(units, new FirstSkillDecider());
        battleFlow.Ready();
        RefreshAll();
        Debug.Log("战斗已创建，点 StepOnce 推进");

        ActingUntilPlayer();
    }


    private void SpawnUnits(
        UnitConfig[] configs,
        Faction faction,
        Transform[] positions,
        BattleUnitView[] unitViews,
        List<BattleUnit> units,
        ref int tie)
    {
        for (int i = 0; i < configs.Length; i++)
        {
            // 这里的 i 代表在场地上的序号
            var u = BattleDataMaping.ToUnit(configs[i], faction, i, tie++);
            if (u == null) continue;

            units.Add(u);
            BindView(u, unitViews, i);

            GameObject obj = Instantiate(configs[i].prefab);
            obj.AddComponent<BattleUnitActor>();

            var actor = obj.GetComponent<BattleUnitActor>();
            actor.Bind(u);

            if (obj.GetComponentInChildren<Collider>() == null)
                obj.AddComponent<CapsuleCollider>();

            obj.transform.position = positions[i].position;
            u.gameObject = obj;
        }
    }


    [Button]
    void StepOnce()
    {
        if (battleFlow == null)
        {
            Debug.LogError("先点 StartBattle");
            return;
        }

        if (!battleFlow.Step())
        {
            RefreshAll();
            Debug.Log("战斗已结束: " + battleFlow.Result);
            return;
        }

        RefreshAll();
    }

#region 战斗选择

    public void ChooseBasic()
    {
        ChooseAbility(SkillKind.Basic);
    }


    public void ChooseSkill()
    {
        ChooseAbility(SkillKind.Skill);
    }


    public void ChooseUlt()
    {
        ChooseAbility(SkillKind.Ultimate);
    }

    public void ChooseAbility(SkillKind kind)
    {
        BattleUnit unit = battleFlow.CurrentUnit;
        SkillConfig ability = null;
        foreach(var skill in unit.skills)
        {
            if(skill.kind == kind)
            {
                ability = skill;
                break;
            }
        }
        if(ability == null)
        {
            string str = kind switch
            {
                SkillKind.Basic => "Basic",
                SkillKind.Skill => "Skill",
                SkillKind.Ultimate => "Ultimate",
                _ => "Null"
            };

            Debug.Log($"没有{str}技能");
            return;
        }

        BattleUnit target = rayCasting.selectedTarget;

        var action = new BattleAction {skill = ability, mainTarget = target};
        battleFlow.Execute(action);
        RefreshAll();
        ActingUntilPlayer();
    }

#endregion

    /// <summary>
    /// 绑定BattleUnit与BattleUnitView，数据与UI视图
    /// </summary>
    /// <param name="u"></param>
    /// <param name="slots"></param>
    /// <param name="i"></param>
    void BindView(BattleUnit u, BattleUnitView[] slots, int i)
    {
        if (slots == null || i >= slots.Length || slots[i] == null)
            return;
        slots[i].Bind(u);
        views[u] = slots[i];
    }

    void RefreshAll()
    {
        foreach (var v in views.Values)
        {
            if (v == null) continue;
            v.Refresh();
            v.SetActing(false);
        }

        if (battleFlow == null || battleFlow.CurrentUnit == null)
            return;
        if (views.TryGetValue(battleFlow.CurrentUnit, out var view) && view != null)
            view.SetActing(true);
    }
    
    void ActingUntilPlayer()
    {
        while(battleFlow.Result == BattleResult.None
            && battleFlow.CurrentUnit != null
            && battleFlow.CurrentUnit.faction == Faction.Enemy)
        {
            if(!battleFlow.Step()) break;
        }
        RefreshAll();
    }


}
