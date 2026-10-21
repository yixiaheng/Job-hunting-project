using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// UI背景控制器
    /// </summary>
    public class UIBackgroundController : MonoBehaviour
    {
        private readonly HashSet<int> blurHandles = new();
        private readonly HashSet<int> modalHandles = new();

        public UIBackgroundState Current { get; private set; }

        public event Action<UIBackgroundState> StateChanged;

        internal void Acquire(UIHandle handle, UIConfig config)
        {
            if (!handle.IsValid)
                return;

            if (config.blurBackground)
                blurHandles.Add(handle.Value);

            if (config.modal)
                modalHandles.Add(handle.Value);

            PublishIfChanged();
        }

        internal void Release(UIHandle handle)
        {
            if (!handle.IsValid)
                return;

            blurHandles.Remove(handle.Value);
            modalHandles.Remove(handle.Value);
            PublishIfChanged();
        }

        private void PublishIfChanged()
        {
            UIBackgroundState next = new(blurHandles.Count, modalHandles.Count);
            if (next == Current)
                return;

            Current = next;
            StateChanged?.Invoke(next);
        }
    }
}
