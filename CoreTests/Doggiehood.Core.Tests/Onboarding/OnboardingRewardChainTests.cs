using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.Onboarding;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Onboarding
{
    /// <summary>
    /// #316: the 4-step scripted, one-time first-run reward chain
    /// (first-quest -> upgrade -> expand -> build), each step paying a flat
    /// 100 coins by reusing the quest reward-payout path (a Wallet deposit),
    /// not the random rotation. Steps fire exactly once, in fixed order, and
    /// the chain self-funds every step.
    /// </summary>
    public class OnboardingRewardChainTests
    {
        [Test]
        public void StartsAtTheFirstStep_InFixedOrder()
        {
            var chain = new OnboardingRewardChain();

            Assert.That(chain.CurrentStep, Is.EqualTo(OnboardingRewardStep.FirstQuest));
            Assert.That(chain.IsComplete, Is.False);
        }

        [Test]
        public void RewardPerStep_IsTheSingleHundredCoinConstant()
        {
            Assert.That(OnboardingRewardChainNumbers.RewardPerStep, Is.EqualTo(100));
        }

        [Test]
        public void CompletingTheCurrentStep_PaysExactlyOneHundred_AndAdvances()
        {
            var chain = new OnboardingRewardChain();
            var wallet = new Wallet();

            var paid = chain.TryAdvance(OnboardingRewardStep.FirstQuest, wallet);

            Assert.That(paid, Is.True);
            Assert.That(wallet.Coins, Is.EqualTo(OnboardingRewardChainNumbers.RewardPerStep));
            Assert.That(chain.CurrentStep, Is.EqualTo(OnboardingRewardStep.UpgradeHouse));
        }

        [Test]
        public void RepeatingTheSameStepsAction_PaysNothing_AndDoesNotAdvance()
        {
            var chain = new OnboardingRewardChain();
            var wallet = new Wallet();
            chain.TryAdvance(OnboardingRewardStep.FirstQuest, wallet);

            var paidAgain = chain.TryAdvance(OnboardingRewardStep.FirstQuest, wallet);

            Assert.That(paidAgain, Is.False, "each step fires exactly once");
            Assert.That(wallet.Coins, Is.EqualTo(OnboardingRewardChainNumbers.RewardPerStep));
            Assert.That(chain.CurrentStep, Is.EqualTo(OnboardingRewardStep.UpgradeHouse));
        }

        [Test]
        public void ALaterStepsActionPerformedEarly_NeitherPaysNorAdvances()
        {
            var chain = new OnboardingRewardChain();
            var wallet = new Wallet();

            // Build attempted while the chain is still waiting on the first quest.
            var paid = chain.TryAdvance(OnboardingRewardStep.BuildHouse, wallet);

            Assert.That(paid, Is.False, "steps fire only in order");
            Assert.That(wallet.Coins, Is.EqualTo(0));
            Assert.That(chain.CurrentStep, Is.EqualTo(OnboardingRewardStep.FirstQuest),
                "the chain keeps waiting on the current step");
        }

        [Test]
        public void StepsAdvanceInFixedOrder_ThroughAllFour_ThenCompletesAndStaysDone()
        {
            var chain = new OnboardingRewardChain();
            var wallet = new Wallet();

            Assert.That(chain.TryAdvance(OnboardingRewardStep.FirstQuest, wallet), Is.True);
            Assert.That(chain.CurrentStep, Is.EqualTo(OnboardingRewardStep.UpgradeHouse));
            Assert.That(chain.TryAdvance(OnboardingRewardStep.UpgradeHouse, wallet), Is.True);
            Assert.That(chain.CurrentStep, Is.EqualTo(OnboardingRewardStep.ExpandMap));
            Assert.That(chain.TryAdvance(OnboardingRewardStep.ExpandMap, wallet), Is.True);
            Assert.That(chain.CurrentStep, Is.EqualTo(OnboardingRewardStep.BuildHouse));
            Assert.That(chain.TryAdvance(OnboardingRewardStep.BuildHouse, wallet), Is.True);

            Assert.That(chain.IsComplete, Is.True);
            Assert.That(wallet.Coins, Is.EqualTo(4 * OnboardingRewardChainNumbers.RewardPerStep));

            // A one-time chain never pays again once complete.
            Assert.That(chain.TryAdvance(OnboardingRewardStep.BuildHouse, wallet), Is.False);
            Assert.That(wallet.Coins, Is.EqualTo(4 * OnboardingRewardChainNumbers.RewardPerStep));
        }

        [Test]
        public void SelfFunding_EachStepsRewardCoversTheNextStepsCost_AcrossAllFourSteps()
        {
            // The player starts with nothing; the chain funds itself end-to-end:
            // 100 bonus >= 100 upgrade; upgrade reward >= 50 expand (#540: the
            // first tile is now only 50); expand reward >= 50 build; the player
            // ends with a (now larger) cushion.
            var state = GameState.CreateNew();
            state.SetTargetMap(Doggiehood.Core.Tests.World.FrontierTestWorld.LoadAuthoredTargetMap());
            Assert.That(state.Wallet.Coins, Is.EqualTo(0));
            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.FirstQuest));

            // Step 1: first quest completed -> onboarding-completion bonus.
            state.GrantOnboardingCompletionReward(state.Houses[0].Id);
            Assert.That(state.Wallet.Coins, Is.EqualTo(OnboardingRewardChainNumbers.RewardPerStep));
            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.UpgradeHouse));

            // Step 2: upgrade a house (L1->L2, cost 100) — funded by the bonus.
            var houseId = state.Houses[0].Id;
            Assert.That(state.Wallet.CanAfford(HouseUpgradeNumbers.CostToLevel2), Is.True,
                "the 100 bonus covers the 100 upgrade");
            Assert.That(state.TryUpgradeHouse(houseId), Is.True);
            Assert.That(state.Wallet.Coins, Is.EqualTo(OnboardingRewardChainNumbers.RewardPerStep));
            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.ExpandMap));

            // Step 3: expand the map (first tile, #540 cost 50) — funded by step 2.
            Assert.That(state.Wallet.CanAfford(TileUnlock.Cost(state.Map.Tiles.Count)), Is.True,
                "the upgrade reward covers the 50 expand");
            Assert.That(state.TryUnlockTile(Doggiehood.Core.Tests.World.FrontierTestWorld.FirstTile), Is.True);
            Assert.That(state.Wallet.Coins,
                Is.EqualTo(OnboardingRewardChainNumbers.RewardPerStep
                    + (OnboardingRewardChainNumbers.RewardPerStep - TileUnlock.Cost(1))),
                "step 2's balance plus step 3's reward, minus the cheaper 50 expand");
            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.BuildHouse));

            // Step 4: build a house on the newly unlocked lot (base cost 50).
            var lot = state.LotsForUnlockedTile(Doggiehood.Core.Tests.World.FrontierTestWorld.FirstTile)[0];
            Assert.That(state.Wallet.CanAfford(HouseBuildNumbers.BaseCost), Is.True,
                "the expand reward covers the 50 build");
            Assert.That(state.TryBuildHouse(lot.HouseId), Is.True);

            Assert.That(state.RewardChain.IsComplete, Is.True);
            Assert.That(state.Wallet.Coins,
                Is.EqualTo(4 * OnboardingRewardChainNumbers.RewardPerStep
                    - HouseUpgradeNumbers.CostToLevel2
                    - TileUnlock.Cost(1)
                    - HouseBuildNumbers.BaseCost),
                "the player ends the chain with a small cushion (larger now the first tile is 50)");
        }

        [Test]
        public void RotationStaysSuppressedThroughTheChain_AndIsReleasedExactlyAtTheBuildStep()
        {
            // #312 -> #310 handoff: no rotation is seeded while the guided chain
            // is in progress; the first normal rotation is released exactly when
            // step 4 (build) completes.
            var state = GameState.CreateNew();
            state.SetTargetMap(Doggiehood.Core.Tests.World.FrontierTestWorld.LoadAuthoredTargetMap());
            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(0));

            state.GrantOnboardingCompletionReward(state.Houses[0].Id);
            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(0), "no rotation at step 1");

            Assert.That(state.TryUpgradeHouse(state.Houses[0].Id), Is.True);
            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(0), "no rotation at step 2");

            Assert.That(state.TryUnlockTile(Doggiehood.Core.Tests.World.FrontierTestWorld.FirstTile), Is.True);
            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(0), "no rotation at step 3");

            var lot = state.LotsForUnlockedTile(Doggiehood.Core.Tests.World.FrontierTestWorld.FirstTile)[0];
            Assert.That(state.TryBuildHouse(lot.HouseId), Is.True);

            Assert.That(state.RewardChain.IsComplete, Is.True);
            Assert.That(state.Quests.ActiveQuests.Count(), Is.InRange(2, 4),
                "the normal rotation is released exactly when the chain completes at build");
        }

        [Test]
        public void RewardChainStep_RoundTripsThroughSaveCodec_SoItIsNotRestartedOnReload()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(Doggiehood.Core.Tests.World.FrontierTestWorld.LoadAuthoredTargetMap());
            state.GrantOnboardingCompletionReward(state.Houses[0].Id);
            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.UpgradeHouse));

            var reloaded = SaveCodec.Load(SaveCodec.Save(state));

            Assert.That(reloaded.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.UpgradeHouse),
                "the one-time chain resumes where it left off, not from the start");
        }

        [Test]
        public void CompletedRewardChain_RoundTripsAsComplete()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(Doggiehood.Core.Tests.World.FrontierTestWorld.LoadAuthoredTargetMap());
            state.GrantOnboardingCompletionReward(state.Houses[0].Id);
            state.TryUpgradeHouse(state.Houses[0].Id);
            state.TryUnlockTile(Doggiehood.Core.Tests.World.FrontierTestWorld.FirstTile);
            state.TryBuildHouse(Doggiehood.Core.Tests.World.FrontierTestWorld.FirstLotId);
            Assert.That(state.RewardChain.IsComplete, Is.True);

            var reloaded = SaveCodec.Load(SaveCodec.Save(state));

            Assert.That(reloaded.RewardChain.IsComplete, Is.True);
        }

        [Test]
        public void LegacySave_OnboardedWithoutARewardChainField_TreatsTheChainAsComplete()
        {
            // A pre-#316 save (onboarded, no rewardChain line) is a player who
            // has long since finished onboarding — never re-offer the guided
            // chain or its bonuses, and never re-suppress their rotation.
            var reloaded = SaveCodec.Load("version=1\ncoins=250\nonboarded=1\n");

            Assert.That(reloaded.RewardChain.IsComplete, Is.True);
        }

        [Test]
        public void FreshSave_NotOnboarded_KeepsTheChainAtTheFirstStep()
        {
            var reloaded = SaveCodec.Load("version=1\ncoins=0\nonboarded=0\n");

            Assert.That(reloaded.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.FirstQuest));
        }

        // --- #372: reward event surfaced for the celebration panel. The panel
        // (thin Unity layer) reacts to this Core event to show "You did it! +100
        // coins"; Core still owns the payout and never re-pays. Copy stays out of
        // Core — the event carries only the just-completed step + the amount. ---

        [Test]
        public void CompletingTheCurrentStep_RaisesASingleRewardEvent_WithTheStepAndAmount()
        {
            var chain = new OnboardingRewardChain();
            var wallet = new Wallet();
            var events = new List<(OnboardingRewardStep step, int amount)>();
            chain.RewardGranted += (step, amount) => events.Add((step, amount));

            chain.TryAdvance(OnboardingRewardStep.FirstQuest, wallet);

            Assert.That(events.Count, Is.EqualTo(1), "exactly one reward event per paid step");
            Assert.That(events[0].step, Is.EqualTo(OnboardingRewardStep.FirstQuest),
                "the event carries the step that was just completed");
            Assert.That(events[0].amount, Is.EqualTo(OnboardingRewardChainNumbers.RewardPerStep),
                "the event carries the flat reward amount (named constant, not a literal)");
        }

        [Test]
        public void EachOfTheFourSteps_RaisesItsOwnRewardEvent_InOrder()
        {
            var chain = new OnboardingRewardChain();
            var wallet = new Wallet();
            var steps = new List<OnboardingRewardStep>();
            chain.RewardGranted += (step, amount) => steps.Add(step);

            chain.TryAdvance(OnboardingRewardStep.FirstQuest, wallet);
            chain.TryAdvance(OnboardingRewardStep.UpgradeHouse, wallet);
            chain.TryAdvance(OnboardingRewardStep.ExpandMap, wallet);
            chain.TryAdvance(OnboardingRewardStep.BuildHouse, wallet);

            Assert.That(steps, Is.EqualTo(new[]
            {
                OnboardingRewardStep.FirstQuest,
                OnboardingRewardStep.UpgradeHouse,
                OnboardingRewardStep.ExpandMap,
                OnboardingRewardStep.BuildHouse,
            }), "each of the four steps celebrates exactly once, in fixed order");
        }

        [Test]
        public void AnOutOfOrderAction_RaisesNoRewardEvent_AndPaysNothing()
        {
            var chain = new OnboardingRewardChain();
            var wallet = new Wallet();
            var events = 0;
            chain.RewardGranted += (step, amount) => events++;

            // Build attempted while the chain is still waiting on the first quest.
            chain.TryAdvance(OnboardingRewardStep.BuildHouse, wallet);

            Assert.That(events, Is.EqualTo(0), "an out-of-turn action never celebrates");
            Assert.That(wallet.Coins, Is.EqualTo(0), "and never pays");
        }

        [Test]
        public void ActionAfterTheChainIsDone_RaisesNoRewardEvent()
        {
            var chain = new OnboardingRewardChain();
            var wallet = new Wallet();
            chain.TryAdvance(OnboardingRewardStep.FirstQuest, wallet);
            chain.TryAdvance(OnboardingRewardStep.UpgradeHouse, wallet);
            chain.TryAdvance(OnboardingRewardStep.ExpandMap, wallet);
            chain.TryAdvance(OnboardingRewardStep.BuildHouse, wallet);
            Assert.That(chain.IsComplete, Is.True);

            var events = 0;
            chain.RewardGranted += (step, amount) => events++;
            chain.TryAdvance(OnboardingRewardStep.BuildHouse, wallet);

            Assert.That(events, Is.EqualTo(0), "a completed one-time chain never celebrates again");
        }

        [Test]
        public void RestoreStep_RaisesNoRewardEvent_SoAReloadNeverReCelebrates()
        {
            var chain = new OnboardingRewardChain();
            var events = 0;
            chain.RewardGranted += (step, amount) => events++;

            chain.RestoreStep(OnboardingRewardStep.ExpandMap);

            Assert.That(events, Is.EqualTo(0),
                "restoring persisted progress never re-pays nor re-celebrates (#316/#372)");
        }
    }
}
