using System;
using Battle.Events;
using Battle.Logic;
using Game;
using Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BattleSessionController : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private string partySceneName = "PartyScreen";
    [SerializeField] private string exitSceneName = "SampleScene";

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
        string battleScene = gameObject.scene.name;
        string destination = previous != null && previous.BattleSceneName == battleScene
            ? previous.ExitSceneName
            : exitSceneName;
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

        string target = message.ReformParty ? partySceneName : session.BattleSceneName;
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

        string target = BattleSessionContext.Current?.ExitSceneName ?? exitSceneName;
        Navigate(target, () =>
        {
            BattleLaunchContext.Clear();
            BattleSessionContext.Clear();
        });
    }

    private void Navigate(string target, Action prepare)
    {
        if (!Application.CanStreamedLevelBeLoaded(target))
        {
            Debug.LogError($"Cannot load scene {target}. Check the active build profile.", this);
            SetResultBusy(false);
            return;
        }

        BattleSession previous = BattleSessionContext.Current;
        navigating = true;
        SetResultBusy(true);
        try
        {
            prepare();
            SceneManager.LoadScene(target);
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
