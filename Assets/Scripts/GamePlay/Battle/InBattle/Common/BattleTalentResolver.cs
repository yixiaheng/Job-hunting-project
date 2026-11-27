using System.Collections.Generic;
using System.Linq;
using Game.Characters;
using UnityEngine;

namespace Battle.Logic
{
    //行动的结算结果
    public sealed class ActionResolution
    {
        public readonly HashSet<BattleUnit> Damaged = new HashSet<BattleUnit>();
        public readonly HashSet<BattleUnit> Healed = new HashSet<BattleUnit>();
        public bool Killed;
        public bool AppliedDebuff;
        public bool AffectedAlly;
        public float SelfActionMultiplier = 1f;
        public readonly List<AttackResult> FollowUps = new List<AttackResult>();
        public BattleActionRecord Record { get; internal set; }
        internal int ActiveEffectGroup { get; set; } = -1;

        internal void AddEffect(BattleEffectRecord effect)
        {
            Record?.effects.Add(effect.InGroup(ActiveEffectGroup));
        }
    }

    /// <summary>由战斗流程同步调用；需要独立表现的反应返回为调度请求，不在父动作内追加结算。</summary>
    public static class BattleTalentResolver
    {
        public static void OnTurnStart(BattleUnit caster, IReadOnlyList<BattleUnit> units)
        {
            if (caster.talent != CharacterTalent.Aegis)
                return;
            BattleUnit target = units.Where(u => u.IsActive && u.faction == caster.faction).OrderBy(u => (float)u.currentHealth / Mathf.Max(1, u.maxHealth)).ThenBy(u => u.pos).FirstOrDefault();
            if (target != null)
                target.shield += Mathf.Max(0, Mathf.RoundToInt(caster.GetStat(StatType.Defence) * caster.talentValue));
        }

        public static IReadOnlyList<BattleAction> AfterAction(BattleUnit caster,
            SkillConfig skill, ActionResolution result, IReadOnlyList<BattleUnit> units,
            ActionQueue queue, BattleActionOrigin origin)
        {
            var triggered = new List<BattleAction>();
            switch (caster.talent)
            {
                case CharacterTalent.Verdant:
                    foreach (BattleUnit target in result.Healed)
                    {
                        if (!target.IsActive)
                            continue;
                        BuffInstance buff = target.buffs.Find(b => b.config != null && b.config.id == -6001);
                        if (buff == null)
                        {
                            var config = ScriptableObject.CreateInstance<BuffConfig>();
                            config.hideFlags = HideFlags.DontSave;
                            config.id = -6001;
                            config.Name = "春风庇护";
                            config.stat = StatType.Defence;
                            config.value = caster.talentValue;
                            config.durationTimes = caster.talentDuration;
                            config.maxStack = 1;
                            buff = new BuffInstance
                            {
                                config = config,
                                stacks = 1,
                                ownsConfig = true
                            };
                            target.buffs.Add(buff);
                        }

                        buff.remainingTurns = caster.talentDuration;
                    }

                    break;
                case CharacterTalent.VoltHunt:
                    if (result.Killed)
                    {
                        Gain(caster, caster.talentEnergy);
                        queue.ModifyAction(caster, caster.talentSecondary);
                    }

                    break;
                case CharacterTalent.FrostNova:
                    if (origin != BattleActionOrigin.FollowUp
                        && origin != BattleActionOrigin.Counter
                        && result.Damaged.Count >= 2)
                    {
                        BattleUnit target = result.Damaged
                            .Where(unit => unit != null && unit.IsActive)
                            .OrderBy(unit => unit.pos).FirstOrDefault();
                        SkillConfig followUp = EnsureFrostNovaAction(caster);
                        if (target != null && followUp != null)
                            triggered.Add(new BattleAction
                            {
                                caster = caster,
                                skill = followUp,
                                mainTarget = target
                            });
                    }

                    break;
                case CharacterTalent.UmbralHex:
                    if (result.AppliedDebuff)
                        Gain(caster, caster.talentEnergy);
                    break;
                case CharacterTalent.AstralChord:
                    if (skill.kind != SkillKind.Basic && result.AffectedAlly)
                        foreach (BattleUnit target in units)
                            if (target != caster && target.IsActive && target.faction == caster.faction)
                                Gain(target, caster.talentEnergy);
                    break;
            }
            return triggered;
        }

        private static SkillConfig EnsureFrostNovaAction(BattleUnit caster)
        {
            if (caster.talentAction != null)
                return caster.talentAction;

            var skill = ScriptableObject.CreateInstance<SkillConfig>();
            skill.hideFlags = HideFlags.DontSave;
            skill.id = -7000 - Mathf.Max(0, caster.tieBreaker);
            skill.Name = "Frost Nova Follow-Up";
            skill.kind = SkillKind.Skill;
            skill.element = caster.element;
            skill.targetSide = TargetSide.OtherSide;
            skill.targetRule = TargetRule.AoE;
            skill.effectConfigs = new List<EffectConfig>
            {
                new()
                {
                    key = "Talent.FrostNova.FollowUp",
                    origin = EffectOrigin.Talent,
                    effectType = EffectType.Damage,
                    value = caster.talentValue,
                    scaleStat = StatType.Attack
                }
            };
            caster.talentAction = skill;
            caster.ownsRuntimeTalentAction = true;
            return skill;
        }

        private static void Gain(BattleUnit target, int value)
            => target.ChangeEnergy(value);
    }
}
