using System.Collections;
using UnityEngine;

namespace Game.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIPanel : MonoBehaviour
    {
        public UIState State { get; private set; } = UIState.Closed;
        public UIHandle Handle { get; private set; }
        public UIConfig Config { get; private set; }
        public IUIContext Context { get; private set; }

        private UIManager owner;
        private UITransition transition;
        private CanvasGroup canvasGroup;
        private bool initialized;
        private bool covered;
        private bool suspended;
        private bool hiddenByWindow;
        private bool hiddenByPresentation;
        private CanvasGroup presentationGroup;
        public bool IsPresentationSuspended => suspended;
        public bool IsOpen => State == UIState.Open || State == UIState.Opening;
        protected UIManager Owner => owner;

        internal void Initialize(UIManager manager, UIHandle handle, UIConfig config)
        {
            if (initialized)
                return;

            owner = manager;
            Handle = handle;
            Config = config;
            canvasGroup = GetComponent<CanvasGroup>();
            presentationGroup = transform.parent != null ? transform.parent.GetComponent<CanvasGroup>() : null;
            transition = GetComponent<UITransition>();
            initialized = true;

            OnInitialize();
        }

        internal IEnumerator OpenRoutine(IUIContext context)
        {
            Context = context;
            State = UIState.Opening;
            gameObject.SetActive(true);
            SetInteractable(false);
            OnOpen(context);

            if (transition != null)
                yield return transition.PlayOpen();

            // 场景释放可能在动画等待期间同步终止此面板。
            if (this == null || State != UIState.Opening)
                yield break;

            State = UIState.Open;
            SetInteractable(!covered);
            OnOpened();
        }

        internal IEnumerator CloseRoutine()
        {
            State = UIState.Closing;
            SetInteractable(false);
            OnClosing();

            if (transition != null)
                yield return transition.PlayClose();

            if (this == null || State != UIState.Closing)
                yield break;

            OnClosed();
            Context = null;
            State = UIState.Closed;
            gameObject.SetActive(false);
        }

        internal void UpdateContext(IUIContext context)
        {
            Context = context;
            OnContextChanged(context);
        }

        internal void SetCovered(bool value, bool hidden = false)
        {
            covered = value;
            hiddenByWindow = hidden;
            RefreshPresentation();
            SetInteractable(!covered && State == UIState.Open);
            OnCoveredChanged(covered);
        }

        internal void StopTransition()
        {
            transition?.Stop();
        }

        internal void ForceCloseImmediate()
        {
            transition?.Stop();
            SetInteractable(false);

            if (State != UIState.Closed && State != UIState.Destroyed)
            {
                if (State != UIState.Closing)
                    OnClosing();
                OnClosed();
            }

            Context = null;
            State = UIState.Closed;
            gameObject.SetActive(false);
        }

        internal void MarkDestroyed()
        {
            transition?.Stop();
            State = UIState.Destroyed;
        }

        internal void SetPresentationSuspended(bool value)
        {
            if (suspended == value) return;
            suspended = value;
            RefreshPresentation();
            SetInteractable(!covered && State == UIState.Open);
        }

        // Independent from scene suspension: a cinematic must not resume a suspended scene's UI.
        internal void SetCinematicHidden(bool value)
        {
            hiddenByPresentation = value;
            RefreshPresentation();
            SetInteractable(!covered && State == UIState.Open);
        }

        private void RefreshPresentation()
        {
            bool visible = !suspended && !hiddenByWindow && !hiddenByPresentation;
            if (presentationGroup != null)
            {
                presentationGroup.alpha = visible ? 1f : 0f;
                presentationGroup.interactable = visible;
                presentationGroup.blocksRaycasts = visible;
            }
        }

        internal void HandleMaskClick()
        {
            if (State == UIState.Open && !covered && !suspended && Config.closeWhenClickMask)
                owner.Close(Handle);
        }

        public virtual bool HandleBack() => false;
        public virtual bool CanCloseFromBack() => true;

        protected virtual void OnInitialize() { }

        protected virtual void OnOpen(IUIContext context) { }

        protected virtual void OnOpened() { }

        protected virtual void OnContextChanged(IUIContext context) { }

        protected virtual void OnClosing() { }

        protected virtual void OnClosed() { }

        protected virtual void OnCoveredChanged(bool value) { }

        private void SetInteractable(bool value)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.interactable = value && !suspended && !hiddenByPresentation;
            canvasGroup.blocksRaycasts = value && !suspended && !hiddenByPresentation;
        }
    }
}
