using System.Collections.Generic;
using System.Data;
using Unity.VisualScripting;
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
            //閫愪釜Add
            foreach(var unit in allUnits)
            {
                Add(unit);
            }
        }

        public void Add(BattleUnit unit)       //瑙掕壊/鏁屼汉鍔犲叆鍒拌鍔ㄧ殑璺戞潯涓?
        {
            if(unit != null)
            {
                if(unit.speed <= 0)
                    return;
                units.Add(unit);
                unit.nextActionTime = CurrentTime + BaseActionValue / unit.speed;
            }
        }
        public BattleUnit PeekNext()           //閫夊嚭褰撳墠璺戞潯鏈€蹇殑涓€浣?
        {
            BattleUnit best = null;
            foreach(var u in units)
            {
                if(!u.IsActive) continue;
                if(best == null || u.nextActionTime < best.nextActionTime)
                    best = u;
                else if(u.nextActionTime == best.nextActionTime &&        //濡傛灉鏃堕棿鎵撳钩
                         u.tieBreaker < best.tieBreaker)    
                    best = u;

            }
            return best;
        }
        public void BeginTurn(BattleUnit u)     //鏃堕挓鍔犻€熷埌褰撳墠琛屽姩鐨勮繖涓€浣嶏紝鐒跺悗缁х画璺戞潯
        {
            CurrentTime = u.nextActionTime;
        }
        public void ModifyAction(BattleUnit unit, float ratio)
        {
            if (unit == null) return;
            float remaining = (float)(unit.nextActionTime - CurrentTime);
            unit.nextActionTime = CurrentTime + remaining * Mathf.Max(0f, 1f - ratio);
        }

        public void EndTurn(BattleUnit u)     //璁＄畻涓嬩竴娆¤鍔ㄧ殑琛屽姩鍊?
        {
            int spd = Mathf.Max(1, u.GetStat(StatType.Speed));
            u.nextActionTime = CurrentTime + BaseActionValue / spd;
        }

    }
}

