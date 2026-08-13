using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Economy;
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
        public void RenderReminder_FillsDogNameAndItemSlots()
        {
            // #472: re-tapping an active quest renders a contextual reminder
            // line — same {dog}/{item} substitution as the opener/closer.
            var dog = new Dog("Pepper", Breed.Chihuahua, Personality.Grumpy, 3, false);

            var line = QuestTemplates.For(QuestType.LostItem).RenderReminder(dog, "ball", new Random(1));

            Assert.That(line, Is.Not.Null.And.Not.Empty);
            Assert.That(line, Does.Contain("Pepper"));
            Assert.That(line, Does.Contain("ball"));
            Assert.That(line, Does.Not.Contain("{dog}"));
            Assert.That(line, Does.Not.Contain("{item}"));
        }

        [TestCaseSource(nameof(AllQuestTypes))]
        public void EveryTemplate_HasNonEmptyDefaultReminderPool(QuestType type)
        {
            // #472: every quest type can be re-tapped while active, so every
            // template must carry a reminder pool with the same Model 2 shape.
            var template = QuestTemplates.For(type);

            Assert.That(template.DefaultReminders, Is.Not.Empty, $"{type} default reminders");
        }

        [TestCaseSource(nameof(AllQuestTypes))]
        public void RenderReminder_AlwaysComesFromTheDefaultUnionPersonalityPools(QuestType type)
        {
            var template = QuestTemplates.For(type);
            var rng = new Random(2024);

            foreach (var dog in DogRoster.CreateStartingDogs())
            {
                var candidates = FilledCandidatePool(template.DefaultReminders, template.FlavoredReminders, dog, "toy");

                for (var i = 0; i < 50; i++)
                {
                    var line = template.RenderReminder(dog, "toy", rng);

                    Assert.That(candidates, Does.Contain(line),
                        $"{type}/{dog.Personality} reminder not from candidate pool");
                }
            }
        }

        [Test]
        public void RenderReminder_IsDeterministicForASeed()
        {
            var dog = new Dog("Nala", Breed.GermanShepherd, Personality.Excited, 1, true);
            var template = QuestTemplates.For(QuestType.LostItem);

            var first = template.RenderReminder(dog, "toy", new Random(7));
            var second = template.RenderReminder(dog, "toy", new Random(7));

            Assert.That(second, Is.EqualTo(first));
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
                new Dictionary<Personality, IReadOnlyList<string>>(),
                new List<string> { "Reminder {dog}" },
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

        [Test]
        public void FenceSubject_DrawsItsOwnPools_PromisingNoDeliveryAndNoPortableGift()
        {
            // #701: the fence's "Buy something" quest has no delivery leg
            // (#318), so selecting its dialogue by QuestType alone promised a
            // truck that never comes and framed a lot fixture as a portable
            // gift. The subject-aware seam gives it its own pools; no line of
            // any kind (opener, closer, reminder) may carry the delivery or
            // portable-gift framing, for any personality.
            var fence = QuestTemplates.For(QuestType.BuyGift, ItemCatalog.FenceItemName);

            Assert.That(fence, Is.Not.SameAs(QuestTemplates.For(QuestType.BuyGift)),
                "the fence subject draws its own pools, not the generic BuyGift pools");

            var rng = new Random(701);
            foreach (var dog in FenceRoster())
            {
                for (var i = 0; i < 50; i++)
                {
                    var text = string.Join("\n", fence.Render(dog, ItemCatalog.FenceItemName, rng))
                        + "\n" + fence.RenderReminder(dog, ItemCatalog.FenceItemName, rng);
                    var lowered = text.ToLowerInvariant();

                    foreach (var phrase in ForbiddenFencePhrases)
                    {
                        Assert.That(lowered, Does.Not.Contain(phrase),
                            $"{dog.Personality} fence line promised \"{phrase}\": {text}");
                    }

                    Assert.That(text, Does.Not.Contain("{dog}").And.Not.Contain("{item}"));
                }
            }
        }

        [Test]
        public void EveryNonFenceSubject_StillDrawsTheUnchangedTypeDefaultPools()
        {
            // #701 regression: the fence seam is subject-scoped — every other
            // Gift subject (and every other quest type, whatever the subject)
            // keeps rendering from exactly the pools it did before.
            var rng = new Random(2025);
            foreach (QuestType type in Enum.GetValues(typeof(QuestType)))
            {
                var template = QuestTemplates.For(type);
                foreach (var item in SubjectsOtherThanTheGiftFence(type))
                {
                    Assert.That(QuestTemplates.For(type, item), Is.SameAs(template),
                        $"{type}/{item} should still use the type's default template");

                    foreach (var dog in FenceRoster())
                    {
                        var openers = FilledCandidatePool(template.DefaultOpeners, template.FlavoredOpeners, dog, item);
                        var closers = FilledCandidatePool(template.DefaultClosers, template.FlavoredClosers, dog, item);

                        for (var i = 0; i < 20; i++)
                        {
                            var lines = QuestTemplates.For(type, item).Render(dog, item, rng);

                            Assert.That(openers, Does.Contain(lines[0]), $"{type}/{item}/{dog.Personality} opener");
                            Assert.That(closers, Does.Contain(lines[1]), $"{type}/{item}/{dog.Personality} closer");
                        }
                    }
                }
            }
        }

        [Test]
        public void FenceTemplate_CarriesTheSamePoolShapeAsEveryOtherTemplate()
        {
            // #189 Model 2 / #472: the fence pools are a template like any
            // other — non-empty opener, closer and reminder defaults.
            var fence = QuestTemplates.For(QuestType.BuyGift, ItemCatalog.FenceItemName);

            Assert.That(fence.DefaultOpeners, Is.Not.Empty, "fence default openers");
            Assert.That(fence.DefaultClosers, Is.Not.Empty, "fence default closers");
            Assert.That(fence.DefaultReminders, Is.Not.Empty, "fence default reminders");
        }

        /// <summary>#701: framings the fence dialogue must never use — the
        /// delivery truck and walk-home promise it does not run, and the
        /// portable-handed-over-gift wording for what is a lot fixture.</summary>
        private static readonly string[] ForbiddenFencePhrases =
        {
            "delivery truck",
            "head home",
            "next adventure",
            "training's better",
        };

        private static IEnumerable<Dog> FenceRoster()
        {
            var houseId = 1;
            foreach (Personality personality in Enum.GetValues(typeof(Personality)))
            {
                yield return new Dog($"Test-{personality}", Breed.GermanShepherd, personality, houseId++, false);
            }
        }

        private static IEnumerable<string> SubjectsOtherThanTheGiftFence(QuestType type)
        {
            foreach (var item in ItemCatalog.Items)
            {
                if (type == QuestType.BuyGift && item.Name == ItemCatalog.FenceItemName)
                {
                    continue;
                }

                yield return item.Name;
            }
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
