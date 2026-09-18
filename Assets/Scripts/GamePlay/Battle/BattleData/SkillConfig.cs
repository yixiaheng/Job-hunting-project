using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Battle/SkillConfig")]
public class SkillConfig : ScriptableObject
{
    public int id;
    public string  Name;
    public SkillKind kind;
    public int energyCost;
    public int energyGain;
    public TargetRule targetRule;
    public TargetSide targetSide;
    public List<EffectConfig> effectConfigs;
}
