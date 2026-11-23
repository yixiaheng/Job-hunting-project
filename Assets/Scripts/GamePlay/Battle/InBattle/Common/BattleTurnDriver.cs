using System;

namespace Battle.Logic
{
    public enum BattleTurnState
    {
        WaitingForPlayer,
        AutoDeciding,
        Resolving,
        Presenting,
        TransitioningWave,
        Completed,
        Faulted,
        Stopped
    }

    /// <summary>驱动玩家提交与敌方自动回合，不负责 UI、模型或战斗结果页面。</summary>
    public sealed class BattleTurnDriver
    {
        private readonly BattleFlow flow;
        private readonly IBattleResolutionPresenter presenter;
        private readonly Func<bool> tryTransitionWave;
        private bool stopRequested;
        private int generation;
        public Action<Action<bool>> TransitionWaveAsync { get; set; }
        public bool CanSubmit => !stopRequested && State == BattleTurnState.WaitingForPlayer;

        public BattleFlow Flow => flow;
        public BattleTurnState State { get; private set; } = BattleTurnState.Stopped;
        public ActionValidation LastFailure { get; private set; }
        public event Action<BattleTurnState> StateChanged;

        public BattleTurnDriver(BattleFlow flow,
            IBattleResolutionPresenter presenter = null,
            Func<bool> tryTransitionWave = null)
        {
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            this.presenter = presenter ?? new BattlePresentationQueue();
            this.tryTransitionWave = tryTransitionWave;
        }

        public void Start()
        {
            generation++;
            stopRequested = false;
            LastFailure = default;
            flow.Ready();
            ContinueUntilPlayer();
        }

        public BattleActionResult Submit(BattleAction action)
        {
            if (stopRequested || State == BattleTurnState.Stopped)
                return StoppedResult();

            if (!CanSubmit) return BusyResult();

            SetState(BattleTurnState.Resolving);
            BattleActionResult result = flow.TryExecute(action);
            if (!result.Succeeded)
            {
                LastFailure = result.Validation;
                SetState(flow.Result != BattleResult.None
                    ? BattleTurnState.Completed : BattleTurnState.WaitingForPlayer);
                return result;
            }

            Present(result, ContinueUntilPlayer);
            return result;
        }

        public BattleActionResult StepOnce()
        {
            if (stopRequested || State == BattleTurnState.Stopped)
                return StoppedResult();

            if (State == BattleTurnState.Presenting || State == BattleTurnState.Resolving
                || State == BattleTurnState.TransitioningWave) return BusyResult();

            SetState(BattleTurnState.Resolving);
            BattleActionResult result = flow.TryStep();
            if (result.Succeeded)
                Present(result, () => UpdateAfterStep(result));
            else
                UpdateAfterStep(result);
            return result;
        }

        public void Stop()
        {
            generation++;
            stopRequested = true;
            SetState(BattleTurnState.Stopped);
        }

        private void ContinueUntilPlayer()
        {
            if (stopRequested)
            {
                SetState(BattleTurnState.Stopped);
                return;
            }
            if (flow.Result != BattleResult.None)
            {
                SetState(BattleTurnState.Completed);
                return;
            }
            if (flow.IsWaveCleared)
            {
                SetState(BattleTurnState.TransitioningWave);
                if (TransitionWaveAsync != null)
                {
                    int session = generation; bool finished = false;
                    TransitionWaveAsync(success =>
                    {
                        if (finished || session != generation || stopRequested) return;
                        finished = true;
                        if (success) ContinueUntilPlayer(); else SetState(BattleTurnState.Faulted);
                    });
                    return;
                }
                if (tryTransitionWave == null || !tryTransitionWave())
                {
                    SetState(BattleTurnState.Faulted);
                    return;
                }
                ContinueUntilPlayer();
                return;
            }
            if (flow.CurrentUnit != null && flow.CurrentUnit.faction == Faction.OurSide)
            {
                SetState(BattleTurnState.WaitingForPlayer);
                return;
            }
            if (flow.CurrentUnit != null && flow.CurrentUnit.faction == Faction.Enemy)
            {
                SetState(BattleTurnState.AutoDeciding);
                BattleActionResult result = flow.TryStep();
                if (!result.Succeeded)
                {
                    LastFailure = result.Validation;
                    SetState(BattleTurnState.Faulted);
                    return;
                }
                Present(result, ContinueUntilPlayer);
                return;
            }
            SetState(BattleTurnState.Faulted);
        }

        private void UpdateAfterStep(BattleActionResult result)
        {
            if (stopRequested) return;
            if (result.Succeeded && flow.IsWaveCleared && flow.Result == BattleResult.None)
            { ContinueUntilPlayer(); return; }
            if (!result.Succeeded)
            {
                LastFailure = result.Validation;
                SetState(flow.Result != BattleResult.None
                    ? BattleTurnState.Completed : BattleTurnState.Faulted);
            }
            else if (flow.Result != BattleResult.None)
                SetState(BattleTurnState.Completed);
            else if (flow.CurrentUnit?.faction == Faction.OurSide)
                SetState(BattleTurnState.WaitingForPlayer);
            else
                SetState(BattleTurnState.AutoDeciding);
        }

        private void SetState(BattleTurnState state)
        {
            if (State == state)
                return;
            State = state;
            StateChanged?.Invoke(state);
        }

        private void Present(BattleActionResult result, Action completed)
        {
            if (!result.Succeeded || result.Resolution == null)
            {
                completed?.Invoke();
                return;
            }
            SetState(BattleTurnState.Presenting);
            int session = generation;
            bool finished = false;
            presenter.Present(result.Resolution, () =>
            {
                if (finished || session != generation || stopRequested) return;
                finished = true; completed?.Invoke();
            });
        }

        private BattleActionResult BusyResult()
        {
            LastFailure = new ActionValidation(ActionFailure.Resolving);
            return new BattleActionResult(LastFailure, flow.Result);
        }

        private BattleActionResult StoppedResult()
        {
            LastFailure = new ActionValidation(ActionFailure.DriverStopped);
            return new BattleActionResult(LastFailure, flow.Result);
        }
    }
}
