using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Battle/SkillConfig")]
public class SkillConfig : ScriptableObject
{
    public int id;
    public string  Name;
    public SkillKind kind;
    public ElementType element;
    public int skillPointCost;
    public int skillPointGain;
    public int energyCost;
    public int energyGain;
    public TargetRule targetRule;
    public TargetSide targetSide;       //目标是我方还是敌方
    public List<EffectConfig> effectConfigs;    //技能效果
}
