using System.Collections.Generic;
using System;
using System.Data;
using Unity.VisualScripting;
using UnityEngine;
namespace Battle.Logic
{
    public readonly struct BattleActionOrderEntry
    {
        public BattleActionOrderEntry(BattleUnit unit, double absoluteActionTime,
            int remainingActionValue, bool isCurrent)
            : this(-(unit?.tieBreaker ?? 0) - 1L, unit, absoluteActionTime,
                remainingActionValue, isCurrent, BattleActionOrigin.NormalTurn, 0)
        {
        }

        public BattleActionOrderEntry(long entryId, BattleUnit unit,
            double absoluteActionTime, int remainingActionValue, bool isCurrent,
            BattleActionOrigin origin, long requestId)
        {
            EntryId = entryId;
            Unit = unit;
            AbsoluteActionTime = absoluteActionTime;
            RemainingActionValue = remainingActionValue;
            IsCurrent = isCurrent;
            Origin = origin;
            RequestId = requestId;
        }

        public long EntryId { get; }
        public BattleUnit Unit { get; }
        public double AbsoluteActionTime { get; }
        public int RemainingActionValue { get; }
        public bool IsCurrent { get; }
        public BattleActionOrigin Origin { get; }
        public long RequestId { get; }
        public bool IsQueuedAction => RequestId > 0;
    }

    public interface IBattleActionOrderSource
    {
        IReadOnlyList<BattleActionOrderEntry> GetActionOrderSnapshot(BattleUnit displayedActor = null);
    }

    public class ActionQueue
    {
        public const double BaseActionValue = 10000d;

        private readonly List<BattleUnit> units = new();
        public double CurrentTime {get; private set;}

        public ActionQueue(IEnumerable<BattleUnit> allUnits)
        {
            //逐个添加
            foreach(var unit in allUnits)
            {
                Add(unit);
            }
        }

        public void Add(BattleUnit unit)       
        {
            if(unit != null)
            {
                if(unit.speed <= 0)
                    return;
                if (units.Contains(unit))
                    return;
                units.Add(unit);
                unit.nextActionTime = CurrentTime + BaseActionValue / unit.speed;
            }
        }
        public BattleUnit PeekNext()           
        {
            BattleUnit best = null; //最早行动的
            foreach(var u in units)
            {
                if(!u.IsActive) continue;
                if(best == null || u.nextActionTime < best.nextActionTime)
                    best = u;
                else if(u.nextActionTime == best.nextActionTime &&        
                         u.tieBreaker < best.tieBreaker)    
                    best = u;

            }
            return best;
        }

        //开始这个unit的回合
        public void BeginTurn(BattleUnit u)     
        {
            CurrentTime = u.nextActionTime;
        }

        /// <summary>
        /// 拉条
        /// </summary>
        /// <param name="unit">对象</param>
        /// <param name="ratio">拉条进度 XX% </param>
        public void ModifyAction(BattleUnit unit, float ratio)
        {
            if (unit == null) return;
            float remaining = (float)(unit.nextActionTime - CurrentTime);
            unit.nextActionTime = CurrentTime + remaining * Mathf.Max(0f, 1f - ratio);
        }

        //结束他的回合(重新设置他的下次行动时间)
        public void EndTurn(BattleUnit u)     
        {
            int spd = Mathf.Max(1, u.GetStat(StatType.Speed));
            u.nextActionTime = CurrentTime + BaseActionValue / spd;
        }

        //获取当前行动条的映射，用于UI
        public IReadOnlyList<BattleActionOrderEntry> GetOrderSnapshot(BattleUnit displayedActor = null)
        {
            var active = new List<BattleUnit>();
            foreach (BattleUnit unit in units)
                if (unit != null && unit.IsActive)
                    active.Add(unit);

            bool hasDisplayedActor = displayedActor != null && active.Contains(displayedActor);
            active.Sort((left, right) =>
            {
                if (hasDisplayedActor)
                {
                    if (ReferenceEquals(left, displayedActor)) return -1;
                    if (ReferenceEquals(right, displayedActor)) return 1;
                }

                int time = left.nextActionTime.CompareTo(right.nextActionTime);
                return time != 0 ? time : left.tieBreaker.CompareTo(right.tieBreaker);
            });

            double origin = CurrentTime;
            if (hasDisplayedActor)
                origin = Math.Max(origin, displayedActor.nextActionTime);

            var snapshot = new List<BattleActionOrderEntry>(active.Count);
            foreach (BattleUnit unit in active)
            {
                bool isCurrent = hasDisplayedActor && ReferenceEquals(unit, displayedActor);
                double remaining = Math.Max(0d, unit.nextActionTime - origin);
                int actionValue = isCurrent ? 0 : remaining >= int.MaxValue
                    ? int.MaxValue : (int)Math.Ceiling(remaining);
                snapshot.Add(new BattleActionOrderEntry(
                    unit, unit.nextActionTime, actionValue, isCurrent));
            }
            return snapshot.AsReadOnly();
        }

    }
}

