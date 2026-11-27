public enum EffectType
{
    Damage,
    Heal,
    AddShield,
    ApplyBuff,
    GainEnergy,
    AdvanceAction,
    RemoveDeBuff,
    RemoveBuff,
    GainSkillPoint,

}

/// <summary>
/// 属性类型
/// </summary>
public enum StatType
{
    Attack,
    Health,
    Speed,
    Defence
}

public enum BuffType
{
    StatMod
}

public enum TargetRule
{
    Single,
    Spread,
    AoE,
    Self
}
public enum Faction
{
    Enemy,
    OurSide
}
public enum TargetSide
{
    OurSide,
    OtherSide
}
public enum SkillKind
{   
    Basic,
    Skill,
    Ultimate
}

public enum ElementType
{
    Fire = 0,
    Ice = 1,
    Lightning = 2,
    Wind = 3,
    Quantum = 4,
    Imaginary = 5,
    // 追加在末尾，保留已有SO和技能中序列化的元素编号。
    Physical = 6
}

public enum PathType
{
    Preservation,
    Abundance,
    Hunt,
    Erudition,
    Nihility,
    Harmony
}
