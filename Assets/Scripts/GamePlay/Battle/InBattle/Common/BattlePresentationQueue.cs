using System;
using System.Collections.Generic;

namespace Battle.Logic
{
    public interface IBattleResolutionPresenter { void Present(ActionResolution resolution, Action completed); }

    /// <summary>Serial whole-action/scene segments, with cancellation and once-only continuations.</summary>
    public sealed class BattlePresentationQueue : IBattleResolutionPresenter
    {
        private readonly Queue<(Action<Action> play, Action done)> pending = new();
        private bool presenting;
        private bool pumping;
        private int generation;
        public bool IsPresenting => presenting || pending.Count > 0;
        public Action<BattleEffectRecord, Action> EntryPresenter { get; set; }
        public Action<ActionResolution, Action> ActionPresenter { get; set; }
        public event Action<ActionResolution> PresentationStarted;
        public event Action<ActionResolution> PresentationCompleted;

        public void Present(ActionResolution resolution, Action completed)
        {
            if (resolution == null) { completed?.Invoke(); return; }
            Enqueue(done =>
            {
                PresentationStarted?.Invoke(resolution);
                if (ActionPresenter != null) ActionPresenter(resolution, done);
                else PlayEntries(resolution.Record?.Effects, 0, generation, done);
            }, () =>
            {
                PresentationCompleted?.Invoke(resolution);
                // Events appended during this action are part of its presentation barrier.
                if (pending.Count == 0) completed?.Invoke();
                else Enqueue(done => done(), completed);
            });
        }
        public void Enqueue(Action<Action> play, Action completed = null)
        {
            pending.Enqueue((play, completed)); Pump();
        }
        public void Cancel() { generation++; pending.Clear(); presenting = false; }
        private void Pump()
        {
            if (pumping) return;
            pumping = true;
            try
            {
                while (!presenting && pending.Count > 0)
                {
                    var item = pending.Dequeue(); int session = generation; bool finished = false;
                    presenting = true;
                    item.play(() =>
                    {
                        if (finished || session != generation) return;
                        finished = true; presenting = false;
                        item.done?.Invoke(); Pump();
                    });
                }
            }
            finally { pumping = false; }
        }
        private void PlayEntries(IReadOnlyList<BattleEffectRecord> effects, int index, int session, Action done)
        {
            if (session != generation) return;
            if (effects == null || EntryPresenter == null) { done(); return; }
            if (index >= effects.Count) { done(); return; }
            bool consumed = false;
            EntryPresenter(effects[index], () =>
            {
                if (consumed || session != generation) return;
                consumed = true; PlayEntries(effects, index + 1, session, done);
            });
        }
    }
}
