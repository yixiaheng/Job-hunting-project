using System;
using System.Collections.Generic;
using Game.Inventory;
using UnityEngine;

namespace Game.Characters
{
    public static class CharacterStatCalculator
    {
        public static string DescribeSources(UnitConfig config, CharacterProgress progress, InventoryService inventory)
        {
            var text = new System.Text.StringBuilder("属性来源\n初始：生命 ").Append(config.BaseStats.Health).Append(" 攻击 ").Append(config.BaseStats.Attack).Append(" 防御 ").Append(config.BaseStats.Defence).Append(" 速度 ").Append(config.BaseStats.Speed);
            var growth = config.growth;
            text.Append("\n等级：").Append(progress.level).Append("；突破阶段：").Append(progress.ascension).Append("；三围系数：").Append(CharacterGrowthTable.Shared.Evaluate(progress.level, progress.ascension).ToString("F3"));
            text.Append("\n角色升阶：").Append(progress.rank).Append(" 阶（百分比加成）");
            var sets = new Dictionary<RelicSetConfig, int>();
            foreach (string id in progress.equipment)
            {
                if (!inventory.TryGetEquipment(id, out var instance) || !inventory.TryGetConfig(instance.configId, out var item))
                    continue;
                var equipment = (EquipmentConfig)item;
                text.Append("\n").Append(equipment.displayName).Append(" Lv.").Append(instance.level);
                if (equipment.equipmentKind == EquipmentKind.LightCone)
                    text.Append("（基础属性与命途被动，合并 ").Append(instance.superimposition).Append("）");
                else
                    text.Append("（固定主属性）");
                if (equipment.relicSet != null)
                {
                    sets.TryGetValue(equipment.relicSet, out int n);
                    sets[equipment.relicSet] = n + 1;
                }
            }

            foreach (var pair in sets)
                text.Append("\n").Append(pair.Key.displayName).Append("：").Append(pair.Value).Append(pair.Value >= 4 && !pair.Key.planar ? "件，2/4件效果生效" : pair.Value >= 2 ? "件，2件效果生效" : "件，未激活");
            return text.ToString();
        }

        public static CharacterStats Calculate(UnitConfig config, CharacterProgress progress, InventoryService inventory, out float openingShield)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (progress.level < 1) throw new ArgumentOutOfRangeException(nameof(progress.level));
            if (progress.ascension < 0) throw new ArgumentOutOfRangeException(nameof(progress.ascension));
            openingShield = 0f;
            config.BaseStats.Validate();
            CharacterGrowthConfig growth = config.growth;
            float multiplier = CharacterGrowthTable.Shared.Evaluate(progress.level, progress.ascension);
            float health = config.BaseStats.Health * multiplier;
            float attack = config.BaseStats.Attack * multiplier;
            float defence = config.BaseStats.Defence * multiplier;
            float speed = config.BaseStats.Speed;
            float flatHealth = 0, flatAttack = 0, flatDefence = 0, flatSpeed = 0, damage = 0;
            var percentages = new float[4];
            var counts = new Dictionary<RelicSetConfig, int>();
            if (growth != null)
            {
                if (progress.rank >= 1)
                    percentages[(int)growth.primaryStat] += growth.rankPrimaryFirst;
                if (progress.rank >= 3)
                    percentages[(int)StatType.Health] += growth.rankHealth;
                if (progress.rank >= 5)
                    percentages[(int)growth.primaryStat] += growth.rankPrimaryLast;
            }

            for (int index = 0; index < 7; index++)
            {
                if (progress.equipment == null || index >= progress.equipment.Length || inventory == null || !inventory.TryGetEquipment(progress.equipment[index], out EquipmentInstance instance) || !inventory.TryGetConfig(instance.configId, out ItemConfig item) || !(item is EquipmentConfig equipment))
                    continue;
                EquipmentStats stats = equipment.GetStats(instance.level);
                if (equipment.equipmentKind == EquipmentKind.LightCone)
                {
                    health += stats.health;
                    attack += stats.attack;
                    defence += stats.defence;
                    speed += stats.speed;
                    if (equipment.path == config.path)
                        percentages[(int)equipment.passiveStat] += equipment.passiveBase + equipment.passivePerRank * (instance.superimposition - 1);
                }
                else
                {
                    flatHealth += stats.health;
                    flatAttack += stats.attack;
                    flatDefence += stats.defence;
                    flatSpeed += stats.speed;
                    percentages[(int)equipment.percentStat] += equipment.basePercent + equipment.percentPerLevel * instance.level;
                    if (equipment.relicSet != null)
                    {
                        bool planarSlot = index == (int)EquipmentSlot.Sphere || index == (int)EquipmentSlot.Rope;
                        if (planarSlot == equipment.relicSet.planar)
                        {
                            counts.TryGetValue(equipment.relicSet, out int count);
                            counts[equipment.relicSet] = count + 1;
                        }
                    }
                }

                damage += stats.damageBonus;
            }

            foreach (KeyValuePair<RelicSetConfig, int> pair in counts)
            {
                if (pair.Value >= 2)
                    percentages[(int)pair.Key.twoPieceStat] += pair.Key.twoPieceBonus;
                if (!pair.Key.planar && pair.Value >= 4)
                {
                    damage += pair.Key.fourPieceDamageBonus;
                    openingShield += pair.Key.fourPieceOpeningShield;
                }
            }

            return new CharacterStats
            {
                Level = progress.level,
                health = Mathf.Max(1, Mathf.RoundToInt(health * (1 + percentages[(int)StatType.Health]) + flatHealth)),
                attack = Mathf.Max(0, Mathf.RoundToInt(attack * (1 + percentages[(int)StatType.Attack]) + flatAttack)),
                defence = Mathf.Max(0, Mathf.RoundToInt(defence * (1 + percentages[(int)StatType.Defence]) + flatDefence)),
                speed = Mathf.Max(1, Mathf.RoundToInt(speed * (1 + percentages[(int)StatType.Speed]) + flatSpeed)),
                damageBonus = Mathf.Max(0, damage),
                // 双暴、元素和击破属性不参与自然成长。
                OffensiveStats = config.BaseStats.OffensiveStats
            };
        }
    }
}
