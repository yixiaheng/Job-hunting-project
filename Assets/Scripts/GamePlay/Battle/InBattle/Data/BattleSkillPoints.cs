using System;
using UnityEngine;

namespace Battle.Logic
{
    /// <summary>本场战斗由我方共享的可变战技点资源。</summary>
    public sealed class BattleSkillPoints
    {
        public int Current { get; private set; }
        public int Maximum { get; private set; }
        public event Action<int, int> Changed;

        public BattleSkillPoints(int initial = 4, int maximum = 5)
        {
            Maximum = Mathf.Max(0, maximum);
            Current = Mathf.Clamp(initial, 0, Maximum);
        }

        public bool CanSpend(int amount) => amount >= 0 && Current >= amount;

        public bool TrySpend(int amount)
        {
            if (!CanSpend(amount))
                return false;
            Set(Current - amount, Maximum);
            return true;
        }

        public int Gain(int amount)
        {
            if (amount <= 0)
                return 0;
            int before = Current;
            Set(Current + amount, Maximum);
            return Current - before;
        }

        public void SetMaximum(int maximum, bool fillAddedCapacity = false)
        {
            int next = Mathf.Max(0, maximum);
            int current = fillAddedCapacity && next > Maximum
                ? Current + next - Maximum : Current;
            Set(current, next);
        }

        private void Set(int current, int maximum)
        {
            int nextMaximum = Mathf.Max(0, maximum);
            int nextCurrent = Mathf.Clamp(current, 0, nextMaximum);
            if (Current == nextCurrent && Maximum == nextMaximum)
                return;
            Current = nextCurrent;
            Maximum = nextMaximum;
            Changed?.Invoke(Current, Maximum);
        }
    }
}
