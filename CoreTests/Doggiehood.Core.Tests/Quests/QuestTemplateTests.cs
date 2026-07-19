using System;
using System.Collections.Generic;
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

            var lines = QuestTemplates.For(QuestType.BuyGift).Render(dog, "ball", new Random(1));

            Assert.That(lines, Is.Not.Empty);
            Assert.That(string.Join("\n", lines), Does.Contain("ball"));
            Assert.That(string.Join("\n", lines), Does.Not.Contain("{dog}"));
            Assert.That(string.Join("\n", lines), Does.Not.Contain("{item}"));
        }

        [Test]
        public void Rendering_NeverThrowsOrProducesEmptyText_ForTheWholeRoster()
        {
            // #69: every (dog, personality, item) combo in the roster renders.
            var rng = new Random(11);
            foreach (var dog in DogRoster.CreateStartingDogs())
            {
                foreach (QuestType type in Enum.GetValues(typeof(QuestType)))
                {
                    var lines = QuestTemplates.For(type).Render(dog, "toy", rng);

                    Assert.That(lines, Is.Not.Empty, $"{dog.Name}/{type}");
                    Assert.That(lines.All(l => !string.IsNullOrWhiteSpace(l)), Is.True, $"{dog.Name}/{type}");
                }
            }
        }

        [TestCaseSource(nameof(AllQuestTypes))]
        public void EveryTemplate_HasNonEmptyDefaultOpenerAndCloserPools(QuestType type)
        {
            // #189 Model 2: default pools are the always-present base voice;
            // per-personality pools are optional seasoning (0+).
            var template = QuestTemplates.For(type);

            Assert.That(template.DefaultOpeners, Is.Not.Empty, $"{type} default openers");
            Assert.That(template.DefaultClosers, Is.Not.Empty, $"{type} default closers");
        }

        [TestCaseSource(nameof(AllQuestTypes))]
        public void Render_OpenerAndCloser_AlwaysComeFromTheDefaultUnionPersonalityPools(QuestType type)
        {
            var template = QuestTemplates.For(type);
            var rng = new Random(2024);

            foreach (var dog in DogRoster.CreateStartingDogs())
            {
                var openerCandidates = FilledCandidatePool(template.DefaultOpeners, template.FlavoredOpeners, dog, "toy");
                var closerCandidates = FilledCandidatePool(template.DefaultClosers, template.FlavoredClosers, dog, "toy");

                // Many draws per dog to make sure we're not just getting lucky.
                for (var i = 0; i < 50; i++)
                {
                    var lines = template.Render(dog, "toy", rng);

                    Assert.That(openerCandidates, Does.Contain(lines[0]),
                        $"{type}/{dog.Personality} opener not from candidate pool");
                    Assert.That(closerCandidates, Does.Contain(lines[1]),
                        $"{type}/{dog.Personality} closer not from candidate pool");
                }
            }
        }

        [Test]
        public void Render_IsDeterministicForASeed()
        {
            var dog = new Dog("Nala", Breed.GermanShepherd, Personality.Excited, 1, true);
            var template = QuestTemplates.For(QuestType.LostItem);

            var first = template.Render(dog, "toy", new Random(7));
            var second = template.Render(dog, "toy", new Random(7));

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Render_OpenerSelection_IsUniformAcrossDefaultAndPersonalityPools()
        {
            // #189: uniform per-string across default UNION personality, not
            // per-bucket — a personality-specific line and a default line
            // must be equally likely to be picked.
            var defaultOpeners = new List<string> { "Default A {dog}", "Default B {dog}" };
            var flavoredOpeners = new Dictionary<Personality, IReadOnlyList<string>>
            {
                { Personality.Grumpy, new List<string> { "Grumpy-only {dog}" } },
            };
            var template = new QuestTemplate(
                defaultOpeners,
                flavoredOpeners,
                new List<string> { "Closer {dog}" },
                new Dictionary<Personality, IReadOnlyList<string>>());

            var dog = new Dog("Pepper", Breed.Chihuahua, Personality.Grumpy, 3, false);
            var rng = new Random(1234);
            var counts = new Dictionary<string, int>
            {
                { "Default A Pepper", 0 },
                { "Default B Pepper", 0 },
                { "Grumpy-only Pepper", 0 },
            };
            const int trials = 6000;

            for (var i = 0; i < trials; i++)
            {
                var opener = template.Render(dog, "toy", rng)[0];
                counts[opener]++;
            }

            const double expectedShare = trials / 3.0;
            foreach (var count in counts.Values)
            {
                Assert.That(count, Is.InRange(expectedShare * 0.75, expectedShare * 1.25));
            }
        }

        [Test]
        public void NoPerDogStateIsStored_SameTemplateInstanceServesEveryDogIndependently()
        {
            // #189: pure random each fire, no anti-repeat memory, no
            // per-dog/session persisted state on the template itself.
            var template = QuestTemplates.For(QuestType.PestControl);
            var a = new Dog("Rex", Breed.GermanShepherd, Personality.Brave, 4, false);
            var b = new Dog("Milo", Breed.Puggle, Personality.Shy, 2, false);

            Assert.DoesNotThrow(() =>
            {
                template.Render(a, "bug spray", new Random(1));
                template.Render(b, "bug spray", new Random(2));
                template.Render(a, "bug spray", new Random(3));
            });
        }

        private static IEnumerable<QuestType> AllQuestTypes()
        {
            return Enum.GetValues(typeof(QuestType)).Cast<QuestType>();
        }

        private static HashSet<string> FilledCandidatePool(
            IReadOnlyList<string> defaults,
            IReadOnlyDictionary<Personality, IReadOnlyList<string>> flavored,
            Dog dog,
            string item)
        {
            var raw = new List<string>(defaults);
            if (flavored.TryGetValue(dog.Personality, out var personalityLines))
            {
                raw.AddRange(personalityLines);
            }

            return new HashSet<string>(raw.Select(t => t.Replace("{dog}", dog.Name).Replace("{item}", item)));
        }
    }
}
