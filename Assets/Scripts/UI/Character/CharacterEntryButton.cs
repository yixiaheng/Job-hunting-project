using Game.UI;
using UnityEngine;

namespace Game.Characters
{
    /// <summary>Main exploration HUD entry; UIManager owns its visibility and interaction scope.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class CharacterEntryButton : MonoBehaviour
    {
        private UIManager manager;
        private UIPanel panel;
        private CanvasGroup group;

        private void OnEnable()
        {
            group = GetComponent<CanvasGroup>();
            panel = GetComponentInParent<UIPanel>();
            manager = UIManager.Instance;
            if (manager != null) manager.PresentationStateChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (manager != null) manager.PresentationStateChanged -= Refresh;
            manager = null;
        }

        private bool CanShow => manager != null && panel != null && panel.Config != null
            && panel.Config.id == UIId.ExplorationHUD && manager.IsUnobstructedHUD(panel.Handle);

        private void Refresh()
        {
            bool visible = CanShow;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        public void Open()
        {
            if (CanShow) CharacterSceneNavigator.TryEnter();
        }
    }
}
