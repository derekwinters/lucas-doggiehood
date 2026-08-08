using Doggiehood.Core.Expansion;
using Doggiehood.Core.Onboarding;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #469: the onboarding "upgrade a house" reward-chain step
    /// (<see cref="OnboardingRewardStep.UpgradeHouse"/>) is scoped to the house
    /// of the first-quest dog. While the chain waits on that step,
    /// <see cref="GameState.TryUpgradeHouse"/> upgrades only the stored target
    /// house — any other house is a no-op (no charge, no level change), so the
    /// self-funding ladder can't be soft-locked by spending the step's coins
    /// on the wrong house. Once the chain advances past the step, upgrading any
    /// house is unrestricted again.
    /// </summary>
    public class OnboardingUpgradeTargetTests
    {
        [Test]
        public void DuringUpgradeStep_NonTargetHouse_IsANoOp_NoChargeNoLevelChange()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(Doggiehood.Core.Tests.World.FrontierTestWorld.LoadAuthoredTargetMap());
            var target = state.Houses[0].Id;
            var other = state.Houses[1].Id;

            // Step 1 completes: pays the flat step reward and records the target house.
            state.GrantOnboardingCompletionReward(target);
            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.UpgradeHouse));
            var coinsBefore = state.Wallet.Coins;
            var otherLevelBefore = state.GetHouseLevel(other);

            var upgraded = state.TryUpgradeHouse(other);

            Assert.That(upgraded, Is.False, "a non-target house cannot be upgraded during the UpgradeHouse step");
            Assert.That(state.Wallet.Coins, Is.EqualTo(coinsBefore), "no coins are spent on the wrong house");
            Assert.That(state.GetHouseLevel(other), Is.EqualTo(otherLevelBefore), "the wrong house's level is unchanged");
            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.UpgradeHouse),
                "the chain keeps waiting on the UpgradeHouse step");
        }

        [Test]
        public void DuringUpgradeStep_TargetHouse_Charges_RaisesLevel_AndAdvancesTheChain()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(Doggiehood.Core.Tests.World.FrontierTestWorld.LoadAuthoredTargetMap());
            var target = state.Houses[0].Id;

            state.GrantOnboardingCompletionReward(target);
            var levelBefore = state.GetHouseLevel(target);

            var upgraded = state.TryUpgradeHouse(target);

            Assert.That(upgraded, Is.True, "the first-quest dog's house is the eligible upgrade");
            Assert.That(state.GetHouseLevel(target), Is.EqualTo(levelBefore + 1), "the target house rose one level");
            Assert.That(state.Wallet.Coins,
                Is.EqualTo(2 * OnboardingRewardChainNumbers.RewardPerStep - HouseUpgradeNumbers.CostToLevel2),
                "the first-quest bonus funded the upgrade, and advancing pays the next step's reward");
            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.ExpandMap),
                "upgrading the target house advances the reward chain");
        }

        [Test]
        public void PastUpgradeStep_AnyHouseUpgradesFreely_TargetRestrictionLifted()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(Doggiehood.Core.Tests.World.FrontierTestWorld.LoadAuthoredTargetMap());
            var target = state.Houses[0].Id;
            var other = state.Houses[1].Id;

            // Walk the chain past UpgradeHouse (to ExpandMap and beyond).
            state.GrantOnboardingCompletionReward(target);
            state.TryUpgradeHouse(target);            // -> ExpandMap
            state.TryUnlockTile(Doggiehood.Core.Tests.World.FrontierTestWorld.FirstTile);                // -> BuildHouse
            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.BuildHouse));

            // A non-target starting house can now be upgraded normally (fund it).
            state.Wallet.Deposit(HouseUpgradeNumbers.CostToLevel2);
            var otherLevelBefore = state.GetHouseLevel(other);

            var upgraded = state.TryUpgradeHouse(other);

            Assert.That(upgraded, Is.True, "past the UpgradeHouse step any house upgrades normally");
            Assert.That(state.GetHouseLevel(other), Is.EqualTo(otherLevelBefore + 1));
        }

        [Test]
        public void EligibilityQuery_TracksTheTargetHouseGate()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(Doggiehood.Core.Tests.World.FrontierTestWorld.LoadAuthoredTargetMap());
            var target = state.Houses[0].Id;
            var other = state.Houses[1].Id;

            // Before the UpgradeHouse step, every house is eligible.
            Assert.That(state.IsHouseUpgradeEligible(target), Is.True);
            Assert.That(state.IsHouseUpgradeEligible(other), Is.True);

            state.GrantOnboardingCompletionReward(target);

            // During the UpgradeHouse step only the target house is eligible.
            Assert.That(state.IsHouseUpgradeEligible(target), Is.True, "the target house is eligible");
            Assert.That(state.IsHouseUpgradeEligible(other), Is.False, "a non-target house is not eligible");

            state.TryUpgradeHouse(target); // advances past UpgradeHouse

            // Past the step, every house is eligible again.
            Assert.That(state.IsHouseUpgradeEligible(other), Is.True,
                "the restriction lifts once the chain advances past UpgradeHouse");
        }

        [Test]
        public void TargetHouseId_RoundTripsThroughSaveCodec_SoTheRestrictionSurvivesReload()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(Doggiehood.Core.Tests.World.FrontierTestWorld.LoadAuthoredTargetMap());
            var target = state.Houses[0].Id;
            var other = state.Houses[1].Id;

            state.GrantOnboardingCompletionReward(target);
            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.UpgradeHouse));

            var reloaded = SaveCodec.Load(SaveCodec.Save(state));

            Assert.That(reloaded.OnboardingUpgradeTargetHouseId, Is.EqualTo(target),
                "the eligible-house id survives a save/reload mid-chain");
            Assert.That(reloaded.IsHouseUpgradeEligible(other), Is.False,
                "the target-house restriction still applies after reload");
            Assert.That(reloaded.TryUpgradeHouse(other), Is.False,
                "a non-target house is still a no-op after reload");
        }

        [Test]
        public void LegacySave_WithNoTargetLine_ImposesNoUpgradeRestriction()
        {
            // A pre-#469 save that is mid-chain on UpgradeHouse carries no
            // upgradeTarget= line; it must load without a phantom restriction so
            // the player is never soft-locked out of every house.
            var legacy = "version=1\ncoins=100\nonboarded=1\nrewardChain=UpgradeHouse\n";

            var state = SaveCodec.Load(legacy);

            Assert.That(state.OnboardingUpgradeTargetHouseId, Is.Null, "no target is stored for a legacy save");
            Assert.That(state.IsHouseUpgradeEligible(state.Houses[1].Id), Is.True,
                "with no stored target, every house stays upgradable");
        }
    }
}
