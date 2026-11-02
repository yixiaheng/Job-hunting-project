using System.Collections.Generic;
using Battle.Logic;
using Game.Inventory;
using UnityEngine;

namespace Game.Characters
{
    public sealed class BattleCharacterSnapshot
    {
        private readonly UnitConfig source;
        private readonly CharacterStats stats;
        private readonly float openingShield;
        private readonly CharacterTalent talent;
        private readonly float talentValue, talentSecondary;
        private readonly int talentEnergy, talentDuration;
        private readonly List<SkillSnapshot> skills = new List<SkillSnapshot>();
        public BattleCharacterSnapshot(UnitConfig source, CharacterProgress progress, InventoryService inventory)
        {
            this.source = source;
            stats = CharacterStatCalculator.Calculate(source, progress, inventory, out openingShield);
            CharacterGrowthConfig g = source.growth;
            talent = g != null && progress.level >= g.talentUnlockLevel ? g.talent : CharacterTalent.None;
            talentValue = g != null ? g.talentValue : 0;
            talentSecondary = g != null ? g.talentSecondary : 0;
            talentEnergy = g != null ? g.talentEnergy : 0;
            talentDuration = g != null ? g.talentDuration : 0;
            foreach (SkillConfig skill in source.skills)
            {
                if (skill == null)
                    continue;
                skills.Add(new SkillSnapshot(skill, progress, g));
            }
        }

        public BattleUnit CreateUnit(int pos, int tie)
        {
            BattleUnit unit = BattleDataMaping.ToUnit(source, Faction.OurSide, pos, tie);
            unit.maxHealth = unit.currentHealth = stats.health;
            unit.attack = stats.attack;
            unit.defence = stats.defence;
            unit.speed = stats.speed;
            unit.damageBonus = stats.damageBonus;
            unit.shield = Mathf.RoundToInt(stats.defence * openingShield);
            unit.talent = talent;
            unit.talentValue = talentValue;
            unit.talentSecondary = talentSecondary;
            unit.talentEnergy = talentEnergy;
            unit.talentDuration = talentDuration;
            unit.currentEnergy = Mathf.Clamp(unit.currentEnergy, 0, unit.maxEnergy);
            unit.skills = new List<SkillConfig>();
            foreach (SkillSnapshot snapshot in skills)
                unit.skills.Add(snapshot.Create());
            unit.ownsRuntimeSkills = true;
            return unit;
        }

        private sealed class SkillSnapshot
        {
            private readonly int id, energyCost, energyGain, skillPointCost;
            private readonly string name;
            private readonly SkillKind kind;
            private readonly ElementType element;
            private readonly TargetRule rule;
            private readonly TargetSide side;
            private readonly List<EffectConfig> effects = new List<EffectConfig>();
            public SkillSnapshot(SkillConfig skill, CharacterProgress progress, CharacterGrowthConfig growth)
            {
                id = skill.id;
                name = skill.Name;
                kind = skill.kind;
                element = skill.element;
                energyCost = skill.energyCost;
                energyGain = skill.energyGain;
                skillPointCost = skill.skillPointCost;
                rule = skill.targetRule;
                side = skill.targetSide;
                if (skill.effectConfigs == null)
                    return;
                foreach (EffectConfig source in skill.effectConfigs)
                {
                    if (source == null)
                        continue;
                    EffectConfig copy = Copy(source);
                    if (copy.growsWithSkill)
                    {
                        float factor = CharacterSkillCalculator.Factor(growth, progress, skill, copy);
                        if (copy.effectType == EffectType.ApplyBuff)
                            copy.buffValueMultiplier *= factor;
                        else
                            copy.value *= factor;
                    }

                    effects.Add(copy);
                }
            }

            public SkillConfig Create()
            {
                var skill = ScriptableObject.CreateInstance<SkillConfig>();
                skill.hideFlags = HideFlags.DontSave;
                skill.id = id;
                skill.Name = name;
                skill.kind = kind;
                skill.element = element;
                skill.energyCost = energyCost;
                skill.energyGain = energyGain;
                skill.skillPointCost = skillPointCost;
                skill.targetRule = rule;
                skill.targetSide = side;
                skill.effectConfigs = new List<EffectConfig>();
                foreach (EffectConfig effect in effects)
                    skill.effectConfigs.Add(Copy(effect));
                return skill;
            }

            private static EffectConfig Copy(EffectConfig e) => new EffectConfig
            {
                origin = e.origin,
                key = e.key,
                growsWithSkill = e.growsWithSkill,
                effectType = e.effectType,
                value = e.value,
                scaleStat = e.scaleStat,
                target = e.target,
                buffId = e.buffId,
                buff = e.buff,
                stack = e.stack,
                overrideTarget = e.overrideTarget,
                targetSide = e.targetSide,
                buffValueMultiplier = e.buffValueMultiplier
            };
        }
    }
}
