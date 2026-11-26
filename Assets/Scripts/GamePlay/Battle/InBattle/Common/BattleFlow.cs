using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Battle.Logic
{
    public enum BattleResult { None, AllyWin, EnemyWin, Draw }

    public class BattleFlow : IBattleActionOrderSource
    {
        #region Fields
        public const int MaxTurns = 500;
        private readonly List<BattleUnit> units;
        private readonly ActionQueue queue;
        private readonly BattleActionScheduler scheduler;
        private readonly TargetSelector selector;
        private readonly EffectService effectService;
        public System.Guid BattleId { get; } = System.Guid.NewGuid();
        public BattleResult Result { get; private set; } = BattleResult.None;
        public int TurnCount { get; private set; }
        public ActionResolution LastResolution { get; private set; }
        public IReadOnlyList<BattleUnit> Units => units;
        public IReadOnlyList<BattleActionRecord> ActionHistory => actionHistory;
        public BattleActionScheduler Scheduler => scheduler;
        public BattleSkillPoints SkillPoints { get; }
        public bool HasPendingWave { get; private set; }
        public bool IsWaveCleared => Result == BattleResult.None && CountActive(Faction.OurSide) > 0
            && CountActive(Faction.Enemy) == 0;
        private readonly List<BattleActionRecord> actionHistory = new();
        private readonly IActionDecider decider;
        private BattleUnit currentUnit;
        public BattleUnit CurrentUnit => currentUnit;
        public double CurrentTime => queue.CurrentTime;
        public bool IsExecuting { get; private set; }
        public int ActionSequence { get; private set; }

        public UnityAction<BattleResult> GameOverEvent;
        #endregion

        public BattleFlow(IEnumerable<BattleUnit> allUnits, IActionDecider decider = null,
            int initialSkillPoints = 4, int maximumSkillPoints = 5)
        {
            units = new List<BattleUnit>(allUnits);
            foreach (BattleUnit unit in units)
                unit.BattleId = BattleId;

            queue = new ActionQueue(units);
            scheduler = new BattleActionScheduler();
            selector = new TargetSelector(units);
            SkillPoints = new BattleSkillPoints(initialSkillPoints, maximumSkillPoints);
            effectService = new EffectService(actionQueue: queue, skillPoints: SkillPoints);
            this.decider = decider ?? new FirstSkillDecider();
        }

        public BattleResult Run()
        {
            Debug.Log($"Battle started. Allies={Count(Faction.OurSide)}, Enemies={Count(Faction.Enemy)}");

            while (Step())
            {
                
            }

            Debug.Log($"Battle {(Result == BattleResult.None ? "stopped" : "ended")}: {ResultText(Result)}, Turns={TurnCount}");
            return Result;
        }

        public BattleUnit Peek() => queue.PeekNext();
        public IReadOnlyList<BattleActionOrderEntry> GetActionOrderSnapshot(BattleUnit displayedActor = null)
        {
            var snapshot = new List<BattleActionOrderEntry>();
            BattleActionRequest active = scheduler.ActiveRequest;
            if (active?.Action?.caster != null && active.Action.caster.IsActive)
                snapshot.Add(QueuedOrder(active, true));
            foreach (BattleActionRequest request in scheduler.Pending)
                if (request?.Action?.caster != null && request.Action.caster.IsActive)
                    snapshot.Add(QueuedOrder(request, false));

            BattleUnit pinned = active == null ? displayedActor ?? currentUnit : null;
            snapshot.AddRange(queue.GetOrderSnapshot(pinned));
            return snapshot.AsReadOnly();
        }

        public bool HasQueuedActions => scheduler.PendingCount > 0
            || scheduler.ActiveRequest != null;
        public int ReservedEnergy(BattleUnit unit) => scheduler.GetReservedEnergy(unit);

        public ActionValidation QueueUltimate(BattleAction action, out BattleActionRequest request)
        {
            request = null;
            if (action?.caster != null && action.skill != null
                && scheduler.HasPending(action.caster, action.skill,
                    BattleActionOrigin.Ultimate))
                return new ActionValidation(ActionFailure.AlreadyQueued);
            ActionValidation validation = ValidateAction(action, BattleActionOrigin.Ultimate);
            if (!validation.IsValid)
                return validation;

            BattleAction prepared = PrepareAction(action);
            request = scheduler.Enqueue(prepared, BattleActionOrigin.Ultimate,
                BattleActionExecutionPolicy.FreeAction, 300,
                BattleTargetFallback.AutoSelectFirst, prepared.skill.energyCost);
            return validation;
        }

        internal BattleActionRequest QueueTriggered(BattleAction action,
            BattleActionOrigin origin, int priority)
        {
            if (action?.caster == null || action.skill == null
                || !units.Contains(action.caster) || !action.caster.IsActive
                || action.caster.BattleId != BattleId
                || !ReferenceEquals(action.caster.talentAction, action.skill))
                return null;
            return scheduler.Enqueue(PrepareAction(action), origin,
                origin == BattleActionOrigin.ExtraTurn
                    ? BattleActionExecutionPolicy.ExtraTurn
                    : BattleActionExecutionPolicy.FreeAction,
                priority, BattleTargetFallback.AutoSelectFirst, 0);
        }

        public bool TryTakeQueuedAction(out BattleActionRequest request)
            => scheduler.TryTakeNext(out request);

        public void CompleteRequest(BattleActionRequest request)
            => scheduler.Complete(request);

        public bool CancelQueuedAction(long requestId) => scheduler.Cancel(requestId);

        public void CancelQueuedActions() => scheduler.CancelAll();
        public void SetPendingWave(bool value) => HasPendingWave = value;

        public bool AddUnits(IEnumerable<BattleUnit> reinforcements)
        {
            if (reinforcements == null || IsExecuting || Result != BattleResult.None)
                return false;

            bool added = false;
            foreach (BattleUnit unit in reinforcements)
            {
                if (unit == null || units.Contains(unit) || !unit.IsActive || unit.speed <= 0)
                    continue;
                unit.BattleId = BattleId;
                units.Add(unit);
                queue.Add(unit);
                selector.Add(unit);
                added = true;
            }
            if (added)
                currentUnit = Peek();
            return added;
        }

        public void Ready()
        {
            if (!IsExecuting && Result == BattleResult.None)
                currentUnit = Peek();
        }

        public BattleActionPreview PreviewAction(BattleAction requested,
            BattleActionOrigin origin = BattleActionOrigin.NormalTurn)
        {
            var action = new BattleAction { caster = requested?.caster, skill = requested?.skill, mainTarget = requested?.mainTarget };
            var validation = ValidateAction(action, origin);
            var targets = new List<BattleUnit>();
            var perEffect = new List<IReadOnlyList<BattleUnit>>();
            if (action.caster != null && action.skill != null && validation.Failure != ActionFailure.ForeignTarget)
            {
                var primary = selector.Select(action.caster, action.skill.targetSide, action.skill.targetRule, action.mainTarget);
                if (primary.Count > 0 && !primary.Contains(action.mainTarget)) action.mainTarget = primary[0];
                if (action.skill.effectConfigs != null) foreach (var effect in action.skill.effectConfigs)
                {
                    if (effect == null) continue;
                    var found = selector.Select(action.caster, effect.overrideTarget ? effect.targetSide : action.skill.targetSide,
                        effect.overrideTarget ? effect.target : action.skill.targetRule, action.mainTarget);
                    perEffect.Add(found.AsReadOnly());
                    foreach (var unit in found) if (!targets.Contains(unit)) targets.Add(unit);
                }
            }
            return new BattleActionPreview(validation, action, targets, perEffect);
        }

        public ActionValidation ValidateAction(BattleAction action)
            => ValidateAction(action, BattleActionOrigin.NormalTurn);

        public ActionValidation ValidateAction(BattleAction action, BattleActionOrigin origin,
            BattleActionRequest executingRequest = null)
        {
            if (IsExecuting) return new ActionValidation(ActionFailure.Resolving);
            if (PendingResult() != BattleResult.None) return new ActionValidation(ActionFailure.BattleEnded);
            BattleUnit actor = currentUnit ?? Peek();
            if (actor == null) return new ActionValidation(ActionFailure.NoActor);
            if (action == null) return new ActionValidation(ActionFailure.MissingAction);
            if (action.caster == null || !units.Contains(action.caster)
                || action.caster.BattleId != BattleId || !action.caster.IsActive)
                return new ActionValidation(ActionFailure.InvalidActor);
            if (origin == BattleActionOrigin.NormalTurn && action.caster != actor)
                return new ActionValidation(ActionFailure.NotCurrentActor);
            SkillConfig skill = action.skill;
            if (skill == null) return new ActionValidation(ActionFailure.MissingSkill);
            bool ownsRegularSkill = action.caster.skills != null && action.caster.skills.Contains(skill);
            bool ownsTriggeredSkill = ReferenceEquals(action.caster.talentAction, skill);
            if (!ownsRegularSkill && !ownsTriggeredSkill)
                return new ActionValidation(ActionFailure.SkillNotOwned);
            if (origin == BattleActionOrigin.NormalTurn && skill.kind == SkillKind.Ultimate)
                return new ActionValidation(ActionFailure.InvalidActionOrigin);
            if (origin == BattleActionOrigin.Ultimate
                && (action.caster.faction != Faction.OurSide || skill.kind != SkillKind.Ultimate))
                return new ActionValidation(ActionFailure.InvalidActionOrigin);
            if ((origin == BattleActionOrigin.FollowUp || origin == BattleActionOrigin.Counter)
                && !ownsTriggeredSkill)
                return new ActionValidation(ActionFailure.InvalidActionOrigin);
            if (skill.energyCost < 0 || skill.skillPointCost < 0 || skill.skillPointGain < 0
                || skill.effectConfigs == null
                || !System.Enum.IsDefined(typeof(TargetSide), skill.targetSide)
                || !System.Enum.IsDefined(typeof(TargetRule), skill.targetRule))
                return new ActionValidation(ActionFailure.InvalidSkillConfig);
            if (action.mainTarget != null && (!units.Contains(action.mainTarget) || action.mainTarget.BattleId != BattleId))
                return new ActionValidation(ActionFailure.ForeignTarget);

            bool hasEffect = false, hasTargets = false;
            foreach (EffectConfig effect in skill.effectConfigs)
            {
                if (effect == null) continue;
                hasEffect = true;
                if (!System.Enum.IsDefined(typeof(EffectType), effect.effectType)
                    || !System.Enum.IsDefined(typeof(StatType), effect.scaleStat)
                    || float.IsNaN(effect.value) || float.IsInfinity(effect.value)
                    || (effect.overrideTarget && (!System.Enum.IsDefined(typeof(TargetSide), effect.targetSide)
                        || !System.Enum.IsDefined(typeof(TargetRule), effect.target)))
                    || (effect.effectType == EffectType.ApplyBuff && (effect.buff == null
                        || float.IsNaN(effect.buffValueMultiplier) || float.IsInfinity(effect.buffValueMultiplier))))
                    return new ActionValidation(ActionFailure.InvalidSkillConfig);
                hasTargets |= selector.Select(action.caster,
                    effect.overrideTarget ? effect.targetSide : skill.targetSide,
                    effect.overrideTarget ? effect.target : skill.targetRule, action.mainTarget).Count > 0;
            }
            if (!hasEffect) return new ActionValidation(ActionFailure.InvalidSkillConfig);
            int ownReservation = executingRequest != null ? executingRequest.ReservedEnergy : 0;
            int availableEnergy = action.caster.currentEnergy
                - Mathf.Max(0, scheduler.GetReservedEnergy(action.caster) - ownReservation);
            if (skill.energyCost > availableEnergy)
                return new ActionValidation(scheduler.GetReservedEnergy(action.caster) > 0
                    ? ActionFailure.ResourceReserved : ActionFailure.InsufficientEnergy);
            if (action.caster.faction == Faction.OurSide
                && !SkillPoints.CanSpend(SkillPointCost(skill)))
                return new ActionValidation(ActionFailure.InsufficientSkillPoints);
            return new ActionValidation(hasTargets ? ActionFailure.None : ActionFailure.NoTargets);
        }

        public bool Execute(BattleAction action) => TryExecute(action).Succeeded;

        public BattleActionResult TryExecute(BattleAction action)
            => action == null
                ? ExecuteRequest(null)
                : ExecuteRequest(scheduler.CreateImmediate(action));

        public BattleActionResult TryExecute(BattleActionRequest request)
            => ExecuteRequest(request);

        private BattleActionResult ExecuteRequest(BattleActionRequest request)
        {
            if (IsExecuting)
            {
                var resolving = new ActionValidation(ActionFailure.Resolving);
                return new BattleActionResult(resolving, Result);
            }
            BattleAction action = request?.Action;
            BattleActionOrigin origin = request?.Origin
                ?? BattleActionOrigin.NormalTurn;
            ActionValidation validation = ValidateAction(action, origin, request);
            if (validation.IsValid)
                validation = ValidateRequestPolicy(request);
            if (!validation.IsValid)
            {
                // 查询不结算终局；提交/推进时才发布一次结束事件。
                if (validation.Failure == ActionFailure.BattleEnded)
                    CompleteIfEnded();
                else if (validation.Failure == ActionFailure.NoActor)
                    Complete(BattleResult.Draw);
                return new BattleActionResult(validation, Result);
            }

            // 固定本次请求，避免事件监听者改写调用者持有的 BattleAction。
            BattleAction submitted = PrepareAction(action);
            BattleUnit previousCurrent = currentUnit;
            bool normalTurn = request.Policy == BattleActionExecutionPolicy.NormalTurn;
            bool turnLifecycle = request.Policy != BattleActionExecutionPolicy.FreeAction;
            IsExecuting = true;
            try
            {
                currentUnit = submitted.caster;
                if (normalTurn)
                    queue.BeginTurn(currentUnit);
                if (turnLifecycle)
                    BattleTalentResolver.OnTurnStart(currentUnit, units);
                if (request.IsQueuedAction)
                    scheduler.ConsumeReservation(request);
                ActionSequence++;
                var resolution = new ActionResolution
                {
                    Record = new BattleActionRecord
                    {
                        RequestId = request.RequestId,
                        Origin = request.Origin,
                        ActionSequence = ActionSequence,
                        NormalTurnIndex = TurnCount + (turnLifecycle ? 1 : 0),
                        ConsumedNormalTurn = normalTurn,
                        Turn = TurnCount + (turnLifecycle ? 1 : 0),
                        StartedAt = queue.CurrentTime,
                        Caster = currentUnit,
                        Skill = submitted.skill,
                        MainTarget = submitted.mainTarget,
                        SkillPointsBefore = SkillPoints.Current,
                        EnergyBefore = currentUnit.currentEnergy
                    }
                };
                Act(currentUnit, submitted, resolution);
                if (turnLifecycle)
                    TickBuffs(currentUnit);
                if (normalTurn)
                {
                    // Resolve speed changes first; apply deferred self-advance to the new interval.
                    queue.EndTurn(currentUnit);
                    queue.ModifyAction(currentUnit, 1f - resolution.SelfActionMultiplier);
                }
                IReadOnlyList<BattleAction> triggered = BattleTalentResolver.AfterAction(
                    currentUnit, submitted.skill, resolution, units, queue, request.Origin);
                if (triggered != null)
                    foreach (BattleAction followUp in triggered)
                        QueueTriggered(followUp, BattleActionOrigin.FollowUp, 100);
                resolution.Record.EndedAt = queue.CurrentTime;
                resolution.Record.SkillPointsAfter = SkillPoints.Current;
                resolution.Record.EnergyAfter = currentUnit.currentEnergy;
                LastResolution = resolution;
                actionHistory.Add(resolution.Record);
                if (turnLifecycle)
                    TurnCount++;
                CompleteIfEnded();
                currentUnit = Result != BattleResult.None ? null
                    : normalTurn ? Peek()
                    : previousCurrent != null && previousCurrent.IsActive ? previousCurrent : Peek();
                return new BattleActionResult(validation, Result, resolution);
            }
            finally
            {
                IsExecuting = false;
            }
        }

        public bool Step() => TryStep().Succeeded;

        public BattleActionResult TryStep()
        {
            if (IsExecuting || PendingResult() != BattleResult.None || (currentUnit ?? Peek()) == null)
                return TryExecute((BattleAction)null);
            BattleUnit actor = currentUnit ?? Peek();
            BattleAction preferred = decider.Decide(actor, units);
            if (ValidateAction(preferred).IsValid)
                return TryExecute(preferred);
            // 最小兼容回退：正式的候选评分和待机规则由后续 AI 阶段引入。
            if (actor.skills != null)
            {
                foreach (SkillConfig skill in actor.skills)
                {
                    var candidate = new BattleAction { caster = actor, skill = skill };
                    if (ValidateAction(candidate).IsValid)
                        return TryExecute(candidate);
                }
            }
            var failure = new ActionValidation(ActionFailure.NoLegalAction);
            Debug.LogWarning(failure.Message);
            return new BattleActionResult(failure, Result);
        }

        private ActionValidation ValidateRequestPolicy(BattleActionRequest request)
        {
            if (request == null)
                return new ActionValidation(ActionFailure.MissingAction);
            bool valid = request.Origin switch
            {
                BattleActionOrigin.NormalTurn => request.Policy == BattleActionExecutionPolicy.NormalTurn,
                BattleActionOrigin.ExtraTurn => request.Policy == BattleActionExecutionPolicy.ExtraTurn,
                _ => request.Policy == BattleActionExecutionPolicy.FreeAction
            };
            return new ActionValidation(valid
                ? ActionFailure.None : ActionFailure.InvalidExecutionPolicy);
        }

        private BattleAction PrepareAction(BattleAction action)
        {
            var prepared = new BattleAction
            {
                caster = action?.caster,
                skill = action?.skill,
                mainTarget = action?.mainTarget
            };
            if (prepared.caster == null || prepared.skill == null)
                return prepared;
            List<BattleUnit> targets = selector.Select(prepared.caster,
                prepared.skill.targetSide, prepared.skill.targetRule,
                prepared.mainTarget);
            if (targets.Count > 0 && !targets.Contains(prepared.mainTarget))
                prepared.mainTarget = targets[0];
            return prepared;
        }

        private BattleActionOrderEntry QueuedOrder(BattleActionRequest request,
            bool isCurrent)
            => new(request.RequestId, request.Action.caster, queue.CurrentTime,
                0, isCurrent, request.Origin, request.RequestId);

        private BattleResult PendingResult()
        {
            if (Result != BattleResult.None) return Result;
            BattleResult result = CheckResult();
            if (result == BattleResult.AllyWin && HasPendingWave)
                return BattleResult.None;
            return result == BattleResult.None && TurnCount >= MaxTurns ? BattleResult.Draw : result;
        }

        private bool CompleteIfEnded()
        {
            if (Result != BattleResult.None)
                return true;

            BattleResult result = PendingResult();
            if (result == BattleResult.None)
                return false;

            Complete(result);
            return true;
        }

        private void Complete(BattleResult result)
        {
            if (Result != BattleResult.None || result == BattleResult.None)
                return;

            Result = result;
            currentUnit = null;
            GameOverEvent?.Invoke(result);
            Debug.Log(ResultText(result));
        }

        private void Act(BattleUnit caster, BattleAction action, ActionResolution resolution)
        {
            var skill = action.skill;
            if (caster.faction == Faction.OurSide)
            {
                int before = SkillPoints.Current;
                SkillPoints.TrySpend(SkillPointCost(skill));
                SkillPoints.Gain(SkillPointGain(skill));
                if (before != SkillPoints.Current)
                    resolution.AddEffect(new BattleEffectRecord(BattleEffectRecordType.SkillPoint,
                        caster, caster, SkillPoints.Current - before, before, SkillPoints.Current,
                        "Skill"));
            }
            caster.ChangeEnergy(-skill.energyCost);

            Debug.Log($"[{TurnCount + 1}] {Name(caster)} uses {SkillName(skill)}. HP={Hp(caster)}, Energy={caster.currentEnergy}");

            var targets = selector.Select(
                caster,
                skill.targetSide,
                skill.targetRule,
                action.mainTarget);

            resolution.Record.PrimaryTargets = new List<BattleUnit>(targets).AsReadOnly();
            if (!targets.Contains(resolution.Record.MainTarget)) resolution.Record.MainTarget = targets.Count > 0 ? targets[0] : null;
            for (int effectIndex = 0; effectIndex < skill.effectConfigs.Count; effectIndex++)
            {
                EffectConfig effect = skill.effectConfigs[effectIndex];
                if (effect == null) continue;
                var effectTargets = effect.overrideTarget
                    ? selector.Select(caster, effect.targetSide, effect.target, action.mainTarget)
                    : targets;
                resolution.Record.resolvedTargets.Add(new BattleResolvedEffectTargets(effectIndex, effectTargets));
                resolution.ActiveEffectGroup = effectIndex;
                effectService.Execute(caster, effectTargets, effect, resolution);
            }
            resolution.ActiveEffectGroup = -1;

            int energyBefore = caster.currentEnergy;
            caster.ChangeEnergy(skill.energyGain);

            if (skill.energyGain != 0)
                Debug.Log($"    {Name(caster)} energy {energyBefore}->{caster.currentEnergy}");
        }

        BattleResult CheckResult()
        {
            bool allyAlive = false, enemyAlive = false;
            foreach (var u in units)
            {
                if (!u.IsActive) continue;
                if (u.faction == Faction.OurSide) allyAlive = true;
                else enemyAlive = true;
            }
            if (!allyAlive && !enemyAlive) return BattleResult.Draw;
            if (!enemyAlive) return BattleResult.AllyWin;
            if (!allyAlive) return BattleResult.EnemyWin;
            return BattleResult.None;
        }

        void TickBuffs(BattleUnit u)
        {
            for (int i = u.buffs.Count - 1; i >= 0; i--)
            {
                var b = u.buffs[i];
                if (b.skipNextOwnerTick) { b.skipNextOwnerTick = false; continue; }
                b.remainingTurns--;
                if (b.remainingTurns <= 0)
                {
                    string n = b.config != null ? b.config.Name : "Buff";
                    u.buffs.RemoveAt(i);
                    if (b.ownsConfig && b.config != null) Object.Destroy(b.config);
                    Debug.Log($"    {Name(u)}'s [{n}] expired.");
                }
            }
        }

        int Count(Faction f)
        {
            int n = 0;
            foreach (var u in units)
                if (u.faction == f) n++;
            return n;
        }

        int CountActive(Faction faction)
        {
            int count = 0;
            foreach (BattleUnit unit in units)
                if (unit.IsActive && unit.faction == faction)
                    count++;
            return count;
        }

        private static int SkillPointCost(SkillConfig skill) => skill?.skillPointCost ?? 0;

        private static int SkillPointGain(SkillConfig skill)
        {
            if (skill == null)
                return 0;
            return skill.skillPointGain > 0
                ? skill.skillPointGain
                : skill.kind == SkillKind.Basic && skill.skillPointCost == 0 ? 1 : 0;
        }

        static string Name(BattleUnit u)
        {
            if (u == null) return "?";
            if (!string.IsNullOrEmpty(u.displayName))
                return u.displayName;
            string side = u.faction == Faction.OurSide ? "Ally" : "Enemy";
            return $"{side}{u.pos}";
        }

        static string SkillName(SkillConfig s)
        {
            if (s == null) return "?";
            if (!string.IsNullOrEmpty(s.Name)) return s.Name;
            return s.name;
        }

        static string Hp(BattleUnit u)
        {
            int max = Mathf.Max(u.maxHealth, u.currentHealth);
            return $"{u.currentHealth}/{max}";
        }

        static string ResultText(BattleResult r) => r switch
        {
            BattleResult.AllyWin => "Ally Victory",
            BattleResult.EnemyWin => "Enemy Victory",
            BattleResult.Draw => "Draw",
            _ => r.ToString()
        };
    }
}

