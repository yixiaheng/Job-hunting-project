using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Battle.Logic
{
    public enum BattleResult { None, AllyWin, EnemyWin, Draw }

    /// <summary>
    /// 鎴樻枟娴佺▼锛岀函鏁版嵁鎿嶄綔锛屽紑濮嬫垬鏂楀氨鏄痭ew BattleFlow
    /// </summary>
    public class BattleFlow
    {
        #region Fields
        public const int MaxTurns = 500;
        private readonly List<BattleUnit> units;
        private readonly ActionQueue queue;
        private readonly TargetSelector selector;
        private readonly EffectService effectService;
        public System.Guid BattleId { get; } = System.Guid.NewGuid();
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
            foreach (BattleUnit unit in units)
                unit.BattleId = BattleId;

            queue = new ActionQueue(units);
            selector = new TargetSelector(units);
            effectService = new EffectService(actionQueue: queue);
            this.decider = decider ?? new FirstSkillDecider();
        }

        public BattleResult Run()
        {
            Debug.Log($"Battle started. Allies={Count(Faction.OurSide)}, Enemies={Count(Faction.Enemy)}");

            while (Step())
            {
                
            }

            Debug.Log($"Battle ended: {ResultText(Result)}, Turns={TurnCount}");
            return Result;
        }

        public BattleUnit Peek() => queue.PeekNext(); 
        public void Ready()
        {
            currentUnit = Peek();
        }
        public bool Execute(BattleAction action)
        {
            if (CompleteIfEnded())
                return false;
            if (currentUnit == null)
                currentUnit = Peek();
            if (currentUnit == null)
            {
                Complete(BattleResult.Draw);
                return false;
            }

            queue.BeginTurn(currentUnit);
            if(Act(currentUnit, action) == false)
            {
                return false;
            }
            TickBuffs(currentUnit);
            queue.EndTurn(currentUnit);
            TurnCount++;
            CompleteIfEnded();

            currentUnit = Result == BattleResult.None ? Peek() : null;
            return true;
        }

        //Step鏄洿鎺ョ敤浜巃i璋冪敤鐨勶紝step鍒嗕负鑾峰彇 Action 銆?Execute 銆丳eek涓夋
        //浜轰负閫夋嫨鐨勬椂鍊欑洿鎺ヨ皟鐢‥xecute鍗冲彲
        public bool Step()
        {
            if (CompleteIfEnded())
                return false;
            if (currentUnit == null)
                currentUnit = Peek();
            if (currentUnit == null)
            {
                Complete(BattleResult.Draw);
                return false;
            }
            return Execute(decider.Decide(currentUnit, units));
        }

        private bool CompleteIfEnded()
        {
            if (Result != BattleResult.None)
                return true;

            BattleResult result = CheckResult();
            if (result == BattleResult.None && TurnCount >= MaxTurns)
                result = BattleResult.Draw;
            if (result == BattleResult.None)
                return false;

            Complete(result);
            return true;
        }

        private void Complete(BattleResult result)
        {
            if (Result != BattleResult.None || result == BattleResult.None)
                return;

            Result = result;
            currentUnit = null;
            GameOverEvent?.Invoke(result);
            Debug.Log(ResultText(result));
        }

        private bool Act(BattleUnit caster, BattleAction action)
        {
            if (action == null || action.skill == null)
            {
                Debug.Log($"[{TurnCount + 1}] {Name(caster)} cannot act.");
                return false;
            }

            var skill = action.skill;

            if(skill.energyCost > caster.currentEnergy)
            {
                Debug.Log($"{Name(caster)} does not have enough energy.");
                return false;
            }
            else
            {
                caster.currentEnergy -= skill.energyCost;
            }

            Debug.Log($"[{TurnCount + 1}] {Name(caster)} uses {SkillName(skill)}. HP={Hp(caster)}, Energy={caster.currentEnergy}");

            var targets = selector.Select(
                caster,
                skill.targetSide,
                skill.targetRule,
                action.mainTarget);

            if (targets == null || targets.Count == 0)
            {
                Debug.Log("    No targets.");
                caster.currentEnergy += skill.energyGain;
                return false;
            }

            if (skill.effectConfigs == null)
            {
                caster.currentEnergy += skill.energyGain;
                return false;
            }

            /// Effect鏁堟灉澶勭悊锛氳皟鐢ㄦ晥鏋滄湇鍔℃潵杩涜澶勭悊
            foreach (EffectConfig effect in skill.effectConfigs)
            {
                
                effectService.Execute(caster, targets, effect);
            }

            int energyBefore = caster.currentEnergy;
            caster.currentEnergy += skill.energyGain;

            if (skill.energyGain != 0)
                Debug.Log($"    {Name(caster)} energy {energyBefore}->{caster.currentEnergy}");
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
                    Debug.Log($"    {Name(u)}'s [{n}] expired.");
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
            string side = u.faction == Faction.OurSide ? "Ally" : "Enemy";
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
            BattleResult.AllyWin => "Ally Victory",
            BattleResult.EnemyWin => "Enemy Victory",
            BattleResult.Draw => "Draw",
            _ => r.ToString()
        };
    }
}

