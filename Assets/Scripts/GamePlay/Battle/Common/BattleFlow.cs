using System.Collections.Generic;
using Unity.PlasticSCM.Editor.WebApi;
using Unity.VisualScripting;
using UnityEngine;

namespace Battle.Logic
{
    public enum BattleResult {None, AllyWin, EnemyWin, Draw}

    public class BattleFlow
    {
        public const int MaxTurns = 500;
        private readonly List<BattleUnit> units;
        private readonly ActionQueue queue;
        private readonly TargetSelector selector;
        public BattleResult Result { get; private set; } = BattleResult.None;
        public int TurnCount{ get; private set;}
        private readonly IActionDecider decider;

        public BattleFlow(IEnumerable<BattleUnit> allUnits, IActionDecider decider = null)
        {
            units = new List<BattleUnit>(allUnits);
            queue = new ActionQueue(units);
            selector = new TargetSelector(units);

            this.decider = decider ?? new FirstSkillDecider();
        }

        public BattleResult Run()
        {
            while(Result == BattleResult.None && TurnCount < MaxTurns)
            {
                var unit = queue.PeekNext();
                if(unit == null)   break;

                queue.BeginTurn(unit);
                //行动
                var action = decider.Decide(unit);
                Act(unit, action);
                queue.EndTurn(unit);
                TurnCount++;
                Result = CheckResult();
            }

            if (Result == BattleResult.None)
                Result = BattleResult.Draw; // 防死循环
            return Result;
        }

        private void Act(BattleUnit caster, BattleAction action)
        {
            if (action == null || action.skill == null)
                return;

            var skill = action.skill;
            var targets = selector.Select(
                caster,
                skill.targetSide,
                skill.targetRule,
                action.mainTarget);

            if (skill.effectConfigs == null)
                return;

            foreach (var effect in skill.effectConfigs)
            {
                switch (effect.effectType)
                {
                    case EffectType.Damage:
                        foreach (var u in targets)
                        {
                            if (!u.isActive) continue;
                            int scaled = Scale(caster, effect.scaleStat, effect.value);
                            int dmg = Mathf.Max(1, scaled - u.defence);
                            u.currentHealth -= dmg;
                            Debug.Log($"t={queue.CurrentTime:F1}  {Name(caster)} -> {Name(u)}  dmg={dmg}  hp={u.currentHealth}");
                        }
                        break;

                    case EffectType.Heal:
                        foreach (var u in targets)
                        {
                            if (!u.isActive) continue;
                            int heal = Mathf.Max(1, Scale(caster, effect.scaleStat, effect.value));
                            u.currentHealth = Mathf.Min(u.maxHealth, u.currentHealth + heal);
                            Debug.Log($"t={queue.CurrentTime:F1}  {Name(caster)} heals {Name(u)}  +{heal}  hp={u.currentHealth}");
                        }
                        break;

                    case EffectType.GainEnergy:
                        foreach (var u in targets)
                        {
                            if (!u.isActive) continue;
                            u.currentEnergy += (int)effect.value;
                        }
                        break;

                    case EffectType.AddShield:
                    case EffectType.ApplyBuff:
                    case EffectType.RemoveBuff:
                    case EffectType.RemoveDeBuff:
                    case EffectType.AdvanceAction:
                        break;
                }
            }

            caster.currentEnergy += skill.energyGain;
        }

        static int Scale(BattleUnit caster, StatType stat, float value)
        {
            int baseStat = stat == StatType.Health ? caster.currentHealth : caster.attack;
            return Mathf.RoundToInt(baseStat * value);
        }


        BattleResult CheckResult()
        {
            bool allyAlive = false, enemyAlive = false;
            foreach (var u in units)
            {
                if (!u.isActive) continue;
                if (u.faction == Faction.OurSide) allyAlive = true;
                else enemyAlive = true;
            }
            if (!allyAlive && !enemyAlive) return BattleResult.Draw;
            if (!enemyAlive) return BattleResult.AllyWin;
            if (!allyAlive) return BattleResult.EnemyWin;
            return BattleResult.None;
        }

        static string Name(BattleUnit u) => $"{u.faction}#{u.tieBreaker}";
    }
}
