using System;
using Battle.Logic;
using UnityEngine;

namespace Game.Characters
{
    /// <summary>独立静态属性SO。三围存1级白值，其余属性不自然成长。</summary>
    public sealed class UnitBaseStatsConfig : ScriptableObject
    {
        [SerializeField, InspectorName("1级生命"), Min(1)] private float health = 100;
        [SerializeField, InspectorName("1级攻击"), Min(0)] private float attack = 50;
        [SerializeField, InspectorName("1级防御"), Min(0)] private float defence = 20;
        [SerializeField, InspectorName("固定基础速度"), Min(1)] private float speed = 100;
        [SerializeField, InspectorName("初始能量"), Min(0)] private int startEnergy;
        [SerializeField, InspectorName("能量上限"), Min(1)] private int maxEnergy = 100;
        [SerializeField, InspectorName("固定伤害属性（不随等级或突破增长）")]
        private BattleOffensiveStats offensiveStats = BattleOffensiveStats.Default;
        public float Health => health;
        public float Attack => attack;
        public float Defence => defence;
        public float Speed => speed;
        public int StartEnergy => startEnergy;
        public int MaxEnergy => maxEnergy;
        public BattleOffensiveStats OffensiveStats => offensiveStats;
        public void Validate()
        {
            if (!Finite(health) || health < 1 || !Finite(attack) || attack < 0 || !Finite(defence) || defence < 0
                || !Finite(speed) || speed < 1 || startEnergy < 0 || maxEnergy < 1 || startEnergy > maxEnergy)
                throw new InvalidOperationException("基础属性、速度或能量范围无效：" + name);
            offensiveStats.Validate();
        }
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
