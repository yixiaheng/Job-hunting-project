using System;
using System.Collections.Generic;
using System.Linq;

namespace Battle.Logic
{
    public sealed class BattleActionPreview
    {
        public ActionValidation Validation { get; }
        public BattleAction Action { get; }
        public IReadOnlyList<BattleUnit> Targets { get; }
        public IReadOnlyList<IReadOnlyList<BattleUnit>> EffectTargets { get; }
        public BattleActionPreview(ActionValidation validation, BattleAction action, List<BattleUnit> targets,
            List<IReadOnlyList<BattleUnit>> effects)
        { Validation = validation; Action = action; Targets = targets.AsReadOnly(); EffectTargets = effects.AsReadOnly(); }
    }

    public sealed class BattleSelectionState
    {
        private readonly BattleFlow flow;
        public BattleUnit Actor { get; private set; }
        public SkillConfig Skill { get; private set; }
        public BattleUnit Target { get; private set; }
        public BattleActionPreview Preview { get; private set; }
        public event Action Changed;
        public BattleSelectionState(BattleFlow value) => flow = value;
        public void Begin(BattleUnit actor)
        {
            Actor = actor; Target = null; Skill = null;
            foreach (var kind in new[] { SkillKind.Basic, SkillKind.Skill })
            {
                var candidate = actor?.skills?.FirstOrDefault(s => s != null && s.kind == kind);
                if (candidate != null && flow.PreviewAction(new BattleAction { caster = actor, skill = candidate }).Validation.IsValid)
                { Skill = candidate; break; }
            }
            Refresh();
        }
        public bool SelectSkill(SkillKind kind)
        {
            var skill = Actor?.skills?.FirstOrDefault(s => s != null && s.kind == kind);
            if (skill == null || !flow.PreviewAction(new BattleAction { caster = Actor, skill = skill, mainTarget = Target }).Validation.IsValid) return false;
            Skill = skill; Refresh(); return true;
        }
        public bool SelectTarget(BattleUnit target)
        {
            if (Skill == null || target == null || !target.IsActive || target.BattleId != flow.BattleId || !flow.Units.Contains(target)) return false;
            var candidates = new TargetSelector(flow.Units).Select(Actor, Skill.targetSide,
                Skill.targetRule == TargetRule.Self ? TargetRule.Self : TargetRule.AoE, null);
            if (!candidates.Contains(target)) return false;
            Target = target; Refresh(); return true;
        }
        public void Refresh()
        {
            Preview = flow.PreviewAction(new BattleAction { caster = Actor, skill = Skill, mainTarget = Target });
            Target = Preview.Action.mainTarget;
            Changed?.Invoke();
        }
        public BattleAction CreateAction() => new() { caster = Actor, skill = Skill, mainTarget = Target };
    }
}
