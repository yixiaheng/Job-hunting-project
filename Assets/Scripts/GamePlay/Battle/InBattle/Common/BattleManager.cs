using System;
using System.Collections.Generic;
using Battle.Events;
using Battle.Logic;
using Game;
using Game.UI;
using Game.Inventory;
using Game.Characters;
using Battle.SaveData;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>战斗场景入口：组装本场服务，并保留场景与 UI 使用的兼容 API。</summary>
[DefaultExecutionOrder(-50)]
public class BattleManager : MonoBehaviour, IBattleCommandSource
{
    public static BattleManager Instance { get; private set; }

    public MouseRayCasting rayCasting;

    // Inspector allies are only used when the scene has no launch snapshot.
    public UnitConfig[] allies;
    public UnitConfig[] enemies;

    [SerializeField] private BattleEncounterConfig encounter;
    [SerializeField] private string encounterId;
    [SerializeField] private List<ItemAmount> victoryRewards = new();

    [SerializeField] private Camera battleCamera;
    [SerializeField] private BattleCameraController battleCameraController;

    public Transform[] allyPos;
    public Transform[] enemyPos;

    public BattleFlow battleFlow { get; private set; }
    public BattleFlow Flow => battleFlow;
    public BattleTurnDriver TurnDriver => turnDriver;
    public IBattleActorLookup ActorLookup => actorRegistry;
    public BattlePresentationQueue PresentationQueue => presentationQueue;
    public BattleCameraController CameraController => battleCameraController;
    public int CurrentWave => waveController?.CurrentWaveNumber ?? 0;
    public int WaveCount => waveController?.WaveCount ?? 0;
    public string EncounterId => !string.IsNullOrWhiteSpace(encounter?.id)
        ? encounter.id : !string.IsNullOrWhiteSpace(encounterId) ? encounterId : gameObject.scene.path;
    public IReadOnlyList<ItemAmount> VictoryRewards => encounter != null
        ? encounter.victoryRewards : victoryRewards;

    private UnitConfig[] launchAllies;
    private BattleLaunchData launchSnapshot;
    private bool resultPublished;
    private BattleUnitFactory unitFactory;
    private BattleActorRegistry actorRegistry;
    private BattleTurnDriver turnDriver;
    private BattleUIPresenter uiPresenter;
    private BattlePresentationQueue presentationQueue;
    private BattleWaveController waveController;
    private List<BattleUnit> activeEnemies = new();
    private int nextTieBreaker;

    public event Action<BattleFlow> BattleStarted;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (BattleLaunchContext.TryConsume(out BattleLaunchData data))
        {
            launchSnapshot = data;
            launchAllies = data.CopyAllies();
        }
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

