using UnityEngine;

namespace Game.UI
{
    /// <summary>Simple UIId entry. Business panels that need data should pass their own context.</summary>
    public sealed class UIOpenButton : MonoBehaviour
    {
        [SerializeField] private UIId id;

        public void Open()
        {
            var visibility = GetComponent<UIHUDEntryVisibility>();
            if (visibility != null && !visibility.CanActivate)
                return;

            UIManager.Instance?.Open(id);
        }
    }
}
