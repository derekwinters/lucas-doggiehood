using System.Collections.Generic;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.Onboarding;
using Doggiehood.Core.Tests.World;
using Doggiehood.Core.Tuning;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Onboarding
{
    /// <summary>
    /// #674: the onboarding reward-chain is a <b>self-funding ladder</b> — the
    /// four scripted steps must pay for themselves, so a brand-new player who
    /// has earned nothing else can walk quest -> upgrade -> expand -> build
    /// without ever stalling on a cost they cannot cover.
    ///
    /// <para>This is the guard that stops a balance pass from silently
    /// soft-locking the tutorial (it has happened once already: #540 dropped
    /// the unlock cost and left the spec's ladder walkthrough stale). Every
    /// cost here is read from the live pricing seams
    /// (<see cref="OnboardingLadder.StepCosts"/> -> <see cref="TuningConfig"/>),
    /// and the minimum viable reward is <b>derived</b> from those costs rather
    /// than typed in — so the relationship keeps holding when any of the
    /// numbers is tuned again.</para>
    /// </summary>
    public class OnboardingLadderTests
    {
        [TearDown]
        public void RestoreDefaults()
        {
            TuningConfig.ResetToDefaults();
        }

        [Test]
        public void StepCosts_ReadTheLivePricingSeams_InStepOrder()
        {
            Assert.That(OnboardingLadder.StepCosts, Is.EqualTo(new[]
            {
                OnboardingLadder.FirstQuestStepCost,
                HouseUpgradeNumbers.CostToLevel2,
                TileUnlock.Cost(TileUnlockNumbers.OriginTileCount),
                HouseBuildNumbers.Cost(housesBuilt: 0),
            }), "the ladder prices itself off the live seams, never off literals");
        }

        [Test]
        public void StepCosts_TrackATunedCost_SoTheGuardCannotGoStale()
        {
            const int overriddenUnlockBase = 777;
            TuningConfig.Active.TileUnlockBaseCost = overriddenUnlockBase;

            Assert.That(OnboardingLadder.StepCosts[(int)OnboardingRewardStep.ExpandMap],
                Is.EqualTo(overriddenUnlockBase),
                "step 3's cost follows TuningConfig, so tuning the unlock re-checks the ladder");
        }

        [Test]
        public void MinimumSelfFundingReward_IsTheSmallestRewardTheLadderSurvives()
        {
            // Characterizes the derived minimum against an independent walk of
            // the ladder: at the minimum every step is affordable when reached,
            // and one coin per step less breaks it. No literal anywhere.
            var minimum = OnboardingLadder.MinimumSelfFundingRewardPerStep;

            Assert.That(OnboardingLadder.IsSelfFundingAt(minimum), Is.True,
                "the derived minimum funds the whole ladder");
            Assert.That(OnboardingLadder.IsSelfFundingAt(minimum - 1), Is.False,
                "and it really is the SMALLEST such reward");
        }

        [Test]
        public void TheShippingRewardPerStep_FundsTheLadder()
        {
            Assert.That(OnboardingLadder.IsSelfFunding, Is.True,
                "the shipped onboarding reward must cover the shipped step costs");
            Assert.That(OnboardingRewardChainNumbers.RewardPerStep,
                Is.GreaterThanOrEqualTo(OnboardingLadder.MinimumSelfFundingRewardPerStep));
        }

        [Test]
        public void RaisingTheUnlockWithoutRaisingTheReward_WouldSoftLockTheLadder()
        {
            // The #674 pairing, pinned: the tile-unlock raise on its own leaves
            // the player at the expand step holding 2R - upgrade, short of the
            // new unlock price. This is why the two values move together.
            TuningConfig.Active.TileUnlockBaseCost = 200;
            TuningConfig.Active.OnboardingRewardPerStep = 100;

            Assert.That(OnboardingLadder.IsSelfFunding, Is.False,
                "a 200 unlock against a 100 reward strands the player at step 3");
        }

        [Test]
        public void WalkedEndToEndThroughGameState_EveryStepIsAffordable_AndTheBalanceNeverGoesNegative()
        {
            // The real Core entry points, not arithmetic: a player who starts
            // with nothing completes all four steps, and at no point does the
            // wallet dip below zero or a step's action become unaffordable.
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            var balances = new List<int> { state.Wallet.Coins };
            Assert.That(state.Wallet.Coins, Is.EqualTo(0), "a new player has earned nothing yet");

            // Step 1 — complete the tutorial quest (costs nothing).
            state.GrantOnboardingCompletionReward(state.Houses[0].Id);
            balances.Add(state.Wallet.Coins);

            // Step 2 — upgrade the target house.
            Assert.That(state.Wallet.CanAfford(OnboardingLadder.StepCosts[(int)OnboardingRewardStep.UpgradeHouse]),
                Is.True, "step 2's upgrade is affordable when the chain reaches it");
            Assert.That(state.TryUpgradeHouse(state.Houses[0].Id), Is.True);
            balances.Add(state.Wallet.Coins);

            // Step 3 — unlock the scripted first tile.
            Assert.That(state.Wallet.CanAfford(OnboardingLadder.StepCosts[(int)OnboardingRewardStep.ExpandMap]),
                Is.True, "step 3's tile unlock is affordable when the chain reaches it");
            Assert.That(state.TryUnlockTile(FrontierTestWorld.FirstTile), Is.True);
            balances.Add(state.Wallet.Coins);

            // Step 4 — build a house on the newly unlocked lot.
            Assert.That(state.Wallet.CanAfford(OnboardingLadder.StepCosts[(int)OnboardingRewardStep.BuildHouse]),
                Is.True, "step 4's build is affordable when the chain reaches it");
            Assert.That(state.TryBuildHouse(FrontierTestWorld.FirstLotId), Is.True);
            balances.Add(state.Wallet.Coins);

            Assert.That(state.RewardChain.IsComplete, Is.True, "all four steps paid out");
            Assert.That(balances, Is.All.GreaterThanOrEqualTo(0),
                "the ladder never leaves the player in the red");
            Assert.That(state.Wallet.Coins,
                Is.EqualTo(OnboardingRewardChainNumbers.RewardPerStep * OnboardingLadder.StepCount
                    - OnboardingLadder.TotalCost),
                "the player finishes holding four rewards minus the four step costs");
        }
    }
}
