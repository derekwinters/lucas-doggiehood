using System;

namespace Doggiehood.Core.Art
{
    /// <summary>
    /// Deterministically assigns a <see cref="HouseVariant"/> to a zone-built
    /// house (#299): each house id >= <see cref="FirstZoneHouseId"/> rolls one
    /// of the five <see cref="HouseLevelModelTable"/> ladders and one of the
    /// <see cref="TintCount"/> generated palette tints. The roll is a pure
    /// function of the house id — a seeded RNG whose seed is a hash of the id,
    /// so the same id always yields the same variant regardless of when or in
    /// what order houses are built, and no wall-clock or shared mutable RNG is
    /// involved. The result is assigned once at build (GameState.TryBuildHouse)
    /// and persisted (SaveCodec) so it survives relaunch and L1->L4 upgrades.
    ///
    /// The four starting houses (ids 1-4) are exempt: they keep their fixed
    /// <see cref="HouseStyleTable"/> ladder/tint, so <see cref="ForHouse"/>
    /// only accepts zone-house ids.
    /// </summary>
    public static class HouseVariantAssignment
    {
        /// <summary>The lowest house id that is a zone-built house rather than
        /// one of the four fixed starters (matches ZoneCatalog's first zone
        /// lot id). Ids below this keep their <see cref="HouseStyleTable"/>
        /// assignment and are not rolled.</summary>
        public const int FirstZoneHouseId = 5;

        /// <summary>How many ladders the roll picks from — the four starter
        /// ladders plus the #299 5th ladder, all keyed 1..5 in
        /// <see cref="HouseLevelModelTable"/>.</summary>
        public const int LadderCount = 5;

        /// <summary>How many palette tints the roll picks from — the size of
        /// the generated even-hue house palette. Single source of truth for
        /// that count: <see cref="Palette.HouseTintHex"/> generates exactly
        /// this many colors and <see cref="HouseVariant.TintIndex"/> indexes
        /// 0..TintCount-1.</summary>
        public const int TintCount = 20;

        /// <summary>Whether <paramref name="houseId"/> is a zone-built house
        /// (id >= <see cref="FirstZoneHouseId"/>) that rolls a variant, rather
        /// than a fixed starter.</summary>
        public static bool IsZoneHouse(int houseId)
        {
            return houseId >= FirstZoneHouseId;
        }

        /// <summary>The deterministic <see cref="HouseVariant"/> for a
        /// zone-built house. Throws for a starter id (1-4), which has no rolled
        /// variant.</summary>
        public static HouseVariant ForHouse(int houseId)
        {
            if (!IsZoneHouse(houseId))
            {
                throw new ArgumentException(
                    $"House id {houseId} is a starter house (< {FirstZoneHouseId}) and has no rolled variant.",
                    nameof(houseId));
            }

            var rng = new Random(SeedFor(houseId));
            var ladderId = rng.Next(1, LadderCount + 1);
            var tintIndex = rng.Next(0, TintCount);
            return new HouseVariant(ladderId, tintIndex);
        }

        /// <summary>
        /// A well-mixed seed derived from the house id, so seeding
        /// <see cref="Random"/> spreads consecutive ids across the whole
        /// ladder/tint range instead of clustering (close seeds otherwise
        /// produce close first draws). A fixed integer avalanche mix (constant
        /// in, constant out) — no external state, so the mapping is stable
        /// across sessions and machines.
        /// </summary>
        private static int SeedFor(int houseId)
        {
            unchecked
            {
                var h = (uint)houseId;
                h ^= 2166136261u;
                h *= 16777619u;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return (int)h;
            }
        }
    }
}
