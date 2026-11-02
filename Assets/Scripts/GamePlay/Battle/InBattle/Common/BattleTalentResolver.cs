using System.Collections.Generic;
using System.Linq;
using Game.Characters;
using UnityEngine;

namespace Battle.Logic
{
    public sealed class ActionResolution
    {
        public readonly HashSet<BattleUnit> Damaged = new HashSet<BattleUnit>();
        public readonly HashSet<BattleUnit> Healed = new HashSet<BattleUnit>();
        public bool Killed;
        public bool AppliedDebuff;
        public bool AffectedAlly;
        public float SelfActionMultiplier = 1f;
        public readonly List<AttackResult> FollowUps = new List<AttackResult>();
    }

    /// <summary>由战斗流程同步调用，追加伤害不再次进入主动技能结算。</summary>
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

        public static void AfterAction(BattleUnit caster, SkillConfig skill, ActionResolution result, IReadOnlyList<BattleUnit> units, ActionQueue queue)
        {
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
                    if (result.Damaged.Count >= 2)
                    {
                        var effect = new EffectConfig
                        {
                            key = "Talent.FrostNova.FollowUp",
                            origin = EffectOrigin.Talent,
                            effectType = EffectType.Damage,
                            value = caster.talentValue,
                            scaleStat = StatType.Attack
                        };
                        var attack = new AttackService();
                        foreach (BattleUnit target in result.Damaged)
                            if (target.IsActive)
                                result.FollowUps.Add(attack.Execute(caster, target, effect));
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
        }

        private static void Gain(BattleUnit target, int value) => target.currentEnergy = Mathf.Clamp(target.currentEnergy + value, 0, Mathf.Max(1, target.maxEnergy));
    }
}
