using UnityEngine;

/// <summary>
/// 玩家对话选项
/// </summary> <summary>
/// 
/// </summary>
[System.Serializable]
public class DialogueChoice
{
    public string choiceText;
    public string nextNodeId;
    public DialogueCondition[] conditions;
}