using UnityEngine;

namespace Battle.Logic
{
    public readonly struct DamageApplication
    {
        public int ShieldDamage { get; }
        public int HealthDamage { get; }
        public int PreviousHealth { get; }
        public int CurrentHealth { get; }
        public bool IsDead => CurrentHealth <= 0;

        public DamageApplication(int shieldDamage, int healthDamage, int previousHealth, int currentHealth)
        {
            ShieldDamage = shieldDamage;
            HealthDamage = healthDamage;
            PreviousHealth = previousHealth;
            CurrentHealth = currentHealth;
        }
    }

    public readonly struct AttackResult
    {
        public BattleUnit Attacker { get; }
        public BattleUnit Target { get; }
        public int RawDamage { get; }
        public int FinalDamage { get; }
        public int ShieldDamage { get; }
        public int HealthDamage { get; }
        public int PreviousHealth { get; }
        public int CurrentHealth { get; }
        public bool IsDead => CurrentHealth <= 0;

        public AttackResult(BattleUnit attacker, BattleUnit target, int rawDamage, int finalDamage, DamageApplication application)
        {
            Attacker = attacker;
            Target = target;
            RawDamage = rawDamage;
            FinalDamage = finalDamage;
            ShieldDamage = application.ShieldDamage;
            HealthDamage = application.HealthDamage;
            PreviousHealth = application.PreviousHealth;
            CurrentHealth = application.CurrentHealth;
        }
    }

    public sealed class AttackService
    {
        public AttackResult Execute(BattleUnit attacker, BattleUnit target, EffectConfig effect)
        {
            if (attacker == null) throw new System.ArgumentNullException(nameof(attacker));
            if (target == null) throw new System.ArgumentNullException(nameof(target));
            if (effect == null) throw new System.ArgumentNullException(nameof(effect));

            int rawDamage = Mathf.Max(0, Mathf.RoundToInt(attacker.GetStat(effect.scaleStat) * effect.value));
            int defence = target.GetStat(StatType.Defence);
            int finalDamage = Mathf.Max(1, rawDamage - Mathf.Max(0, defence));
            DamageApplication application = target.ApplyDamage(finalDamage);
            var result = new AttackResult(attacker, target, rawDamage, finalDamage, application);

            Debug.Log($"    伤害 {Name(target)}  -{result.HealthDamage}  HP {result.PreviousHealth}→{result.CurrentHealth}/{target.maxHealth}  防御{defence}{(result.IsDead ? "  倒下" : "")}");
            return result;
        }

        private static string Name(BattleUnit unit)
        {
            if (unit == null) return "?";
            if (!string.IsNullOrEmpty(unit.displayName)) return unit.displayName;
            return unit.faction == Faction.OurSide ? $"我方{unit.pos}" : $"敌方{unit.pos}";
        }
    }
}
