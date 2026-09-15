using UnityEngine;
using System.Collections.Generic;
[RequireComponent(typeof(Collider))]
public class PlayerScopeCheck : MonoBehaviour
{
    //Trigger检测
    void OnTriggerEnter(Collider other)
    {
        var dt = other.GetComponent<DialogueTrigger>();
        if(dt != null && !DialogueRunner.Instance.dialogues.Contains(dt))
        {
            DialogueRunner.Instance.DialogueTriggerAdd(dt);
            Debug.Log("添加DialogueTrigger");
        }
    }

    void OnTriggerExit(Collider other)
    {
        var dt = other.GetComponent<DialogueTrigger>();
        if (dt != null)
        {
            Debug.Log("移除DialogueTrigger");
            DialogueRunner.Instance.DialogueTriggerSub(dt);
        }
    }
}
