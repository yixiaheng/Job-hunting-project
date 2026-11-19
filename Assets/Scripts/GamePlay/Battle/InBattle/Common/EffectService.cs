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
        private readonly BattleSkillPoints skillPoints;

        public EffectService(
            AttackService attackService = null,
            HealService healService = null,
            EnergyService energyService = null,
            BuffService buffService = null,
            ActionQueue actionQueue = null,
            BattleSkillPoints skillPoints = null)
        {
            this.attackService = attackService ?? new AttackService();
            this.healService = healService ?? new HealService();
            this.energyService = energyService ?? new EnergyService();
            this.buffService = buffService ?? new BuffService();
            this.actionQueue = actionQueue;
            this.skillPoints = skillPoints;
        }

        public void Execute(
            BattleUnit caster,
            IReadOnlyList<BattleUnit> targets,
            EffectConfig effect, ActionResolution resolution = null)
        {
            if (caster == null || targets == null || effect == null) return;

            foreach (BattleUnit target in targets)
            {
                if (target == null || !target.IsActive) continue;
                if (target.faction == caster.faction && effect.effectType != EffectType.Damage
                    && effect.effectType != EffectType.ApplyBuff)
                    if (resolution != null) resolution.AffectedAlly = true;

                switch (effect.effectType)
                {
                    case EffectType.Damage:
                        if (target.IsActive)
                        {
                            AttackResult attack = attackService.Execute(caster, target, effect);
                            resolution?.AddEffect(new BattleEffectRecord(BattleEffectRecordType.Damage,
                                caster, target, attack.HealthDamage, attack.PreviousHealth,
                                attack.CurrentHealth, effect.key));
                            if (target.faction != caster.faction && resolution != null)
                            { resolution.Damaged.Add(target); resolution.Killed |= !target.IsActive; }
                        }
                        break;
                    case EffectType.Heal:
                        if (target.IsActive)
                        {
                            int before = target.currentHealth;
                            int healed = healService.Execute(caster, target, effect);
                            if (healed > 0)
                            {
                                resolution?.Healed.Add(target);
                                resolution?.AddEffect(new BattleEffectRecord(BattleEffectRecordType.Heal,
                                    caster, target, healed, before, target.currentHealth, effect.key));
                            }
                        }
                        break;
                    case EffectType.GainEnergy:
                        if (target.IsActive)
                        {
                            int before = target.currentEnergy;
                            int gained = energyService.Execute(target, effect);
                            resolution?.AddEffect(new BattleEffectRecord(BattleEffectRecordType.Energy,
                                caster, target, gained, before, target.currentEnergy, effect.key));
                        }
                        break;
                    case EffectType.ApplyBuff:
                        if (buffService.Execute(target, effect) && resolution != null)
                        {
                            if (target == caster)
                            {
                                var applied = target.buffs.Find(b => b.config != null && b.config.id == effect.buff.id);
                                if (applied != null) applied.skipNextOwnerTick = true;
                            }
                            resolution.AppliedDebuff |= effect.buff.isDebuff && target.faction != caster.faction;
                            resolution.AffectedAlly |= !effect.buff.isDebuff && target.faction == caster.faction;
                            resolution.AddEffect(new BattleEffectRecord(BattleEffectRecordType.BuffApplied,
                                caster, target, key: effect.buff.Name));
                        }
                        break;
                    case EffectType.AddShield:
                        if (target.IsActive)
                        {
                            int before = target.shield;
                            int basis = effect.scaleStat == StatType.Health
                                ? caster.maxHealth
                                : caster.GetStat(effect.scaleStat);
                            target.shield += Mathf.Max(0, Mathf.RoundToInt(effect.value * basis));
                            resolution?.AddEffect(new BattleEffectRecord(BattleEffectRecordType.Shield,
                                caster, target, target.shield - before, before, target.shield, effect.key));
                        }
                        break;
                    case EffectType.RemoveBuff:
                        RecordRemoved(caster, target, RemoveStatuses(target, false), resolution, effect.key);
                        break;
                    case EffectType.RemoveDeBuff:
                        RecordRemoved(caster, target, RemoveStatuses(target, true), resolution, effect.key);
                        break;
                    case EffectType.AdvanceAction:
                        if (target == caster && resolution != null)
                            resolution.SelfActionMultiplier *= Mathf.Max(0f, 1f - effect.value);
                        else
                        {
                            double before = target.nextActionTime;
                            actionQueue?.ModifyAction(target, effect.value);
                            resolution?.AddEffect(new BattleEffectRecord(BattleEffectRecordType.ActionAdvanced,
                                caster, target, before: Mathf.RoundToInt((float)before),
                                after: Mathf.RoundToInt((float)target.nextActionTime), key: effect.key));
                        }
                        break;
                    case EffectType.GainSkillPoint:
                        if (target.faction == Faction.OurSide && skillPoints != null)
                        {
                            int before = skillPoints.Current;
                            int gained = skillPoints.Gain(Mathf.RoundToInt(effect.value));
                            resolution?.AddEffect(new BattleEffectRecord(BattleEffectRecordType.SkillPoint,
                                caster, target, gained, before, skillPoints.Current, "SkillPoint"));
                        }
                        break;
                }
            }
        }

        private static int RemoveStatuses(BattleUnit target, bool debuffsOnly)
        {
            int removed = 0;
            for (int i = target.buffs.Count - 1; i >= 0; i--)
            {
                BuffInstance instance = target.buffs[i];
                if (instance?.config == null) continue;
                if (!debuffsOnly || instance.config.isDebuff)
                {
                    target.buffs.RemoveAt(i);
                    removed++;
                    if (instance.ownsConfig) Object.Destroy(instance.config);
                }
            }
            return removed;
        }

        private static void RecordRemoved(BattleUnit caster, BattleUnit target, int removed,
            ActionResolution resolution, string key)
        {
            if (removed > 0)
                resolution?.AddEffect(new BattleEffectRecord(BattleEffectRecordType.BuffRemoved,
                    caster, target, removed, key: key));
        }
    }
}






