using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Battle.Events;
using Battle.Logic;
using Game;
using Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Battle.SaveData;

/// <summary>Explicit menu/Temp command verification; never runs tests merely on import.</summary>
[InitializeOnLoad]
public static class BattleActionVerification
{
    private const string Folder = "Temp/BattleActionVerification";
    private const string Request = Folder + "/request.txt";
    private static readonly List<string> checks = new();

    static BattleActionVerification() => EditorApplication.update += Poll;

    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        string command;
        try { command = File.ReadAllText(Request).Trim(); }
        catch (IOException) { return; }
        File.Delete(Request);
        if (command != "test") { Debug.LogError("Unknown battle verification command: " + command); return; }
        try { Run(); }
        catch (Exception error) { Debug.LogException(error); }
    }

    [MenuItem("Tools/Battle/Verify Action Submission")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Edit Mode required.");
        checks.Clear();
        Directory.CreateDirectory(Folder);
        try
        {
            ValidationChecks();
            SelectionChecks();
            ExecutionChecks();
            AutomaticChecks();
            QueueChecks();
            InterruptSchedulerChecks();
            ActionTimelineResourceChecks();
            ServiceChecks();
            ResourceChecks();
            EnemyDecisionChecks();
            ResolutionChecks();
            WaveChecks();
            PersistenceChecks();
            WriteReport(null);
            Debug.Log("Battle action verification passed: " + checks.Count);
        }
        catch (Exception error) { WriteReport(error.ToString()); throw; }
    }

    private static void WriteReport(string error) => File.WriteAllText(Folder + "/results.json",
        Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            passed = error == null, error, completedAt = DateTimeOffset.Now,
            unityVersion = Application.unityVersion, checkCount = checks.Count, checks
        }, Newtonsoft.Json.Formatting.Indented));

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + label);
        checks.Add(label);
    }

    private sealed class Fixture : IDisposable
    {
        public readonly BattleUnit A = Unit(Faction.OurSide, 0, 200);
        public readonly BattleUnit Ally = Unit(Faction.OurSide, 1, 100);
        public readonly BattleUnit Enemy = Unit(Faction.Enemy, 0, 100);
        public readonly BattleUnit Enemy2 = Unit(Faction.Enemy, 1, 100);
        public readonly List<Object> Assets = new();
        public readonly BattleFlow Flow;
        public readonly SkillConfig Basic;
        public BattleUnit[] Units => new[] { A, Ally, Enemy, Enemy2 };

        public Fixture(bool ready = true, IActionDecider decider = null)
        {
            Basic = Skill();
            Basic.energyCost = 10;
            Basic.energyGain = 5;
            A.skills.Add(Basic);
            Flow = new BattleFlow(Units, decider);
            if (ready) Flow.Ready();
        }

        public SkillConfig Skill(params EffectConfig[] effects)
        {
            var skill = ScriptableObject.CreateInstance<SkillConfig>();
            Assets.Add(skill);
            skill.Name = "Verification";
            skill.targetSide = TargetSide.OtherSide;
            skill.targetRule = TargetRule.Single;
            skill.effectConfigs = effects.Length == 0
                ? new List<EffectConfig> { new() { effectType = EffectType.Damage, scaleStat = StatType.Attack, value = 1 } }
                : effects.ToList();
            return skill;
        }

        public BuffConfig Buff()
        {
            var buff = ScriptableObject.CreateInstance<BuffConfig>();
            Assets.Add(buff);
            buff.id = 9999; buff.stat = StatType.Attack; buff.value = .1f;
            buff.durationTimes = 3; buff.maxStack = 1;
            return buff;
        }

        public BattleAction Action(SkillConfig skill = null, BattleUnit target = null) => new()
        { caster = A, skill = skill ?? Basic, mainTarget = target };

        public string State() => Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            Flow.CurrentTime, Flow.TurnCount, Flow.Result, Flow.IsExecuting,
            skillPoints = new { Flow.SkillPoints.Current, Flow.SkillPoints.Maximum },
            actor = Array.IndexOf(Units, Flow.CurrentUnit),
            units = Units.Select(u => new
            {
                u.currentHealth, u.shield, u.currentEnergy, u.nextActionTime,
                buffs = u.buffs.Select(b => new { b.remainingTurns, b.stacks, b.skipNextOwnerTick, b.valueMultiplier })
            })
        });

        public void Reject(BattleAction action, ActionFailure failure, string label)
        {
            string before = State();
            ActionResolution resolution = Flow.LastResolution;
            Check(Flow.ValidateAction(action).Failure == failure, label + " query reason");
            Check(State() == before, label + " query is pure");
            var result = Flow.TryExecute(action);
            Check(!result.Succeeded && result.Validation.Failure == failure && result.Resolution == null, label + " rejects");
            Check(State() == before && ReferenceEquals(resolution, Flow.LastResolution), label + " preserves all state");
        }

        public void Dispose()
        {
            foreach (var unit in Units)
            {
                foreach (var buff in unit.buffs)
                    if (buff.ownsConfig && buff.config != null) Object.DestroyImmediate(buff.config);
                if (unit.ownsRuntimeTalentAction && unit.talentAction != null)
                    Object.DestroyImmediate(unit.talentAction);
            }
            foreach (var asset in Assets) if (asset != null) Object.DestroyImmediate(asset);
        }
    }

    private static BattleUnit Unit(Faction faction, int pos, int speed) => new()
    {
        faction = faction, pos = pos, tieBreaker = (int)faction * 10 + pos,
        maxHealth = 1000, currentHealth = 1000, attack = 100, defence = 10,
        speed = speed, currentEnergy = 50, maxEnergy = 100
    };

    private static void ValidationChecks()
    {
        using var f = new Fixture(false);
        f.A.buffs.Add(new BuffInstance { config = f.Buff(), stacks = 1, remainingTurns = 3 });
        f.A.talent = Game.Characters.CharacterTalent.Aegis;
        f.A.talentValue = 1;
        f.Reject(null, ActionFailure.MissingAction, "null action before Ready");
        f.Reject(new BattleAction { skill = f.Basic }, ActionFailure.InvalidActor, "missing actor");
        f.Reject(new BattleAction { caster = f.Ally, skill = f.Basic }, ActionFailure.NotCurrentActor, "wrong turn");
        f.Reject(new BattleAction { caster = f.A }, ActionFailure.MissingSkill, "missing skill");
        f.Reject(f.Action(f.Skill()), ActionFailure.SkillNotOwned, "foreign skill instance");
        using var other = new Fixture();
        f.Reject(new BattleAction { caster = other.A, skill = other.Basic }, ActionFailure.InvalidActor, "foreign actor");
        f.Reject(f.Action(target: other.Enemy), ActionFailure.ForeignTarget, "other battle target");
        var impostor = Unit(Faction.Enemy, 0, 100);
        typeof(BattleUnit).GetProperty("BattleId").SetValue(impostor, f.Flow.BattleId);
        f.Reject(f.Action(target: impostor), ActionFailure.ForeignTarget, "same ID nonmember target");
        f.Basic.energyCost = 51;
        f.Reject(f.Action(), ActionFailure.InsufficientEnergy, "insufficient energy");
        f.Basic.energyCost = -1;
        f.Reject(f.Action(), ActionFailure.InvalidSkillConfig, "negative cost");
        f.Basic.energyCost = 10;
        var effects = f.Basic.effectConfigs;
        f.Basic.effectConfigs = null;
        f.Reject(f.Action(), ActionFailure.InvalidSkillConfig, "null effect list");
        f.Basic.effectConfigs = new();
        f.Reject(f.Action(), ActionFailure.InvalidSkillConfig, "empty effect list");
        f.Basic.effectConfigs.Add(null);
        f.Reject(f.Action(), ActionFailure.InvalidSkillConfig, "only null effects");
        f.Basic.effectConfigs = effects;
        var effect = effects[0];
        effect.effectType = (EffectType)999;
        f.Reject(f.Action(), ActionFailure.InvalidSkillConfig, "unknown effect type");
        effect.effectType = EffectType.ApplyBuff;
        f.Reject(f.Action(), ActionFailure.InvalidSkillConfig, "missing buff config");
        effect.effectType = EffectType.Damage;
        effect.value = float.NaN;
        f.Reject(f.Action(), ActionFailure.InvalidSkillConfig, "nonfinite multiplier");
        effect.value = 1;
        effects.Insert(0, null);
        string before = f.State();
        Check(f.Flow.ValidateAction(f.Action()).IsValid && f.State() == before, "valid query before Ready leaves actor unset");
        Check(f.Flow.TryExecute(f.Action()).Succeeded && f.Flow.TurnCount == 1, "valid submit before Ready initializes actor only on success");
        var deadAction = new BattleAction { caster = f.Ally, skill = f.Basic };
        f.Ally.currentHealth = 0;
        f.Reject(deadAction, ActionFailure.InvalidActor, "dead actor");
        // Existing terminal state is detected by a query without firing the end event.
        f.Enemy.currentHealth = f.Enemy2.currentHealth = 0;
        int ended = 0;
        f.Flow.GameOverEvent += _ => ended++;
        before = f.State();
        Check(f.Flow.ValidateAction(f.Action()).Failure == ActionFailure.BattleEnded
            && ended == 0 && f.State() == before, "terminal query has no lifecycle side effects");
        Check(!f.Flow.TryExecute(f.Action()).Succeeded && ended == 1 && f.Flow.Result == BattleResult.AllyWin, "submission finalizes pending victory");
        f.Flow.TryStep(); f.Flow.TryExecute(f.Action());
        Check(ended == 1, "repeated terminal calls publish once");
    }

    private static void SelectionChecks()
    {
        foreach (TargetRule rule in new[] { TargetRule.Single, TargetRule.Spread })
        {
            foreach (string selected in new[] { "none", "wrong side", "dead" })
            {
                using var f = new Fixture();
                f.Basic.targetRule = rule;
                BattleUnit target = selected == "none" ? null : selected == "wrong side" ? f.Ally : f.Enemy2;
                if (selected == "dead") f.Enemy2.currentHealth = 0;
                Check(f.Flow.TryExecute(f.Action(target: target)).Succeeded && f.Enemy.currentHealth == 910,
                    rule + " auto selects first living target: " + selected);
                Check(f.Ally.currentHealth == 1000, "fallback never attacks allies");
            }
        }
        using (var f = new Fixture())
        {
            f.Basic.targetRule = TargetRule.AoE;
            Check(f.Flow.TryExecute(f.Action()).Succeeded && f.Enemy.currentHealth == 910 && f.Enemy2.currentHealth == 910, "AoE hits both living enemies");
        }
        using (var f = new Fixture())
        {
            f.Basic.targetRule = TargetRule.Self;
            f.Basic.effectConfigs[0].effectType = EffectType.Heal;
            Check(f.Flow.TryExecute(f.Action(target: f.Enemy)).Succeeded && f.A.currentHealth == 1000, "full health self heal is legal");
        }
        using (var f = new Fixture())
        {
            f.Basic.effectConfigs.Add(new EffectConfig { overrideTarget = true, targetSide = TargetSide.OurSide,
                target = TargetRule.AoE, effectType = EffectType.AddShield, scaleStat = StatType.Defence, value = 2 });
            Check(f.Flow.TryExecute(f.Action(target: f.Enemy)).Succeeded && f.Enemy.currentHealth == 910
                && f.Ally.shield == 20 && f.A.shield == 20 && f.Enemy.shield == 0, "mixed enemy damage and ally shield");
        }
        using (var f = new Fixture())
        {
            f.Basic.effectConfigs.Clear();
            f.Basic.effectConfigs.Add(new EffectConfig { effectType = EffectType.RemoveDeBuff,
                overrideTarget = true, targetSide = TargetSide.OurSide, target = TargetRule.Self });
            Check(f.Flow.TryExecute(f.Action()).Succeeded, "override-only repeated cleanse is legal");
        }
    }

    private static void ExecutionChecks()
    {
        using (var f = new Fixture())
        {
            f.A.buffs.Add(new BuffInstance { config = f.Buff(), stacks = 1, remainingTurns = 3 });
            int callbacks = 0;
            bool nestedRejected = true;
            var action = f.Action();
            using (GameEvents.Battle.Subscribe<HealthChangedEvent>(message =>
            {
                if (message.BattleId != f.Flow.BattleId) return;
                callbacks++;
                nestedRejected &= f.Flow.TryExecute(action).Validation.Failure == ActionFailure.Resolving;
                nestedRejected &= f.Flow.TryStep().Validation.Failure == ActionFailure.Resolving;
                f.Flow.Ready();
                nestedRejected &= f.Flow.CurrentUnit == f.A;
                action.caster = f.Enemy; action.skill = null;
            }))
            {
                var result = f.Flow.TryExecute(action);
                Check(result.Succeeded && ReferenceEquals(result.Resolution, f.Flow.LastResolution), "returns this action resolution");
            }
            Check(callbacks == 1 && nestedRejected, "health callback cannot reenter execution or change current actor");
            Check(f.A.currentEnergy == 45 && f.A.buffs[0].remainingTurns == 2
                && f.Flow.CurrentTime == 50 && f.A.nextActionTime == 100 && f.Flow.TurnCount == 1, "exactly one cost, buff tick, queue interval and turn");
            Check(!f.Flow.IsExecuting, "execution guard released");
            f.Basic.energyCost = 999;
            f.Reject(f.Action(), f.Flow.CurrentUnit == f.A ? ActionFailure.InsufficientEnergy : ActionFailure.NotCurrentActor,
                "rejected action retains last successful resolution");
        }
        using (var f = new Fixture())
        {
            var action = f.Action();
            Check(f.Flow.ValidateAction(action).IsValid, "UI preview allowed");
            f.A.currentEnergy = 0;
            f.Reject(action, ActionFailure.InsufficientEnergy, "submit revalidates changed energy");
        }
        using (var f = new Fixture())
        {
            f.Basic.targetRule = TargetRule.AoE;
            f.Enemy.currentHealth = f.Enemy2.currentHealth = 1;
            int ended = 0;
            f.Flow.GameOverEvent += _ => ended++;
            Check(f.Flow.TryExecute(f.Action()).Succeeded && ended == 1 && f.Flow.Result == BattleResult.AllyWin, "lethal action succeeds and ends battle");
            f.Flow.Step(); f.Flow.Execute(f.Action());
            Check(ended == 1 && f.Flow.CurrentUnit == null, "no duplicate game over after lethal action");
        }
        using (var f = new Fixture())
        {
            f.Basic.targetRule = TargetRule.AoE;
            f.Enemy.currentHealth = f.Enemy2.currentHealth = 1;
            f.Flow.GameOverEvent += _ => throw new InvalidOperationException("injected listener failure");
            bool threw = false;
            try { f.Flow.TryExecute(f.Action()); } catch (InvalidOperationException) { threw = true; }
            Check(threw && !f.Flow.IsExecuting, "guard released even if legacy end listener throws");
        }
    }

    private sealed class EmptyDecider : IActionDecider
    {
        public BattleAction Decide(BattleUnit caster, IReadOnlyList<BattleUnit> units) => null;
    }

    private static void AutomaticChecks()
    {
        using (var f = new Fixture(decider: new EmptyDecider()))
            Check(f.Flow.TryStep().Succeeded && f.Flow.TurnCount == 1, "invalid AI choice falls back to legal owned skill");
        using (var f = new Fixture())
        {
            var invalidUlt = f.Skill();
            invalidUlt.kind = SkillKind.Ultimate;
            invalidUlt.effectConfigs.Clear();
            f.A.skills.Insert(0, invalidUlt);
            Check(f.Flow.TryStep().Succeeded && f.Enemy.currentHealth == 910, "invalid preferred ultimate falls back to basic");
        }
        using (var f = new Fixture())
        {
            f.A.skills.Insert(0, null);
            f.Enemy.currentHealth = 0;
            Check(new FirstSkillDecider().Decide(f.A, f.Units).mainTarget == f.Enemy2, "AI ignores null skills and dead targets");
            Check(f.Flow.TryStep().Succeeded && f.Enemy2.currentHealth == 910, "AI action uses shared executor");
        }
        using (var f = new Fixture())
        {
            f.A.skills.Clear();
            var ult = f.Skill(); ult.kind = SkillKind.Ultimate; ult.energyCost = 100;
            f.A.skills.Add(ult);
            string before = f.State();
            Check(new FirstSkillDecider().Decide(f.A, f.Units) == null, "unaffordable only ultimate does not dereference null");
            var result = f.Flow.TryStep();
            Check(!result.Succeeded && result.Validation.Failure == ActionFailure.NoLegalAction
                && before == f.State() && f.Flow.Result == BattleResult.None, "no legal AI action stops without fake victory or consumed turn");
        }
    }

    private static void QueueChecks()
    {
        var a = Unit(Faction.OurSide, 0, 100);
        var b = Unit(Faction.OurSide, 1, 200);
        var queue = new ActionQueue(new[] { a, b });
        Check(queue.PeekNext() == b && b.nextActionTime == 50, "speed selects earliest absolute time");
        double beforeTime = queue.CurrentTime;
        double beforeA = a.nextActionTime;
        double beforeB = b.nextActionTime;
        IReadOnlyList<BattleActionOrderEntry> initialOrder = queue.GetOrderSnapshot(b);
        Check(initialOrder.Count == 2
            && initialOrder[0].Unit == b && initialOrder[0].IsCurrent
            && initialOrder[0].RemainingActionValue == 0
            && initialOrder[1].Unit == a && initialOrder[1].RemainingActionValue == 50,
            "action order snapshot pins current actor and measures from its turn time");
        Check(queue.CurrentTime == beforeTime && a.nextActionTime == beforeA && b.nextActionTime == beforeB,
            "action order snapshot is side-effect free");
        queue.BeginTurn(b); queue.EndTurn(b);
        Check(queue.CurrentTime == 50 && b.nextActionTime == 100 && queue.PeekNext() == a, "equal time uses stable tie breaker");
        IReadOnlyList<BattleActionOrderEntry> tiedOrder = queue.GetOrderSnapshot(a);
        Check(tiedOrder.Count == 2 && tiedOrder.Select(entry => entry.Unit).Distinct().Count() == 2
            && tiedOrder[0].Unit == a && tiedOrder[1].Unit == b,
            "action order contains each active unit once and preserves tie order");
        queue.ModifyAction(a, .5f);
        Check(a.nextActionTime == 75, "advance scales remaining distance");
        b.currentHealth = 0;
        IReadOnlyList<BattleActionOrderEntry> survivingOrder = queue.GetOrderSnapshot(a);
        Check(survivingOrder.Count == 1 && survivingOrder[0].Unit == a,
            "action order excludes defeated units");
        using var f = new Fixture();
        var buff = f.Buff(); buff.stat = StatType.Speed; buff.value = 1;
        f.Basic.effectConfigs.Add(new EffectConfig { effectType = EffectType.ApplyBuff, buff = buff, stack = 1,
            overrideTarget = true, target = TargetRule.Self, targetSide = TargetSide.OurSide });
        f.Basic.effectConfigs.Add(new EffectConfig { effectType = EffectType.AdvanceAction, value = .5f,
            overrideTarget = true, target = TargetRule.Self, targetSide = TargetSide.OurSide });
        Check(f.Flow.TryExecute(f.Action()).Succeeded && f.A.nextActionTime == 62.5,
            "self speed then self advance applies to new interval");
        Check(f.A.buffs[0].remainingTurns == 3 && !f.A.buffs[0].skipNextOwnerTick, "fresh self buff skips immediate owner tick");
    }

    private sealed class DeferredPresenter : IBattleResolutionPresenter
    {
        private readonly Queue<Action> completions = new();
        public int PresentedCount { get; private set; }
        public int PendingCount => completions.Count;

        public void Present(ActionResolution resolution, Action completed)
        {
            PresentedCount++;
            completions.Enqueue(completed);
        }

        public void CompleteNext() => completions.Dequeue()?.Invoke();
    }

    private static void InterruptSchedulerChecks()
    {
        UltimateReservationAndLifecycleChecks();
        SchedulerOrderingChecks();
        PresentationBarrierChecks();
        FollowUpRequestChecks();
    }

    private static void UltimateReservationAndLifecycleChecks()
    {
        using (var f = new Fixture())
        {
            SkillConfig ultimate = f.Skill();
            ultimate.kind = SkillKind.Ultimate;
            ultimate.energyCost = 40;
            f.A.skills.Add(ultimate);
            f.A.buffs.Add(new BuffInstance
            {
                config = f.Buff(), stacks = 1, remainingTurns = 3
            });
            double nextActionTime = f.A.nextActionTime;
            double battleTime = f.Flow.CurrentTime;
            ActionValidation queued = f.Flow.QueueUltimate(new BattleAction
            {
                caster = f.A, skill = ultimate, mainTarget = f.Enemy2
            }, out BattleActionRequest request);
            Check(queued.IsValid && request != null
                && f.Flow.ReservedEnergy(f.A) == 40
                && f.Flow.Scheduler.PendingCount == 1,
                "ultimate queue reserves energy without spending it");
            Check(f.A.currentEnergy == 50 && f.Flow.TurnCount == 0
                && f.Flow.CurrentTime == battleTime && f.A.nextActionTime == nextActionTime,
                "queuing ultimate does not mutate normal turn state");
            Check(f.Flow.QueueUltimate(new BattleAction
            {
                caster = f.A, skill = ultimate, mainTarget = f.Enemy
            }, out _).Failure == ActionFailure.AlreadyQueued,
                "same ultimate cannot be queued twice");

            IReadOnlyList<BattleActionOrderEntry> order = f.Flow.GetActionOrderSnapshot();
            BattleActionOrderEntry[] actorEntries = order.Where(entry => entry.Unit == f.A).ToArray();
            Check(actorEntries.Length == 2
                && actorEntries.Select(entry => entry.EntryId).Distinct().Count() == 2
                && actorEntries.Count(entry => entry.Origin == BattleActionOrigin.Ultimate) == 1,
                "queued ultimate and normal turn have distinct action timeline entries");

            f.Enemy2.currentHealth = 0;
            Check(f.Flow.TryTakeQueuedAction(out BattleActionRequest active)
                && ReferenceEquals(active, request), "queued ultimate becomes the active request");
            BattleActionResult result = f.Flow.TryExecute(active);
            Check(result.Succeeded && result.Resolution.Record.MainTarget == f.Enemy,
                "queued ultimate reselects a living target at execution time");
            Check(f.A.currentEnergy == 10 && f.Flow.ReservedEnergy(f.A) == 0
                && f.Flow.TurnCount == 0 && f.Flow.ActionSequence == 1
                && f.Flow.CurrentTime == battleTime && f.A.nextActionTime == nextActionTime
                && f.A.buffs[0].remainingTurns == 3,
                "free ultimate spends once without advancing time or ticking buffs");
            Check(f.Flow.ActionHistory.Count == 1
                && f.Flow.ActionHistory[0].Origin == BattleActionOrigin.Ultimate
                && f.Flow.ActionHistory[0].RequestId == request.RequestId
                && !f.Flow.ActionHistory[0].ConsumedNormalTurn,
                "ultimate writes an independent non-normal action record");
            f.Flow.CompleteRequest(active);
            Check(f.Flow.Scheduler.ActiveRequest == null && !f.Flow.HasQueuedActions,
                "completed ultimate releases the active request");
        }

        using (var f = new Fixture())
        {
            SkillConfig ultimate = f.Skill();
            ultimate.kind = SkillKind.Ultimate;
            ultimate.energyCost = 30;
            f.A.skills.Add(ultimate);
            Check(f.Flow.QueueUltimate(new BattleAction
            {
                caster = f.A, skill = ultimate, mainTarget = f.Enemy
            }, out BattleActionRequest request).IsValid
                && f.Flow.CancelQueuedAction(request.RequestId)
                && f.Flow.ReservedEnergy(f.A) == 0
                && f.A.currentEnergy == 50,
                "cancelling a pending ultimate releases its reservation");

            using var foreign = new Fixture();
            Check(f.Flow.QueueUltimate(new BattleAction
            {
                caster = f.A, skill = ultimate, mainTarget = foreign.Enemy
            }, out _).Failure == ActionFailure.ForeignTarget,
                "ultimate queue rejects a target from another battle");
        }

        using (var f = new Fixture())
        {
            SkillConfig ultimate = f.Skill();
            ultimate.kind = SkillKind.Ultimate;
            ultimate.energyCost = 25;
            f.A.skills.Add(ultimate);
            f.Flow.SetPendingWave(true);
            Check(f.Flow.QueueUltimate(new BattleAction
            {
                caster = f.A, skill = ultimate, mainTarget = f.Enemy
            }, out BattleActionRequest request).IsValid,
                "ultimate can be queued before its target becomes invalid");
            f.Enemy.currentHealth = f.Enemy2.currentHealth = 0;
            Check(f.Flow.TryTakeQueuedAction(out BattleActionRequest active)
                && !f.Flow.TryExecute(active).Succeeded,
                "queued ultimate is revalidated at the safe execution point");
            f.Flow.CompleteRequest(request);
            Check(f.Flow.ReservedEnergy(f.A) == 0
                && f.Flow.Scheduler.ActiveRequest == null,
                "failed active request releases its unconsumed reservation");
        }
    }

    private static void SchedulerOrderingChecks()
    {
        var skill = ScriptableObject.CreateInstance<SkillConfig>();
        try
        {
            BattleUnit caster = Unit(Faction.OurSide, 0, 100);
            var action = new BattleAction { caster = caster, skill = skill };
            var scheduler = new BattleActionScheduler();
            BattleActionRequest first = scheduler.Enqueue(action,
                BattleActionOrigin.FollowUp, BattleActionExecutionPolicy.FreeAction,
                100, BattleTargetFallback.AutoSelectFirst, 0);
            BattleActionRequest counter = scheduler.Enqueue(action,
                BattleActionOrigin.Counter, BattleActionExecutionPolicy.FreeAction,
                200, BattleTargetFallback.AutoSelectFirst, 0);
            BattleActionRequest second = scheduler.Enqueue(action,
                BattleActionOrigin.FollowUp, BattleActionExecutionPolicy.FreeAction,
                100, BattleTargetFallback.AutoSelectFirst, 0);
            Check(scheduler.TryTakeNext(out BattleActionRequest taken)
                && taken == counter && !scheduler.TryTakeNext(out _),
                "scheduler takes highest priority and blocks while a request is active");
            scheduler.Complete(taken);
            Check(scheduler.TryTakeNext(out taken) && taken == first,
                "scheduler preserves FIFO inside the same priority");
            scheduler.Complete(taken);
            Check(scheduler.TryTakeNext(out taken) && taken == second,
                "scheduler returns later equal-priority requests last");
            scheduler.Complete(taken);
            BattleActionRequest active = scheduler.Enqueue(action,
                BattleActionOrigin.Ultimate, BattleActionExecutionPolicy.FreeAction,
                300, BattleTargetFallback.AutoSelectFirst, 20);
            Check(scheduler.TryTakeNext(out taken) && taken == active,
                "scheduler can activate a reserved request");
            scheduler.ConsumeReservation(taken);
            scheduler.CancelAll();
            Check(scheduler.ActiveRequest == null && scheduler.PendingCount == 0
                && scheduler.GetReservedEnergy(caster) == 0,
                "cancel all clears an active request after its reservation was consumed");
        }
        finally { Object.DestroyImmediate(skill); }
    }

    private static void PresentationBarrierChecks()
    {
        var ultimateA = ScriptableObject.CreateInstance<SkillConfig>();
        var ultimateB = ScriptableObject.CreateInstance<SkillConfig>();
        try
        {
            foreach (SkillConfig skill in new[] { ultimateA, ultimateB })
            {
                skill.kind = SkillKind.Ultimate;
                skill.energyCost = 10;
                skill.targetSide = TargetSide.OtherSide;
                skill.targetRule = TargetRule.Single;
                skill.effectConfigs = new List<EffectConfig>
                {
                    new() { effectType = EffectType.Damage, scaleStat = StatType.Attack, value = 1 }
                };
            }
            BattleUnit first = Unit(Faction.OurSide, 0, 200);
            BattleUnit second = Unit(Faction.OurSide, 1, 100);
            BattleUnit enemy = Unit(Faction.Enemy, 0, 50);
            first.skills.Add(ultimateA);
            second.skills.Add(ultimateB);
            var flow = new BattleFlow(new[] { first, second, enemy });
            var presenter = new DeferredPresenter();
            var driver = new BattleTurnDriver(flow, presenter);
            driver.Start();
            Check(flow.QueueUltimate(new BattleAction
            {
                caster = first, skill = ultimateA, mainTarget = enemy
            }, out _).IsValid && driver.StepOnce().Succeeded
                && driver.State == BattleTurnState.Presenting
                && flow.ActionHistory.Count == 1 && presenter.PendingCount == 1,
                "single-step execution prioritizes a queued ultimate and enters its presentation barrier");
            Check(driver.QueueUltimate(new BattleAction
            {
                caster = second, skill = ultimateB, mainTarget = enemy
            }, out _).IsValid && flow.ActionHistory.Count == 1
                && flow.Scheduler.PendingCount == 1,
                "second ultimate waits while the first presentation is active");
            presenter.CompleteNext();
            Check(flow.ActionHistory.Count == 2
                && flow.ActionHistory[1].Caster == second
                && driver.State == BattleTurnState.Presenting
                && presenter.PendingCount == 1,
                "presentation completion releases exactly the next queued action");
            presenter.CompleteNext();
            Check(driver.State == BattleTurnState.WaitingForPlayer
                && flow.TurnCount == 0 && flow.CurrentTime == 0
                && first.nextActionTime == 50 && second.nextActionTime == 100,
                "queued ultimates return to the untouched normal action axis");

            BattleUnit stopCaster = Unit(Faction.OurSide, 0, 200);
            BattleUnit stopEnemy = Unit(Faction.Enemy, 0, 50);
            stopCaster.skills.Add(ultimateA);
            var stopFlow = new BattleFlow(new[] { stopCaster, stopEnemy });
            var stopPresenter = new DeferredPresenter();
            var stopDriver = new BattleTurnDriver(stopFlow, stopPresenter);
            stopDriver.Start();
            Check(stopDriver.QueueUltimate(new BattleAction
            {
                caster = stopCaster, skill = ultimateA, mainTarget = stopEnemy
            }, out _).IsValid && stopFlow.Scheduler.ActiveRequest != null,
                "stop regression starts with an active queued request");
            stopDriver.Stop();
            Check(stopDriver.State == BattleTurnState.Stopped
                && stopFlow.Scheduler.ActiveRequest == null
                && !stopFlow.HasQueuedActions
                && stopFlow.ReservedEnergy(stopCaster) == 0,
                "stopping the driver clears active and pending queued requests");
            stopPresenter.CompleteNext();
            Check(stopDriver.State == BattleTurnState.Stopped,
                "stale presentation completion cannot restart a stopped driver");
        }
        finally
        {
            Object.DestroyImmediate(ultimateA);
            Object.DestroyImmediate(ultimateB);
        }
    }

    private static void FollowUpRequestChecks()
    {
        using var f = new Fixture();
        f.A.talent = Game.Characters.CharacterTalent.FrostNova;
        f.A.talentValue = .5f;
        f.Basic.energyCost = 0;
        f.Basic.targetRule = TargetRule.AoE;
        Check(f.Flow.TryExecute(f.Action()).Succeeded
            && f.Flow.Scheduler.PendingCount == 1
            && f.Flow.LastResolution.FollowUps.Count == 0,
            "Frost Nova creates a scheduled follow-up instead of inline damage");
        IReadOnlyList<BattleActionOrderEntry> order = f.Flow.GetActionOrderSnapshot();
        Check(order.Any(entry => entry.Unit == f.A
                && entry.Origin == BattleActionOrigin.FollowUp
                && entry.IsQueuedAction)
            && order.Count(entry => entry.Unit == f.A) == 2,
            "follow-up and caster normal turn coexist in the action timeline");
        int turns = f.Flow.TurnCount;
        double time = f.Flow.CurrentTime;
        double next = f.A.nextActionTime;
        Check(f.Flow.TryTakeQueuedAction(out BattleActionRequest request)
            && request.Origin == BattleActionOrigin.FollowUp
            && f.Flow.TryExecute(request).Succeeded,
            "scheduled Frost Nova follow-up executes as its own request");
        f.Flow.CompleteRequest(request);
        Check(f.Flow.ActionHistory.Count == 2
            && f.Flow.ActionHistory[1].Origin == BattleActionOrigin.FollowUp
            && f.Flow.TurnCount == turns && f.Flow.CurrentTime == time
            && f.A.nextActionTime == next,
            "follow-up records independently without consuming a normal turn");
    }

    private static void ActionTimelineResourceChecks()
    {
        const string configPath = "Assets/Config/UI/HUD/BattleActionTimelineUIConfig.asset";
        const string panelPath = "Assets/Prefabs/UIPrefabs/HUD/Battle/BattleActionTimelinePanel.prefab";
        const string itemPath = "Assets/Prefabs/UIPrefabs/Widgets/Battle/BattleActionTimelineItem.prefab";
        UIConfig config = AssetDatabase.LoadAssetAtPath<UIConfig>(configPath);
        GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(panelPath);
        GameObject itemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(itemPath);
        UIRegistry registry = AssetDatabase.LoadAssetAtPath<UIRegistry>("Assets/Config/UI/UIRegistry.asset");
        Check(config != null && config.id == UIId.BattleActionTimeline
            && config.layer == UILayer.HUD && config.space == UISpace.Screen
            && config.prefab == panelPrefab?.GetComponent<BattleActionTimelinePanel>(),
            "action timeline UIConfig is categorized as a registered screen HUD");
        Check(registry != null && registry.TryGet(UIId.BattleActionTimeline, out UIConfig registered)
            && registered == config,
            "action timeline UIConfig is registered in UIManager registry");
        Check(panelPrefab != null && panelPrefab.GetComponent<BattleActionTimelinePanel>() != null
            && itemPrefab != null && itemPrefab.GetComponent<BattleActionTimelineItem>() != null,
            "action timeline panel and widget prefabs are loadable");
        Check(panelPrefab.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget)
            && itemPrefab.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget),
            "action timeline visuals do not intercept pointer input");

        GameObject allyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UIPrefabs/Widgets/Battle/HpShow.prefab");
        BattleUnitAllyUI allyUI = allyPrefab?.GetComponent<BattleUnitAllyUI>();
        var allySerialized = allyUI != null ? new SerializedObject(allyUI) : null;
        Image energyBar = allySerialized?.FindProperty("energy_bar").objectReferenceValue as Image;
        Check(energyBar != null && energyBar.gameObject.name == "Energy"
            && energyBar.type == Image.Type.Filled,
            "ally HUD binds its filled Energy image to BattleUnitAllyUI");
        GameObject allyInstance = allyPrefab != null
            ? (GameObject)PrefabUtility.InstantiatePrefab(allyPrefab) : null;
        try
        {
            BattleUnit ally = Unit(Faction.OurSide, 0, 100);
            BattleUnit enemy = Unit(Faction.Enemy, 0, 100);
            _ = new BattleFlow(new[] { ally, enemy });
            BattleUnitAllyUI instanceUI = allyInstance?.GetComponent<BattleUnitAllyUI>();
            Image instanceEnergy = instanceUI != null
                ? new SerializedObject(instanceUI).FindProperty("energy_bar").objectReferenceValue as Image
                : null;
            typeof(BattleUnitAllyUI).GetMethod("OnEnable",
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic)?.Invoke(instanceUI, null);
            instanceUI?.Bind(ally);
            Check(instanceEnergy != null && Mathf.Approximately(instanceEnergy.fillAmount, .5f),
                "ally energy image initializes from bound runtime energy");
            ally.ChangeEnergy(25);
            Check(Mathf.Approximately(instanceEnergy.fillAmount, .75f),
                "ally energy image reacts to EnergyChangedEvent");
        }
        finally
        {
            if (allyInstance != null)
                Object.DestroyImmediate(allyInstance);
        }
    }

    private static void ServiceChecks()
    {
        FactoryAndActorChecks();
        TurnDriverChecks();
    }

    private static void FactoryAndActorChecks()
    {
        var config = ScriptableObject.CreateInstance<UnitConfig>();
        var prefab = new GameObject("BattleActorRegistryPrefab");
        var anchor = new GameObject("BattleActorRegistryAnchor");
        config.displayName = "Service Unit";
        UnitBaseStatsAuthoring.Set(config, "health", 100f);
        UnitBaseStatsAuthoring.Set(config, "attack", 20f);
        UnitBaseStatsAuthoring.Set(config, "defence", 10f);
        UnitBaseStatsAuthoring.Set(config, "speed", 100f);
        config.prefab = prefab;
        var factory = new BattleUnitFactory();
        var registry = new BattleActorRegistry();
        try
        {
            BattleUnit unit = factory.Create(config, Faction.Enemy, 0, 7);
            Check(unit != null && factory.Units.Count == 1 && unit.tieBreaker == 7,
                "unit factory creates and tracks battle data");
            Check(registry.TrySpawn(config, unit, anchor.transform, out BattleUnitActor actor, out _)
                && registry.Count == 1 && actor.Unit == unit,
                "actor registry spawns and binds model");
            Check(registry.TryGetActor(unit, out BattleUnitActor found) && found == actor
                && actor.GetComponentInChildren<Collider>() != null,
                "actor registry owns lookup and collider setup");

            var runtimeSkill = ScriptableObject.CreateInstance<SkillConfig>();
            var runtimeBuff = ScriptableObject.CreateInstance<BuffConfig>();
            unit.ownsRuntimeSkills = true;
            unit.skills.Add(runtimeSkill);
            unit.buffs.Add(new BuffInstance { config = runtimeBuff, ownsConfig = true });
            registry.Clear();
            Check(registry.Count == 0 && actor == null && !registry.TryGetActor(unit, out _),
                "actor registry clears bindings and models");
            factory.ReleaseAll();
            Check(factory.Units.Count == 0 && runtimeSkill == null && runtimeBuff == null,
                "unit factory releases owned runtime assets");
        }
        finally
        {
            registry.Clear();
            factory.ReleaseAll();
            Object.DestroyImmediate(anchor);
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(config.BaseStats);
            Object.DestroyImmediate(config);
        }
    }

    private static void TurnDriverChecks()
    {
        var skill = ScriptableObject.CreateInstance<SkillConfig>();
        skill.targetSide = TargetSide.OtherSide;
        skill.targetRule = TargetRule.Single;
        skill.effectConfigs = new List<EffectConfig>
        {
            new() { effectType = EffectType.Damage, scaleStat = StatType.Attack, value = 1 }
        };
        try
        {
            BattleUnit ally = Unit(Faction.OurSide, 0, 200);
            BattleUnit enemy = Unit(Faction.Enemy, 0, 150);
            ally.skills.Add(skill);
            enemy.skills.Add(skill);
            var flow = new BattleFlow(new[] { ally, enemy }, new FirstSkillDecider());
            var driver = new BattleTurnDriver(flow);
            var states = new List<BattleTurnState>();
            driver.StateChanged += states.Add;
            driver.Start();
            Check(driver.State == BattleTurnState.WaitingForPlayer && flow.CurrentUnit == ally,
                "turn driver starts at player input boundary");
            BattleActionResult result = driver.Submit(new BattleAction
                { caster = ally, skill = skill, mainTarget = enemy });
            Check(result.Succeeded && flow.TurnCount == 2 && flow.CurrentUnit == ally
                && driver.State == BattleTurnState.WaitingForPlayer,
                "turn driver resolves player action and advances enemy turns");
            Check(states.Contains(BattleTurnState.Resolving)
                && states.Contains(BattleTurnState.AutoDeciding),
                "turn driver exposes resolving and auto-deciding states");

            int turns = flow.TurnCount;
            double time = flow.CurrentTime;
            driver.Stop();
            Check(!driver.StepOnce().Succeeded && driver.State == BattleTurnState.Stopped
                && flow.TurnCount == turns && flow.CurrentTime == time,
                "stopped turn driver cannot continue battle flow");

            BattleUnit fastEnemy = Unit(Faction.Enemy, 0, 200);
            BattleUnit slowAlly = Unit(Faction.OurSide, 0, 100);
            var blockedFlow = new BattleFlow(new[] { slowAlly, fastEnemy }, new FirstSkillDecider());
            var blockedDriver = new BattleTurnDriver(blockedFlow);
            blockedDriver.Start();
            Check(blockedDriver.State == BattleTurnState.Faulted
                && blockedDriver.LastFailure.Failure == ActionFailure.NoLegalAction
                && blockedFlow.TurnCount == 0,
                "turn driver faults instead of looping when AI has no legal action");

            BattleUnit finisher = Unit(Faction.OurSide, 0, 200);
            BattleUnit victim = Unit(Faction.Enemy, 0, 100);
            finisher.skills.Add(skill);
            victim.currentHealth = 1;
            var endingFlow = new BattleFlow(new[] { finisher, victim }, new FirstSkillDecider());
            var endingDriver = new BattleTurnDriver(endingFlow);
            endingDriver.Start();
            endingDriver.Submit(new BattleAction { caster = finisher, skill = skill, mainTarget = victim });
            Check(endingDriver.State == BattleTurnState.Completed
                && endingFlow.Result == BattleResult.AllyWin,
                "turn driver enters completed state after battle result");
        }
        finally
        {
            Object.DestroyImmediate(skill);
        }
    }

    private static void ResourceChecks()
    {
        using (var f = new Fixture())
        {
            int eventCount = 0;
            EnergyChangedEvent observed = default;
            using (GameEvents.Battle.Subscribe<EnergyChangedEvent>(message =>
            {
                if (message.BattleId != f.Flow.BattleId || message.Unit != f.A)
                    return;
                eventCount++;
                observed = message;
            }))
            {
                Check(f.A.ChangeEnergy(25) == 25
                    && eventCount == 1
                    && observed.PreviousEnergy == 50
                    && observed.CurrentEnergy == 75
                    && observed.MaxEnergy == 100,
                    "energy mutation publishes the exact ally energy change");
                f.A.SetEnergy(999);
                f.A.SetEnergy(100);
                Check(f.A.currentEnergy == 100 && eventCount == 2,
                    "energy clamps to maximum and unchanged values do not republish");
            }
        }
        using (var f = new Fixture())
        {
            Check(f.Flow.SkillPoints.Current == 4 && f.Flow.SkillPoints.Maximum == 5,
                "battle starts with configurable shared skill points");
            Check(f.Flow.TryExecute(f.Action()).Succeeded && f.Flow.SkillPoints.Current == 5,
                "basic attack restores one default skill point up to cap");
        }
        using (var f = new Fixture())
        {
            var skill = f.Skill();
            skill.kind = SkillKind.Skill;
            skill.skillPointCost = 2;
            f.A.skills.Add(skill);
            Check(f.Flow.TryExecute(f.Action(skill)).Succeeded && f.Flow.SkillPoints.Current == 2,
                "skill consumes configured shared skill points");
        }
        using (var f = new Fixture())
        {
            var skill = f.Skill();
            skill.kind = SkillKind.Skill;
            skill.skillPointCost = 1;
            f.A.skills.Add(skill);
            var flow = new BattleFlow(f.Units, initialSkillPoints: 0, maximumSkillPoints: 7);
            flow.Ready();
            var action = new BattleAction { caster = f.A, skill = skill, mainTarget = f.Enemy };
            Check(flow.SkillPoints.Maximum == 7
                && flow.ValidateAction(action).Failure == ActionFailure.InsufficientSkillPoints,
                "configured cap and insufficient skill points are validated");
        }
        var resource = new BattleSkillPoints(4, 5);
        resource.SetMaximum(7, true);
        Check(resource.Current == 6 && resource.Maximum == 7,
            "skill point maximum can change and optionally fill added capacity");
    }

    private static void EnemyDecisionChecks()
    {
        var heal = ScriptableObject.CreateInstance<SkillConfig>();
        var attack = ScriptableObject.CreateInstance<SkillConfig>();
        try
        {
            heal.kind = SkillKind.Skill;
            heal.targetSide = TargetSide.OurSide;
            heal.targetRule = TargetRule.Single;
            heal.effectConfigs = new List<EffectConfig>
            { new() { effectType = EffectType.Heal, scaleStat = StatType.Attack, value = 1 } };
            attack.kind = SkillKind.Basic;
            attack.targetSide = TargetSide.OtherSide;
            attack.targetRule = TargetRule.Single;
            attack.effectConfigs = new List<EffectConfig>
            { new() { effectType = EffectType.Damage, scaleStat = StatType.Attack, value = 1 } };
            BattleUnit caster = Unit(Faction.Enemy, 0, 100);
            BattleUnit wounded = Unit(Faction.Enemy, 1, 100);
            BattleUnit ally = Unit(Faction.OurSide, 0, 100);
            wounded.currentHealth = 100;
            caster.skills.Add(attack);
            caster.skills.Add(heal);
            BattleAction chosen = new EnemyActionDecider().Decide(caster,
                new[] { caster, wounded, ally });
            Check(chosen.skill == heal && chosen.mainTarget == wounded,
                "enemy AI prioritizes valuable healing and lowest-health ally");
            ally.currentHealth = 1;
            chosen = new EnemyActionDecider().Decide(caster,
                new[] { caster, wounded, ally });
            Check(chosen.skill == attack && chosen.mainTarget == ally,
                "enemy AI prioritizes a lethal attack");
        }
        finally
        {
            Object.DestroyImmediate(heal);
            Object.DestroyImmediate(attack);
        }
    }

    private static void ResolutionChecks()
    {
        using var f = new Fixture();
        Check(f.Flow.TryExecute(f.Action()).Succeeded, "structured resolution action succeeds");
        ActionResolution resolution = f.Flow.LastResolution;
        Check(resolution?.Record != null && resolution.Record.Turn == 1
            && resolution.Record.Caster == f.A && resolution.Record.Skill == f.Basic
            && resolution.Record.Effects.Any(effect => effect.Type == BattleEffectRecordType.Damage),
            "action record contains actor, skill, turn and damage effect");
        Check(f.Flow.ActionHistory.Count == 1
            && ReferenceEquals(f.Flow.ActionHistory[0], resolution.Record)
            && resolution.Record.SkillPointsBefore == 4
            && resolution.Record.SkillPointsAfter == 5,
            "battle history records shared resource changes");
        var queue = new BattlePresentationQueue();
        int started = 0, entries = 0, completed = 0;
        queue.PresentationStarted += _ => started++;
        queue.PresentationCompleted += _ => completed++;
        queue.EntryPresenter = (_, next) => { entries++; next(); };
        queue.Present(resolution, () => completed++);
        Check(started == 1 && entries == resolution.Record.Effects.Count && completed == 2,
            "presentation queue serializes all structured effects and completion");
    }

    private static void WaveChecks()
    {
        var skill = ScriptableObject.CreateInstance<SkillConfig>();
        skill.targetSide = TargetSide.OtherSide;
        skill.targetRule = TargetRule.Single;
        skill.effectConfigs = new List<EffectConfig>
        { new() { effectType = EffectType.Damage, scaleStat = StatType.Attack, value = 1 } };
        try
        {
            BattleUnit ally = Unit(Faction.OurSide, 0, 200);
            BattleUnit first = Unit(Faction.Enemy, 0, 100);
            ally.skills.Add(skill);
            first.currentHealth = 1;
            var flow = new BattleFlow(new[] { ally, first });
            flow.SetPendingWave(true);
            flow.Ready();
            Check(flow.TryExecute(new BattleAction { caster = ally, skill = skill, mainTarget = first }).Succeeded
                && flow.Result == BattleResult.None && flow.IsWaveCleared,
                "clearing a non-final wave does not publish victory");
            double waveTime = flow.CurrentTime;
            double allyNext = ally.nextActionTime;
            BattleUnit second = Unit(Faction.Enemy, 0, 100);
            second.currentHealth = 1;
            Check(flow.AddUnits(new[] { second })
                && second.BattleId == flow.BattleId
                && second.nextActionTime == waveTime + ActionQueue.BaseActionValue / second.speed
                && ally.nextActionTime == allyNext,
                "new wave joins at current time while ally action time is preserved");
            flow.SetPendingWave(false);
            Check(flow.TryExecute(new BattleAction { caster = ally, skill = skill, mainTarget = second }).Succeeded
                && flow.Result == BattleResult.AllyWin,
                "clearing final wave completes battle");
        }
        finally { Object.DestroyImmediate(skill); }
    }

    private static void PersistenceChecks()
    {
        var progress = new BattleProgressData();
        Check(progress.formation.TryAdd("Aegis", 0), "persistent formation accepts valid member");
        progress.completions.Add(new BattleCompletionRecord
        {
            encounterId = "verification",
            clearCount = 2,
            bestTurnCount = 8,
            lastRewards = new List<Game.Inventory.ItemAmount>
                { new("character_exp", 3) }
        });
        progress.revision = 4;
        BattleProgressData copy = progress.Copy();
        copy.formation.TryReplace("Verdant", 0);
        copy.completions[0].clearCount++;
        Check(progress.formation.ContainsUnit("Aegis")
            && !progress.formation.ContainsUnit("Verdant")
            && progress.completions[0].clearCount == 2
            && copy.revision == 4,
            "battle profile deep copy isolates formation and completion history");
    }

    /// <summary>Called by the existing isolated Play Mode scene regression after real battle initialization.</summary>
    public static void VerifyLiveCommands(Action<bool, string> check)
    {
        var manager = BattleManager.Instance;
        var flow = manager.battleFlow;
        var panel = Object.FindFirstObjectByType<BattleUIController>();
        var timeline = Object.FindFirstObjectByType<BattleActionTimelinePanel>();
        check(panel != null && flow.CurrentUnit?.faction == Faction.OurSide, "battle commands bind current player turn");
        IReadOnlyList<BattleActionOrderEntry> order = flow.GetActionOrderSnapshot(flow.CurrentUnit);
        check(UIManager.Instance.IsOpen(UIId.BattleActionTimeline) && timeline != null
            && timeline.VisibleEntryCount == flow.Units.Count(unit => unit.IsActive),
            "UIManager opens one action timeline entry per active battle unit");
        check(order.Count > 0 && order[0].Unit == flow.CurrentUnit
            && order[0].IsCurrent && order[0].RemainingActionValue == 0,
            "live action timeline starts with the current actor at zero action value");
        check(timeline.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget),
            "live action timeline does not block battle input");
        check(manager.TurnDriver != null && manager.TurnDriver.State == BattleTurnState.WaitingForPlayer,
            "battle manager delegates turn driving");
        BattleUnitActor modelActor = null;
        check(manager.ActorLookup != null
            && manager.ActorLookup.TryGetActor(flow.CurrentUnit, out modelActor)
            && modelActor.Unit == flow.CurrentUnit, "battle UI uses session actor lookup");
        var fields = new SerializedObject(panel);
        Button basic = (Button)fields.FindProperty("basicButton").objectReferenceValue;
        Button ultimate = (Button)fields.FindProperty("ultimateButton").objectReferenceValue;
        BattleUnit actor = flow.CurrentUnit;
        int oldEnergy = actor.currentEnergy;
        try
        {
            actor.currentEnergy = 0; panel.Refresh();
            check(!ultimate.interactable && basic.interactable, "real buttons disable unaffordable ultimate and allow basic");
            int turns = flow.TurnCount;
            double time = flow.CurrentTime;
            panel.OnUlt();
            check(flow.TurnCount == turns && flow.CurrentTime == time && flow.CurrentUnit == actor,
                "real rejected button callback preserves player turn");
            var skill = manager.CreateAbilityAction(SkillKind.Ultimate).skill;
            actor.currentEnergy = skill.energyCost; panel.Refresh();
            check(ultimate.interactable, "real ultimate button enables at exact energy cost");
        }
        finally { actor.currentEnergy = oldEnergy; panel.Refresh(); }
        int before = flow.TurnCount;
        panel.OnBasic();
        check(flow.TurnCount > before && (flow.Result != BattleResult.None || flow.CurrentUnit.faction == Faction.OurSide),
            "real basic command executes and advances enemies until player turn");
    }
}
