using Game.UI;
using UnityEngine;

namespace DialogueSystem
{
    /// <summary>连接场景对话事件与 UI 框架，不持有场景中的 UI 对象。</summary>
    [RequireComponent(typeof(DialogueRunner))]
    public sealed class DialogueUIPresenter : MonoBehaviour
    {
        private DialogueRunner runner;
        private UIManager manager;
        private UIHandle dialogueHandle;
        private UIHandle interactionsHandle;
        private DialogueUIContext currentDialogue;

        private void OnEnable()
        {
            runner = GetComponent<DialogueRunner>();
            manager = UIManager.Instance;
            if (runner == null || manager == null)
            {
                Debug.LogError("对话 UI 入口需要 DialogueRunner 和全局 UIManager。", this);
                return;
            }

            runner.DialogueContextShow += ShowDialogue;
            runner.DialogueContextClose += CloseDialogue;
            runner.OnInteractionListAdd += HandleInteractionsChanged;
            runner.OnInteractionListSub += HandleInteractionsChanged;
            runner.OnInterectionSelectionChange += HandleInteractionsChanged;

            if (runner.isDialoguing)
                ShowDialogue(new DialogueLine(runner.Session.Current, runner.Session.VisibleChoices));
            else
                RefreshInteractions();
        }

        private void ShowDialogue(DialogueLine line)
        {
            CloseInteractions();
            currentDialogue = new DialogueUIContext(runner, line, HandleDialoguePanelClosed);
            dialogueHandle = manager.Open(UIId.Dialogue, currentDialogue);
        }

        private void CloseDialogue()
        {
            currentDialogue = null;
            if (manager != null && dialogueHandle.IsValid)
                manager.Close(dialogueHandle);
            dialogueHandle = default;
            RefreshInteractions();
        }

        private void HandleDialoguePanelClosed(DialogueUIContext context)
        {
            // 旧窗口结束关闭动画时，不应结束刚刚开始的新会话。
            if (!ReferenceEquals(currentDialogue, context))
                return;

            currentDialogue = null;
            dialogueHandle = default;
            if (runner != null)
                runner.EndDialogue();
        }

        private void HandleInteractionsChanged(DialogueTrigger target) => RefreshInteractions();

        private void RefreshInteractions()
        {
            if (manager == null || runner == null)
                return;

            if (runner.isDialoguing || runner.dialogues.Count == 0)
            {
                CloseInteractions();
                return;
            }

            interactionsHandle = manager.Open(UIId.DialogueInteractions, new DialogueInteractionsUIContext(runner));
        }

        private void CloseInteractions()
        {
            if (manager != null && interactionsHandle.IsValid)
                manager.Close(interactionsHandle);
            interactionsHandle = default;
        }

        private void OnDisable()
        {
            currentDialogue = null;
            if (runner != null)
            {
                runner.DialogueContextShow -= ShowDialogue;
                runner.DialogueContextClose -= CloseDialogue;
                runner.OnInteractionListAdd -= HandleInteractionsChanged;
                runner.OnInteractionListSub -= HandleInteractionsChanged;
                runner.OnInterectionSelectionChange -= HandleInteractionsChanged;
                runner.EndDialogue();
            }

            if (manager != null && dialogueHandle.IsValid)
                manager.Close(dialogueHandle);
            dialogueHandle = default;
            CloseInteractions();
            runner = null;
            manager = null;
        }
    }
}
