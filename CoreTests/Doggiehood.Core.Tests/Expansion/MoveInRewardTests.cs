using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Tuning;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Expansion
{
    /// <summary>
    /// #675: a completed move-in pays the player a flat
    /// <see cref="EconomyNumbers.MoveInReward"/> — "a new neighbour pays for the
    /// next house" — deposited on the move-in state change itself, through the
    /// same wallet path quest and onboarding payouts use.
    ///
    /// <para>The payout is <b>per household, not per dog</b>: one move-in event
    /// fills exactly one house and pays exactly once, whatever the household's
    /// size. And it is rate-limited by quest completions, never by the number of
    /// vacant houses (the docs/specs/expansion.md invariant) — stockpiling
    /// vacancies can never turn building into a money loop.</para>
    /// </summary>
    public class MoveInRewardTests
    {
        // A move-in fires only when the roll lands under the current chance, so
        // the low end of SequenceRandom forces a success and the high end forces
        // a miss (matching MoveInSystemTests / MoveInPersistenceTests).
        private const double ForceMoveIn = 0.0;
        private const double ForceNoMoveIn = 0.99;

        // Enough coins to unlock a tile and build every lot on it during setup.
        private const int SetupCoins = 100_000;

        [TearDown]
        public void RestoreDefaults()
        {
            TuningConfig.ResetToDefaults();
        }

        // A game whose only vacant houses are freshly built frontier lots — a
        // fresh GameState's four starting houses are all occupied, so a move-in
        // roll would have nowhere to land.
        private static GameState StateWithVacantHouses(out int vacantCount)
        {
            var state = Doggiehood.Core.Tests.World.FrontierTestWorld.WithFirstTileUnlocked(SetupCoins);
            var tile = Doggiehood.Core.Tests.World.FrontierTestWorld.FirstTile;
            foreach (var lot in state.LotsForUnlockedTile(tile))
            {
                Assert.That(state.TryBuildHouse(lot.HouseId), Is.True, "precondition: the lot is buildable");
            }

            vacantCount = state.Houses.Count(h => h.IsVacant);
            Assert.That(vacantCount, Is.GreaterThan(0), "precondition: at least one vacant house exists");
            return state;
        }

        private static GameState StateWithOneVacantHouse()
        {
            var state = Doggiehood.Core.Tests.World.FrontierTestWorld.WithFirstTileUnlocked(SetupCoins);
            var lotId = Doggiehood.Core.Tests.World.FrontierTestWorld.FirstLotId;
            Assert.That(state.TryBuildHouse(lotId), Is.True, "precondition: a vacant lot is built");
            return state;
        }

        [Test]
        public void CompletedMoveIn_DepositsExactlyTheMoveInReward()
        {
            var state = StateWithOneVacantHouse();
            var before = state.Wallet.Coins;

            var household = state.HandleQuestCompleted(new SequenceRandom(ForceMoveIn));

            Assert.That(household, Is.Not.Empty, "precondition: a move-in fired");
            Assert.That(state.Wallet.Coins, Is.EqualTo(before + EconomyNumbers.MoveInReward),
                "a move-in pays the flat move-in reward");
        }

        [Test]
        public void MultiDogHousehold_PaysTheFlatRewardOnce_NotOncePerDog()
        {
            // Per household, not per dog: the reward equals what one house costs
            // to build, and that equivalence only holds once per move-in event.
            // The household mix is rolled, so drive move-ins until a multi-dog
            // household turns up and assert the payout never scaled with size.
            for (var seed = 0; seed < 200; seed++)
            {
                var state = StateWithOneVacantHouse();
                var before = state.Wallet.Coins;
                var household = state.HandleQuestCompleted(new Random(seed));
                if (household.Count < 2)
                {
                    continue;
                }

                Assert.That(state.Wallet.Coins, Is.EqualTo(before + EconomyNumbers.MoveInReward),
                    $"a {household.Count}-dog household still pays the flat reward once");
                return;
            }

            Assert.Fail("no multi-dog household was rolled — the test never exercised its case");
        }

        [Test]
        public void QuestCompletionWithoutAMoveIn_PaysNoMoveInReward()
        {
            var state = StateWithOneVacantHouse();
            var before = state.Wallet.Coins;

            var household = state.HandleQuestCompleted(new SequenceRandom(ForceNoMoveIn));

            Assert.That(household, Is.Empty, "precondition: the move-in roll failed");
            Assert.That(state.Wallet.Coins, Is.EqualTo(before),
                "a completion that produced no move-in pays no move-in reward");
        }

        [Test]
        public void MoveInReward_RidesTheOrdinaryWalletDepositPath()
        {
            // No second money-granting mechanism: the reward lands as one
            // ordinary Wallet deposit, so the #542 CoinsChanged signal (and the
            // HUD chip that rides it) sees it like any other payout.
            var state = StateWithOneVacantHouse();
            var deltas = new List<int>();
            state.Wallet.CoinsChanged += delta => deltas.Add(delta);

            state.HandleQuestCompleted(new SequenceRandom(ForceMoveIn));

            Assert.That(deltas, Is.EqualTo(new[] { EconomyNumbers.MoveInReward }),
                "exactly one deposit, of exactly the move-in reward");
        }

        [Test]
        public void ManyVacantHouses_StillPayAtMostOnceForOneQuestCompletion()
        {
            // The invariant (docs/specs/expansion.md): move-in income is
            // rate-limited by quest completions, never by the number of vacant
            // houses. Stockpiling vacancies must not multiply the payout.
            var state = StateWithVacantHouses(out var vacantCount);
            Assert.That(vacantCount, Is.GreaterThan(1),
                "precondition: several vacant houses are standing at once");
            var before = state.Wallet.Coins;

            var household = state.HandleQuestCompleted(new SequenceRandom(ForceMoveIn));

            Assert.That(household, Is.Not.Empty, "precondition: a move-in fired");
            Assert.That(state.Wallet.Coins - before, Is.EqualTo(EconomyNumbers.MoveInReward),
                "one completion pays for at most one move-in, however many houses stand empty");
            Assert.That(state.Houses.Count(h => h.IsVacant), Is.EqualTo(vacantCount - 1),
                "exactly one house was filled");
        }

        [Test]
        public void MoveInReward_ReadsFromTuningConfig_SoTheDebugMenuCanRetuneIt()
        {
            const int overridden = 175;
            TuningConfig.Active.MoveInReward = overridden;

            Assert.That(EconomyNumbers.MoveInReward, Is.EqualTo(overridden));

            var state = StateWithOneVacantHouse();
            var before = state.Wallet.Coins;
            Assert.That(state.HandleQuestCompleted(new SequenceRandom(ForceMoveIn)), Is.Not.Empty);
            Assert.That(state.Wallet.Coins, Is.EqualTo(before + overridden),
                "the overridden amount flows through the real deposit path");
        }

        [Test]
        public void AlreadyPaidMoveIn_DoesNotRePayOnLoad_AndTheBalanceRoundTrips()
        {
            var state = StateWithOneVacantHouse();
            Assert.That(state.HandleQuestCompleted(new SequenceRandom(ForceMoveIn)), Is.Not.Empty,
                "precondition: a move-in fired and paid");
            var paidBalance = state.Wallet.Coins;

            var reloaded = SaveCodec.Load(SaveCodec.Save(state));

            Assert.That(reloaded.Wallet.Coins, Is.EqualTo(paidBalance),
                "the balance round-trips — the reward is banked, not re-granted");
            Assert.That(reloaded.Houses.Count(h => h.IsVacant), Is.EqualTo(0),
                "the filled house is still occupied, so no second payout is waiting on it");
        }
    }
}
