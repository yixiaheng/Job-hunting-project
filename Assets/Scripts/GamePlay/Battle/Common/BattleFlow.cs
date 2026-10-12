using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Battle.Logic
{
    public enum BattleResult { None, AllyWin, EnemyWin, Draw }

    public class BattleFlow
    {
        public const int MaxTurns = 500;
        private readonly List<BattleUnit> units;
        private readonly ActionQueue queue;
        private readonly TargetSelector selector;
        public BattleResult Result { get; private set; } = BattleResult.None;
        public int TurnCount { get; private set; }
        private readonly IActionDecider decider;
        private BattleUnit currentUnit;
        public BattleUnit CurrentUnit => currentUnit;

        public UnityAction<BattleResult> GameOverEvent;

        public BattleFlow(IEnumerable<BattleUnit> allUnits, IActionDecider decider = null)
        {
            units = new List<BattleUnit>(allUnits);
            queue = new ActionQueue(units);
            selector = new TargetSelector(units);
            this.decider = decider ?? new FirstSkillDecider();
        }

        public BattleResult Run()
        {
            Debug.Log($"战斗开始  我方{Count(Faction.OurSide)}人  敌方{Count(Faction.Enemy)}人");

            while (Step())
            {
                
            }

            if (Result == BattleResult.None)
                Result = BattleResult.Draw;

            Debug.Log($"战斗结束：{ResultText(Result)}  共行动{TurnCount}次");
            return Result;
        }

        public BattleUnit Peek() => queue.PeekNext(); 
        public void Ready()
        {
            currentUnit = Peek();
        }
        public bool Execute(BattleAction action)
        {
            if (currentUnit == null)
                currentUnit = Peek();
            if (currentUnit == null || Result != BattleResult.None)
                return false;

            queue.BeginTurn(currentUnit);
            if(Act(currentUnit, action) == false)
            {
                return false;
            }
            TickBuffs(currentUnit);
            queue.EndTurn(currentUnit);
            TurnCount++;
            Result = CheckResult();

            if(Result != BattleResult.None)
            {   
                //游戏结束
                GameOverEvent?.Invoke(Result);
                Debug.Log(Result switch
                {
                    BattleResult.AllyWin => "玩家胜利",
                    BattleResult.EnemyWin => "敌人胜利",
                    BattleResult.Draw => "平局",
                    _ => "未知结果"
                });
            }

            currentUnit = Result == BattleResult.None ? Peek() : null;
            return true;
        }

        //Step是直接用于ai调用的，step分为获取 Action 、 Execute 、Peek三步
        //人为选择的时候直接调用Execute即可
        public bool Step()
        {
            if (Result != BattleResult.None || TurnCount >= MaxTurns)
                return false;
            if (currentUnit == null)
                currentUnit = Peek();
            if (currentUnit == null)
                return false;
            return Execute(decider.Decide(currentUnit, units));
        }

        private bool Act(BattleUnit caster, BattleAction action)
        {
            if (action == null || action.skill == null)
            {
                Debug.Log($"[{TurnCount + 1}] {Name(caster)} 无法行动");
                return false;
            }

            var skill = action.skill;

            if(skill.energyCost > caster.currentEnergy)
            {
                Debug.Log($"{Name(caster)}能量不足，无法释放");
                return false;
            }
            else
            {
                caster.currentEnergy -= skill.energyCost;
            }

            Debug.Log($"[{TurnCount + 1}] {Name(caster)} 使用【{SkillName(skill)}】  HP {Hp(caster)}  能量{caster.currentEnergy}");

            var targets = selector.Select(
                caster,
                skill.targetSide,
                skill.targetRule,
                action.mainTarget);

            if (targets == null || targets.Count == 0)
            {
                Debug.Log("    没有目标");
                caster.currentEnergy += skill.energyGain;
                return false;
            }

            if (skill.effectConfigs == null)
            {
                caster.currentEnergy += skill.energyGain;
                return false;
            }

            //真正技能开始生效。
            foreach (var effect in skill.effectConfigs)
            {
                switch (effect.effectType)
                {
                    case EffectType.Damage:
                        foreach (var u in targets)
                        {
                            if (!u.IsActive) continue;
                            int scaled = Scale(caster, effect.scaleStat, effect.value);
                            int def = u.GetStat(StatType.Defence);
                            int dmg = Mathf.Max(1, scaled - def);
                            int hpBefore = u.currentHealth;
                            u.currentHealth = Mathf.Max(0, u.currentHealth - dmg);
                            string dead = u.currentHealth <= 0 ? "  倒下" : "";
                            int hpMax = Mathf.Max(u.maxHealth, hpBefore);
                            Debug.Log($"    伤害 {Name(u)}  -{dmg}  HP {hpBefore}→{u.currentHealth}/{hpMax}  防御{def}{dead}");
                        }
                        break;

                    case EffectType.Heal:
                        foreach (var u in targets)
                        {
                            if (!u.IsActive) continue;
                            int heal = Mathf.Max(1, Scale(caster, effect.scaleStat, effect.value));
                            int hpBefore = u.currentHealth;
                            int cap = Mathf.Max(u.maxHealth, u.currentHealth);
                            u.currentHealth = Mathf.Min(cap, u.currentHealth + heal);
                            Debug.Log($"    治疗 {Name(u)}  +{heal}  HP {hpBefore}→{u.currentHealth}/{cap}");
                        }
                        break;

                    case EffectType.GainEnergy:
                        foreach (var u in targets)
                        {
                            if (!u.IsActive) continue;
                            int before = u.currentEnergy;
                            u.currentEnergy += (int)effect.value;
                            Debug.Log($"    {Name(u)} 能量 {before}→{u.currentEnergy}");
                        }
                        break;

                    case EffectType.ApplyBuff:
                        foreach (var u in targets)
                            ApplyBuff(u, effect.buff, effect.stack);
                        break;

                    case EffectType.AddShield:
                    case EffectType.RemoveBuff:
                    case EffectType.RemoveDeBuff:
                    case EffectType.AdvanceAction:
                        break;
                }
            }

            int energyBefore = caster.currentEnergy;
            caster.currentEnergy += skill.energyGain;

            if (skill.energyGain != 0)
                Debug.Log($"    {Name(caster)} 能量 {energyBefore}→{caster.currentEnergy}");
            return true;
        }

        int Scale(BattleUnit caster, StatType stat, float value)
        {
            return Mathf.RoundToInt(caster.GetStat(stat) * value);
        }

        BattleResult CheckResult()
        {
            bool allyAlive = false, enemyAlive = false;
            foreach (var u in units)
            {
                if (!u.IsActive) continue;
                if (u.faction == Faction.OurSide) allyAlive = true;
                else enemyAlive = true;
            }
            if (!allyAlive && !enemyAlive) return BattleResult.Draw;
            if (!enemyAlive) return BattleResult.AllyWin;
            if (!allyAlive) return BattleResult.EnemyWin;
            return BattleResult.None;
        }

        void ApplyBuff(BattleUnit target, BuffConfig config, int addStacks)
        {
            if (target == null || !target.IsActive || config == null)
                return;

            int max = Mathf.Max(1, config.maxStack);
            int add = Mathf.Max(1, addStacks);

            foreach (var b in target.buffs)
            {
                if (b.config != null && b.config.id == config.id)
                {
                    b.stacks = Mathf.Min(max, b.stacks + add);
                    b.remainingTurns = config.durationTimes;
                    Debug.Log($"    Buff {Name(target)} 获得【{config.Name}】 {b.stacks}层/{max}  持续{b.remainingTurns}回合  {StatNow(target, config.stat)}");
                    return;
                }
            }

            var inst = new BuffInstance
            {
                config = config,
                stacks = Mathf.Min(max, add),
                remainingTurns = config.durationTimes
            };
            target.buffs.Add(inst);
            Debug.Log($"    Buff {Name(target)} 获得【{config.Name}】 {inst.stacks}层/{max}  持续{inst.remainingTurns}回合  {StatNow(target, config.stat)}");
        }

        void TickBuffs(BattleUnit u)
        {
            for (int i = u.buffs.Count - 1; i >= 0; i--)
            {
                var b = u.buffs[i];
                b.remainingTurns--;
                if (b.remainingTurns <= 0)
                {
                    string n = b.config != null ? b.config.Name : "Buff";
                    u.buffs.RemoveAt(i);
                    Debug.Log($"    {Name(u)} 的【{n}】结束");
                }
            }
        }


        string StatNow(BattleUnit u, StatType stat)
        {
            return stat switch
            {
                StatType.Defence => $"防御{u.defence}→{u.GetStat(stat)}",
                StatType.Attack => $"攻击{u.attack}→{u.GetStat(stat)}",
                StatType.Speed => $"速度{u.speed}→{u.GetStat(stat)}",
                _ => ""
            };
        }

        int Count(Faction f)
        {
            int n = 0;
            foreach (var u in units)
                if (u.faction == f) n++;
            return n;
        }

        static string Name(BattleUnit u)
        {
            if (u == null) return "?";
            if (!string.IsNullOrEmpty(u.displayName))
                return u.displayName;
            string side = u.faction == Faction.OurSide ? "我方" : "敌方";
            return $"{side}{u.pos}";
        }

        static string SkillName(SkillConfig s)
        {
            if (s == null) return "?";
            if (!string.IsNullOrEmpty(s.Name)) return s.Name;
            return s.name;
        }

        static string Hp(BattleUnit u)
        {
            int max = Mathf.Max(u.maxHealth, u.currentHealth);
            return $"{u.currentHealth}/{max}";
        }

        static string ResultText(BattleResult r) => r switch
        {
            BattleResult.AllyWin => "我方胜利",
            BattleResult.EnemyWin => "敌方胜利",
            BattleResult.Draw => "平局",
            _ => r.ToString()
        };
    }
}
