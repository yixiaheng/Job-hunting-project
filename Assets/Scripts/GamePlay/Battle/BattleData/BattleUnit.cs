using System.Collections.Generic;
using UnityEngine;

public class BattleUnit
{
    public Faction faction;
    public int pos;
    public int maxHealth;
    public int currentHealth;
    public bool isActive => currentHealth > 0;
    public int attack;
    public int defence;
    public int speed;
    public int currentEnergy;
    public double nextActionTime;   // 下次行动的时刻（绝对时间戳）

    public int tieBreaker;

    public List<SkillConfig> skills = new ();

}
