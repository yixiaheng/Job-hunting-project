using System;
using UnityEngine;

namespace Battle.Logic
{
    /// <summary>伤害相关的面板属性。使用值类型，配置、养成快照和战斗单位互不共享可变数据。</summary>
    [Serializable]
    public struct BattleOffensiveStats
    {
        [SerializeField, InspectorName("暴击率"), Min(0), Tooltip("暴击率；0.05 = 5%。允许超过100%，实际判定时使用EffectiveCritRate。")]
        private float critRate;
        [SerializeField, InspectorName("暴击伤害"), Min(0), Tooltip("暴击额外伤害；0.5 = 50%，暴击总倍率为1.5。")]
        private float critDamage;
        [SerializeField, InspectorName("击破特攻"), Min(0), Tooltip("击破特攻；1 = 100%，对应倍率为1 + 此值。不是普通伤害加成。")]
        private float breakEffect;
        [SerializeField, InspectorName("击破效率加成"), Min(0), Tooltip("击破效率加成；0 = 基础100%效率，0.5 = 150%效率。用于削韧，不直接加到击破特攻。")]
        private float breakEfficiencyBonus;
        [SerializeField, InspectorName("元素伤害加成"), Tooltip("按本次伤害元素读取；不是仅按角色所属元素读取。")]
        private ElementDamageBonuses elementalDamage;

        public float CritRate => critRate;
        public float EffectiveCritRate => Mathf.Clamp01(critRate);
        public float CritDamage => critDamage;
        public float BreakEffect => breakEffect;
        public float BreakEfficiencyBonus => breakEfficiencyBonus;
        public float BreakEfficiencyMultiplier => 1f + breakEfficiencyBonus;
        public ElementDamageBonuses ElementalDamage => elementalDamage;
        public static BattleOffensiveStats Default => new(0.05f, 0.5f);

        public BattleOffensiveStats(float critRate, float critDamage, float breakEffect = 0f,
            float breakEfficiencyBonus = 0f, ElementDamageBonuses elementalDamage = default)
        {
            this.critRate = critRate;
            this.critDamage = critDamage;
            this.breakEffect = breakEffect;
            this.breakEfficiencyBonus = breakEfficiencyBonus;
            this.elementalDamage = elementalDamage;
            Validate();
        }

        public void Validate()
        {
            RequireNonNegativeFinite(critRate, nameof(critRate));
            RequireNonNegativeFinite(critDamage, nameof(critDamage));
            RequireNonNegativeFinite(breakEffect, nameof(breakEffect));
            RequireNonNegativeFinite(breakEfficiencyBonus, nameof(breakEfficiencyBonus));
            elementalDamage.Validate();
        }

        internal static void RequireNonNegativeFinite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                throw new ArgumentOutOfRangeException(name, value, "基础伤害属性必须是非负有限小数；0.2表示20%。");
        }
    }

    /// <summary>七种元素的独立加成；字段固定且不依赖枚举数组下标。</summary>
    [Serializable]
    public struct ElementDamageBonuses
    {
        [SerializeField, InspectorName("物理"), Min(0)] private float physical;
        [SerializeField, InspectorName("火"), Min(0)] private float fire;
        [SerializeField, InspectorName("冰"), Min(0)] private float ice;
        [SerializeField, InspectorName("雷"), Min(0)] private float lightning;
        [SerializeField, InspectorName("风"), Min(0)] private float wind;
        [SerializeField, InspectorName("量子"), Min(0)] private float quantum;
        [SerializeField, InspectorName("虚数"), Min(0)] private float imaginary;

        public ElementDamageBonuses(float physical = 0, float fire = 0, float ice = 0,
            float lightning = 0, float wind = 0, float quantum = 0, float imaginary = 0)
        {
            this.physical = physical;
            this.fire = fire;
            this.ice = ice;
            this.lightning = lightning;
            this.wind = wind;
            this.quantum = quantum;
            this.imaginary = imaginary;
            Validate();
        }

        public float Get(ElementType element) => element switch
        {
            ElementType.Physical => physical,
            ElementType.Fire => fire,
            ElementType.Ice => ice,
            ElementType.Lightning => lightning,
            ElementType.Wind => wind,
            ElementType.Quantum => quantum,
            ElementType.Imaginary => imaginary,
            _ => throw new ArgumentOutOfRangeException(nameof(element), element, "未知元素。")
        };

        public void Validate()
        {
            BattleOffensiveStats.RequireNonNegativeFinite(physical, nameof(physical));
            BattleOffensiveStats.RequireNonNegativeFinite(fire, nameof(fire));
            BattleOffensiveStats.RequireNonNegativeFinite(ice, nameof(ice));
            BattleOffensiveStats.RequireNonNegativeFinite(lightning, nameof(lightning));
            BattleOffensiveStats.RequireNonNegativeFinite(wind, nameof(wind));
            BattleOffensiveStats.RequireNonNegativeFinite(quantum, nameof(quantum));
            BattleOffensiveStats.RequireNonNegativeFinite(imaginary, nameof(imaginary));
        }
    }
}
