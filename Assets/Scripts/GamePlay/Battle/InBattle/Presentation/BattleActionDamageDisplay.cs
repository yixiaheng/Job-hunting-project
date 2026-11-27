using System;
using System.Collections.Generic;
using Battle.Logic;

namespace Battle.Presentation
{
    /// <summary>Presentation-time total, independent of the already completed logical resolution.</summary>
    public sealed class BattleActionDamageDisplay
    {
        private readonly HashSet<int> presented = new();
        public BattleActionRecord Action { get; private set; }
        public long Total { get; private set; }
        public bool Visible => Action != null && Total > 0;
        public event Action Changed;

        public void Begin(BattleActionRecord action)
        {
            Action = action;
            Total = 0;
            presented.Clear();
            Changed?.Invoke();
        }

        public void Present(int effectIndex, BattleEffectRecord effect)
        {
            if (Action == null || effectIndex < 0 || !presented.Add(effectIndex)) return;
            if (effect.Type != BattleEffectRecordType.Damage && effect.Type != BattleEffectRecordType.FollowUp) return;
            long damage = Math.Max(0, effect.Amount);
            if (effect.ShieldBefore >= 0 && effect.ShieldAfter >= 0)
                damage += Math.Max(0L, (long)effect.ShieldBefore - effect.ShieldAfter);
            if (damage == 0) return;
            Total += damage;
            Changed?.Invoke();
        }

        public void End()
        {
            Action = null;
            Total = 0;
            presented.Clear();
            Changed?.Invoke();
        }
    }
}
