using System;
using System.Collections.Generic;

namespace Doggiehood.Core.Audio
{
    public enum SfxEvent
    {
        Bark,
        TruckArrival,
        ItemDelivered,
        UiTap,
        UiConfirm,
    }

    /// <summary>
    /// Decouples gameplay from sound (#40): game code publishes events; the
    /// Unity audio layer subscribes and plays clips. Publishing never
    /// throws — a failing handler (missing clip, device issue) can never
    /// block gameplay.
    /// </summary>
    public static class AudioEventBus
    {
        private static readonly List<Action<SfxEvent>> Subscribers = new List<Action<SfxEvent>>();

        public static void Subscribe(Action<SfxEvent> handler)
        {
            Subscribers.Add(handler);
        }

        public static void Publish(SfxEvent sfxEvent)
        {
            foreach (var subscriber in Subscribers)
            {
                try
                {
                    subscriber(sfxEvent);
                }
                catch
                {
                    // Audio must never take gameplay down with it (#40).
                }
            }
        }

        /// <summary>Test hook: clears all subscribers.</summary>
        public static void Reset()
        {
            Subscribers.Clear();
        }
    }
}
