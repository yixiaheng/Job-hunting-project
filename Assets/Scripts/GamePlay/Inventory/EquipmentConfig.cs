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
        [Min(1)] public int maxLevel = 80;
        public EquipmentStats baseStats;
        public EquipmentStats statsPerLevel;
        public string passiveEffectId;

        public EquipmentStats GetStats(int level)
        {
            int growth = Mathf.Clamp(level, 1, maxLevel) - 1;
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
