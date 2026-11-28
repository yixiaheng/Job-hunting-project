using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

namespace Battle.Logic
{
    /// <summary>
    /// 战斗数据映射
    /// </summary>
    public static class BattleDataMaping
    {
        public static BattleUnit ToUnit(UnitConfig cfg, Faction faction, int pos, int tieBreaker)
        {
            if (cfg == null)
                return null;

            cfg.BaseStats.Validate();
            var stats = cfg.BaseStats;
            var skills = cfg.skills != null
                ? new List<SkillConfig>(cfg.skills)
                : new List<SkillConfig>();

            var unit = new BattleUnit
            {
                displayName = string.IsNullOrEmpty(cfg.displayName) ? cfg.name : cfg.displayName,
                Icon = cfg.icon,
                element = cfg.element,
                path = cfg.path,
                faction = faction,
                pos = pos,
                tieBreaker = tieBreaker,
                maxHealth = Mathf.RoundToInt(stats.Health),
                currentHealth = Mathf.RoundToInt(stats.Health),
                attack = Mathf.RoundToInt(stats.Attack),
                defence = Mathf.RoundToInt(stats.Defence),
                speed = Mathf.RoundToInt(stats.Speed),
                currentEnergy = stats.StartEnergy,
                maxEnergy = Mathf.Max(1, stats.MaxEnergy),
                skills = skills,
                talentAction = cfg.talentAction
            };
            unit.SetOffensiveStats(stats.OffensiveStats);
            return unit;
        }
    }
}
