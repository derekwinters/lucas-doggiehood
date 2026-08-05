using System;
using System.Collections.Generic;
using Doggiehood.Core.Ui;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Ui
{
    /// <summary>
    /// #541: <see cref="ToastQueue{T}"/> is the engine-free, single-slot
    /// sequential queue behind the toast notification (docs/specs/ui/toast.md,
    /// <c>ToastQueueSlotCount = 1</c>). One toast is current at a time; a second
    /// request enqueued while one is current waits, and is promoted first-come,
    /// first-served only when the current one is explicitly dismissed. Payload is
    /// opaque to Core (copy is assembled in the Unity layer), so these tests use a
    /// plain string stand-in.
    /// </summary>
    public class ToastQueueTests
    {
        [Test]
        public void SlotCount_IsOne_TheSettledSequentialQueueModel()
        {
            Assert.That(ToastQueue<string>.SlotCount, Is.EqualTo(1),
                "toast.md ToastQueueSlotCount = 1 — one visible toast");
        }

        [Test]
        public void StartsEmpty_WithNoCurrentAndNothingPending()
        {
            var queue = new ToastQueue<string>();

            Assert.That(queue.HasCurrent, Is.False);
            Assert.That(queue.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void FirstEnqueue_BecomesTheCurrentItem()
        {
            var queue = new ToastQueue<string>();

            queue.Enqueue("Quest complete! +10 coins");

            Assert.That(queue.HasCurrent, Is.True);
            Assert.That(queue.Current, Is.EqualTo("Quest complete! +10 coins"));
            Assert.That(queue.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void SecondEnqueue_WhileOneIsCurrent_StaysQueued_NotShown()
        {
            var queue = new ToastQueue<string>();
            queue.Enqueue("first");

            queue.Enqueue("second");

            Assert.That(queue.Current, Is.EqualTo("first"),
                "only one toast is ever current (slot count 1)");
            Assert.That(queue.PendingCount, Is.EqualTo(1),
                "the second request waits rather than showing");
        }

        [Test]
        public void DismissCurrent_PromotesTheNextQueuedItem_FirstComeFirstServed()
        {
            var queue = new ToastQueue<string>();
            queue.Enqueue("first");
            queue.Enqueue("second");
            queue.Enqueue("third");

            Assert.That(queue.DismissCurrent(), Is.True);
            Assert.That(queue.Current, Is.EqualTo("second"), "FCFS: the earliest-queued is promoted");
            Assert.That(queue.PendingCount, Is.EqualTo(1));

            Assert.That(queue.DismissCurrent(), Is.True);
            Assert.That(queue.Current, Is.EqualTo("third"));
            Assert.That(queue.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void CurrentNeverAdvances_UntilTheCurrentIsExplicitlyDismissed()
        {
            var queue = new ToastQueue<string>();
            queue.Enqueue("first");
            queue.Enqueue("second");

            // Any number of enqueues never changes who is current — only a
            // dismiss promotes the next.
            queue.Enqueue("third");
            Assert.That(queue.Current, Is.EqualTo("first"));

            queue.DismissCurrent();
            Assert.That(queue.Current, Is.EqualTo("second"));
        }

        [Test]
        public void DismissingTheLastItem_LeavesTheQueueEmpty()
        {
            var queue = new ToastQueue<string>();
            queue.Enqueue("only");

            Assert.That(queue.DismissCurrent(), Is.True);

            Assert.That(queue.HasCurrent, Is.False);
            Assert.That(queue.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void DismissWhenEmpty_IsANoOp_ReturningFalse()
        {
            var queue = new ToastQueue<string>();

            Assert.That(queue.DismissCurrent(), Is.False);
            Assert.That(queue.HasCurrent, Is.False);
        }

        [Test]
        public void Current_WhenEmpty_Throws()
        {
            var queue = new ToastQueue<string>();

            Assert.That(() => queue.Current, Throws.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public void CurrentChanged_FiresOnPromotionAndOnClearingToEmpty_NotOnQueuingBehind()
        {
            var queue = new ToastQueue<string>();
            var changes = new List<bool>();
            queue.CurrentChanged += () => changes.Add(queue.HasCurrent);

            queue.Enqueue("first");   // empty -> current: fires (true)
            queue.Enqueue("second");  // queued behind: no change, no fire
            queue.DismissCurrent();   // promote second: fires (true)
            queue.DismissCurrent();   // clear to empty: fires (false)

            Assert.That(changes, Is.EqualTo(new[] { true, true, false }));
        }
    }
}
