using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace Battle.Logic
{
    public class TargetSelector
    {
        private readonly List<BattleUnit> units;

        public TargetSelector(IEnumerable<BattleUnit> units)
        {
            this.units = new List<BattleUnit>(units);
        }

        public List<BattleUnit> Select(BattleUnit caster, TargetSide side ,TargetRule rule, BattleUnit mainTarget)
        {
            List<BattleUnit> res = new List<BattleUnit>();

            Faction targetFaction;
            if(side == TargetSide.OurSide)
                targetFaction = caster.faction;
            else
                targetFaction = caster.faction == Faction.OurSide ? Faction.Enemy : Faction.OurSide;

            switch(rule)
            {
                case TargetRule.Single:
                    BattleUnit unit;
                    if(mainTarget != null && mainTarget.isActive && mainTarget.faction == targetFaction)
                    {
                        unit = mainTarget;
                    }
                    else 
                        unit = FirstAlive(targetFaction);
                    
                    if(unit != null)
                        res.Add(unit);
                    break;
                case TargetRule.AoE:
                    foreach(var u in units)
                    {
                        if(u.faction == targetFaction && u.isActive)
                        {
                            res.Add(u);
                        }
                    }
                    break;
                case TargetRule.Self:
                    if(caster.isActive)
                        res.Add(caster);
                    break;
                case TargetRule.Spread:
                    BattleUnit center = null;
                    if(mainTarget != null && mainTarget.isActive && mainTarget.faction == targetFaction)
                    {
                        center = mainTarget;
                    }
                    else 
                        center = FirstAlive(targetFaction);

                    if (center == null)
                        break;
                    TryAdd(res, center);
                    TryAdd(res, Find(targetFaction, center.pos - 1));
                    TryAdd(res, Find(targetFaction, center.pos + 1));

                    break;
            }

            return res;
        }
    

        private BattleUnit Find(Faction faction, int pos)
        {
            foreach(var u in units)
            {
                if(u.isActive && u.faction == faction && u.pos == pos)
                    return u;
            }
            return null;
        }

        private BattleUnit FirstAlive(Faction targetFaction)
        {
            BattleUnit res;
            foreach(var u in units)
            {
                if(u.faction == targetFaction && u.isActive)
                {
                    res = u;
                    return res;
                }
            }
            return null;
        }
        void TryAdd(List<BattleUnit> res, BattleUnit unit)
        {
            if (unit == null || !unit.isActive)
                return;
            if (res.Contains(unit))
                return;
            res.Add(unit);
        }

    }
}
