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
            state.Quests.StartNewDay(new System.Random(1));
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
        public void CompletingOnboarding_BeginsTheFirstNormalRotation_ExactlyOnce()
        {
            // #312: onboarding-time suppression ends when onboarding completes
            // — the Done transition begins the first normal 2-4 rotation once,
            // handing off to #310's recurrence. Re-signalling never re-runs it.
            var state = GameState.CreateNew();
            state.Quests.BeginInitialQuests(new System.Random(3));
            var onboarding = new OnboardingSequence(state, new System.Random(7));
            onboarding.NotifyPanned();
            onboarding.NotifyZoomed();
            onboarding.NotifyConversationOpened(onboarding.TargetDog);

            var quest = state.Quests.ActiveQuests.First(q => q.DogName == onboarding.TargetDog.Name);
            state.Quests.Accept(quest);
            state.Quests.TapWorldPosition(quest.HiddenItemPosition.Value);
            onboarding.NotifyQuestCompleted(quest);

            Assert.That(onboarding.CurrentStep, Is.EqualTo(OnboardingStep.Done));
            var afterCompletion = state.Quests.ActiveQuests.Count();
            Assert.That(afterCompletion, Is.InRange(2, 4),
                "completing onboarding begins the first normal 2-4 rotation");

            // Idempotent: signalling completion again must not start a second rotation.
            onboarding.NotifyQuestCompleted(quest);
            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(afterCompletion),
                "the first rotation begins exactly once");
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
        public void CompletingTheRealQuest_FinishesOnboarding_WithTheStandardPayout()
        {
            // #44/#24: the reward is the normal quest payout, nothing special.
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
                Is.EqualTo(Doggiehood.Core.Economy.EconomyNumbers.QuestPayout - (quest.Cost ?? 0)));
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
    }
}
