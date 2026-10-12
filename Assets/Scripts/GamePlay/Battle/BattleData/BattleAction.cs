using UnityEngine;

/// <summary>
/// 战时操作行为数据传递，包括选择的技能，选择的主目标
/// </summary>
public class BattleAction
{
    public SkillConfig skill;
    public BattleUnit mainTarget; //全体 / self 可以是null;
}
