using System.Collections.Generic;

namespace Battle.Logic
{
    public sealed class EffectService
    {
        private readonly AttackService attackService;
        private readonly HealService healService;
        private readonly EnergyService energyService;
        private readonly BuffService buffService;

        public EffectService(
            AttackService attackService = null,
            HealService healService = null,
            EnergyService energyService = null,
            BuffService buffService = null)
        {
            this.attackService = attackService ?? new AttackService();
            this.healService = healService ?? new HealService();
            this.energyService = energyService ?? new EnergyService();
            this.buffService = buffService ?? new BuffService();
        }

        public void Execute(
            BattleUnit caster,
            IReadOnlyList<BattleUnit> targets,
            EffectConfig effect)
        {
            if (caster == null || targets == null || effect == null) return;

            foreach (BattleUnit target in targets)
            {
                if (target == null) continue;

                switch (effect.effectType)
                {
                    case EffectType.Damage:
                        if (target.IsActive) attackService.Execute(caster, target, effect);
                        break;
                    case EffectType.Heal:
                        if (target.IsActive) healService.Execute(caster, target, effect);
                        break;
                    case EffectType.GainEnergy:
                        if (target.IsActive) energyService.Execute(target, effect);
                        break;
                    case EffectType.ApplyBuff:
                        buffService.Execute(target, effect);
                        break;
                    case EffectType.AddShield:
                    case EffectType.RemoveBuff:
                    case EffectType.RemoveDeBuff:
                    case EffectType.AdvanceAction:
                        break;
                }
            }
        }
    }
}