        TearDownBattle();
        Instance = null;
    }

    public void StartBattle()
    {
        if (battleFlow != null && battleFlow.IsExecuting)
            return;
        if (UIManager.Instance == null)
        {
            Debug.LogError("请先初始化 UIManager，再开始战斗。", this);
            return;
        }

        UnitConfig[] allyConfigs = launchAllies ?? allies;
        if (!ValidateAllies(allyConfigs))
            return;
        if (launchSnapshot == null
            && !BattleLaunchData.TryCreate(allyConfigs, out launchSnapshot, out string launchError))
        {
            Debug.LogError(launchError, this);
            return;
        }

        TearDownBattle();
        resultPublished = false;
        unitFactory = new BattleUnitFactory(launchSnapshot);
        actorRegistry = new BattleActorRegistry(rayCasting);
        waveController = new BattleWaveController(enemies, encounter);

        var units = new List<BattleUnit>();
        nextTieBreaker = 0;
        CreateUnits(allyConfigs, Faction.OurSide, allyPos, units, ref nextTieBreaker);
        if (!waveController.TryTakeNext(out UnitConfig[] firstWave))
        {
            TearDownBattle();
            Debug.LogError("战斗没有配置有效的敌人波次。", this);
            return;
        }
        activeEnemies = new List<BattleUnit>();
        CreateUnits(firstWave, Faction.Enemy, enemyPos, activeEnemies, ref nextTieBreaker);
        units.AddRange(activeEnemies);
        if (units.Count == 0 || activeEnemies.Count == 0)
        {
            TearDownBattle();
            Debug.LogError("没有单位：把 UnitConfig 拖到 Allies / Enemies", this);
            return;
        }

        int maximumSkillPoints = encounter != null ? encounter.maximumSkillPoints : 5;
        int initialSkillPoints = encounter != null ? encounter.initialSkillPoints : 4;
        battleFlow = new BattleFlow(units, new EnemyActionDecider(),
            initialSkillPoints, maximumSkillPoints);
        battleFlow.SetPendingWave(waveController.HasNext);
        battleFlow.GameOverEvent += HandleBattleEnded;
        presentationQueue = new BattlePresentationQueue();
        turnDriver = new BattleTurnDriver(battleFlow, presentationQueue, TryTransitionWave);
        uiPresenter = new BattleUIPresenter(UIManager.Instance, gameObject.scene.handle);
        Camera activeBattleCamera = battleCamera != null ? battleCamera : Camera.main;
        if (battleCameraController == null && activeBattleCamera != null)
        {
            battleCameraController = activeBattleCamera.GetComponent<BattleCameraController>();
            if (battleCameraController == null)
                battleCameraController = activeBattleCamera.gameObject.AddComponent<BattleCameraController>();
        }
        battleCameraController?.ConfigureDefaultShots(allyPos, enemyPos);
        battleCameraController?.CutToDefault();
        uiPresenter.Open(
            units.FindAll(unit => unit.faction == Faction.OurSide),
            activeEnemies,
            actorRegistry,
            activeBattleCamera,
            this);

        battleFlow.Ready();
        BattleStarted?.Invoke(battleFlow);
        uiPresenter.Refresh(battleFlow.CurrentUnit);
        turnDriver.Start();
        uiPresenter.Refresh(battleFlow.CurrentUnit);

        if (turnDriver.State == BattleTurnState.Faulted)
            Debug.LogError("自动回合推进失败：" + turnDriver.LastFailure.Message, this);
        else
            Debug.Log("战斗已创建。", this);
    }

    public bool TryCreateLaunchData(out BattleLaunchData data, out string error)
    {
        if (launchSnapshot != null)
        {
            data = launchSnapshot;
            error = null;
            return true;
        }
        return BattleLaunchData.TryCreate(launchAllies ?? allies, out data, out error);
    }

    private void HandleBattleEnded(BattleResult result)
    {
        if (resultPublished || battleFlow == null || result == BattleResult.None)
            return;

        resultPublished = true;
        uiPresenter?.Refresh(battleFlow.CurrentUnit);
        BattleRewardReceipt receipt = null;
        string rewardError = null;
        if (result == BattleResult.AllyWin)
        {
            PlayerRuntime runtime = PlayerRuntime.Instance;
            if (runtime == null || !runtime.TryGrantBattleRewards(EncounterId,
                battleFlow.TurnCount, VictoryRewards, out receipt, out rewardError))
            {
                rewardError ??= "玩家档案不可用，奖励未写入。";
                Debug.LogError("战斗奖励结算失败：" + rewardError, this);
            }
        }
        GameEvents.Battle.Publish(new BattleEndedEvent(
            battleFlow.BattleId, result, battleFlow.TurnCount, receipt, rewardError));
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

    private void CreateUnits(UnitConfig[] configs, Faction faction, Transform[] positions,
        List<BattleUnit> output, ref int tieBreaker)
    {
        if (configs == null)
            return;

        for (int position = 0; position < configs.Length; position++)
        {
            UnitConfig config = configs[position];
            if (config == null)
                continue;

            Transform anchor = positions != null && position < positions.Length
                ? positions[position] : null;
            BattleUnit unit = unitFactory.Create(config, faction, position, tieBreaker);
            if (unit == null)
                continue;

            if (!actorRegistry.TrySpawn(config, unit, anchor, out _, out string error))
            {
                unitFactory.Release(unit);
                Debug.LogError(error, this);
                continue;
            }

            output.Add(unit);
            tieBreaker++;
        }
    }

    private void TearDownBattle()
    {
        turnDriver?.Stop();
        uiPresenter?.Close();
        if (battleFlow != null)
            battleFlow.GameOverEvent -= HandleBattleEnded;
        actorRegistry?.Clear();
        unitFactory?.ReleaseAll();

        uiPresenter = null;
        presentationQueue = null;
        turnDriver = null;
        actorRegistry = null;
        unitFactory = null;
        waveController = null;
        activeEnemies = new List<BattleUnit>();
        nextTieBreaker = 0;
        battleFlow = null;
    }

    private bool TryTransitionWave()
    {
        if (battleFlow == null || waveController == null
            || !waveController.TryTakeNext(out UnitConfig[] nextWave))
            return false;

        var previous = activeEnemies;
        foreach (BattleUnit unit in previous)
            actorRegistry.Remove(unit);

        var next = new List<BattleUnit>();
        CreateUnits(nextWave, Faction.Enemy, enemyPos, next, ref nextTieBreaker);
        if (next.Count == 0 || !battleFlow.AddUnits(next))
        {
            foreach (BattleUnit unit in next)
            {
                actorRegistry.Remove(unit);
                unitFactory.Release(unit);
            }
            return false;
        }

        activeEnemies = next;
        battleFlow.SetPendingWave(waveController.HasNext);
        uiPresenter?.ReplaceEnemies(previous, next);
        uiPresenter?.Refresh(battleFlow.CurrentUnit);
        Debug.Log($"进入第 {waveController.CurrentWaveNumber}/{waveController.WaveCount} 波。", this);
        return true;
    }

    [Button]
    private void StepOnce()
    {
        if (turnDriver == null)
        {
            Debug.LogError("先点 StartBattle", this);
            return;
        }

        BattleActionResult result = turnDriver.StepOnce();
        uiPresenter?.Refresh(battleFlow?.CurrentUnit);
        if (!result.Succeeded)
            Debug.Log(result.Result == BattleResult.None
                ? "自动推进已停止，战斗尚未结束。"
                : "战斗已结束: " + result.Result, this);
    }

    public void ChooseBasic() => ChooseAbility(SkillKind.Basic);
    public void ChooseSkill() => ChooseAbility(SkillKind.Skill);
    public void ChooseUlt() => ChooseAbility(SkillKind.Ultimate);

    public BattleAction CreateAbilityAction(SkillKind kind)
    {
        BattleUnit unit = battleFlow?.CurrentUnit;
        SkillConfig ability = null;
        if (unit?.skills != null)
        {
            foreach (SkillConfig skill in unit.skills)
            {
                if (skill != null && skill.kind == kind)
                {
                    ability = skill;
                    break;
                }
            }
        }

        BattleUnit target = rayCasting != null ? rayCasting.selectedTarget : null;
        return new BattleAction { caster = unit, skill = ability, mainTarget = target };
    }

    public void ChooseAbility(SkillKind kind)
    {
        if (turnDriver == null || battleFlow?.CurrentUnit == null
            || battleFlow.CurrentUnit.faction != Faction.OurSide)
            return;

        BattleActionResult result = turnDriver.Submit(CreateAbilityAction(kind));
        uiPresenter?.Refresh(battleFlow.CurrentUnit);
        if (!result.Succeeded)
            Debug.LogWarning(result.Validation.Message, this);
        if (turnDriver.State == BattleTurnState.Faulted)
            Debug.LogError("自动回合推进失败：" + turnDriver.LastFailure.Message, this);
    }
}
