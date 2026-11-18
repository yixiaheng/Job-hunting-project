using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Global Overlay owned by UIManager. Scene flow only drives its opacity.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class SceneTransitionPanel : UIPanel
    {
        private Image background;

        protected override void OnInitialize()
        {
            background = GetComponent<Image>();
        }

        protected override void OnOpen(IUIContext context)
        {
            SetOpacity(0f);
        }

        public IEnumerator FadeTo(float opacity, float duration)
        {
            float start = background.color.a;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                SetOpacity(Mathf.Lerp(start, opacity, elapsed / duration));
                yield return null;
            }
            SetOpacity(opacity);
        }

        private void SetOpacity(float value)
        {
            // Fade the graphic, not the CanvasGroup: even a transparent transition blocks clicks.
            background.color = new Color(0f, 0f, 0f, value);
        }

        public override bool CanCloseFromBack() => false;
    }
}
