using Doggiehood.Core.Debugging;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Debugging
{
    /// <summary>
    /// #219: the Android developer-options unlock gesture — tap the version
    /// label 10 times within a 10s rolling window to reveal the Debug tab.
    /// Time is parameterized (the caller passes each tap's timestamp), the
    /// same deterministic-injection pattern the economy/move-in systems use
    /// for RNG, so these tests need no clock or Unity runtime.
    /// </summary>
    public class DebugUnlockGestureTests
    {
        [Test]
        public void StartsLocked()
        {
            var gesture = new DebugUnlockGesture();

            Assert.That(gesture.IsUnlocked, Is.False);
        }

        [Test]
        public void TenTapsWithinTheWindow_Unlocks()
        {
            var gesture = new DebugUnlockGesture();

            // Ten taps spread across 4.5s — comfortably inside the 10s window.
            for (var i = 0; i < DebugUnlockGesture.TapsToUnlock; i++)
            {
                gesture.RegisterTap(i * 0.5);
            }

            Assert.That(gesture.IsUnlocked, Is.True);
        }

        [Test]
        public void RegisterTap_ReturnsTheUnlockedStateAfterTheTap()
        {
            var gesture = new DebugUnlockGesture();

            for (var i = 0; i < DebugUnlockGesture.TapsToUnlock - 1; i++)
            {
                Assert.That(gesture.RegisterTap(i * 0.1), Is.False,
                    "not unlocked until the tenth tap");
            }

            Assert.That(gesture.RegisterTap(0.9), Is.True, "the tenth tap unlocks");
        }

        [Test]
        public void NineTapsWithinTheWindow_DoesNotUnlock()
        {
            var gesture = new DebugUnlockGesture();

            for (var i = 0; i < DebugUnlockGesture.TapsToUnlock - 1; i++)
            {
                gesture.RegisterTap(i * 0.5);
            }

            Assert.That(gesture.IsUnlocked, Is.False);
        }

        [Test]
        public void TenTapsSpreadOverMoreThanTheWindow_DoesNotUnlock()
        {
            var gesture = new DebugUnlockGesture();

            // Ten taps 2s apart span 18s — no 10s window ever holds all ten.
            for (var i = 0; i < DebugUnlockGesture.TapsToUnlock; i++)
            {
                gesture.RegisterTap(i * 2.0);
            }

            Assert.That(gesture.IsUnlocked, Is.False);
        }

        [Test]
        public void OldTapsFallOutOfTheWindow_ButAFreshBurstStillUnlocks()
        {
            var gesture = new DebugUnlockGesture();

            // Five stale taps, then a 10-tap burst well after they expire.
            for (var i = 0; i < 5; i++)
            {
                gesture.RegisterTap(i * 0.5);
            }

            for (var i = 0; i < DebugUnlockGesture.TapsToUnlock; i++)
            {
                gesture.RegisterTap(100.0 + i * 0.2);
            }

            Assert.That(gesture.IsUnlocked, Is.True);
        }

        [Test]
        public void OnceUnlocked_StaysUnlocked()
        {
            var gesture = new DebugUnlockGesture();

            for (var i = 0; i < DebugUnlockGesture.TapsToUnlock; i++)
            {
                gesture.RegisterTap(i * 0.2);
            }

            Assert.That(gesture.IsUnlocked, Is.True);

            // A much later lone tap must not re-lock it.
            gesture.RegisterTap(500.0);

            Assert.That(gesture.IsUnlocked, Is.True);
        }
    }
}
