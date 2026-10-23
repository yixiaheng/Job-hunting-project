using System;
using System.Collections.Generic;

namespace Game.UI
{
    /// <summary>
    /// 输入协调器，负责把多个窗口的配置，合并成一份 UI 输入需求。
    /// </summary>
    public sealed class UIInputCoordinator
    {
        public UIInputState Current { get; private set; }

        public event Action<UIInputState> StateChanged;

        internal void Refresh(IReadOnlyList<UIInstanceRecord> openedPanels)
        {
            bool showCursor = false;
            bool blockMovement = false;
            bool blockCamera = false;

            for (int index = 0; index < openedPanels.Count; index++)
            {
                UIInstanceRecord instance = openedPanels[index];
                if (instance.Panel.State == UIState.Destroyed)
                    continue;

                showCursor |= instance.Config.showCursor;
                blockMovement |= instance.Config.blockMovement;
                blockCamera |= instance.Config.blockCamera;
            }

            UIInputState next = new(showCursor, blockMovement, blockCamera);
            if (next == Current)
                return;

            Current = next;
            StateChanged?.Invoke(next);
        }
    }
}
