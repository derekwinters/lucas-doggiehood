using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Quests;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Quests
{
    public class QuestTemplateTests
    {
        [Test]
        public void AllFourTemplates_ExistAsTemplateInstances()
        {
            // #69: 3 MVP quest types + the decoration request are templates,
            // never hard-coded strings.
            Assert.That(QuestTemplates.For(QuestType.LostItem), Is.InstanceOf<QuestTemplate>());
            Assert.That(QuestTemplates.For(QuestType.BuyGift), Is.InstanceOf<QuestTemplate>());
            Assert.That(QuestTemplates.For(QuestType.PestControl), Is.InstanceOf<QuestTemplate>());
            Assert.That(QuestTemplates.For(QuestType.DecorationRequest), Is.InstanceOf<QuestTemplate>());
        }

        [Test]
        public void Rendering_FillsDogNameAndItemSlots()
        {
            var dog = new Dog("Pepper", Breed.Chihuahua, Personality.Grumpy, 3, false);

            var lines = QuestTemplates.For(QuestType.BuyGift).Render(dog, "ball");

            Assert.That(lines, Is.Not.Empty);
            Assert.That(string.Join("\n", lines), Does.Contain("ball"));
            Assert.That(string.Join("\n", lines), Does.Not.Contain("{dog}"));
            Assert.That(string.Join("\n", lines), Does.Not.Contain("{item}"));
        }

        [Test]
        public void Rendering_SelectsThePersonalityFlavoredVariant()
        {
            // #69: a Grumpy dog's line reads differently than an Excited one's.
            var grumpy = new Dog("Pepper", Breed.Chihuahua, Personality.Grumpy, 3, false);
            var excited = new Dog("Nala", Breed.GermanShepherd, Personality.Excited, 1, true);

            var template = QuestTemplates.For(QuestType.LostItem);

            Assert.That(template.Render(grumpy, "toy"), Is.Not.EqualTo(template.Render(excited, "toy")));
        }

        [Test]
        public void Rendering_NeverThrowsOrProducesEmptyText_ForTheWholeRoster()
        {
            // #69: every (dog, personality, item) combo in the roster renders.
            foreach (var dog in DogRoster.CreateStartingDogs())
            {
                foreach (QuestType type in System.Enum.GetValues(typeof(QuestType)))
                {
                    var lines = QuestTemplates.For(type).Render(dog, "toy");

                    Assert.That(lines, Is.Not.Empty, $"{dog.Name}/{type}");
                    Assert.That(lines.All(l => !string.IsNullOrWhiteSpace(l)), Is.True, $"{dog.Name}/{type}");
                }
            }
        }
    }
}
