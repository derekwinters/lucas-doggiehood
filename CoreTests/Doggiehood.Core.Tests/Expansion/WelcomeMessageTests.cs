using System.Collections.Generic;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Expansion;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Expansion
{
    /// <summary>
    /// #518: the pure message composer behind the "Welcome to the
    /// neighborhood!" pop-up (docs/specs/ui/welcome-popup.md). Given a
    /// moved-in household it produces the name line, the one dynamic meta
    /// line, and whether the member-chip row shows — per the wireframe's
    /// "Household variants" table (single / parent+puppy / three-dog). No
    /// engine dependency: strings only, composed from the roster Core already
    /// holds.
    /// </summary>
    public class WelcomeMessageTests
    {
        private const int HouseId = 7;

        private static Dog Adult(string name, Breed breed) =>
            new Dog(name, breed, Personality.Brave, HouseId, isPuppy: false);

        private static Dog Puppy(string name, Breed breed) =>
            new Dog(name, breed, Personality.Excited, HouseId, isPuppy: true);

        [Test]
        public void SingleDogHousehold_NamesTheDog_AndHidesTheMemberChipRow()
        {
            var message = WelcomeMessage.ForHousehold(new List<Dog>
            {
                Adult("Waffles", Breed.FrenchBulldog),
            });

            Assert.That(message.NameLine, Is.EqualTo("Waffles"));
            Assert.That(message.MetaLine, Is.EqualTo("French Bulldog · moved in next door"));
            Assert.That(message.ShowsMemberChips, Is.False,
                "a lone dog needs no chip echoing the portrait above it");
        }

        [Test]
        public void ParentAndPuppyHousehold_ReadsBothNames_SharedBreedFamilyOfTwo_TwoChips()
        {
            var household = new List<Dog>
            {
                Adult("Biscuit", Breed.FrenchBulldog),
                Puppy("Pepper", Breed.FrenchBulldog),
            };

            var message = WelcomeMessage.ForHousehold(household);

            Assert.That(message.NameLine, Is.EqualTo("Biscuit & Pepper"));
            Assert.That(message.MetaLine, Is.EqualTo("French Bulldog family of 2"));
            Assert.That(message.ShowsMemberChips, Is.True);
            Assert.That(message.MemberNames, Is.EqualTo(new[] { "Biscuit", "Pepper" }));
        }

        [Test]
        public void ThreeDogHousehold_OnePopup_MovedInThreeDogs_ThreeChips()
        {
            var household = new List<Dog>
            {
                Adult("Mochi", Breed.Beagle),
                Adult("Nori", Breed.Labrador),
                Puppy("Yuzu", Breed.Chihuahua),
            };

            var message = WelcomeMessage.ForHousehold(household);

            Assert.That(message.NameLine, Is.EqualTo("Mochi, Nori & Yuzu"));
            Assert.That(message.MetaLine, Is.EqualTo("moved in — 3 dogs"));
            Assert.That(message.ShowsMemberChips, Is.True);
            Assert.That(message.MemberNames, Is.EqualTo(new[] { "Mochi", "Nori", "Yuzu" }));
        }
    }
}
