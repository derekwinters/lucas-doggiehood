using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Tuning;

namespace Doggiehood.Core.Quests
{
    /// <summary>#317: the cost tier a purchasable quest subject falls into.
    /// Bands over the existing item catalog — new cost tiers within the 3
    /// frozen quest types (quest-content.md), not new quest mechanics.</summary>
    public enum QuestCostTier
    {
        /// <summary>Today's baseline band — always eligible.</summary>
        Starter,

        /// <summary>Bigger asks for an established neighborhood.</summary>
        Mid,

        /// <summary>Marquee purchases reserved for a mature neighborhood.</summary>
        Premium,
    }

    /// <summary>
    /// #317: population-driven quest cost tiers. Quest difficulty is a pure
    /// function of total dog population (<c>state.Dogs.Count</c>) — as the
    /// neighborhood grows, higher-cost quest tiers become eligible and their
    /// pricier catalog entries enter the purchasable subject pool. No hidden
    /// counter and no new persisted state: population is already known.
    ///
    /// <para>Eligibility is <b>cumulative and monotonic</b> — reaching a
    /// higher population only ever adds tiers, never removes a cheaper one,
    /// so an established player still gets affordable requests mixed in. At
    /// minimum population only the <see cref="QuestCostTier.Starter"/> band is
    /// eligible, so onboarding/early game is unchanged.</para>
    ///
    /// <para>All band boundaries and population gates are draft placeholders
    /// (#161) — one-line tunes here. Deterministic, no RNG.</para>
    /// </summary>
    public static class QuestCostTiers
    {
        // --- Cost band boundaries (coins) ---

        // As of #620 each band/gate reads from the runtime-overridable
        // TuningConfig.Active; the shipping defaults live on TuningConfig.

        /// <summary>Cheapest starter-band cost — today's catalog floor.</summary>
        public static int StarterMinCost => TuningConfig.Active.StarterMinCost;

        /// <summary>Starter-band ceiling; the gated cost cap at minimum
        /// population, so the early pool matches today's behavior exactly.</summary>
        public static int StarterMaxCost => TuningConfig.Active.StarterMaxCost;

        /// <summary>Mid-band floor.</summary>
        public static int MidMinCost => TuningConfig.Active.MidMinCost;

        /// <summary>Mid-band ceiling; the gated cost cap once the mid tier is
        /// eligible.</summary>
        public static int MidMaxCost => TuningConfig.Active.MidMaxCost;

        /// <summary>Premium-band floor; the premium tier carries no ceiling
        /// (marquee items priced at or above this are all eligible once the
        /// neighborhood is mature).</summary>
        public static int PremiumMinCost => TuningConfig.Active.PremiumMinCost;

        // --- Population gates (dog count at which each tier becomes eligible) ---

        /// <summary>Starter tier is eligible from the first dog onward — i.e.
        /// always, so today's behavior is preserved.</summary>
        public static int StarterPopulationGate => TuningConfig.Active.StarterPopulationGate;

        /// <summary>Mid tier becomes eligible once the neighborhood reaches
        /// this population.</summary>
        public static int MidPopulationGate => TuningConfig.Active.MidPopulationGate;

        /// <summary>Premium tier becomes eligible once the neighborhood reaches
        /// this population.</summary>
        public static int PremiumPopulationGate => TuningConfig.Active.PremiumPopulationGate;

        /// <summary>The cost tiers eligible at a given dog population, cheapest
        /// first. Cumulative and monotonic in <paramref name="dogCount"/>.</summary>
        public static IReadOnlyList<QuestCostTier> EligibleCostTiers(int dogCount)
        {
            var tiers = new List<QuestCostTier>();
            if (dogCount >= StarterPopulationGate)
            {
                tiers.Add(QuestCostTier.Starter);
            }

            if (dogCount >= MidPopulationGate)
            {
                tiers.Add(QuestCostTier.Mid);
            }

            if (dogCount >= PremiumPopulationGate)
            {
                tiers.Add(QuestCostTier.Premium);
            }

            return tiers;
        }

        /// <summary>The highest catalog cost that is purchasable at a given
        /// population: the ceiling of the top eligible tier
        /// (<see cref="int.MaxValue"/> once premium is eligible — no cap).
        /// Entries priced above this are excluded from the subject pool. Zero
        /// when no tier is eligible (a dogless neighborhood offers no
        /// purchases).</summary>
        public static int EligibleCostCeiling(int dogCount)
        {
            if (dogCount >= PremiumPopulationGate)
            {
                return int.MaxValue;
            }

            if (dogCount >= MidPopulationGate)
            {
                return MidMaxCost;
            }

            if (dogCount >= StarterPopulationGate)
            {
                return StarterMaxCost;
            }

            return 0;
        }

        /// <summary>The purchasable subject pool for a quest type at a given
        /// population: every catalog entry tagged <paramref name="tag"/> whose
        /// <see cref="CatalogItem.Cost"/> falls at or below the
        /// population-eligible ceiling. Find-only entries (no cost) are never
        /// included — this is a purchasable-subject filter. The catalog is
        /// injected so callers pass the live <see cref="ItemCatalog.Items"/>
        /// and tests can supply synthetic higher-band entries.</summary>
        public static IReadOnlyList<string> EligibleNames(
            IEnumerable<CatalogItem> catalog, ItemEligibility tag, int dogCount)
        {
            var ceiling = EligibleCostCeiling(dogCount);
            return catalog
                .Where(i => i.IsEligibleFor(tag) && i.Cost.HasValue && i.Cost.Value <= ceiling)
                .Select(i => i.Name)
                .ToList();
        }
    }
}
