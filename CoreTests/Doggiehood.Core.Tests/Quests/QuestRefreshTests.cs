using System;
using System.Linq;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Quests
{
    /// <summary>
    /// #310: the policy-driven refresh boundary on <see cref="QuestManager"/>.
    /// The refresh is a top-up toward the population-scaled cap that fires on
    /// the 8h UTC cadence, is purely additive (never clears/expires/fails a
    /// quest — economy.md #27/#28), does not simulate missed intervals, and
    /// guarantees at least one free-type quest so the player is never
    /// soft-locked at 0 coins.
    /// </summary>
    public class QuestRefreshTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 7, 29, 0, 0, 0, DateTimeKind.Utc);

        private static bool IsFree(QuestType type)
        {
            return type == QuestType.LostItem || type == QuestType.PestControl;
        }

        private static int Target(GameState state)
        {
            return new QuestPacingPolicy().TargetActiveCount(state);
        }

        [Test]
        public void MaybeStartNewDay_RefreshesOnBoundary_AndRecordsTimestamp()
        {
            var state = GameState.CreateNew();

            state.Quests.MaybeStartNewDay(T0, new Random(1));

            Assert.That(state.Quests.ActiveQuests.Count(), Is.GreaterThan(0), "a first refresh adds quests");
            Assert.That(state.LastRotationUtc, Is.EqualTo(T0), "the refresh records its UTC instant");
        }

        [Test]
        public void MaybeStartNewDay_UnderInterval_IsNoOp()
        {
            var state = GameState.CreateNew();
            state.Quests.MaybeStartNewDay(T0, new Random(1));
            var countAfterFirst = state.Quests.ActiveQuests.Count();

            state.Quests.MaybeStartNewDay(T0 + TimeSpan.FromHours(1), new Random(2));

            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(countAfterFirst),
                "a call under the 8h interval must not add quests");
            Assert.That(state.LastRotationUtc, Is.EqualTo(T0), "an under-interval no-op leaves the timestamp alone");
        }

        [Test]
        public void MaybeStartNewDay_TopsUpTowardTarget_NeverExceedingTheCap()
        {
            var state = GameState.CreateNew();
            var target = Target(state); // 8 dogs -> 3

            var rng = new Random(5);
            var now = T0;
            for (var i = 0; i < 12; i++)
            {
                state.Quests.MaybeStartNewDay(now, rng);
                Assert.That(state.Quests.ActiveQuests.Count(), Is.LessThanOrEqualTo(target),
                    "the top-up never pushes the active count past the cap");
                now += EconomyNumbers.RefreshInterval;
            }

            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(target),
                "repeated refreshes fill up to — and hold at — the cap");
        }

        [Test]
        public void Refresh_IsNonPunitive_NeverClearingOrCompletingExistingQuests()
        {
            // economy.md #27/#28: the boundary only adds — an active quest, a
            // completed quest's payout, and a delivered item all survive it.
            var state = GameState.CreateNew();
            state.Wallet.Deposit(500);

            var active = state.Quests.GiveQuestTo(state.Dogs[0], QuestType.LostItem, new Random(1));

            var buy = state.Quests.GiveQuestTo(state.Dogs[1], QuestType.BuyGift, new Random(2));
            state.Quests.Accept(buy);
            state.Quests.NotifyDogArrivedHome(buy);
            state.Quests.DeliverPackage(buy);
            var placedAfterDelivery = state.PlacedItems.Count;

            var now = T0;
            for (var i = 0; i < 20; i++)
            {
                state.Quests.MaybeStartNewDay(now, new Random(i));
                now += EconomyNumbers.RefreshInterval;
            }

            Assert.That(active.Status, Is.Not.EqualTo(QuestStatus.Completed), "an active quest is never expired");
            Assert.That(state.Quests.ActiveQuests.Select(q => q.Id), Does.Contain(active.Id));
            Assert.That(buy.Status, Is.EqualTo(QuestStatus.Completed), "a completed quest stays completed");
            Assert.That(state.PlacedItems.Count, Is.EqualTo(placedAfterDelivery), "delivered items are never removed");
        }

        [Test]
        public void MissedTime_PerformsExactlyOneTopUp_RegardlessOfElapsedIntervals()
        {
            // Away 8h vs away 4 days must land in the same place: one top-up to
            // the cap, no per-interval catch-up flood.
            var away8h = GameState.CreateNew();
            var away4d = GameState.CreateNew();
            away8h.RecordRotationUtc(T0 - EconomyNumbers.RefreshInterval);
            away4d.RecordRotationUtc(T0 - TimeSpan.FromDays(4));

            away8h.Quests.MaybeStartNewDay(T0, new Random(9));
            away4d.Quests.MaybeStartNewDay(T0, new Random(9));

            Assert.That(away8h.Quests.ActiveQuests.Count(), Is.LessThanOrEqualTo(Target(away8h)),
                "one top-up never exceeds the cap");
            Assert.That(away4d.Quests.ActiveQuests.Count(), Is.EqualTo(away8h.Quests.ActiveQuests.Count()),
                "the count added is independent of how long the player was away");
            Assert.That(away4d.LastRotationUtc, Is.EqualTo(T0), "the timestamp resets to now, not per missed interval");
        }

        [Test]
        public void FreeQuestInvariant_ForcesAFreeType_WhenTheTopUpWouldLeaveAnAllPaidSet()
        {
            // Soft-lock guard: 0 coins + every active quest costs coins to
            // accept is a dead end. Two paid quests already active; the single
            // top-up slot is forced to a free type.
            var state = GameState.CreateNew();
            state.Quests.GiveQuestTo(state.Dogs[0], QuestType.BuyGift, new Random(1));
            state.Quests.GiveQuestTo(state.Dogs[1], QuestType.BuyGift, new Random(2));
            Assert.That(state.Quests.ActiveQuests.All(q => !IsFree(q.Type)), Is.True,
                "precondition: the active set is all-paid");

            state.Quests.MaybeStartNewDay(T0, new Random(3));

            Assert.That(state.Quests.ActiveQuests.Any(q => IsFree(q.Type)), Is.True,
                "the refresh must guarantee at least one free-type quest");
        }

        [Test]
        public void CompletingAQuest_DoesNotReplenishAFreeQuest_OffCadence()
        {
            // Free-quest income is rate-limited to the refresh tick — completing
            // a quest must NOT instantly inject a free quest (that would be a
            // coin faucet). Two paid quests; completing one leaves the set
            // still all-paid until the next refresh boundary.
            var state = GameState.CreateNew();
            state.Wallet.Deposit(500);
            var buyA = state.Quests.GiveQuestTo(state.Dogs[0], QuestType.BuyGift, new Random(1));
            state.Quests.GiveQuestTo(state.Dogs[1], QuestType.BuyGift, new Random(2));

            state.Quests.Accept(buyA);
            state.Quests.NotifyDogArrivedHome(buyA);
            state.Quests.DeliverPackage(buyA);

            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(1), "completion adds no replacement quest");
            Assert.That(state.Quests.ActiveQuests.Any(q => IsFree(q.Type)), Is.False,
                "no free quest is injected off the refresh cadence");
        }

        [Test]
        public void LastRotationUtc_RoundTripsThroughSaveCodec()
        {
            var state = GameState.CreateNew();
            var instant = new DateTime(2026, 7, 29, 14, 37, 5, DateTimeKind.Utc);
            state.RecordRotationUtc(instant);

            var loaded = SaveCodec.Load(SaveCodec.Save(state));

            Assert.That(loaded.LastRotationUtc, Is.EqualTo(instant), "the exact UTC instant survives save/load");
            Assert.That(loaded.LastRotationUtc.Value.Kind, Is.EqualTo(DateTimeKind.Utc), "and stays UTC");
            Assert.That(new QuestPacingPolicy().ShouldRefresh(instant + TimeSpan.FromHours(1), loaded), Is.False,
                "so the 8h boundary still holds after a reload");
        }

        [Test]
        public void UnrotatedState_RoundTripsAsNull()
        {
            var loaded = SaveCodec.Load(SaveCodec.Save(GameState.CreateNew()));

            Assert.That(loaded.LastRotationUtc, Is.Null, "a never-rotated save loads back with no timestamp");
        }
    }
}
