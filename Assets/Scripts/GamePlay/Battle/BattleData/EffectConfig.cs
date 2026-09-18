using System.Data;
using UnityEngine;

[System.Serializable]
public class EffectConfig
{
    public EffectType effectType;
    public float value;
    public StatType scaleStat;
    public TargetRule target;
    public int buffId;
    public int stack;
}
