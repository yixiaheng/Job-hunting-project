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

        internal void SetCovered(bool value)
        {
            covered = value;
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

            canvasGroup.interactable = value;
            canvasGroup.blocksRaycasts = value;
        }
    }
}
