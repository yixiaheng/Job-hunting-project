using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DialogueGraph", menuName = "Dialogue/DialogueGraph")]
public class DialogueGraphSO : ScriptableObject
{
    public string EntryNodeId;  //入口
    public List<DialogueNodeSO> Nodes = new();
}
