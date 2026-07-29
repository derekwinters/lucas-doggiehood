using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Quests;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Quests
{
    /// <summary>
    /// #317: population-driven quest cost tiers. As the neighborhood's dog
    /// population grows, higher-cost quest tiers become eligible — new cost
    /// tiers within the existing 3 quest types, not new mechanics. Pure,
    /// deterministic Core logic driven only by <c>Dogs.Count</c>; no RNG,
    /// no persisted state.
    /// </summary>
    public class QuestCostTiersTests
    {
        [Test]
        public void EligibleCostTiers_AtPopulationOne_YieldsStarterOnly()
        {
            Assert.That(QuestCostTiers.EligibleCostTiers(1),
                Is.EquivalentTo(new[] { QuestCostTier.Starter }));
        }

        [Test]
        public void EligibleCostTiers_AtPopulationFive_YieldsStarterAndMid()
        {
            Assert.That(QuestCostTiers.EligibleCostTiers(5),
                Is.EquivalentTo(new[] { QuestCostTier.Starter, QuestCostTier.Mid }));
        }

        [Test]
        public void EligibleCostTiers_AtPopulationTen_YieldsAllThreeTiers()
        {
            Assert.That(QuestCostTiers.EligibleCostTiers(10),
                Is.EquivalentTo(new[]
                {
                    QuestCostTier.Starter, QuestCostTier.Mid, QuestCostTier.Premium,
                }));
        }

        [Test]
        public void EligibleCostTiers_JustBelowEachGate_DoesNotIncludeThatTier()
        {
            Assert.That(QuestCostTiers.EligibleCostTiers(4),
                Is.EquivalentTo(new[] { QuestCostTier.Starter }));
            Assert.That(QuestCostTiers.EligibleCostTiers(9),
                Is.EquivalentTo(new[] { QuestCostTier.Starter, QuestCostTier.Mid }));
        }

        [Test]
        public void EligibleCostTiers_IsMonotonic_AcrossGrowingPopulation()
        {
            // #317: reaching a higher population only ever ADDS tiers — an
            // established player never loses access to cheaper tiers.
            var previous = new HashSet<QuestCostTier>();
            for (var dogCount = 0; dogCount <= 30; dogCount++)
            {
                var current = new HashSet<QuestCostTier>(QuestCostTiers.EligibleCostTiers(dogCount));
                Assert.That(current.IsSupersetOf(previous), Is.True,
                    $"population {dogCount} dropped a previously-eligible tier");
                previous = current;
            }
        }

        [Test]
        public void EligibleCostCeiling_RisesWithPopulation()
        {
            Assert.That(QuestCostTiers.EligibleCostCeiling(1),
                Is.EqualTo(QuestCostTiers.StarterMaxCost));
            Assert.That(QuestCostTiers.EligibleCostCeiling(5),
                Is.EqualTo(QuestCostTiers.MidMaxCost));
            Assert.That(QuestCostTiers.EligibleCostCeiling(10),
                Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void EligibleNames_AtMinimumPopulation_ExcludesEntriesAboveStarterCeiling()
        {
            // #317: entries whose Cost is above the population-gated ceiling
            // are excluded from the candidate set. Uses a synthetic catalog
            // with mid/premium-priced gift entries the real catalog does not
            // yet carry, so the exclusion is provable independent of content.
            var catalog = new[]
            {
                new CatalogItem("cheap", ItemEligibility.Gift, 30),
                new CatalogItem("starter-ceiling", ItemEligibility.Gift, 50),
                new CatalogItem("mid", ItemEligibility.Gift, 75),
                new CatalogItem("premium", ItemEligibility.Gift, 120),
            };

            var names = QuestCostTiers.EligibleNames(catalog, ItemEligibility.Gift, dogCount: 1);

            Assert.That(names, Is.EquivalentTo(new[] { "cheap", "starter-ceiling" }));
        }

        [Test]
        public void EligibleNames_AtMidPopulation_AddsMidBandButNotPremium()
        {
            var catalog = new[]
            {
                new CatalogItem("cheap", ItemEligibility.Gift, 30),
                new CatalogItem("mid", ItemEligibility.Gift, 75),
                new CatalogItem("premium", ItemEligibility.Gift, 120),
            };

            var names = QuestCostTiers.EligibleNames(catalog, ItemEligibility.Gift, dogCount: 5);

            Assert.That(names, Is.EquivalentTo(new[] { "cheap", "mid" }));
        }

        [Test]
        public void EligibleNames_AtPremiumPopulation_IncludesEveryBand()
        {
            var catalog = new[]
            {
                new CatalogItem("cheap", ItemEligibility.Gift, 30),
                new CatalogItem("mid", ItemEligibility.Gift, 75),
                new CatalogItem("premium", ItemEligibility.Gift, 120),
            };

            var names = QuestCostTiers.EligibleNames(catalog, ItemEligibility.Gift, dogCount: 10);

            Assert.That(names, Is.EquivalentTo(new[] { "cheap", "mid", "premium" }));
        }

        [Test]
        public void EligibleNames_OnlyReturnsEntriesTaggedForTheRequestedType()
        {
            var catalog = new[]
            {
                new CatalogItem("gift", ItemEligibility.Gift, 30),
                new CatalogItem("deco", ItemEligibility.Decoration, 30),
            };

            Assert.That(QuestCostTiers.EligibleNames(catalog, ItemEligibility.Gift, dogCount: 10),
                Is.EquivalentTo(new[] { "gift" }));
            Assert.That(QuestCostTiers.EligibleNames(catalog, ItemEligibility.Decoration, dogCount: 10),
                Is.EquivalentTo(new[] { "deco" }));
        }

        [Test]
        public void EligibleNames_SeededPick_IsReproducibleAndAlwaysWithinTheEligibleSlice()
        {
            // #317: the injectable RNG picks only from the eligible slice,
            // deterministically under a fixed seed.
            var catalog = new[]
            {
                new CatalogItem("cheap", ItemEligibility.Gift, 30),
                new CatalogItem("starter-ceiling", ItemEligibility.Gift, 50),
                new CatalogItem("mid", ItemEligibility.Gift, 75),
                new CatalogItem("premium", ItemEligibility.Gift, 120),
            };

            var eligible = QuestCostTiers.EligibleNames(catalog, ItemEligibility.Gift, dogCount: 1);

            var firstRun = new List<string>();
            var rng = new Random(1234);
            for (var i = 0; i < 50; i++)
            {
                var pick = eligible[rng.Next(eligible.Count)];
                Assert.That(pick, Is.AnyOf("cheap", "starter-ceiling"));
                firstRun.Add(pick);
            }

            var secondRun = new List<string>();
            var rng2 = new Random(1234);
            for (var i = 0; i < 50; i++)
            {
                secondRun.Add(eligible[rng2.Next(eligible.Count)]);
            }

            Assert.That(secondRun, Is.EqualTo(firstRun));
        }
    }
}
