/// <summary>标识主动技能、天赋或装备触发的效果来源。</summary>
public enum EffectOrigin
{
    ActiveSkill,
    Talent,
    Equipment
}

/// <summary>效果数据单元，一个技能由多个效果组成。</summary>
[System.Serializable]
public class EffectConfig
{
    public EffectOrigin origin;
    public string key;
    public bool growsWithSkill;
    public bool overrideTarget;
    public TargetSide targetSide;
    public float buffValueMultiplier = 1f;
    public EffectType effectType;
    public float value;
    public StatType scaleStat;
    public TargetRule target;
    public int buffId;
    public BuffConfig buff; //暂留，以后改成用id查询buff
    public int stack;
}
