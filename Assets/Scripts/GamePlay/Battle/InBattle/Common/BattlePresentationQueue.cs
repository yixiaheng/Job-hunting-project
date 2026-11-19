using System;
using System.Collections.Generic;

namespace Battle.Logic
{
    public interface IBattleResolutionPresenter
    {
        void Present(ActionResolution resolution, Action completed);
    }

    /// <summary>
    /// 串行播放结算记录。当前默认即时完成，后续动画层只需替换 EntryPresenter。
    /// </summary>
    public sealed class BattlePresentationQueue : IBattleResolutionPresenter
    {
        private readonly Queue<(ActionResolution resolution, Action completed)> pending = new();
        private bool presenting;

        public Action<BattleEffectRecord, Action> EntryPresenter { get; set; }
        public event Action<ActionResolution> PresentationStarted;
        public event Action<ActionResolution> PresentationCompleted;

        public void Present(ActionResolution resolution, Action completed)
        {
            if (resolution == null)
            {
                completed?.Invoke();
                return;
            }
            pending.Enqueue((resolution, completed));
            if (!presenting)
                PlayNextResolution();
        }

        private void PlayNextResolution()
        {
            if (pending.Count == 0)
            {
                presenting = false;
                return;
            }

            presenting = true;
            var item = pending.Dequeue();
            PresentationStarted?.Invoke(item.resolution);
            PlayEntry(item, 0);
        }

        private void PlayEntry((ActionResolution resolution, Action completed) item, int index)
        {
            IReadOnlyList<BattleEffectRecord> effects = item.resolution.Record?.Effects;
            if (effects == null || index >= effects.Count)
            {
                PresentationCompleted?.Invoke(item.resolution);
                item.completed?.Invoke();
                PlayNextResolution();
                return;
            }

            if (EntryPresenter == null)
                PlayEntry(item, index + 1);
            else
                EntryPresenter(effects[index], () => PlayEntry(item, index + 1));
        }
    }
}
