using System.Collections.Generic;
using UnityEngine;

namespace Battle.Logic
{
    public sealed class EffectService
    {
        private readonly AttackService attackService;
        private readonly HealService healService;
        private readonly EnergyService energyService;
        private readonly BuffService buffService;
        private readonly ActionQueue actionQueue;

        public EffectService(
            AttackService attackService = null,
            HealService healService = null,
            EnergyService energyService = null,
            BuffService buffService = null,
            ActionQueue actionQueue = null)
        {
            this.attackService = attackService ?? new AttackService();
            this.healService = healService ?? new HealService();
            this.energyService = energyService ?? new EnergyService();
            this.buffService = buffService ?? new BuffService();
            this.actionQueue = actionQueue;
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
                        if (target.IsActive)
                        {
                            int basis = effect.scaleStat == StatType.Health
                                ? caster.maxHealth
                                : caster.GetStat(effect.scaleStat);
                            target.shield += Mathf.Max(0, Mathf.RoundToInt(effect.value * basis));
                        }
                        break;
                    case EffectType.RemoveBuff:
                        RemoveStatuses(target, false);
                        break;
                    case EffectType.RemoveDeBuff:
                        RemoveStatuses(target, true);
                        break;
                    case EffectType.AdvanceAction:
                        actionQueue?.ModifyAction(target, effect.value);
                        break;
                }
            }
        }

        private static void RemoveStatuses(BattleUnit target, bool debuffsOnly)
        {
            for (int i = target.buffs.Count - 1; i >= 0; i--)
            {
                BuffInstance instance = target.buffs[i];
                if (instance?.config == null) continue;
                if (!debuffsOnly || instance.config.isDebuff)
                    target.buffs.RemoveAt(i);
            }
        }
    }
}






