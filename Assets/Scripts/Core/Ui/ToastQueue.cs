using System;
using System.Collections.Generic;

namespace Doggiehood.Core.Ui
{
    /// <summary>
    /// #541: the engine-free, single-slot sequential queue behind the toast
    /// notification (docs/specs/ui/toast.md). Exactly one toast is
    /// <see cref="Current"/> at a time (<see cref="SlotCount"/> = 1, the settled
    /// <c>ToastQueueSlotCount</c> model); a request enqueued while one is current
    /// waits and is promoted first-come, first-served only when the current one is
    /// explicitly <see cref="DismissCurrent">dismissed</see>. Never stacks or
    /// overlaps.
    ///
    /// <para>Pure queue mechanics only — the payload <typeparamref name="T"/> is
    /// opaque to Core, so the toast's copy is assembled entirely in the thin Unity
    /// layer (rule #2: no copy strings in Core). The Unity view subscribes to
    /// <see cref="CurrentChanged"/> to drive its slide-in when a toast is promoted
    /// and clears the slot (which promotes the next) when it dismisses.</para>
    /// </summary>
    public sealed class ToastQueue<T>
    {
        /// <summary>docs/specs/ui/toast.md <c>ToastQueueSlotCount</c> — one toast
        /// visible at a time; the next waits for this one to clear.</summary>
        public const int SlotCount = 1;

        private readonly Queue<T> pending = new Queue<T>();
        private bool hasCurrent;
        private T current;

        /// <summary>Fires whenever the current slot changes — a new toast is
        /// promoted into it (from empty, or after a dismiss) or it clears to
        /// empty. Never fires for a request that merely queues behind the current
        /// one. Inspect <see cref="HasCurrent"/>/<see cref="Current"/> to react.</summary>
        public event Action CurrentChanged;

        /// <summary>True while a toast occupies the single slot.</summary>
        public bool HasCurrent => hasCurrent;

        /// <summary>How many requests are waiting behind the current one.</summary>
        public int PendingCount => pending.Count;

        /// <summary>The toast currently occupying the slot. Throws when none is
        /// showing — callers gate on <see cref="HasCurrent"/> or
        /// <see cref="CurrentChanged"/>.</summary>
        public T Current
        {
            get
            {
                if (!hasCurrent)
                {
                    throw new InvalidOperationException("No toast is currently showing.");
                }

                return current;
            }
        }

        /// <summary>Adds a toast request. If the slot is free it becomes
        /// <see cref="Current"/> immediately (raising <see cref="CurrentChanged"/>);
        /// otherwise it waits its turn behind the current one, first-come,
        /// first-served, with no change to who is current.</summary>
        public void Enqueue(T request)
        {
            if (!hasCurrent)
            {
                current = request;
                hasCurrent = true;
                CurrentChanged?.Invoke();
            }
            else
            {
                pending.Enqueue(request);
            }
        }

        /// <summary>Clears the current toast and promotes the next queued request
        /// into the slot (first-come, first-served), or leaves the queue empty if
        /// none is waiting — raising <see cref="CurrentChanged"/> either way.
        /// Returns false (a no-op) when nothing is currently showing. This is the
        /// only thing that advances the queue: enqueuing never displaces the
        /// current toast.</summary>
        public bool DismissCurrent()
        {
            if (!hasCurrent)
            {
                return false;
            }

            if (pending.Count > 0)
            {
                current = pending.Dequeue();
            }
            else
            {
                current = default;
                hasCurrent = false;
            }

            CurrentChanged?.Invoke();
            return true;
        }
    }
}
