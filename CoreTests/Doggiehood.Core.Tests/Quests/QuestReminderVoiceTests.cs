using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Quests
{
    /// <summary>
    /// #708: the active-quest reminder (#472) was quest-agnostic — every
    /// accepted quest got the same nagging "have you got it yet?" voice and the
    /// same "Still looking" dismiss pill, including the two purchase types
    /// whose accept IS the purchase (the coins are spent and a truck is already
    /// driving the item over). These tests pin the invariant: a reminder never
    /// asks the player for something they have already done — the line and the
    /// dismiss label both follow one Core-side reading of who owes the next
    /// action, selected through the same subject-aware seam as the rest of the
    /// dialogue (#701).
    /// </summary>
    public class QuestReminderVoiceTests
    {
        /// <summary>Wordings that ask the player for progress they cannot make
        /// once the item is bought and in flight.</summary>
        private static readonly string[] PlayerProgressAsks =
        {
            "any luck",
            "any word",
            "any news",
            "any progress",
            "any chance",
            "did you manage",
            "still thinking",
            "buy itself",
            "still need",
            "where's my",
            "getting me",
            "yet?",
        };

        /// <summary>Wordings that acknowledge the purchase is made and the
        /// delivery is on its way.</summary>
        private static readonly string[] DeliveryAcknowledgements =
        {
            "on its way",
            "on the way",
            "coming",
            "truck",
            "delivery",
            "ordered",
            "paid for",
            "en route",
        };

        [TestCase(QuestType.BuyGift)]
        [TestCase(QuestType.DecorationRequest)]
        public void PurchaseReminders_AcknowledgeTheDelivery_AndNeverAskThePlayerForTheItem(QuestType type)
        {
            // The purchase types deduct the cost at accept and dispatch a truck,
            // and reminders only ever render post-accept — so every line in the
            // pool (default plus all six personality pools) must read as "it's
            // bought and coming", never as "have you got it yet?".
            var template = QuestTemplates.For(type);

            foreach (var line in AllReminderLines(template))
            {
                var lowered = line.ToLowerInvariant();

                foreach (var ask in PlayerProgressAsks)
                {
                    Assert.That(lowered, Does.Not.Contain(ask),
                        $"{type} reminder still asks the player for a purchase they made: {line}");
                }

                Assert.That(DeliveryAcknowledgements.Any(lowered.Contains), Is.True,
                    $"{type} reminder does not acknowledge the delivery is coming: {line}");
            }
        }

        [TestCase(QuestType.LostItem)]
        [TestCase(QuestType.PestControl)]
        public void PlayerOwedReminders_StillReadAsOutstandingPlayerWork(QuestType type)
        {
            // Regression guard: the fix must not flatten all four types into one
            // voice. Lost-item and pest-control quests really are waiting on the
            // player, so their pools keep asking — and must never pick up the
            // purchase types' "it's already on its way" acknowledgement.
            var template = QuestTemplates.For(type);

            foreach (var line in AllReminderLines(template))
            {
                var lowered = line.ToLowerInvariant();

                foreach (var acknowledgement in DeliveryAcknowledgements)
                {
                    Assert.That(lowered, Does.Not.Contain(acknowledgement),
                        $"{type} reminder wrongly acknowledges a delivery: {line}");
                }
            }

            Assert.That(AllReminderLines(template).Any(l =>
                    PlayerProgressAsks.Any(l.ToLowerInvariant().Contains)),
                Is.True,
                $"{type} reminders should still ask the player how the hunt is going");
        }

        [Test]
        public void LostItemAndPestControlPools_AreUnchanged()
        {
            // Spot-pin the exact pre-#708 lines so a future rewrite of the
            // purchase voice cannot quietly take these with it.
            Assert.That(QuestTemplates.For(QuestType.LostItem).DefaultReminders,
                Does.Contain("{dog} looks up expectantly. \"Any sign of my {item} yet?\""));
            Assert.That(QuestTemplates.For(QuestType.PestControl).DefaultReminders,
                Does.Contain("{dog} scratches nervously. \"Are those bugs still crawling around my house?\""));
        }

        [TestCase(QuestType.LostItem, PendingActionOwner.Player)]
        [TestCase(QuestType.PestControl, PendingActionOwner.Player)]
        [TestCase(QuestType.BuyGift, PendingActionOwner.Game)]
        [TestCase(QuestType.DecorationRequest, PendingActionOwner.Game)]
        public void WhoOwesTheNextAction_IsASingleCoreSideProperty(QuestType type, PendingActionOwner expected)
        {
            // One source of truth, carried by the same template the reminder
            // pool comes from — never a switch on QuestType in the presenter.
            Assert.That(QuestTemplates.For(type).ReminderOwner, Is.EqualTo(expected));
        }

        [Test]
        public void TheDismissLabel_FollowsWhoOwesTheNextAction()
        {
            Assert.That(QuestTemplates.For(QuestType.LostItem).ReminderDismissLabel,
                Is.EqualTo(QuestTemplate.PlayerOwedDismissLabel));
            Assert.That(QuestTemplates.For(QuestType.PestControl).ReminderDismissLabel,
                Is.EqualTo(QuestTemplate.PlayerOwedDismissLabel));
            Assert.That(QuestTemplates.For(QuestType.BuyGift).ReminderDismissLabel,
                Is.EqualTo(QuestTemplate.GameOwedDismissLabel));
            Assert.That(QuestTemplates.For(QuestType.DecorationRequest).ReminderDismissLabel,
                Is.EqualTo(QuestTemplate.GameOwedDismissLabel));

            Assert.That(QuestTemplate.PlayerOwedDismissLabel, Is.EqualTo("Still looking"));
            Assert.That(QuestTemplate.GameOwedDismissLabel, Is.EqualTo("On its way"));
        }

        [Test]
        public void TheFenceSubject_KeepsItsOwnPlayerOwedVoice()
        {
            // #701/#318: the fence is the one Gift subject with no delivery leg
            // — it completes at accept, so it never sits Accepted and never
            // reaches a reminder. It must not inherit the generic BuyGift
            // "your delivery is on its way" acknowledgement.
            var fence = QuestTemplates.For(QuestType.BuyGift, ItemCatalog.FenceItemName);

            Assert.That(fence.ReminderOwner, Is.EqualTo(PendingActionOwner.Player));
            Assert.That(fence.ReminderDismissLabel, Is.EqualTo(QuestTemplate.PlayerOwedDismissLabel));
        }

        [TestCase(QuestType.BuyGift)]
        [TestCase(QuestType.DecorationRequest)]
        public void PurchaseReminders_FillTheDogAndItemSlots_ForEveryPersonality(QuestType type)
        {
            var template = QuestTemplates.For(type);
            var rng = new Random(708);

            foreach (var dog in EveryPersonality())
            {
                for (var i = 0; i < 40; i++)
                {
                    var line = template.RenderReminder(dog, "cushion", rng);

                    Assert.That(line, Does.Contain(dog.Name));
                    Assert.That(line, Does.Contain("cushion"));
                    Assert.That(line, Does.Not.Contain("{dog}").And.Not.Contain("{item}"));
                }
            }
        }

        [Test]
        public void AnAcceptedDecorationRequest_RemindsAboutTheItemChosenAtAccept()
        {
            var state = PlayableState();
            var dog = state.Dogs[0];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.DecorationRequest, new Random(5));
            var chosen = quest.Options[0];
            Assert.That(state.Quests.AcceptWithChoice(quest, chosen), Is.True,
                "precondition: the decoration request accepts with a chosen option");

            var line = QuestTemplates.For(quest.Type, quest.ItemName)
                .RenderReminder(dog, quest.ItemName, new Random(9));

            Assert.That(line, Does.Contain(chosen), "the reminder names the item the player picked");
            Assert.That(DeliveryAcknowledgements.Any(line.ToLowerInvariant().Contains), Is.True,
                $"the reminder acknowledges the chosen item is coming: {line}");
        }

        [Test]
        public void AnAcceptedBuyGiftQuest_StillReadsAsAlreadyBought_AfterARelaunch()
        {
            // #704: quests survive a relaunch. The reminder is derived from the
            // restored quest's type and subject, so a gift bought before the
            // relaunch must still acknowledge the purchase afterwards — never
            // fall back to asking the player to buy it again.
            var state = PlayableState();
            var dog = state.Dogs[0];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new Random(DeliveredGiftSeed()));
            Assert.That(state.Quests.Accept(quest), Is.True, "precondition: the gift quest accepts");

            var reloaded = SaveCodec.Load(SaveCodec.Save(state));
            var restored = reloaded.Quests.ActiveQuests.First(q => q.Id == quest.Id);
            var restoredDog = reloaded.Dogs.First(d => d.Name == restored.DogName);

            Assert.That(restored.Status, Is.EqualTo(QuestStatus.Accepted));

            var template = QuestTemplates.For(restored.Type, restored.ItemName);
            Assert.That(template.ReminderOwner, Is.EqualTo(PendingActionOwner.Game),
                "a restored, already-paid gift is still the game's to finish");
            Assert.That(template.ReminderDismissLabel, Is.EqualTo(QuestTemplate.GameOwedDismissLabel));

            var line = template.RenderReminder(restoredDog, restored.ItemName, new Random(3));
            Assert.That(DeliveryAcknowledgements.Any(line.ToLowerInvariant().Contains), Is.True,
                $"the restored quest's reminder still acknowledges the delivery: {line}");
        }

        private static GameState PlayableState()
        {
            var state = GameState.CreateNew();
            state.MarkOnboardingComplete();
            state.Wallet.Deposit(500);
            return state;
        }

        /// <summary>The fence is the one Gift subject with no delivery leg
        /// (#318), so probe throwaway states for a seed that rolls a subject a
        /// truck actually delivers.</summary>
        private static int DeliveredGiftSeed()
        {
            for (var seed = 1; seed < 50; seed++)
            {
                var probe = GameState.CreateNew();
                var rolled = probe.Quests.GiveQuestTo(probe.Dogs[0], QuestType.BuyGift, new Random(seed));
                if (rolled.ItemName != ItemCatalog.FenceItemName)
                {
                    return seed;
                }
            }

            throw new InvalidOperationException("no delivered-gift subject rolled");
        }

        private static IEnumerable<string> AllReminderLines(QuestTemplate template)
        {
            foreach (var line in template.DefaultReminders)
            {
                yield return line;
            }

            foreach (var pool in template.FlavoredReminders.Values)
            {
                foreach (var line in pool)
                {
                    yield return line;
                }
            }
        }

        private static IEnumerable<Dog> EveryPersonality()
        {
            var houseId = 1;
            foreach (Personality personality in Enum.GetValues(typeof(Personality)))
            {
                yield return new Dog($"Test-{personality}", Breed.GermanShepherd, personality, houseId++, false);
            }
        }
    }
}
