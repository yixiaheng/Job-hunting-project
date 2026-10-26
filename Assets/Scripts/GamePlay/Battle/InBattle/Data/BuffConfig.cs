using UnityEngine;


[CreateAssetMenu(fileName = "BuffConfig", menuName = "Battle/BuffConfig")]
public class BuffConfig : ScriptableObject
{
   public int id;
   public string Name;
   public BuffType buffType;
   public int durationTimes;        //持有者行动几次后消失
   public int maxStack;             
   public StatType stat;            // 属性类型
   public float value;              // 0.2 = +20% , -0.15 = -15%
   public bool isDebuff;
}
