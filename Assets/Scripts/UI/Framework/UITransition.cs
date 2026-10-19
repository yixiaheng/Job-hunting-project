using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace Game.UI
{
    public abstract class UITransition : MonoBehaviour
    {
        public abstract IEnumerator PlayOpen();

        public abstract IEnumerator PlayClose();

        public abstract void Stop();
    }

    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UIFadeTransition : UITransition
    {
        [SerializeField] private float duration = 0.2f;
        [SerializeField] private Ease ease = Ease.OutQuad;

        private CanvasGroup canvasGroup;
        private Tween currentTween;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        public override IEnumerator PlayOpen()
        {
            Stop();
            canvasGroup.alpha = 0f;
            currentTween = canvasGroup
                .DOFade(1f, Mathf.Max(0f, duration))
                .SetEase(ease)
                .SetUpdate(true);
            yield return currentTween.WaitForCompletion();
            currentTween = null;
        }

        public override IEnumerator PlayClose()
        {
            Stop();
            currentTween = canvasGroup
                .DOFade(0f, Mathf.Max(0f, duration))
                .SetEase(ease)
                .SetUpdate(true);
            yield return currentTween.WaitForCompletion();
            currentTween = null;
        }

        public override void Stop()
        {
            if (currentTween == null)
                return;

            currentTween.Kill(false);
            currentTween = null;
        }
    }
}
