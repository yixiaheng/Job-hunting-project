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
    public bool isDialoguing;


#region 事件
    
    public Action<DialogueTrigger> OnInteractionListAdd;
    public Action<DialogueTrigger> OnInteractionListSub;
    public Action<DialogueTrigger> OnInterectionSelectionChange;
    //对话开始/结束
    public Action<DialogueNodeSO> DialogueContextShow;
    public Action DialogueContextClose;
#endregion
    void Awake()
    {
        if(Instance != null)
            Destroy(this);
        else
            Instance = this;
        
        isDialoguing = false;
    }

    public void DialogueTriggerAdd(DialogueTrigger dialogue)
    {
        if(dialogues.Count == 0)
            interactionSelectID = 0;

        dialogues.Add(dialogue);
        OnInteractionListAdd(dialogue);
        OnInterectionSelectionChange(dialogues[interactionSelectID]);
    }
    public void DialogueTriggerSub(DialogueTrigger dialogue)
    {
        dialogues.Remove(dialogue);
        OnInteractionListSub(dialogue);

        if(interactionSelectID >= dialogues.Count)
            interactionSelectID = dialogues.Count - 1;

        if(interactionSelectID >= dialogues.Count || interactionSelectID < 0)
            return;
            
        OnInterectionSelectionChange(dialogues[interactionSelectID]);
    }
    public int GetSelectID()
    {
        return interactionSelectID;
    }
    /// <summary>
    /// 改变选中对话索引,传入-1代表减ID,传1代表加ID
    /// </summary>
    /// <param name="dir"></param>
    [Button]
    public void ChangeSelectID(int dir)
    {
        interactionSelectID += dir;
        if(interactionSelectID > dialogues.Count)
            interactionSelectID = 0;
        if(interactionSelectID < 0)
            interactionSelectID = dialogues.Count - 1;

        if(interactionSelectID >= dialogues.Count || interactionSelectID < 0)
            return;

        OnInterectionSelectionChange(dialogues[interactionSelectID]);
    }

    public bool ShowDialogue()
    {
        if(dialogues.Count == 0)
        {
            Debug.Log("当前没有可对话选项");
            return false;
        }

        if(interactionSelectID >= dialogues.Count || interactionSelectID < 0)
        {
            Debug.LogError("interactionSelcecID索引不在范围之内");
            return true;
        }
        Debug.Log("ShowDialogue");
        
        DialogueContextShow(dialogues[interactionSelectID].dialogueConfigSO);
        return true;
    }

}
