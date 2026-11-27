using System;
using System.Collections.Generic;

namespace Battle.Logic
{
    /// <summary>
    /// 战斗行动的来源：正常回合/大招/追击/反击/额外回合/剧情
    /// </summary>
    public enum BattleActionOrigin
    {
        NormalTurn,
        Ultimate,
        FollowUp,
        Counter,
        ExtraTurn,
        Scripted
    }
    /// <summary>
    /// 战斗行动的回合资源消耗：正常、自由/不占回合、额外回合
    /// </summary>
    public enum BattleActionExecutionPolicy
    {
        NormalTurn,
        FreeAction,
        ExtraTurn
    }
    /// <summary>
    /// 目标回退策略枚举，用来决定 当技能/效果原本要选的目标无效或不可选时，该怎么办
    /// </summary>
    public enum BattleTargetFallback
    {
        AutoSelectFirst,    //自动选择第一个有效目标
        Cancel              //取消本次释放
    }

    public sealed class BattleActionRequest
    {
        internal BattleActionRequest(long requestId, long enqueueSequence,
            BattleAction action, BattleActionOrigin origin,
            BattleActionExecutionPolicy policy, int priority,
            BattleTargetFallback targetFallback, int reservedEnergy)
        {
            RequestId = requestId;
            EnqueueSequence = enqueueSequence;
            Action = action;
            Origin = origin;
            Policy = policy;
            Priority = priority;
            TargetFallback = targetFallback;
            ReservedEnergy = reservedEnergy;
        }

        public long RequestId { get; }
        public long EnqueueSequence { get; }
        public BattleAction Action { get; }
        public BattleActionOrigin Origin { get; }
        public BattleActionExecutionPolicy Policy { get; }
        public int Priority { get; }
        public BattleTargetFallback TargetFallback { get; }
        public int ReservedEnergy { get; }
        public bool IsQueuedAction => Origin != BattleActionOrigin.NormalTurn;
        internal bool ReservationConsumed { get; set; }
        internal bool ReservationReleased { get; set; }
    }

    //战斗行动插队
    public sealed class BattleActionScheduler
    {
        private readonly List<BattleActionRequest> pending = new();
        private readonly Dictionary<BattleUnit, int> reservedEnergy = new();
        private long nextRequestId = 1;
        private long nextSequence = 1;

        public event Action Changed;
        public int PendingCount => pending.Count;
        public IReadOnlyList<BattleActionRequest> Pending => pending.AsReadOnly();
        public BattleActionRequest ActiveRequest { get; private set; }

        public int GetReservedEnergy(BattleUnit unit)
            => unit != null && reservedEnergy.TryGetValue(unit, out int value) ? value : 0;

        public bool HasPending(BattleUnit caster, SkillConfig skill,
            BattleActionOrigin origin = BattleActionOrigin.Ultimate)
        {
            foreach (BattleActionRequest request in pending)
                if (request.Origin == origin
                    && ReferenceEquals(request.Action?.caster, caster)
                    && ReferenceEquals(request.Action?.skill, skill))
                    return true;
            if (ActiveRequest != null && ActiveRequest.Origin == origin
                && ReferenceEquals(ActiveRequest.Action?.caster, caster)
                && ReferenceEquals(ActiveRequest.Action?.skill, skill))
                return true;
            return false;
        }

        public BattleActionRequest Enqueue(BattleAction action, BattleActionOrigin origin,
            BattleActionExecutionPolicy policy, int priority,
            BattleTargetFallback fallback, int energyReservation)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var snapshot = new BattleAction
            {
                caster = action.caster,
                skill = action.skill,
                mainTarget = action.mainTarget
            };
            var request = new BattleActionRequest(nextRequestId++, nextSequence++,
                snapshot, origin, policy, priority, fallback,
                Math.Max(0, energyReservation));
            pending.Add(request);
            SortPending();
            Reserve(request);
            Changed?.Invoke();
            return request;
        }

        public BattleActionRequest CreateImmediate(BattleAction action,
            BattleActionOrigin origin = BattleActionOrigin.NormalTurn,
            BattleActionExecutionPolicy policy = BattleActionExecutionPolicy.NormalTurn)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            return new BattleActionRequest(0, 0, new BattleAction
            {
                caster = action.caster,
                skill = action.skill,
                mainTarget = action.mainTarget
            }, origin, policy, 0, BattleTargetFallback.AutoSelectFirst, 0);
        }

        public bool TryTakeNext(out BattleActionRequest request)
        {
            if (ActiveRequest != null || pending.Count == 0)
            {
                request = null;
                return false;
            }

            request = pending[0];
            pending.RemoveAt(0);
            ActiveRequest = request;
            Changed?.Invoke();
            return true;
        }

        public void ConsumeReservation(BattleActionRequest request)
        {
            if (request == null || request.ReservationReleased)
                return;
            Release(request);
            request.ReservationConsumed = true;
            request.ReservationReleased = true;
            Changed?.Invoke();
        }

        public void Complete(BattleActionRequest request)
        {
            if (request == null)
                return;
            if (!request.ReservationReleased)
            {
                Release(request);
                request.ReservationReleased = true;
            }
            if (ReferenceEquals(ActiveRequest, request))
                ActiveRequest = null;
            Changed?.Invoke();
        }

        public bool Cancel(long requestId)
        {
            int index = pending.FindIndex(request => request.RequestId == requestId);
            if (index < 0)
                return false;
            BattleActionRequest request = pending[index];
            pending.RemoveAt(index);
            if (!request.ReservationReleased)
            {
                Release(request);
                request.ReservationReleased = true;
            }
            Changed?.Invoke();
            return true;
        }

        public void CancelAll()
        {
            if (pending.Count == 0 && reservedEnergy.Count == 0
                && ActiveRequest == null)
                return;
            foreach (BattleActionRequest request in pending)
                request.ReservationReleased = true;
            if (ActiveRequest != null)
                ActiveRequest.ReservationReleased = true;
            pending.Clear();
            reservedEnergy.Clear();
            ActiveRequest = null;
            Changed?.Invoke();
        }

        private void Reserve(BattleActionRequest request)
        {
            BattleUnit caster = request?.Action?.caster;
            if (caster == null || request.ReservedEnergy <= 0)
                return;
            reservedEnergy[caster] = GetReservedEnergy(caster) + request.ReservedEnergy;
        }

        private void Release(BattleActionRequest request)
        {
            BattleUnit caster = request?.Action?.caster;
            if (caster == null || request.ReservedEnergy <= 0)
                return;
            int remaining = GetReservedEnergy(caster) - request.ReservedEnergy;
            if (remaining > 0)
                reservedEnergy[caster] = remaining;
            else
                reservedEnergy.Remove(caster);
        }

        private void SortPending()
        {
            pending.Sort((left, right) =>
            {
                int priority = right.Priority.CompareTo(left.Priority);
                return priority != 0
                    ? priority
                    : left.EnqueueSequence.CompareTo(right.EnqueueSequence);
            });
        }
    }
}
