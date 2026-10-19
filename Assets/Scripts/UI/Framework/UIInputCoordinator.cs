using System;
using System.Collections.Generic;

namespace Game.UI
{
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
                if (!instance.Panel.IsOpen)
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
