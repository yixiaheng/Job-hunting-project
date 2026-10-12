using System.Data;
using UnityEngine;


/// <summary>
/// 效果数据单元，一个技能由多个效果组成
/// </summary>
[System.Serializable]
public class EffectConfig
{
    public EffectType effectType;
    public float value;
    public StatType scaleStat;
    public TargetRule target;
    public int buffId;
    public BuffConfig buff;         //暂留，以后改成用id查询buff
    public int stack;
}
