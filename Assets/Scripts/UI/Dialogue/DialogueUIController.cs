using System.Collections.Generic;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DialogueSystem
{
    /// <summary>对话窗口与选项由 UIManager 创建，数据由场景入口传入。</summary>
    public sealed class DialogueUIController : UIPanel, IPointerClickHandler
    {
        [SerializeField] private CanvasGroup DialogueContext;
        [SerializeField] private CanvasGroup DialogueChoices;
        [SerializeField] private TextMeshProUGUI speakerName;
        [SerializeField] private TextMeshProUGUI dialogueContext;
        [SerializeField] private GameObject Choice_prefab;
        [SerializeField] private Transform Choice_parent;

        private readonly List<GameObject> choices = new List<GameObject>();
        private DialogueRunner runner;

        protected override void OnOpen(IUIContext context) => Bind(context);
        protected override void OnContextChanged(IUIContext context) => Bind(context);

        private void Bind(IUIContext context)
        {
            ClearChoices();
            if (!(context is DialogueUIContext data))
            {
                Debug.LogError("对话窗口需要 DialogueUIContext。", this);
                return;
            }

            runner = data.Runner;
            DialogueLine line = data.Line;
            DialogueNode node = line.Node;
            SpeakerSO speaker = node != null ? node.speaker : null;
            string body = node != null ? node.ResolveText() : string.Empty;
            string speakerLabel = speaker != null ? speaker.speakerName : string.Empty;
            SetGroup(DialogueContext, true);

            if (speakerName != null)
            {
                speakerName.text = speakerLabel;
                speakerName.color = speaker != null ? speaker.nameColor : Color.white;
            }
            if (dialogueContext != null)
            {
                dialogueContext.text = speakerName != null || string.IsNullOrEmpty(speakerLabel)
                    ? body : speakerLabel + "\n" + body;
            }

            bool hasChoices = line.Choices != null && line.Choices.Count > 0;
            SetGroup(DialogueChoices, hasChoices);
            if (!hasChoices || Choice_prefab == null || Choice_parent == null)
                return;

            foreach (DialogueChoice choice in line.Choices)
            {
                GameObject item = Instantiate(Choice_prefab, Choice_parent, false);
                choices.Add(item);
                DialogueChoiceUI view = item.GetComponent<DialogueChoiceUI>() ?? item.AddComponent<DialogueChoiceUI>();
                view.Bind(choice, HandleChoiceClicked);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsOpen || runner == null || !runner.isDialoguing || runner.Session.HasVisibleChoices
                || eventData.button != PointerEventData.InputButton.Left || DialogueContext == null)
                return;

            RectTransform area = DialogueContext.transform as RectTransform;
            if (area != null && RectTransformUtility.RectangleContainsScreenPoint(area,
                eventData.position, eventData.pressEventCamera))
                runner.Advance();
        }

        private void HandleChoiceClicked(DialogueChoice choice)
        {
            if (IsOpen && runner != null)
                runner.SelectChoice(choice);
        }

        protected override void OnClosed()
        {
            runner = null;
            ClearChoices();
            SetGroup(DialogueContext, false);
            SetGroup(DialogueChoices, false);
            // 会话结束由场景入口处理，旧窗口不会误关新会话。
            (Context as DialogueUIContext)?.NotifyClosed();
        }

        private void ClearChoices()
        {
            foreach (GameObject item in choices)
            {
                if (item == null)
                    continue;
                item.SetActive(false);
                Destroy(item);
            }
            choices.Clear();
        }

        private static void SetGroup(CanvasGroup group, bool visible)
        {
            if (group == null)
                return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
