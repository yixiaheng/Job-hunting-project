using TMPro;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.Rendering;
using Unity.VisualScripting;

public class DialogueUIController : MonoBehaviour
{
    [Header("CanvasGroup")]
    public CanvasGroup DialogueContext;
    public CanvasGroup DialogueInteraction;
    public CanvasGroup DialogueChoices;
    [Header("对话部分UI")]
    public TextMeshProUGUI dialogueContext;
    private Image dialogueBack_1;

    [Header("Prefabs/ParentObject")]
    public GameObject Interaction_prefab;
    public Transform Interaction_parent;
    public GameObject Choice_prefab;
    public Transform Choice_parent;
    private GameObject currentInteractionObject;

    public Dictionary<DialogueTrigger, GameObject> Dic_Intercations;

    void Awake()
    {
        Dic_Intercations = new Dictionary<DialogueTrigger, GameObject>();
        Dic_Intercations.Clear();
    }

    void Start()
    {
        CloseDialogueContext();
    }

    void OnEnable()
    {
        DialogueRunner.Instance.DialogueContextShow += ShowDialogueContext;
        DialogueRunner.Instance.DialogueContextClose += CloseDialogueContext;
        DialogueRunner.Instance.OnInteractionListAdd += OnListAdd_Interaction;
        DialogueRunner.Instance.OnInteractionListSub += OnListSub_Interaction;
        DialogueRunner.Instance.OnInterectionSelectionChange += OnSelectChange_Interaction;

    }

    void OnDisable()
    {
        DialogueRunner.Instance.DialogueContextShow -= ShowDialogueContext;
        DialogueRunner.Instance.DialogueContextClose -= CloseDialogueContext;
        DialogueRunner.Instance.OnInteractionListAdd -= OnListAdd_Interaction;
        DialogueRunner.Instance.OnInteractionListSub -= OnListSub_Interaction;
        DialogueRunner.Instance.OnInterectionSelectionChange -= OnSelectChange_Interaction;
    }

    public void ShowDialogueContext(DialogueConfigSO configSO)
    {
        DialogueContext.alpha = 1;
        DialogueContext.interactable = true;
        DialogueContext.blocksRaycasts = true;

        dialogueContext.text = configSO.text;

        if(configSO.nodeType == NodeType.HaveChoice)
        {
            ShowDialogueChoice(configSO);
        }
        else
            CloseDialogueChoice();
    }

    public void CloseDialogueContext()
    {
        DialogueContext.alpha = 0;
        DialogueContext.interactable = false;
        DialogueContext.blocksRaycasts = false;
    }

    //对话交互
    public void OnListAdd_Interaction(DialogueTrigger dialogue)
    {
        GameObject obj = Instantiate(Interaction_prefab, Interaction_parent);
        Dic_Intercations.Add(dialogue, obj);
    }
    public void OnListSub_Interaction(DialogueTrigger dialogue)
    {
        GameObject obj;
        if(Dic_Intercations.TryGetValue(dialogue, out obj))
        {
            Destroy(obj);
            Dic_Intercations.Remove(dialogue);
        }
        else
        {
            Debug.Log("在字典中未找到对应的DialogueTrigger");
        }
    }
    public void OnSelectChange_Interaction(DialogueTrigger dialogue)
    {
        if(currentInteractionObject != null)
        {
            DialogueInteractUI d = currentInteractionObject.GetComponent<DialogueInteractUI>();
            d.image.color = Color.white;
        }
        GameObject obj;
        if(Dic_Intercations.TryGetValue(dialogue, out obj))
        {
            DialogueInteractUI d = obj.GetComponent<DialogueInteractUI>();
            d.image.color = Color.red;
        }
    }

    //对话选项
    public void ShowDialogueChoice(DialogueConfigSO configSO)
    {
        DialogueChoices.alpha = 1;
        DialogueChoices.interactable = true;
        DialogueChoices.blocksRaycasts = true;
    }

    public void CloseDialogueChoice()
    {
        DialogueChoices.alpha = 0;
        DialogueChoices.interactable = false;
        DialogueChoices.blocksRaycasts = false;
    }
}
