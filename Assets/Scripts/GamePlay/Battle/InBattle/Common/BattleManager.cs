using System.Collections.Generic;
using Battle.Logic;
using Battle.Events;
using Game;
using System;
using Game.UI;
using Sirenix.OdinInspector;
using UnityEngine;


/// <summary>
/// 为BattleFlow提供各种信息与玩家操作，为战斗流程提供
/// </summary>
[DefaultExecutionOrder(-50)]
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance{get; private set;}

    public MouseRayCasting rayCasting;

    // Inspector allies are only used when the scene has no launch snapshot.
    public UnitConfig[] allies;
    public UnitConfig[] enemies;

    [SerializeField] private Camera battleCamera;

    //pos由BattleManager中自己设置
    public Transform[] allyPos;
    public Transform[] enemyPos;
    public BattleFlow battleFlow { get; private set;}
    private readonly List<BattleUnit> spawnedUnits = new();
    private UnitConfig[] launchAllies;
    private bool resultPublished;
    private UIManager battleUIManager;
    private UIHandle alliesStatusHandle;
    private UIHandle enemiesStatusHandle;
    private UIHandle commandsHandle;

    public event Action<BattleFlow> BattleStarted;

    private void Awake() 
    {
        if(Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;    

        if (BattleLaunchContext.TryConsume(out BattleLaunchData data))
            launchAllies = data.CopyAllies();

    }

    private void Start()
    {
        if (Instance == this && battleFlow == null)
            StartBattle();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        CloseBattlePanels();

        if (battleFlow != null)
            battleFlow.GameOverEvent -= HandleBattleEnded;

        ClearUnits();
        Instance = null;
    }

    public void StartBattle()
    {
        if (UIManager.Instance == null)
        {
            Debug.LogError("请先初始化 UIManager，再开始战斗。", this);
            return;
        }

        UnitConfig[] allyConfigs = launchAllies ?? allies;
        if (!ValidateAllies(allyConfigs))
            return;

        if (battleFlow != null)
            battleFlow.GameOverEvent -= HandleBattleEnded;
        ClearUnits();
        battleFlow = null;
        resultPublished = false;
        var units = new List<BattleUnit>();
        int tie = 0;

        SpawnUnits(allyConfigs, Faction.OurSide, allyPos, units, ref tie);
        SpawnUnits(enemies, Faction.Enemy, enemyPos, units, ref tie);

        if (units.Count == 0)
        {
            CloseBattlePanels();
            Debug.LogError("没有单位：把 UnitConfig 拖到 Allies / Enemies");
            return;
        }

        battleFlow = new BattleFlow(units, new FirstSkillDecider());
        battleFlow.GameOverEvent += HandleBattleEnded;
        battleUIManager = UIManager.Instance;
        alliesStatusHandle = battleUIManager.Open(UIId.BattleAlliesStatus,
            new BattleAlliesUIContext(units.FindAll(unit => unit.faction == Faction.OurSide)));
        enemiesStatusHandle = battleUIManager.Open(UIId.BattleEnemiesStatus,
            new BattleEnemiesUIContext(units.FindAll(unit => unit.faction == Faction.Enemy),
                battleCamera != null ? battleCamera : Camera.main));
        commandsHandle = battleUIManager.Open(UIId.BattleCommands,
            new BattleCommandsUIContext(this));
        battleFlow.Ready();
        BattleStarted?.Invoke(battleFlow);
        RefreshAll();
        Debug.Log("战斗已创建，点 StepOnce 推进");

        ActingUntilPlayer();
    }

    public bool TryCreateLaunchData(out BattleLaunchData data, out string error)
    {
        return BattleLaunchData.TryCreate(launchAllies ?? allies, out data, out error);
    }

    private void HandleBattleEnded(BattleResult result)
    {
        if (resultPublished || battleFlow == null || result == BattleResult.None)
            return;

        resultPublished = true;
        RefreshAll();
        GameEvents.Battle.Publish(new BattleEndedEvent(battleFlow.BattleId, result, battleFlow.TurnCount));
    }

    private bool ValidateAllies(UnitConfig[] configs)
    {
        bool hasAlly = false;
        if (configs != null)
        {
            for (int position = 0; position < configs.Length; position++)
            {
                UnitConfig config = configs[position];
                if (config == null)
                    continue;

                hasAlly = true;
                if (config.prefab == null || allyPos == null || position >= allyPos.Length
                    || allyPos[position] == null)
                {
                    Debug.LogError($"Cannot start battle: ally slot {position + 1} ({config.id}) needs a model prefab and scene anchor.", this);
                    return false;
                }
            }
        }

        if (!hasAlly)
            Debug.LogError("Cannot start battle without allies. Confirm a party, or assign Inspector allies for scene debugging.", this);

        return hasAlly;
    }

    private void ClearUnits()
    {
        if (rayCasting != null)
            rayCasting.ClearSelection();

        foreach (BattleUnit unit in spawnedUnits)
        {
            if (unit.gameObject != null)
                Destroy(unit.gameObject);
        }
        spawnedUnits.Clear();
    }

    private void CloseBattlePanels()
    {
        if (battleUIManager != null)
        {
            if (alliesStatusHandle.IsValid)
                battleUIManager.Close(alliesStatusHandle);
            if (enemiesStatusHandle.IsValid)
                battleUIManager.Close(enemiesStatusHandle);
            if (commandsHandle.IsValid)
                battleUIManager.Close(commandsHandle);
        }
        alliesStatusHandle = default;
        enemiesStatusHandle = default;
        commandsHandle = default;
    }

    private void SpawnUnits(
        UnitConfig[] configs,
        Faction faction,
        Transform[] positions,
        List<BattleUnit> units,
        ref int tie)
    {
        if (configs == null)
            return;

        for (int i = 0; i < configs.Length; i++)
        {
            if (configs[i] == null)
                continue;

            if (configs[i].prefab == null || positions == null
                || i >= positions.Length || positions[i] == null)
            {
                Debug.LogError($"{faction} 站位 {i} 缺少模型或场景站位，跳过该角色。", this);
                continue;
            }

            // 这里的 i 代表在场地上的序号
            var u = BattleDataMaping.ToUnit(configs[i], faction, i, tie++);
            if (u == null) continue;

            units.Add(u);
            spawnedUnits.Add(u);

            GameObject obj = Instantiate(configs[i].prefab);
            var actor = obj.GetComponent<BattleUnitActor>();
            if (actor == null)
                actor = obj.AddComponent<BattleUnitActor>();
            obj.transform.position = positions[i].position;
            actor.Bind(u);

            if (obj.GetComponentInChildren<Collider>() == null)
                obj.AddComponent<CapsuleCollider>();

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
        if (battleFlow == null || battleFlow.Result != BattleResult.None
            || battleFlow.CurrentUnit == null)
            return;

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

        BattleUnit target = rayCasting != null ? rayCasting.selectedTarget : null;

        var action = new BattleAction {skill = ability, mainTarget = target};
        battleFlow.Execute(action);
        RefreshAll();
        ActingUntilPlayer();
    }

#endregion

    void RefreshAll()
    {
        if (battleUIManager != null
            && battleUIManager.TryGetPanel(alliesStatusHandle, out BattleAlliesStatusControll alliesPanel))
        {
            alliesPanel.Refresh(battleFlow?.CurrentUnit);
        }

        if (battleUIManager != null
            && battleUIManager.TryGetPanel(enemiesStatusHandle, out BattleEnemiesStatusPanel enemiesPanel))
        {
            enemiesPanel.Refresh(battleFlow?.CurrentUnit);
        }

        if (battleUIManager != null
            && battleUIManager.TryGetPanel(commandsHandle, out BattleUIController commandsPanel))
        {
            commandsPanel.Refresh();
        }
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
