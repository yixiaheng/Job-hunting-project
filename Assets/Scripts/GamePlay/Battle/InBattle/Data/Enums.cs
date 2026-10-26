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
    Fire,
    Ice,
    Lightning,
    Wind,
    Quantum,
    Imaginary
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
