using System;
using System.Linq;
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
using Battle.Presentation;

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
    [SerializeField] private BattlePresentationCoordinator presentation;
    [SerializeField] private BattlefieldDirector battlefield;
    public BattlefieldDirector Battlefield => battlefield;
    public BattleSelectionState Selection { get; private set; }
    public BattleInterruptSelectionState InterruptSelection { get; private set; }
    public void ConfigureBattlefield(BattlefieldDirector value) => battlefield = value;
    public int EnemyCapacity => waveController?.CurrentCapacity ?? activeEnemies.Count;

    public Transform[] allyPos;
    public Transform[] enemyPos;

    public BattleFlow battleFlow { get; private set; }
    public BattleFlow Flow => battleFlow;
    public BattleTurnDriver TurnDriver => turnDriver;
    public IBattleActorLookup ActorLookup => actorRegistry;
    public BattlePresentationQueue PresentationQueue => presentationQueue;
    public BattleShotDirector CameraController => presentation != null ? presentation.cameraDirector : null;
    public BattlePresentationCoordinator Presentation => presentation;
    public Camera OutputCamera => battleCamera != null ? battleCamera : Camera.main;
    public bool CanAcceptCommands => turnDriver != null && turnDriver.CanSubmit
        && !(CameraController != null && CameraController.IsEditing)
        && !(presentationQueue?.IsPresenting ?? false) && !(battlefield?.IsMoving ?? false)
        && (GameSceneManager.Instance == null || (!GameSceneManager.Instance.IsLoading && !GameSceneManager.Instance.IsSceneSuspended(gameObject.scene)));
    public bool CanAcceptInterrupts => turnDriver != null && turnDriver.CanQueueUltimate
        && !(CameraController != null && CameraController.IsEditing)
        && (GameSceneManager.Instance == null || (!GameSceneManager.Instance.IsLoading
            && !GameSceneManager.Instance.IsSceneSuspended(gameObject.scene)));
    private BattleUnit DisplayedActor => turnDriver?.State == BattleTurnState.Presenting
        ? battleFlow?.LastResolution?.Record?.Caster : battleFlow?.CurrentUnit;
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
        presentationQueue = new BattlePresentationQueue();
        turnDriver = new BattleTurnDriver(battleFlow, presentationQueue, TryTransitionWave);
        turnDriver.StateChanged += OnTurnStateChanged;
        turnDriver.TransitionWaveAsync = TransitionWave;
        uiPresenter = new BattleUIPresenter(UIManager.Instance, gameObject.scene.handle);
        Camera activeBattleCamera = battleCamera != null ? battleCamera : Camera.main;
        if (presentation != null)
        {
            presentation.ConfigureBattlefield(battlefield);
            presentation.cameraDirector?.ConfigureBattlefield(battlefield, encounter != null ? encounter.CameraProfile : null);
            presentation.Bind(battleFlow, actorRegistry);
            presentation.HUDVisibilityChanged += uiPresenter.SetVisible;
            presentation.CommandsVisibilityChanged += uiPresenter.SetCommandsVisible;
            presentationQueue.ActionPresenter = presentation.Present;
        }
        Selection = new BattleSelectionState(battleFlow);
        Selection.Changed += OnSelectionChanged;
        InterruptSelection = new BattleInterruptSelectionState(battleFlow);
        uiPresenter.Open(
            units.FindAll(unit => unit.faction == Faction.OurSide),
            activeEnemies,
            actorRegistry,
            activeBattleCamera,
            this,
            battleFlow);

        battleFlow.Ready();
        BattleStarted?.Invoke(battleFlow);
        uiPresenter.Refresh(battleFlow.CurrentUnit);
        StartCoroutine(StartAfterHUDReady());
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
                if (config.prefab == null || (battlefield == null && (allyPos == null || position >= allyPos.Length
                    || allyPos[position] == null)))
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
            GameObject temporaryAnchor = null;
            if (battlefield != null && battlefield.Layout != null)
            {
                temporaryAnchor = new GameObject("SpawnPose");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(temporaryAnchor, gameObject.scene);
                var pose = battlefield.GetSpawnPose(faction, position, configs.Length, FormationPreset);
                temporaryAnchor.transform.SetPositionAndRotation(pose.position, pose.rotation);
                anchor = temporaryAnchor.transform;
            }
            BattleUnit unit = unitFactory.Create(config, faction, position, tieBreaker);
            if (unit == null) { if (temporaryAnchor != null) Destroy(temporaryAnchor); continue; }

            if (!actorRegistry.TrySpawn(config, unit, anchor, out var actor, out string error))
            {
                if (temporaryAnchor != null) Destroy(temporaryAnchor);
                unitFactory.Release(unit);
                Debug.LogError(error, this);
                continue;
            }
            if (temporaryAnchor != null) Destroy(temporaryAnchor);
            battlefield?.Register(actor, configs.Length, FormationPreset);

            output.Add(unit);
            tieBreaker++;
        }
    }

    private void TearDownBattle()
    {
        if (Selection != null) Selection.Changed -= OnSelectionChanged;
        Selection = null;
        InterruptSelection = null;
        turnDriver?.Stop();
        StopAllCoroutines();
        presentationQueue?.Cancel();
        if (presentation != null)
        {
            presentation.Cancel();
            if (uiPresenter != null) presentation.HUDVisibilityChanged -= uiPresenter.SetVisible;
            if (uiPresenter != null) presentation.CommandsVisibilityChanged -= uiPresenter.SetCommandsVisible;
        }
        uiPresenter?.Close();
        actorRegistry?.Clear();
        battlefield?.Clear();
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
        { battlefield?.Unregister(unit); actorRegistry.Remove(unit); }

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
        presentation?.Register(next);
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
        uiPresenter?.Refresh(DisplayedActor);
        if (!result.Succeeded)
            Debug.Log(result.Result == BattleResult.None
                ? "自动推进已停止，战斗尚未结束。"
                : "战斗已结束: " + result.Result, this);
    }

    public void ChooseBasic() => ChooseAbility(SkillKind.Basic);
    public void ChooseSkill() => ChooseAbility(SkillKind.Skill);
    public void ChooseUlt()
    {
        BattleUnit caster = battleFlow?.CurrentUnit;
        if (caster != null)
            QueueUltimate(caster, rayCasting != null ? rayCasting.selectedTarget : null);
    }

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
        if (kind == SkillKind.Ultimate)
        {
            ChooseUlt();
            return;
        }
        if (!CanAcceptCommands || battleFlow?.CurrentUnit == null
            || battleFlow.CurrentUnit.faction != Faction.OurSide)
            return;

        if (Selection == null) return;
        if (Selection.Skill == null || Selection.Skill.kind != kind) { Selection.SelectSkill(kind); return; }
        SubmitSelection();
    }

    public void SelectTarget(BattleUnit target) { if (CanAcceptCommands) Selection?.SelectTarget(target); }
    public void SubmitSelection()
    {
        if (!CanAcceptCommands || Selection?.Skill == null) return;
        var previous = Selection.Target;
        Selection.Refresh();
        if (previous != Selection.Target || !Selection.Preview.Validation.IsValid) return;
        BattleActionResult result = turnDriver.Submit(Selection.CreateAction());
        uiPresenter?.Refresh(DisplayedActor);
        if (!result.Succeeded)
            Debug.LogWarning(result.Validation.Message, this);
        if (turnDriver.State == BattleTurnState.Faulted)
            Debug.LogError("自动回合推进失败：" + turnDriver.LastFailure.Message, this);
    }

    public bool CanQueueUltimate(BattleUnit caster)
        => CanAcceptInterrupts && PreviewUltimate(caster,
            rayCasting != null ? rayCasting.selectedTarget : null).Validation.IsValid;

    public BattleActionPreview PreviewUltimate(BattleUnit caster,
        BattleUnit target = null)
    {
        SkillConfig skill = caster?.skills?.FirstOrDefault(candidate => candidate != null
            && candidate.kind == SkillKind.Ultimate);
        return battleFlow?.PreviewAction(new BattleAction
        {
            caster = caster,
            skill = skill,
            mainTarget = target
        }, BattleActionOrigin.Ultimate);
    }

    public ActionValidation QueueUltimate(BattleUnit caster,
        BattleUnit target = null)
    {
        if (!CanAcceptInterrupts || InterruptSelection == null)
            return new ActionValidation(ActionFailure.Resolving);
        InterruptSelection.Begin(caster, target);
        BattleAction action = InterruptSelection.CreateAction();
        ActionValidation validation = turnDriver.QueueUltimate(action, out _);
        InterruptSelection.Clear();
        if (validation.IsValid && turnDriver != null
            && (turnDriver.State == BattleTurnState.Resolving
                || turnDriver.State == BattleTurnState.Presenting))
            uiPresenter?.AdvanceAndHoldActionTimeline(DisplayedActor);
        uiPresenter?.Refresh(DisplayedActor);
        if (!validation.IsValid)
            Debug.LogWarning(validation.Message, this);
        return validation;
    }

    public bool CancelQueuedAction(long requestId)
    {
        bool cancelled = battleFlow != null && battleFlow.CancelQueuedAction(requestId);
        if (cancelled)
        {
            if (turnDriver != null
                && (turnDriver.State == BattleTurnState.Resolving
                    || turnDriver.State == BattleTurnState.Presenting))
                uiPresenter?.AdvanceAndHoldActionTimeline(DisplayedActor);
            uiPresenter?.Refresh(DisplayedActor);
        }
        return cancelled;
    }

    private System.Collections.IEnumerator StartAfterHUDReady()
    {
        // UIManager may instantiate its panels asynchronously. Never attack behind the loading HUD.
        while (uiPresenter != null && !uiPresenter.IsReady) yield return null;
        if (turnDriver == null) yield break;
        if (presentation != null)
            presentationQueue.Enqueue(done => presentation.PresentEvent("BattleStart", activeEnemies, done), turnDriver.Start);
        else turnDriver.Start();
    }

    private void TransitionWave(Action<bool> completed)
    {
        if (!TryTransitionWave()) { completed(false); return; }
        if (presentation != null)
            presentationQueue.Enqueue(done => presentation.PresentEvent("WaveEnter", activeEnemies, done), () => completed(true));
        else completed(true);
    }

    private void OnTurnStateChanged(BattleTurnState state)
    {
        if (state == BattleTurnState.Resolving) rayCasting?.ClearSelection();
        switch (state)
        {
            case BattleTurnState.Resolving:
                uiPresenter?.HoldActionTimeline(battleFlow?.CurrentUnit);
                break;
            case BattleTurnState.AutoDeciding:
                uiPresenter?.AdvanceAndHoldActionTimeline(battleFlow?.CurrentUnit);
                break;
            case BattleTurnState.Presenting:
                uiPresenter?.AdvanceAndHoldActionTimeline(DisplayedActor);
                break;
            default:
                uiPresenter?.ReleaseActionTimeline(DisplayedActor);
                break;
        }
        if (state == BattleTurnState.WaitingForPlayer)
        { battlefield?.SetReady(battleFlow.CurrentUnit); Selection?.Begin(battleFlow.CurrentUnit); }
        uiPresenter?.Refresh(DisplayedActor);
        if (state == BattleTurnState.Completed)
        {
            if (presentation != null) QueuePresentationEvent(battleFlow.Result == BattleResult.AllyWin ? "Victory" : "Defeat",
                battleFlow.Units.Where(u => u.IsActive).ToList(), () => HandleBattleEnded(battleFlow.Result));
            else HandleBattleEnded(battleFlow.Result);
        }
    }

    public string FormationPreset => !string.IsNullOrEmpty(waveController?.CurrentFormation) ? waveController.CurrentFormation : encounter != null ? encounter.DefaultFormationPreset : null;
    private void OnSelectionChanged()
    {
        if (Selection == null) return;
        CameraController?.SetContext(BattleCameraContext.FromSelection(Selection));
        rayCasting?.DisplaySelection(Selection.Target, Selection.Preview?.Targets);
        uiPresenter?.Refresh(DisplayedActor);
    }

    /// <summary>Extend from the combat/presentation orchestrator; logical summon/revive happens before this call.</summary>
    public void QueuePresentationEvent(string eventId, IReadOnlyList<BattleUnit> subjects, Action completed = null)
    {
        if (battleFlow == null || presentationQueue == null) return;
        if (presentation == null) { completed?.Invoke(); return; }
        var snapshot = new List<BattleUnit>();
        if (subjects != null) foreach (var unit in subjects)
        {
            if (unit == null || unit.BattleId != battleFlow.BattleId)
                throw new ArgumentException("Presentation subjects must belong to the current battle.", nameof(subjects));
            snapshot.Add(unit);
        }
        presentation.Register(snapshot);
        presentationQueue.Enqueue(done => presentation.PresentEvent(eventId, snapshot, done), () =>
        { if(turnDriver?.State == BattleTurnState.WaitingForPlayer) OnSelectionChanged(); uiPresenter?.Refresh(battleFlow?.CurrentUnit); completed?.Invoke(); });
        uiPresenter?.Refresh(battleFlow?.CurrentUnit);
    }
}
