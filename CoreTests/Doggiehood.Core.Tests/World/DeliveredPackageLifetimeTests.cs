using System.Linq;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #703: the delivered package's visible beat — how long the dropped box
    /// stays at the door before it is removed. The duration and the "has it
    /// elapsed" decision are Core's; the Unity layer only performs the
    /// destruction it is told to perform.
    /// </summary>
    public class DeliveredPackageLifetimeTests
    {
        [Test]
        public void ANewlyDroppedPackage_HasNotElapsedYet()
        {
            // #703 guard against a fix that removes the box instantly (or never
            // creates it): the beat must have a visible front half.
            var lifetime = new DeliveredPackageLifetime();

            Assert.That(lifetime.HasElapsed, Is.False, "a package is visible the moment it is dropped");
            Assert.That(lifetime.ElapsedSeconds, Is.EqualTo(0f));
        }

        [Test]
        public void PartWayThroughTheBeat_HasNotElapsed()
        {
            var lifetime = new DeliveredPackageLifetime();

            lifetime.Advance(DeliveredPackageLifetime.VisibleSeconds / 2f);

            Assert.That(lifetime.HasElapsed, Is.False, "half the beat is not the whole beat");
        }

        [Test]
        public void OnceTheBeatElapses_ItReportsElapsed()
        {
            var lifetime = new DeliveredPackageLifetime();

            lifetime.Advance(DeliveredPackageLifetime.VisibleSeconds);

            Assert.That(lifetime.HasElapsed, Is.True, "the package's beat is over at its full duration");
        }

        [Test]
        public void TheBeatAccumulatesAcrossFrames()
        {
            // The Unity layer feeds it one frame's deltaTime at a time.
            var lifetime = new DeliveredPackageLifetime();
            const float frame = 0.05f;

            for (var elapsed = 0f; elapsed < DeliveredPackageLifetime.VisibleSeconds; elapsed += frame)
            {
                Assert.That(lifetime.HasElapsed, Is.False, "still inside the beat");
                lifetime.Advance(frame);
            }

            Assert.That(lifetime.HasElapsed, Is.True, "the accumulated frames finish the beat");
        }

        [Test]
        public void StaysElapsedOnceOver_AndIgnoresNonPositiveSteps()
        {
            var lifetime = new DeliveredPackageLifetime();

            lifetime.Advance(-1f);
            Assert.That(lifetime.ElapsedSeconds, Is.EqualTo(0f), "a negative step never rewinds the beat");

            lifetime.Advance(DeliveredPackageLifetime.VisibleSeconds * 2f);
            lifetime.Advance(0f);

            Assert.That(lifetime.HasElapsed, Is.True, "the beat does not un-elapse");
        }

        [Test]
        public void TwoPackagesRunIndependentBeats()
        {
            // #600: concurrent deliveries each own their own beat — one
            // package's timer must not decide another's removal.
            var first = new DeliveredPackageLifetime();
            var second = new DeliveredPackageLifetime();

            first.Advance(DeliveredPackageLifetime.VisibleSeconds);

            Assert.That(first.HasElapsed, Is.True);
            Assert.That(second.HasElapsed, Is.False, "the second package's beat is its own");
        }

        [Test]
        public void TheBeatIsShort_ButLongEnoughToSee()
        {
            // The design calls for a short timed beat: long enough for the drop
            // to register while the dog is still in the waiting pose, short
            // enough that no box is left standing around.
            Assert.That(DeliveredPackageLifetime.VisibleSeconds, Is.GreaterThan(0.5f));
            Assert.That(DeliveredPackageLifetime.VisibleSeconds, Is.LessThan(10f));
        }
    }

    /// <summary>
    /// #703 regression guard: a delivered package is SCENE state, never SAVE
    /// state. The gift it carries is what persists (#27); the box itself is
    /// packaging, so a relaunch can neither resurrect a package at a door nor
    /// lose the gift that was delivered inside it (the failure mode #700/#702
    /// hit from the other direction).
    /// </summary>
    public class DeliveredPackageIsNotSaveStateTests
    {
        private static GameState DeliveredGiftState(out Quest quest)
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(100);
            quest = state.Quests.GiveQuestTo(state.Dogs[1], QuestType.BuyGift, new System.Random(3));
            state.Quests.Accept(quest);
            state.Quests.NotifyDogArrivedHome(quest);
            state.Quests.DeliverPackage(quest);
            return state;
        }

        [Test]
        public void ARelaunchAfterADelivery_RestoresNoPackage()
        {
            var state = DeliveredGiftState(out var quest);

            var payload = SaveCodec.Save(state);

            Assert.That(payload.ToLowerInvariant(), Does.Not.Contain("package"),
                "the package is a transient scene object — nothing about it belongs in the save, "
                + "or a relaunch would restore a box to the doorway");
            Assert.That(quest.ItemName.ToLowerInvariant(), Does.Not.Contain("package"),
                "guard for the assertion above: no catalog item is itself named 'package'");
        }

        [Test]
        public void RemovingTheBox_DoesNotRemoveTheGift()
        {
            // The box's lifetime is independent of the delivered item's record,
            // so nothing about the transient beat can cost the player what they
            // paid for.
            var state = DeliveredGiftState(out var quest);

            var loaded = SaveCodec.Load(SaveCodec.Save(state));

            Assert.That(loaded.PlacedItems.Any(p => p.ItemName == quest.ItemName), Is.True,
                "the delivered gift is a permanent PlacedItem (#27) and survives the relaunch");
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Completed), "the quest still completed");
            Assert.That(state.Wallet.Coins, Is.GreaterThan(0), "the quest still paid out");
        }
    }
}
