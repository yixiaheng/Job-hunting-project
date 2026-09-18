using System.Collections.Generic;
using UnityEngine;
namespace Battle.Logic
{
    public class ActionQueue
    {
        public const double BaseActionValue = 10000d;

        private readonly List<BattleUnit> units = new();
        public double CurrentTime {get; private set;}

        public ActionQueue(IEnumerable<BattleUnit> allUnits)
        {
            //逐个Add
            foreach(var unit in allUnits)
            {
                Add(unit);
            }
        }

        public void Add(BattleUnit unit)       //角色/敌人加入到行动的跑条中
        {
            if(unit != null)
            {
                if(unit.speed <= 0)
                    return;
                units.Add(unit);
                unit.nextActionTime = CurrentTime + BaseActionValue / unit.speed;
            }
        }
        public BattleUnit PeekNext()           //选出当前跑条最快的一位 
        {
            BattleUnit best = null;
            foreach(var u in units)
            {
                if(!u.isActive) continue;
                if(best == null || u.nextActionTime < best.nextActionTime)
                    best = u;
                else if(u.nextActionTime == best.nextActionTime &&        //如果时间打平
                         u.tieBreaker < best.tieBreaker)    
                    best = u;

            }
            return best;
        }
        public void BeginTurn(BattleUnit u)     //时钟加速到当前行动的这一位，然后继续跑条
        {
            CurrentTime = u.nextActionTime;
        }
        public void EndTurn(BattleUnit u)     //计算下一次行动的行动值
        {
            u.nextActionTime = CurrentTime + BaseActionValue / u.speed;
        }

    }
}