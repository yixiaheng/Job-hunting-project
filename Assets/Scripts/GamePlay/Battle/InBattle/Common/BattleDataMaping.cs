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

            var skills = cfg.skills != null
                ? new List<SkillConfig>(cfg.skills)
                : new List<SkillConfig>();

            return new BattleUnit
            {
                displayName = string.IsNullOrEmpty(cfg.displayName) ? cfg.name : cfg.displayName,
                element = cfg.element,
                path = cfg.path,
                faction = faction,
                pos = pos,
                tieBreaker = tieBreaker,
                maxHealth = cfg.maxHealth,
                currentHealth = cfg.maxHealth,
                attack = cfg.attack,
                defence = cfg.defence,
                speed = cfg.speed,
                currentEnergy = cfg.startEnergy,
                maxEnergy = Mathf.Max(1, cfg.maxEnergy),
                skills = skills
            };
        }
    }
}
