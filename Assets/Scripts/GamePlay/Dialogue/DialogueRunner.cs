using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
public class DialogueRunner : MonoBehaviour
{
    public static DialogueRunner Instance{get; private set;}
    //当前获取的dialogueTrigger的数目
    [HideInInspector]public List<DialogueTrigger> dialogues = new List<DialogueTrigger>();
    //当前选中的索引
    private int interactionSelectID;


#region 事件
    
    public Action<DialogueTrigger> OnInteractionListAdd;
    public Action<DialogueTrigger> OnInteractionListSub;
    public Action<DialogueTrigger> OnInterectionSelectionChange;
    //对话开始/结束
    public Action<DialogueConfigSO> DialogueContextShow;
    public Action DialogueContextClose;
#endregion
    void Awake()
    {
        if(Instance != null)
            Destroy(this);
        else
            Instance = this;
    }

    public void DialogueTriggerAdd(DialogueTrigger dialogue)
    {
        dialogues.Add(dialogue);
        OnInteractionListAdd(dialogue);
    }
    public void DialogueTriggerSub(DialogueTrigger dialogue)
    {
        dialogues.Remove(dialogue);
        OnInteractionListSub(dialogue);

        if(interactionSelectID >= dialogues.Count)
            interactionSelectID = dialogues.Count - 1;

    }
    public int GetSelectID()
    {
        return interactionSelectID;
    }
    /// <summary>
    /// 改变选中对话索引
    /// </summary>
    /// <param name="dir">传入-1代表减ID,传1代表加ID</param>
    [Button]
    public void ChangeSelectID(int dir)
    {
        interactionSelectID += dir;
        if(interactionSelectID > dialogues.Count)
            interactionSelectID = 0;
        if(interactionSelectID < 0)
            interactionSelectID = dialogues.Count - 1;

        OnInterectionSelectionChange(dialogues[interactionSelectID]);
    }

}
