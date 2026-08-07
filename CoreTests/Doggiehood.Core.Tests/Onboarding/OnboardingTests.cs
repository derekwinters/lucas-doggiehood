using System.Linq;
using Doggiehood.Core.Onboarding;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Onboarding
{
    public class OnboardingTests
    {
        private static GameState StateWithQuests()
        {
            var state = GameState.CreateNew();
            // #543/#624: a single rotation now trickles in a fraction of the
            // target per hour (8 dogs -> 1.25/hr), so drive a full pacing window of
            // hourly boundaries to fill the neighborhood up to its target and
            // have active quests to target in onboarding.
            for (var hour = 0; hour < Doggiehood.Core.Economy.EconomyNumbers.PacingWindowHours; hour++)
            {
                state.Quests.StartNewDay(new System.Random(1 + hour));
            }

            return state;
        }

        [Test]
        public void FirstLaunchSeeding_TargetsTheSingleSeededLostItemDog()
        {
            // #312: the initial seam seeds exactly one lost-item quest, so the
            // sequence's TargetDog resolves to that dog and step 4 is the
            // gentle tap-to-find on the one and only quest.
            var state = GameState.CreateNew();
            state.Quests.BeginInitialQuests(new System.Random(3));

            var onboarding = new OnboardingSequence(state);

            var seededDog = state.Dogs.Single(d => d.HasActiveQuest);
            Assert.That(onboarding.TargetDog, Is.SameAs(seededDog));
            var quest = state.Quests.ActiveQuests.Single();
            Assert.That(quest.DogName, Is.EqualTo(onboarding.TargetDog.Name));
            Assert.That(quest.Type, Is.EqualTo(QuestType.LostItem));
        }

        [Test]
        public void CompletingOnboarding_PaysTheChainBonus_AndKeepsRotationSuppressed_ExactlyOnce()
        {
            // #316: completing the guided first quest no longer starts the
            // normal rotation. It grants step 1 of the onboarding reward chain
            // (a 100-coin completion bonus) and advances the chain to the next
            // guided step, leaving the rotation suppressed until the chain
            // completes at the build step (#312 -> #310 handoff moves to build).
            var state = GameState.CreateNew();
            state.Quests.BeginInitialQuests(new System.Random(3));
            var onboarding = new OnboardingSequence(state, new System.Random(7));
            onboarding.NotifyPanned();
            onboarding.NotifyZoomed();
            onboarding.NotifyConversationOpened(onboarding.TargetDog);

            var quest = state.Quests.ActiveQuests.First(q => q.DogName == onboarding.TargetDog.Name);
            var coinsBefore = state.Wallet.Coins;
            state.Quests.Accept(quest);
            state.Quests.TapWorldPosition(quest.HiddenItemPosition.Value);
            onboarding.NotifyQuestCompleted(quest);

            Assert.That(onboarding.CurrentStep, Is.EqualTo(OnboardingStep.Done));
            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.UpgradeHouse),
                "the reward chain advances to the next guided step");
            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(0),
                "the normal rotation stays suppressed while the guided chain runs");
            Assert.That(state.Wallet.Coins - coinsBefore,
                Is.EqualTo(Doggiehood.Core.Economy.EconomyNumbers.QuestPayout
                    + OnboardingRewardChainNumbers.RewardPerStep),
                "the standard quest payout plus the 100-coin onboarding-completion bonus");

            // Idempotent: re-signalling completion never pays the bonus twice
            // nor starts a rotation.
            onboarding.NotifyQuestCompleted(quest);
            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.UpgradeHouse));
            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(0));
        }

        [Test]
        public void FirstLaunchFlag_PersistsAcrossSaveLoad_AndPreventsReruns()
        {
            // #44: onboarding runs once, ever.
            var state = StateWithQuests();

            Assert.That(state.OnboardingComplete, Is.False);
            Assert.That(OnboardingSequence.ShouldRun(state), Is.True);

            state.MarkOnboardingComplete();
            var loaded = SaveCodec.Load(SaveCodec.Save(state));

            Assert.That(loaded.OnboardingComplete, Is.True);
            Assert.That(OnboardingSequence.ShouldRun(loaded), Is.False);
        }

        [Test]
        public void SequenceTargetsARealDogWithARealQuest()
        {
            // #44: no scripted fake — the tutorial quest is a live one.
            var state = StateWithQuests();
            var onboarding = new OnboardingSequence(state);

            Assert.That(onboarding.TargetDog, Is.Not.Null);
            Assert.That(onboarding.TargetDog.HasActiveQuest, Is.True);
            Assert.That(state.Dogs, Does.Contain(onboarding.TargetDog));
            Assert.That(state.Quests.ActiveQuests.Any(q => q.DogName == onboarding.TargetDog.Name), Is.True);
        }

        [Test]
        public void StepsAdvanceOnRealPlayerActions_InOrder()
        {
            var state = StateWithQuests();
            var onboarding = new OnboardingSequence(state);

            Assert.That(onboarding.CurrentStep, Is.EqualTo(OnboardingStep.Pan));

            onboarding.NotifyPanned();
            Assert.That(onboarding.CurrentStep, Is.EqualTo(OnboardingStep.Zoom));

            onboarding.NotifyZoomed();
            Assert.That(onboarding.CurrentStep, Is.EqualTo(OnboardingStep.TapBubble));

            onboarding.NotifyConversationOpened(onboarding.TargetDog);
            Assert.That(onboarding.CurrentStep, Is.EqualTo(OnboardingStep.CompleteQuest));
        }

        [Test]
        public void CompletingTheRealQuest_FinishesOnboarding_WithTheStandardPayoutPlusTheChainBonus()
        {
            // #44/#24 + #316: the quest pays the normal flat payout, and
            // finishing onboarding now also grants the 100-coin reward-chain
            // completion bonus (step 1).
            var state = StateWithQuests();
            state.Wallet.Deposit(60);
            var onboarding = new OnboardingSequence(state);
            onboarding.NotifyPanned();
            onboarding.NotifyZoomed();
            onboarding.NotifyConversationOpened(onboarding.TargetDog);

            var quest = state.Quests.ActiveQuests.First(q => q.DogName == onboarding.TargetDog.Name);
            var before = state.Wallet.Coins;

            // Complete it through the standard path for its type.
            if (quest.Type == QuestType.LostItem)
            {
                state.Quests.Accept(quest);
                state.Quests.TapWorldPosition(quest.HiddenItemPosition.Value);
            }
            else if (quest.Type == QuestType.BuyGift)
            {
                state.Quests.Accept(quest);
                state.Quests.NotifyDogArrivedHome(quest);
                state.Quests.DeliverPackage(quest);
            }
            else
            {
                state.Quests.Accept(quest);
                state.Quests.SprayHouse(quest.TargetHouseId.Value);
            }

            onboarding.NotifyQuestCompleted(quest);

            Assert.That(onboarding.CurrentStep, Is.EqualTo(OnboardingStep.Done));
            Assert.That(state.OnboardingComplete, Is.True);
            Assert.That(state.Wallet.Coins - before,
                Is.EqualTo(Doggiehood.Core.Economy.EconomyNumbers.QuestPayout - (quest.Cost ?? 0)
                    + OnboardingRewardChainNumbers.RewardPerStep));
        }

        [Test]
        public void TargetDogQuestResolved_AlsoFinishesOnboarding()
        {
            // Unity's overlay only observes that the target dog's quest is
            // gone; that signal must complete the sequence too.
            var state = StateWithQuests();
            var onboarding = new OnboardingSequence(state);
            onboarding.NotifyPanned();
            onboarding.NotifyZoomed();
            onboarding.NotifyConversationOpened(onboarding.TargetDog);

            onboarding.NotifyTargetDogQuestResolved();
            Assert.That(onboarding.CurrentStep, Is.EqualTo(OnboardingStep.CompleteQuest),
                "must not advance while the quest is still active");

            onboarding.TargetDog.ClearQuest();
            onboarding.NotifyTargetDogQuestResolved();

            Assert.That(onboarding.CurrentStep, Is.EqualTo(OnboardingStep.Done));
            Assert.That(state.OnboardingComplete, Is.True);
        }

        [Test]
        public void PollLoopOrder_PanZoomTapThenTargetQuestCleared_ReachesDone()
        {
            // #207 regression: drive the sequence exactly the way the Unity
            // overlay's poll loop does — pan, zoom, open the target dog's
            // conversation, then observe the target dog's quest clear and
            // signal NotifyTargetDogQuestResolved. This must still reach Done,
            // proving the Core state machine is correct and isolating the
            // reported "banner never dismisses" bug to the Unity wiring layer.
            var state = StateWithQuests();
            var onboarding = new OnboardingSequence(state);

            onboarding.NotifyPanned();
            onboarding.NotifyZoomed();
            onboarding.NotifyConversationOpened(onboarding.TargetDog);
            Assert.That(onboarding.CurrentStep, Is.EqualTo(OnboardingStep.CompleteQuest));

            // The overlay's CheckQuestCompletion only signals once the target
            // dog no longer has an active quest.
            onboarding.TargetDog.ClearQuest();
            onboarding.NotifyTargetDogQuestResolved();

            Assert.That(onboarding.CurrentStep, Is.EqualTo(OnboardingStep.Done));
            Assert.That(state.OnboardingComplete, Is.True);
        }

        [Test]
        public void WrongDogOrWrongQuest_DoesNotAdvanceTheSequence()
        {
            var state = StateWithQuests();
            var onboarding = new OnboardingSequence(state);
            onboarding.NotifyPanned();
            onboarding.NotifyZoomed();

            var otherDog = state.Dogs.First(d => d != onboarding.TargetDog);
            onboarding.NotifyConversationOpened(otherDog);

            Assert.That(onboarding.CurrentStep, Is.EqualTo(OnboardingStep.TapBubble));
        }

        [Test]
        public void OpeningTargetConversationEarly_IsRemembered_AndTapBubbleSelfHeals()
        {
            // #329: an early bubble tap during Pan/Zoom must not be silently
            // dropped. The open is remembered (it doesn't skip the pan/zoom
            // teaching steps), and on arriving at TapBubble the step
            // self-heals instead of stranding with "nothing to tap".
            var state = GameState.CreateNew();
            state.Quests.BeginInitialQuests(new System.Random(3));
            var onboarding = new OnboardingSequence(state, new System.Random(7));

            Assert.That(onboarding.CurrentStep, Is.EqualTo(OnboardingStep.Pan));
            onboarding.NotifyConversationOpened(onboarding.TargetDog);
            Assert.That(onboarding.CurrentStep, Is.EqualTo(OnboardingStep.Pan),
                "an early open must not skip the pan/zoom teaching steps");

            onboarding.NotifyPanned();
            onboarding.NotifyZoomed();

            Assert.That(onboarding.CurrentStep, Is.EqualTo(OnboardingStep.CompleteQuest),
                "arriving at TapBubble self-heals because the bubble was already opened");
        }

        [Test]
        public void AcceptingTheQuestEarlyDuringPanZoom_StillReachesDone_NotStranded()
        {
            // #329 core regression: opening AND accepting the seeded lost-item
            // quest before the TapBubble step moves it out of Available, so the
            // bubble can never re-present. The flow must self-heal past
            // TapBubble and still reach Done rather than pinning the coach bar.
            var state = GameState.CreateNew();
            state.Quests.BeginInitialQuests(new System.Random(3));
            var onboarding = new OnboardingSequence(state, new System.Random(7));
            var target = onboarding.TargetDog;

            // Open and accept the quest during the very first (Pan) step.
            onboarding.NotifyConversationOpened(target);
            var quest = state.Quests.ActiveQuests.Single(q => q.DogName == target.Name);
            state.Quests.Accept(quest);
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Accepted));

            onboarding.NotifyPanned();
            onboarding.NotifyZoomed();
            Assert.That(onboarding.CurrentStep, Is.EqualTo(OnboardingStep.CompleteQuest),
                "TapBubble auto-advances when the quest already left Available");

            // Finish the quest through the standard lost-item path.
            state.Quests.TapWorldPosition(quest.HiddenItemPosition.Value);
            onboarding.NotifyQuestCompleted(quest);

            Assert.That(onboarding.CurrentStep, Is.EqualTo(OnboardingStep.Done));
            Assert.That(state.OnboardingComplete, Is.True);
        }

        [Test]
        public void ReconcileOnPoll_SelfHealsTapBubble_WhenTheQuestAlreadyLeftAvailable()
        {
            // #329: even without an explicit early NotifyConversationOpened,
            // a poll must self-heal if the target dog's quest is no longer
            // Available (nothing left for the bubble to re-present).
            var state = GameState.CreateNew();
            state.Quests.BeginInitialQuests(new System.Random(3));
            var onboarding = new OnboardingSequence(state, new System.Random(7));
            var target = onboarding.TargetDog;

            onboarding.NotifyPanned();
            onboarding.NotifyZoomed();

            // The quest leaves Available by some other path; a fresh sequence
            // sitting on TapBubble must reconcile rather than strand.
            var quest = state.Quests.ActiveQuests.Single(q => q.DogName == target.Name);
            state.Quests.Accept(quest);

            var poller = new OnboardingSequence(state, new System.Random(7));
            poller.NotifyPanned();
            poller.NotifyZoomed();
            Assert.That(poller.CurrentStep, Is.EqualTo(OnboardingStep.CompleteQuest),
                "entering TapBubble with no Available quest self-heals via reconcile");

            poller.Reconcile();
            Assert.That(poller.CurrentStep, Is.EqualTo(OnboardingStep.CompleteQuest),
                "reconcile is idempotent while the quest is still being worked");
        }

        [Test]
        public void ReconcileCascades_ToDone_WhenTheQuestIsAlreadyResolved()
        {
            // #329: if the whole interaction (open + accept + complete) already
            // happened before TapBubble, reconcile cascades through
            // CompleteQuest to Done in one poll rather than stranding.
            var state = GameState.CreateNew();
            state.Quests.BeginInitialQuests(new System.Random(3));
            var onboarding = new OnboardingSequence(state, new System.Random(7));
            var target = onboarding.TargetDog;

            var quest = state.Quests.ActiveQuests.Single(q => q.DogName == target.Name);
            onboarding.NotifyConversationOpened(target);
            state.Quests.Accept(quest);
            state.Quests.TapWorldPosition(quest.HiddenItemPosition.Value);
            Assert.That(target.HasActiveQuest, Is.False, "quest fully resolved early");

            onboarding.NotifyPanned();
            onboarding.NotifyZoomed();

            Assert.That(onboarding.CurrentStep, Is.EqualTo(OnboardingStep.Done),
                "an already-resolved quest cascades TapBubble -> CompleteQuest -> Done");
            Assert.That(state.OnboardingComplete, Is.True);
        }

        [Test]
        public void ShouldSuppressBubble_HidesTheTargetDogsBubble_OnlyBeforeTapBubble()
        {
            // #329: the target dog's speech bubble is gated to the TapBubble
            // step — suppressed during Pan/Zoom, tappable from TapBubble on.
            var state = GameState.CreateNew();
            state.Quests.BeginInitialQuests(new System.Random(3));
            var onboarding = new OnboardingSequence(state, new System.Random(7));
            var target = onboarding.TargetDog;
            var other = state.Dogs.First(d => d != target);

            Assert.That(onboarding.ShouldSuppressBubble(target), Is.True, "Pan: suppressed");
            Assert.That(onboarding.ShouldSuppressBubble(other), Is.False, "non-target never suppressed");

            onboarding.NotifyPanned();
            Assert.That(onboarding.ShouldSuppressBubble(target), Is.True, "Zoom: still suppressed");

            onboarding.NotifyZoomed();
            Assert.That(onboarding.ShouldSuppressBubble(target), Is.False,
                "TapBubble: the bubble becomes tappable for the first time");
        }
    }
}
