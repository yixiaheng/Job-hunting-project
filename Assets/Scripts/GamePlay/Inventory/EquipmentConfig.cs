using System;
using UnityEngine;

namespace Game.Inventory
{
    [Serializable]
    public struct EquipmentStats
    {
        public int health;
        public int attack;
        public int defence;
        public int speed;
        public float damageBonus;
    }

    [CreateAssetMenu(fileName = "EquipmentConfig", menuName = "Inventory/Equipment")]
    public class EquipmentConfig : ItemConfig
    {
        public EquipmentKind equipmentKind;
        public PathType path;
        public Game.Characters.EquipmentSlot slot;
        public Game.Characters.RelicSetConfig relicSet;
        public StatType percentStat = StatType.Attack;
        public float basePercent;
        public float percentPerLevel;
        public int[] levelCaps = { 20, 40, 60, 80 };
        public int[] ascensionCosts = { 2, 4, 8 };
        public int experienceBase = 200;
        public int experiencePerLevel = 40;
        public string experienceMaterial = "equipment_exp";
        public string ascensionMaterial = "equipment_ascension";
        public int maxSuperimposition = 5;
        public StatType passiveStat = StatType.Attack;
        public float passiveBase = 0.08f;
        public float passivePerRank = 0.02f;
        [Min(1)] public int maxLevel = 80;
        public EquipmentStats baseStats;
        public EquipmentStats statsPerLevel;
        public string passiveEffectId;

        public EquipmentStats GetStats(int level)
        {
            int growth = equipmentKind == EquipmentKind.Instrument
                ? Mathf.Clamp(level, 0, maxLevel) : Mathf.Clamp(level, 1, maxLevel) - 1;
            return new EquipmentStats
            {
                health = baseStats.health + statsPerLevel.health * growth,
                attack = baseStats.attack + statsPerLevel.attack * growth,
                defence = baseStats.defence + statsPerLevel.defence * growth,
                speed = baseStats.speed + statsPerLevel.speed * growth,
                damageBonus = baseStats.damageBonus + statsPerLevel.damageBonus * growth
            };
        }

        private void OnValidate()
        {
            category = ItemCategory.Equipment;
            useKind = ItemUseKind.None;
        }
    }
}
