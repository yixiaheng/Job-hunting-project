using UnityEngine;

/// <summary>
/// 玩家对话选项
/// </summary> <summary>
/// 
/// </summary>
[System.Serializable]
public class DialogueChoice
{
    string choiceText;
    string nextNodeId;
    DialogueCondition[] conditions;
}