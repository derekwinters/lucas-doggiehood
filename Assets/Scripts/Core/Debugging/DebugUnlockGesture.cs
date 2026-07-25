using System.Collections.Generic;

namespace Doggiehood.Core.Debugging
{
    /// <summary>
    /// The on-device Debug-tab unlock gesture (#219): tapping the version
    /// label <see cref="TapsToUnlock"/> times within a rolling
    /// <see cref="WindowSeconds"/>-second window reveals the Debug tab, the
    /// Android developer-options gesture. Time is parameterized — the caller
    /// passes each tap's timestamp (Unity feeds <c>Time.unscaledTime</c>) —
    /// so the logic is deterministic and engine-free, the same injection
    /// pattern the economy/move-in systems use for RNG. The unlock resets
    /// each session because a fresh instance is created on launch (#219).
    /// </summary>
    public sealed class DebugUnlockGesture
    {
        /// <summary>Taps required within the window to unlock.</summary>
        public const int TapsToUnlock = 10;

        /// <summary>Length of the rolling window, in seconds.</summary>
        public const double WindowSeconds = 10.0;

        private readonly Queue<double> tapTimes = new Queue<double>();

        /// <summary>Whether the Debug tab has been unlocked this session.</summary>
        public bool IsUnlocked { get; private set; }

        /// <summary>
        /// Records a tap at <paramref name="nowSeconds"/> and returns the
        /// resulting unlocked state. Taps older than <see cref="WindowSeconds"/>
        /// relative to this tap fall out of consideration, so only a genuine
        /// burst of <see cref="TapsToUnlock"/> taps inside the window unlocks.
        /// Once unlocked it stays unlocked for the session.
        /// </summary>
        public bool RegisterTap(double nowSeconds)
        {
            if (IsUnlocked)
            {
                return true;
            }

            tapTimes.Enqueue(nowSeconds);

            var cutoff = nowSeconds - WindowSeconds;
            while (tapTimes.Count > 0 && tapTimes.Peek() < cutoff)
            {
                tapTimes.Dequeue();
            }

            if (tapTimes.Count >= TapsToUnlock)
            {
                IsUnlocked = true;
            }

            return IsUnlocked;
        }
    }
}
