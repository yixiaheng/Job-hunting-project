using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "DialogueConfigSO", menuName = "Dialogue/DialogueConfigSO")]
public class DialogueConfigSO : ScriptableObject
{
    //基础对话数据
    public string dialogueName;             //对话名称
    public string id;                       //ID
    public string speakerName;              //说话者的名字
    [TextArea(5,10)]
    public string text;                     //讲话内容
    public NodeType nodeType;               //对话Type
    public string nextNodeId;               //下一个节点的对话

    //进阶需要
    public Vector2 editorPosition;          //在Editor视图界面的位置
    public UnityEvent onEnterEvents;        //进入该节点时触发的事件
    public string textId;                   //本地化键
}

public enum NodeType
{
    Normal,
    HaveChoice
}
