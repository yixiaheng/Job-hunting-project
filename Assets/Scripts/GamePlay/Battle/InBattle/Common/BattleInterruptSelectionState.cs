using System;
using System.Linq;

namespace Battle.Logic
{
    public sealed class BattleInterruptSelectionState
    {
        private readonly BattleFlow flow;

        public BattleInterruptSelectionState(BattleFlow flow)
            => this.flow = flow ?? throw new ArgumentNullException(nameof(flow));

        public BattleUnit Actor { get; private set; }
        public SkillConfig Skill { get; private set; }
        public BattleUnit Target { get; private set; }
        public BattleActionPreview Preview { get; private set; }
        public event Action Changed;

        public bool Begin(BattleUnit actor, BattleUnit target = null)
        {
            Actor = actor;
            Skill = actor?.skills?.FirstOrDefault(skill => skill != null
                && skill.kind == SkillKind.Ultimate);
            Target = target;
            Refresh();
            return Preview?.Validation.IsValid == true;
        }

        public bool SelectTarget(BattleUnit target)
        {
            if (Actor == null || Skill == null || target == null
                || target.BattleId != flow.BattleId)
                return false;
            Target = target;
            Refresh();
            return Preview.Validation.IsValid;
        }

        public void Clear()
        {
            Actor = null;
            Skill = null;
            Target = null;
            Preview = null;
            Changed?.Invoke();
        }

        public BattleAction CreateAction() => new()
        {
            caster = Actor,
            skill = Skill,
            mainTarget = Target
        };

        private void Refresh()
        {
            Preview = flow.PreviewAction(CreateAction(), BattleActionOrigin.Ultimate);
            Target = Preview.Action.mainTarget;
            Changed?.Invoke();
        }
    }
}
