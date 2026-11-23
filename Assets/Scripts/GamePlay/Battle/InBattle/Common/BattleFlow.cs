using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Battle.Logic
{
    public enum BattleResult { None, AllyWin, EnemyWin, Draw }

    public class BattleFlow
    {
        #region Fields
        public const int MaxTurns = 500;
        private readonly List<BattleUnit> units;
        private readonly ActionQueue queue;
        private readonly TargetSelector selector;
        private readonly EffectService effectService;
        public System.Guid BattleId { get; } = System.Guid.NewGuid();
        public BattleResult Result { get; private set; } = BattleResult.None;
        public int TurnCount { get; private set; }
        public ActionResolution LastResolution { get; private set; }
        public IReadOnlyList<BattleUnit> Units => units;
        public IReadOnlyList<BattleActionRecord> ActionHistory => actionHistory;
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

        public UnityAction<BattleResult> GameOverEvent;
        #endregion

        public BattleFlow(IEnumerable<BattleUnit> allUnits, IActionDecider decider = null,
            int initialSkillPoints = 4, int maximumSkillPoints = 5)
        {
            units = new List<BattleUnit>(allUnits);
            foreach (BattleUnit unit in units)
                unit.BattleId = BattleId;

            queue = new ActionQueue(units);
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

        public BattleActionPreview PreviewAction(BattleAction requested)
        {
            var action = new BattleAction { caster = requested?.caster, skill = requested?.skill, mainTarget = requested?.mainTarget };
            var validation = ValidateAction(action);
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
        {
            if (IsExecuting) return new ActionValidation(ActionFailure.Resolving);
            if (PendingResult() != BattleResult.None) return new ActionValidation(ActionFailure.BattleEnded);
            BattleUnit actor = currentUnit ?? Peek();
            if (actor == null) return new ActionValidation(ActionFailure.NoActor);
            if (action == null) return new ActionValidation(ActionFailure.MissingAction);
            if (action.caster == null || !units.Contains(action.caster)
                || action.caster.BattleId != BattleId || !action.caster.IsActive)
                return new ActionValidation(ActionFailure.InvalidActor);
            if (action.caster != actor) return new ActionValidation(ActionFailure.NotCurrentActor);
            SkillConfig skill = action.skill;
            if (skill == null) return new ActionValidation(ActionFailure.MissingSkill);
            if (actor.skills == null || !actor.skills.Contains(skill))
                return new ActionValidation(ActionFailure.SkillNotOwned);
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
                hasTargets |= selector.Select(actor,
                    effect.overrideTarget ? effect.targetSide : skill.targetSide,
                    effect.overrideTarget ? effect.target : skill.targetRule, action.mainTarget).Count > 0;
            }
            if (!hasEffect) return new ActionValidation(ActionFailure.InvalidSkillConfig);
            if (skill.energyCost > actor.currentEnergy) return new ActionValidation(ActionFailure.InsufficientEnergy);
            if (actor.faction == Faction.OurSide
                && !SkillPoints.CanSpend(SkillPointCost(skill)))
                return new ActionValidation(ActionFailure.InsufficientSkillPoints);
            return new ActionValidation(hasTargets ? ActionFailure.None : ActionFailure.NoTargets);
        }

        public bool Execute(BattleAction action) => TryExecute(action).Succeeded;

        public BattleActionResult TryExecute(BattleAction action)
        {
            ActionValidation validation = ValidateAction(action);
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
            var submitted = new BattleAction { caster = action.caster, skill = action.skill, mainTarget = action.mainTarget };
            IsExecuting = true;
            try
            {
                currentUnit = submitted.caster;
                queue.BeginTurn(currentUnit);
                BattleTalentResolver.OnTurnStart(currentUnit, units);
                var resolution = new ActionResolution
                {
                    Record = new BattleActionRecord
                    {
                        Turn = TurnCount + 1,
                        StartedAt = queue.CurrentTime,
                        Caster = currentUnit,
                        Skill = submitted.skill,
                        MainTarget = submitted.mainTarget,
                        SkillPointsBefore = SkillPoints.Current,
                        EnergyBefore = currentUnit.currentEnergy
                    }
                };
                Act(currentUnit, submitted, resolution);
                TickBuffs(currentUnit);
                // Resolve speed changes first; apply deferred self-advance to the new interval.
                queue.EndTurn(currentUnit);
                queue.ModifyAction(currentUnit, 1f - resolution.SelfActionMultiplier);
                BattleTalentResolver.AfterAction(currentUnit, submitted.skill, resolution, units, queue);
                resolution.Record.EndedAt = queue.CurrentTime;
                resolution.Record.SkillPointsAfter = SkillPoints.Current;
                resolution.Record.EnergyAfter = currentUnit.currentEnergy;
                LastResolution = resolution;
                actionHistory.Add(resolution.Record);
                TurnCount++;
                CompleteIfEnded();
                currentUnit = Result == BattleResult.None ? Peek() : null;
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
                return TryExecute(null);
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
            caster.currentEnergy -= skill.energyCost;

            Debug.Log($"[{TurnCount + 1}] {Name(caster)} uses {SkillName(skill)}. HP={Hp(caster)}, Energy={caster.currentEnergy}");

            var targets = selector.Select(
                caster,
                skill.targetSide,
                skill.targetRule,
                action.mainTarget);

            foreach (EffectConfig effect in skill.effectConfigs)
            {
                
                if (effect == null) continue;
                var effectTargets = effect.overrideTarget
                    ? selector.Select(caster, effect.targetSide, effect.target, action.mainTarget)
                    : targets;
                effectService.Execute(caster, effectTargets, effect, resolution);
            }

            int energyBefore = caster.currentEnergy;
            caster.currentEnergy = Mathf.Clamp(caster.currentEnergy + skill.energyGain, 0, caster.maxEnergy);

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

