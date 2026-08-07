using System;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.Onboarding;
using Doggiehood.Core.Quests;
using Doggiehood.Core.Tuning;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Tuning
{
    /// <summary>
    /// #620: proves an overridden <see cref="TuningConfig"/> value flows
    /// through the dependent Core balance seam — the hook the debug tuning
    /// menu (#622) will use. Each test resets to shipping defaults in a
    /// finally so the ambient <see cref="TuningConfig.Active"/> never leaks
    /// into another test.
    /// </summary>
    public class TuningSeamOverrideTests
    {
        [TearDown]
        public void RestoreDefaults()
        {
            TuningConfig.ResetToDefaults();
        }

        [Test]
        public void OverriddenQuestPayout_FlowsThroughToACompletedQuest()
        {
            // Marquee end-to-end: set the reward to 25 and a completed quest
            // pays 25 through the real QuestManager deposit path.
            const int overriddenPayout = 25;
            TuningConfig.Active.QuestPayout = overriddenPayout;

            var state = GameState.CreateNew();
            var lost = state.Quests.GiveQuestTo(state.Dogs[0], QuestType.LostItem, new Random(1));
            var before = state.Wallet.Coins;

            state.Quests.Accept(lost);
            Assert.That(state.Quests.TapWorldPosition(lost.HiddenItemPosition.Value), Is.True);
            Assert.That(lost.Status, Is.EqualTo(QuestStatus.Completed));
            Assert.That(state.Wallet.Coins, Is.EqualTo(before + overriddenPayout));
        }

        [Test]
        public void OverriddenTileUnlockBaseCost_FlowsThroughTileUnlockCost()
        {
            const int overridden = 777;
            TuningConfig.Active.TileUnlockBaseCost = overridden;

            // First unlock (only the origin placed) is priced at the base.
            Assert.That(TileUnlock.Cost(placedTileCount: 1), Is.EqualTo(overridden));
        }

        [Test]
        public void OverriddenHouseBuildBaseCost_FlowsThroughHouseBuildCost()
        {
            const int overridden = 321;
            TuningConfig.Active.HouseBuildBaseCost = overridden;

            // First build (0 already built) is at the base.
            Assert.That(HouseBuildNumbers.Cost(housesBuilt: 0), Is.EqualTo(overridden));
        }

        [Test]
        public void OverriddenHouseUpgradeCost_FlowsThroughCostToReach()
        {
            const int overridden = 555;
            TuningConfig.Active.HouseUpgradeCostToLevel2 = overridden;

            Assert.That(HouseUpgradeNumbers.CostToReach(2), Is.EqualTo(overridden));
        }

        [Test]
        public void OverriddenOnboardingReward_FlowsThroughTheNumbersSeam()
        {
            const int overridden = 250;
            TuningConfig.Active.OnboardingRewardPerStep = overridden;

            Assert.That(OnboardingRewardChainNumbers.RewardPerStep, Is.EqualTo(overridden));
        }

        [Test]
        public void OverriddenMoveInChance_FlowsThroughCurrentMoveInChance()
        {
            const double overridden = 0.42;
            TuningConfig.Active.BaseMoveInChance = overridden;

            var system = new MoveInSystem();
            Assert.That(system.CurrentMoveInChance, Is.EqualTo(overridden));
        }

        [Test]
        public void OverriddenPacingCeiling_FlowsThroughTargetActiveCount()
        {
            const int overridden = 2;
            TuningConfig.Active.TargetActiveCeiling = overridden;
            // Keep the floor from clamping above the new ceiling.
            TuningConfig.Active.TargetActiveFloor = 1;

            var state = GameState.CreateNew(); // 5 starting dogs -> raw target 2 already, but push high to force the ceiling
            var policy = new QuestPacingPolicy();
            // With many dogs the raw target would exceed the ceiling; assert it clamps to the override.
            for (var i = 0; i < 100; i++)
            {
                state.AddDog(new Doggiehood.Core.Dogs.Dog(
                    "Extra" + i, Doggiehood.Core.Dogs.Breed.Puggle,
                    Doggiehood.Core.Dogs.Personality.Brave, houseId: 0, isPuppy: false));
            }

            Assert.That(policy.TargetActiveCount(state), Is.EqualTo(overridden));
        }

        [Test]
        public void OverriddenMidPopulationGate_FlowsThroughEligibleCostTiers()
        {
            const int overridden = 2;
            TuningConfig.Active.MidPopulationGate = overridden;

            // At population == the overridden gate the mid tier is now eligible.
            var tiers = QuestCostTiers.EligibleCostTiers(dogCount: overridden);
            Assert.That(tiers, Does.Contain(QuestCostTier.Mid));
        }
    }
}
