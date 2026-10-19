using System;

namespace Game.UI
{
    public enum UIId
    {
        None = 0,
        Backpack = 1,
        ItemDetail = 2,
        CharacterDetail = 3,
        Settings = 4,
        Confirm = 5
    }

    public enum UILayer
    {
        HUD = 0,
        Window = 1,
        Popup = 2,
        Toast = 3,
        Overlay = 4
    }

    public enum UIState
    {
        Closed = 0,
        Opening = 1,
        Open = 2,
        Closing = 3,
        Destroyed = 4
    }

    public enum UIInstanceMode
    {
        Single = 0,
        Multiple = 1
    }

    public enum UICacheMode
    {
        Keep = 0,
        DestroyOnClose = 1
    }

    public enum UILifetime
    {
        Global = 0,
        Scene = 1
    }

    public interface IUIContext
    {
    }

    public readonly struct UIHandle : IEquatable<UIHandle>
    {
        internal UIHandle(int value)
        {
            Value = value;
        }

        internal int Value { get; }

        public bool IsValid => Value > 0;

        public bool Equals(UIHandle other) => Value == other.Value;

        public override bool Equals(object obj) => obj is UIHandle other && Equals(other);

        public override int GetHashCode() => Value;

        public static bool operator ==(UIHandle left, UIHandle right) => left.Equals(right);

        public static bool operator !=(UIHandle left, UIHandle right) => !left.Equals(right);

        public override string ToString() => IsValid ? $"UIHandle({Value})" : "UIHandle(Invalid)";
    }

    public readonly struct UIInputState : IEquatable<UIInputState>
    {
        public UIInputState(bool showCursor, bool blockMovement, bool blockCamera)
        {
            ShowCursor = showCursor;
            BlockMovement = blockMovement;
            BlockCamera = blockCamera;
        }

        public bool ShowCursor { get; }
        public bool BlockMovement { get; }
        public bool BlockCamera { get; }

        public bool Equals(UIInputState other)
        {
            return ShowCursor == other.ShowCursor
                && BlockMovement == other.BlockMovement
                && BlockCamera == other.BlockCamera;
        }

        public override bool Equals(object obj) => obj is UIInputState other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ShowCursor ? 1 : 0;
                hash = (hash * 397) ^ (BlockMovement ? 1 : 0);
                return (hash * 397) ^ (BlockCamera ? 1 : 0);
            }
        }

        public static bool operator ==(UIInputState left, UIInputState right) => left.Equals(right);

        public static bool operator !=(UIInputState left, UIInputState right) => !left.Equals(right);
    }

    public readonly struct UIBackgroundState : IEquatable<UIBackgroundState>
    {
        public UIBackgroundState(int blurRequests, int modalRequests)
        {
            BlurRequests = blurRequests;
            ModalRequests = modalRequests;
        }

        public int BlurRequests { get; }
        public int ModalRequests { get; }
        public bool ShouldBlur => BlurRequests > 0;
        public bool HasModalWindow => ModalRequests > 0;

        public bool Equals(UIBackgroundState other)
        {
            return BlurRequests == other.BlurRequests
                && ModalRequests == other.ModalRequests;
        }

        public override bool Equals(object obj) => obj is UIBackgroundState other && Equals(other);

        public override int GetHashCode() => (BlurRequests * 397) ^ ModalRequests;

        public static bool operator ==(UIBackgroundState left, UIBackgroundState right) => left.Equals(right);

        public static bool operator !=(UIBackgroundState left, UIBackgroundState right) => !left.Equals(right);
    }
}
