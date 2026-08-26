using System;
using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Quests
{
    /// <summary>
    /// #310: the policy-driven refresh boundary on <see cref="QuestManager"/>.
    /// The refresh is a top-up toward the population-scaled cap that fires on
    /// the persisted UTC cadence, is purely additive (never clears/expires/fails
    /// a quest — economy.md #27/#28), pays out every interval that elapsed while
    /// the game was closed (#743, bounded by the cap rather than by a
    /// max-offline constant), and guarantees at least one free-type quest so the
    /// player is never soft-locked at 0 coins.
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

        /// <summary>A state topped up to <paramref name="dogCount"/> dogs so the
        /// population-scaled target (and hence the #543 per-hour rate) can be
        /// exercised above the floor.</summary>
        private static GameState StateWithDogs(int dogCount)
        {
            var state = GameState.CreateNew();
            for (var i = state.Dogs.Count; i < dogCount; i++)
            {
                state.AddDog(new Dog($"extra-{i}", Breed.GermanShepherd, Personality.Brave, 1, false));
            }

            return state;
        }

        [Test]
        public void TickPacing_RefreshesOnBoundary_AndRecordsTimestamp()
        {
            // 18 dogs -> target 6 -> 1.0/hr, so a single hourly boundary adds a
            // whole quest (the #543 rate at target 6 is exactly one per hour).
            var state = StateWithDogs(18);
            // #704: the clock runs from the moment the board dropped below
            // target (here: it is empty), so arm it an hour before the boundary.
            state.RecordQuestRefreshTimerStart(T0 - EconomyNumbers.RefreshInterval);

            state.Quests.TickPacing(T0, new Random(1));

            Assert.That(state.Quests.ActiveQuests.Count(), Is.GreaterThan(0), "a 1.0/hr refresh adds a quest");
            Assert.That(state.LastRotationUtc, Is.EqualTo(T0), "the refresh records its UTC instant");
        }

        [Test]
        public void TickPacing_UnderInterval_IsNoOp()
        {
            var state = StateWithDogs(18); // 1.0/hr so the first boundary adds a quest
            state.RecordQuestRefreshTimerStart(T0 - EconomyNumbers.RefreshInterval); // #704: arm the wait
            state.Quests.TickPacing(T0, new Random(1));
            var countAfterFirst = state.Quests.ActiveQuests.Count();

            state.Quests.TickPacing(T0 + TimeSpan.FromMinutes(30), new Random(2));

            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(countAfterFirst),
                "a call under the 1h interval must not add quests");
            Assert.That(state.LastRotationUtc, Is.EqualTo(T0), "an under-interval no-op leaves the timestamp alone");
        }

        [Test]
        public void TickPacing_TopsUpTowardTarget_NeverExceedingTheCap()
        {
            var state = GameState.CreateNew();
            var target = Target(state); // 8 dogs -> 3

            var rng = new Random(5);
            var now = T0;
            for (var i = 0; i < 12; i++)
            {
                state.Quests.TickPacing(now, rng);
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
                state.Quests.TickPacing(now, new Random(i));
                now += EconomyNumbers.RefreshInterval;
            }

            Assert.That(active.Status, Is.Not.EqualTo(QuestStatus.Completed), "an active quest is never expired");
            Assert.That(state.Quests.ActiveQuests.Select(q => q.Id), Does.Contain(active.Id));
            Assert.That(buy.Status, Is.EqualTo(QuestStatus.Completed), "a completed quest stays completed");
            Assert.That(state.PlacedItems.Count, Is.EqualTo(placedAfterDelivery), "delivered items are never removed");
        }

        [Test]
        public void MissedTime_PaysOutEveryElapsedInterval_StillBoundedByTheBoardCap()
        {
            // #743 reverses #543/#704's "one top-up however long you were away".
            // Away one interval pays one interval's trickle; away four days pays
            // every interval that was due in them — which, because one pacing
            // window's worth already accrues the whole target, lands on a full
            // board rather than a flood. 100 dogs -> target 12.
            var awayOneInterval = StateWithDogs(100);
            var away4d = StateWithDogs(100);
            // #704: the wait is measured from when the board dropped below
            // target — one player away one interval, the other four days.
            awayOneInterval.RecordQuestRefreshTimerStart(T0 - EconomyNumbers.RefreshInterval);
            away4d.RecordQuestRefreshTimerStart(T0 - TimeSpan.FromDays(4));

            awayOneInterval.Quests.TickPacing(T0, new Random(9));
            away4d.Quests.TickPacing(T0, new Random(9));

            Assert.That(awayOneInterval.Quests.ActiveQuests.Count(),
                Is.LessThan(Target(awayOneInterval)),
                "one interval's trickle cannot fill an empty board on its own");
            Assert.That(away4d.Quests.ActiveQuests.Count(),
                Is.GreaterThan(awayOneInterval.Quests.ActiveQuests.Count()),
                "the longer absence pays out the intervals it was owed");
            Assert.That(away4d.Quests.ActiveQuests.Count(), Is.EqualTo(Target(away4d)),
                "and stops at a full board — the cap, not a max-offline constant, is the ceiling");
            Assert.That(away4d.LastRotationUtc, Is.EqualTo(T0), "the rotation stamp is the instant of the top-up");
        }

        [Test]
        public void FreeQuestInvariant_ForcesAFreeType_WhenTheTopUpWouldLeaveAnAllPaidSet()
        {
            // Soft-lock guard: 0 coins + every active quest costs coins to
            // accept is a dead end. Two paid quests already active; the single
            // top-up slot is forced to a free type. 18 dogs -> 1.0/hr so the
            // boundary actually adds a quest (#543 — a quiet hour that adds
            // nothing has no slot to force).
            var state = StateWithDogs(18);
            state.RecordQuestRefreshTimerStart(T0 - EconomyNumbers.RefreshInterval); // #704: arm the wait
            state.Quests.GiveQuestTo(state.Dogs[0], QuestType.BuyGift, new Random(1));
            state.Quests.GiveQuestTo(state.Dogs[1], QuestType.BuyGift, new Random(2));
            Assert.That(state.Quests.ActiveQuests.All(q => !IsFree(q.Type)), Is.True,
                "precondition: the active set is all-paid");

            state.Quests.TickPacing(T0, new Random(3));

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
            Assert.That(new QuestPacingPolicy().ShouldRefresh(instant + TimeSpan.FromMinutes(30), loaded), Is.False,
                "so the 1h boundary still holds after a reload");
        }

        [Test]
        public void UnrotatedState_RoundTripsAsNull()
        {
            var loaded = SaveCodec.Load(SaveCodec.Save(GameState.CreateNew()));

            Assert.That(loaded.LastRotationUtc, Is.Null, "a never-rotated save loads back with no timestamp");
        }

        // --- #457: the Debug-tab "Refresh quests now" forced refresh ---

        [Test]
        public void ForceRefresh_TopsUp_EvenWhenTheCadenceGateWouldBlock()
        {
            // A rotation just happened, so ShouldRefresh is false under the 1h
            // window — yet the forced refresh must add quests anyway (skip the
            // timer). 18 dogs -> 1.0/hr so a single forced tick adds a quest.
            var state = StateWithDogs(18);
            state.RecordRotationUtc(T0);
            var justAfter = T0 + TimeSpan.FromMinutes(30);
            Assert.That(new QuestPacingPolicy().ShouldRefresh(justAfter, state), Is.False,
                "precondition: the natural cadence gate would block a refresh here");
            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(0), "precondition: no active quests yet");

            state.Quests.ForceRefresh(justAfter, new Random(1));

            Assert.That(state.Quests.ActiveQuests.Count(), Is.GreaterThan(0),
                "the forced refresh tops up even inside the 8h window");
        }

        [Test]
        public void ForceRefresh_RecordsThePassedInstant_RestartingTheEightHourWindow()
        {
            var state = GameState.CreateNew();
            state.RecordRotationUtc(T0);
            var forcedAt = T0 + TimeSpan.FromMinutes(30);

            state.Quests.ForceRefresh(forcedAt, new Random(1));

            Assert.That(state.LastRotationUtc, Is.EqualTo(forcedAt),
                "the forced refresh records its instant, same as a natural rotation");
            Assert.That(new QuestPacingPolicy().ShouldRefresh(forcedAt + TimeSpan.FromMinutes(30), state), Is.False,
                "so the 1h window restarts from the forced refresh");
        }

        [Test]
        public void ForceRefresh_AtTheCap_AddsNothing()
        {
            // Mirror StartNewDay's headroom behavior: once the neighborhood
            // already holds the target number of active quests, a forced
            // refresh is a no-op — no double-add past the cap.
            var state = GameState.CreateNew();
            var target = Target(state);
            var rng = new Random(5);
            var now = T0;
            for (var i = 0; i < 12 && state.Quests.ActiveQuests.Count() < target; i++)
            {
                state.Quests.TickPacing(now, rng);
                now += EconomyNumbers.RefreshInterval;
            }
            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(target), "precondition: at the cap");

            state.Quests.ForceRefresh(now, new Random(99));

            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(target),
                "a forced refresh at the cap adds nothing");
        }
    }
}
