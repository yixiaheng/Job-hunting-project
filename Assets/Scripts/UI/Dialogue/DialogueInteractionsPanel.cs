using System.Collections.Generic;
using Game.UI;
using UnityEngine;

namespace DialogueSystem
{
    /// <summary>交互提示条目由整组 HUD 管理，不单独注册 UIManager。</summary>
    public sealed class DialogueInteractionsPanel : UIPanel
    {
        [SerializeField] private DialogueInteractUI itemPrefab;
        [SerializeField] private Transform itemRoot;

        private readonly List<DialogueInteractUI> items = new List<DialogueInteractUI>();

        protected override void OnOpen(IUIContext context) => Refresh(context);
        protected override void OnContextChanged(IUIContext context) => Refresh(context);

        private void Refresh(IUIContext context)
        {
            if (!(context is DialogueInteractionsUIContext data) || itemPrefab == null || itemRoot == null)
            {
                Debug.LogError("交互提示需要有效的 Context、条目预制体和父节点。", this);
                return;
            }

            int index = 0;
            foreach (DialogueTrigger target in data.Targets)
            {
                if (target == null)
                    continue;

                if (index >= items.Count)
                    items.Add(Instantiate(itemPrefab, itemRoot, false));
                DialogueInteractUI item = items[index++];
                item.gameObject.SetActive(true);
                item.Bind(target.DisplayName, target == data.Selected);
            }
            for (; index < items.Count; index++)
                items[index].gameObject.SetActive(false);
        }

        protected override void OnClosed() => ClearItems();

        private void ClearItems()
        {
            foreach (DialogueInteractUI item in items)
            {
                if (item == null)
                    continue;
                item.gameObject.SetActive(false);
                Destroy(item.gameObject);
            }
            items.Clear();
        }
    }
}
