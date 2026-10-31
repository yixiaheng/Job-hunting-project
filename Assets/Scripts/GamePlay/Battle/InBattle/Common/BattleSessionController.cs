using System;
using Battle.Events;
using Battle.Logic;
using Game;
using Game.UI;
using UnityEngine;

public sealed class BattleSessionController : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private SceneId partyScene = SceneId.Party;
    [SerializeField] private SceneId exitScene = SceneId.Sample;

    private IDisposable endedSubscription;
    private IDisposable retrySubscription;
    private IDisposable exitSubscription;
    private UIManager uiManager;
    private UIHandle resultHandle;
    private Guid endedBattleId;
    private bool navigating;

    private void OnEnable()
    {
        if (battleManager == null)
            battleManager = GetComponent<BattleManager>();

        if (battleManager == null)
        {
            Debug.LogError("BattleSessionController requires a BattleManager.", this);
            return;
        }

        uiManager = UIManager.Instance;
        endedSubscription = GameEvents.Battle.Subscribe<BattleEndedEvent>(HandleBattleEnded);
        retrySubscription = GameEvents.Battle.Subscribe<BattleRetryRequestedEvent>(HandleRetry);
        exitSubscription = GameEvents.Battle.Subscribe<BattleExitRequestedEvent>(HandleExit);
        battleManager.BattleStarted += HandleBattleStarted;

        if (battleManager.battleFlow != null)
        {
            HandleBattleStarted(battleManager.battleFlow);
            if (battleManager.battleFlow.Result != BattleResult.None)
            {
                HandleBattleEnded(new BattleEndedEvent(battleManager.battleFlow.BattleId,
                    battleManager.battleFlow.Result, battleManager.battleFlow.TurnCount));
            }
        }
    }

    private void HandleBattleStarted(BattleFlow flow)
    {
        CloseResult();
        endedBattleId = Guid.Empty;
        navigating = false;

        if (!battleManager.TryCreateLaunchData(out BattleLaunchData data, out string error))
        {
            Debug.LogError(error, this);
            return;
        }

        BattleSession previous = BattleSessionContext.Current;
        GameSceneManager scenes = GameSceneManager.Instance;
        if (scenes == null || scenes.Catalog == null
            || !scenes.Catalog.TryGetId(gameObject.scene.path, out SceneId battleScene))
        {
            Debug.LogError("当前战斗场景尚未在 SceneCatalog 中登记。", this);
            return;
        }

        SceneId destination = previous != null && previous.BattleScene == battleScene
            ? previous.ExitScene
            : exitScene;
        BattleSessionContext.Begin(data, battleScene, destination);
    }

    private void HandleBattleEnded(BattleEndedEvent message)
    {
        if (navigating || battleManager.battleFlow == null || message.Result == BattleResult.None
            || message.BattleId != battleManager.battleFlow.BattleId || endedBattleId == message.BattleId)
            return;

        if (uiManager == null)
        {
            Debug.LogError("Cannot open battle result: UIManager is unavailable.", this);
            return;
        }

        endedBattleId = message.BattleId;
        resultHandle = uiManager.Open(UIId.BattleResult,
            new BattleResultUIContext(message.BattleId, message.Result, message.TurnCount));
    }

    private bool AcceptRequest(Guid battleId)
    {
        return !navigating && battleId != Guid.Empty && battleId == endedBattleId
            && battleManager != null && battleManager.battleFlow != null
            && battleManager.battleFlow.BattleId == battleId
            && battleManager.battleFlow.Result != BattleResult.None;
    }

    private void HandleRetry(BattleRetryRequestedEvent message)
    {
        if (!AcceptRequest(message.BattleId))
            return;

        BattleSession session = BattleSessionContext.Current;
        if (session == null)
        {
            SetResultBusy(false);
            Debug.LogError("There is no battle session to retry.", this);
            return;
        }

        SceneId target = message.ReformParty ? partyScene : session.BattleScene;
        Navigate(target, () =>
        {
            BattleSessionContext.SetReforming(message.ReformParty);
            if (message.ReformParty)
                BattleLaunchContext.Clear();
            else
                BattleLaunchContext.Set(session.LaunchData);
        });
    }

    private void HandleExit(BattleExitRequestedEvent message)
    {
        if (!AcceptRequest(message.BattleId))
            return;

        SceneId target = BattleSessionContext.Current?.ExitScene ?? exitScene;
        Navigate(target, () =>
        {
            BattleLaunchContext.Clear();
            BattleSessionContext.Clear();
        });
    }

    private void Navigate(SceneId target, Action prepare)
    {
        GameSceneManager scenes = GameSceneManager.Instance;
        if (scenes == null)
        {
            Debug.LogError("场景加载器尚未初始化。", this);
            SetResultBusy(false);
            return;
        }

        if (!scenes.CanLoad(target, out string error))
        {
            Debug.LogError(error, this);
            SetResultBusy(false);
            return;
        }

        BattleSession previous = BattleSessionContext.Current;
        navigating = true;
        SetResultBusy(true);
        try
        {
            prepare();
            if (!scenes.TryLoad(target, out error))
                throw new InvalidOperationException(error);
        }
        catch (Exception exception)
        {
            BattleLaunchContext.Clear();
            BattleSessionContext.Restore(previous);
            navigating = false;
            SetResultBusy(false);
            Debug.LogException(exception, this);
        }
    }

    private void SetResultBusy(bool value)
    {
        if (uiManager != null && uiManager.TryGetPanel(resultHandle, out GameOverCanvasUI panel))
            panel.SetBusy(value);
    }

    private void CloseResult()
    {
        if (uiManager != null && resultHandle.IsValid)
            uiManager.Close(resultHandle);
        resultHandle = default;
    }

    private void OnDisable()
    {
        if (battleManager != null)
            battleManager.BattleStarted -= HandleBattleStarted;
        endedSubscription?.Dispose();
        retrySubscription?.Dispose();
        exitSubscription?.Dispose();
        endedSubscription = null;
        retrySubscription = null;
        exitSubscription = null;
        CloseResult();
        endedBattleId = Guid.Empty;
    }
}
