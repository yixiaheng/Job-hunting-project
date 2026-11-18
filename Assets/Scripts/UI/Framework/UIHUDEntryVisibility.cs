using UnityEngine;

namespace Game.UI
{
    /// <summary>Shared visibility and interaction gate for exploration HUD entries.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UIHUDEntryVisibility : MonoBehaviour
    {
        private UIManager manager;
        private UIPanel panel;
        private CanvasGroup group;

        public bool CanActivate => manager != null && panel != null && panel.Config != null
            && panel.Config.id == UIId.ExplorationHUD && manager.IsUnobstructedHUD(panel.Handle);

        private void OnEnable()
        {
            manager = UIManager.Instance;
            panel = GetComponentInParent<UIPanel>();
            group = GetComponent<CanvasGroup>();
            if (manager != null)
                manager.PresentationStateChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (manager != null)
                manager.PresentationStateChanged -= Refresh;
            manager = null;
        }

        private void Refresh()
        {
            bool visible = CanActivate;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
