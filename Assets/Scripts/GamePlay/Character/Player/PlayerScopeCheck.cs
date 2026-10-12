using UnityEngine;
using DialogueSystem;

[RequireComponent(typeof(Collider))]
public class PlayerScopeCheck : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        var dt = other.GetComponentInParent<DialogueTrigger>();
        if (dt != null && DialogueRunner.Instance != null)
            DialogueRunner.Instance.DialogueTriggerAdd(dt);
    }

    void OnTriggerExit(Collider other)
    {
        var dt = other.GetComponentInParent<DialogueTrigger>();
        if (dt != null && DialogueRunner.Instance != null)
            DialogueRunner.Instance.DialogueTriggerSub(dt);
    }
}
