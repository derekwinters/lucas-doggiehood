using Doggiehood.Core.Dogs;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Dogs
{
    public class DogStateMachineTests
    {
        private static Dog StreetDog()
        {
            return new Dog("Testo", Breed.Beagle, Personality.Brave, 1, false);
        }

        [Test]
        public void NewStreetDog_StartsInIdleWander()
        {
            Assert.That(StreetDog().State, Is.EqualTo(DogState.IdleWander));
            Assert.That(StreetDog().Location, Is.EqualTo(DogLocation.Street));
        }

        [Test]
        public void SupportsAllFourPoseStates()
        {
            // #66: {IdleWander, Rest, Sit, WindowWatch}.
            Assert.That(System.Enum.GetNames(typeof(DogState)),
                Is.EquivalentTo(new[] { "IdleWander", "Rest", "Sit", "WindowWatch" }));
        }

        [Test]
        public void Rest_RequiresAComfortDecorationSelectedForUse()
        {
            // #66/#52: Rest only with a comfort decoration present + selected.
            var dog = StreetDog();

            Assert.That(dog.TryRest(comfortDecorationSelected: false), Is.False);
            Assert.That(dog.State, Is.EqualTo(DogState.IdleWander));

            Assert.That(dog.TryRest(comfortDecorationSelected: true), Is.True);
            Assert.That(dog.State, Is.EqualTo(DogState.Rest));
        }

        [Test]
        public void Sit_RequiresAnAcceptedBuyQuestAndBeingHome()
        {
            // #66/#30: Sit only after a "buy me X" quest is accepted and the
            // dog has arrived home to wait for the delivery truck.
            var dog = StreetDog();

            Assert.That(dog.TrySit(buyQuestAccepted: false, isAtHome: true), Is.False);
            Assert.That(dog.TrySit(buyQuestAccepted: true, isAtHome: false), Is.False);
            Assert.That(dog.State, Is.EqualTo(DogState.IdleWander));

            Assert.That(dog.TrySit(buyQuestAccepted: true, isAtHome: true), Is.True);
            Assert.That(dog.State, Is.EqualTo(DogState.Sit));
        }

        [Test]
        public void WindowWatch_OccursExactlyWhenPlacedInsideAtWindow()
        {
            // #66/#9: WindowWatch only when Location = InsideAtWindow.
            var dog = StreetDog();

            dog.PlaceInsideAtWindow();

            Assert.That(dog.Location, Is.EqualTo(DogLocation.InsideAtWindow));
            Assert.That(dog.State, Is.EqualTo(DogState.WindowWatch));

            dog.PlaceOnStreet();

            Assert.That(dog.Location, Is.EqualTo(DogLocation.Street));
            Assert.That(dog.State, Is.EqualTo(DogState.IdleWander));
        }

        [Test]
        public void WindowDog_ProducesNoWanderTargets()
        {
            // #9: a window dog does not wander.
            var dog = StreetDog();
            dog.PlaceInsideAtWindow();

            var wander = new WanderBehavior(seed: 42, MovementProfile.ForPersonality(dog.Personality));

            Assert.That(dog.WantsToWander, Is.False);
            Assert.That(wander.NextTarget(Doggiehood.Core.World.NeighborhoodLayout.Intersection), Is.Not.Null);
        }

        [Test]
        public void DeliveringDog_DoesNotWander_UntilPlacedBackOnStreet()
        {
            // #470: while a buy-gift delivery is in flight (DeliveryPhase
            // HeadingHome/WaitingForDelivery) the QuestDirector owns the dog's
            // transform as it walks home. WantsToWander must be gated OFF for
            // that whole leg so the wander branch and WalkDogHome can never
            // both drive the same transform in one frame (the moonwalk race).
            var dog = StreetDog();
            Assert.That(dog.WantsToWander, Is.True, "a plain street dog wanders");

            dog.BeginDelivery();
            Assert.That(dog.WantsToWander, Is.False,
                "a dog mid-delivery must not wander — the quest walk owns it");

            // The Sit-at-home leg (WaitingForDelivery) is also mid-delivery.
            dog.TrySit(buyQuestAccepted: true, isAtHome: true);
            Assert.That(dog.WantsToWander, Is.False);

            // Delivery finishes and the dog is handed back to the street.
            dog.PlaceOnStreet();
            Assert.That(dog.WantsToWander, Is.True,
                "once delivery hands control back the dog wanders again");
        }

        [Test]
        public void PlaceOnStreet_AfterDelivery_SignalsAFreshWanderTarget()
        {
            // #470: the Unity layer caches a wander target; when the scripted
            // walk hands control back it must pick a FRESH one, not beeline to
            // the stale pre-quest target. Dog exposes a monotonic token the
            // view watches — every hand-back to the street bumps it.
            var dog = StreetDog();
            var before = dog.WanderResetToken;

            dog.BeginDelivery();
            dog.PlaceOnStreet();

            Assert.That(dog.WanderResetToken, Is.GreaterThan(before),
                "handing the dog back to the street must signal a fresh wander target");
        }

        [Test]
        public void WindowDog_IsStillFullyInteractable()
        {
            // #9: window dogs talk exactly like street dogs.
            var dog = StreetDog();
            dog.PlaceInsideAtWindow();
            dog.GiveQuest();

            Assert.That(dog.HasActiveQuest, Is.True);
            Assert.That(ConversationStarter.TryOpen(dog), Is.Not.Null);
        }
    }
}
