using UnityEngine;

namespace Battle.Presentation
{
    /// <summary>One clock for Timeline, camera, HUD trails and presentation waits.</summary>
    public sealed class BattlePresentationClock
    {
        public bool Paused { get; set; }
        public float Speed { get; set; } = 1f;
        public float Delta { get; private set; }
        public double Time { get; private set; }
        public void Tick(float unscaledDelta, bool suspended = false)
        {
            Delta = Paused || suspended ? 0f : Mathf.Max(0f, unscaledDelta) * Mathf.Clamp(Speed, 0f, 8f);
            Time += Delta;
        }
    }
}
