using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Battle.Logic
{
    public enum BattleResult { None, AllyWin, EnemyWin, Draw }

    /// <summary>
    /// 战斗流程，纯数据操作，开始战斗就是new BattleFlow
    /// </summary>
    public class BattleFlow
    {
        #region Fields
        public const int MaxTurns = 500;
        private readonly List<BattleUnit> units;
        private readonly ActionQueue queue;
        private readonly TargetSelector selector;
        private readonly EffectService effectService;
        public BattleResult Result { get; private set; } = BattleResult.None;
        public int TurnCount { get; private set; }
        private readonly IActionDecider decider;
        private BattleUnit currentUnit;
        public BattleUnit CurrentUnit => currentUnit;

        public UnityAction<BattleResult> GameOverEvent;
        #endregion

        public BattleFlow(IEnumerable<BattleUnit> allUnits, IActionDecider decider = null)
        {
            units = new List<BattleUnit>(allUnits);
            queue = new ActionQueue(units);
            selector = new TargetSelector(units);
            effectService = new EffectService();
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

            /// Effect效果处理：调用效果服务来进行处理
            foreach (EffectConfig effect in skill.effectConfigs)
            {
                
                effectService.Execute(caster, targets, effect);
            }

            int energyBefore = caster.currentEnergy;
            caster.currentEnergy += skill.energyGain;

            if (skill.energyGain != 0)
                Debug.Log($"    {Name(caster)} 能量 {energyBefore}→{caster.currentEnergy}");
            return true;
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
