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
