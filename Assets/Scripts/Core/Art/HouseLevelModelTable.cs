using System;
using System.Collections.Generic;

namespace Doggiehood.Core.Art
{
    /// <summary>
    /// Which City Kit Suburban mesh each starting house renders as at each
    /// of its four levels (#59). Leveling a house up swaps its model for the
    /// next rung of its ladder, so a home visibly grows as it is upgraded.
    ///
    /// Each ladder is <b>anchored on its Level-1 mesh</b>: the level-1 entry
    /// is exactly the as-built mesh <see cref="HouseStyleTable"/> already
    /// assigns that house (guarded by a table test), so a level-1 house
    /// renders identically to today. Levels 2-4 are Derek's approved
    /// direction (2026-07-25). Every rung of every ladder has a full 3D
    /// catalog entry (footprint/door anchors in
    /// <see cref="Doggiehood.Core.World.HouseModelCatalog"/>) as of the v0.6
    /// de-graybox pass, so the Unity layer renders each level's chosen kit
    /// mesh (visibly growing) rather than a graybox placeholder — this Core
    /// table only holds the mesh-name strings. The non-L1 meshes' door
    /// anchors are still provisional placeholders pending a gallery authoring
    /// pass; the models themselves are final. See
    /// docs/specs/expansion.md#house-leveling.
    /// </summary>
    public static class HouseLevelModelTable
    {
        /// <summary>Levels are 1-indexed; level 1 is the as-built mesh.</summary>
        public const int MinLevel = 1;

        // Index 0 == level 1 (the HouseStyleTable/as-built mesh) ... index 3
        // == level 4. The count of rungs per house is the level ceiling —
        // kept in step with Expansion.HouseUpgradeNumbers.MaxLevel by the
        // table's completeness test rather than a cross-namespace reference.
        private static readonly Dictionary<int, string[]> Ladders = new Dictionary<int, string[]>
        {
            { 1, new[] { "building-type-r", "building-type-c", "building-type-s", "building-type-b" } },
            { 2, new[] { "building-type-h", "building-type-i", "building-type-g", "building-type-f" } },
            { 3, new[] { "building-type-k", "building-type-l", "building-type-j", "building-type-d" } },
            { 4, new[] { "building-type-q", "building-type-e", "building-type-u", "building-type-n" } },

            // #299: the 5th ladder, added for the zone-house art pass. Unlike
            // ladders 1-4 (each a starting house's fixed upgrade path), this
            // one is a shared pool ladder that any zone-built house (id >= 5)
            // may roll via Doggiehood.Core.Art.HouseVariantAssignment. Size-
            // ordered L1->L4 o -> p -> a -> m (Derek's approved draft order,
            // 2026-07-28; m is the largest/top rung and was already catalogued
            // — every rung has a HouseModelCatalog entry as of #348).
            { 5, new[] { "building-type-o", "building-type-p", "building-type-a", "building-type-m" } },
        };

        /// <summary>Non-throwing existence check: a house built beyond the
        /// starting 4 (an unlocked zone's lot, #57) has no authored ladder
        /// yet, so callers fall back to the plain render instead of catching
        /// <see cref="ForHouseLevel"/>'s exception.</summary>
        public static bool HasHouse(int houseId)
        {
            return Ladders.ContainsKey(houseId);
        }

        /// <summary>The mesh name for <paramref name="houseId"/> at
        /// <paramref name="level"/> (#59). Throws for an unknown house, or a
        /// level below <see cref="MinLevel"/> or above the house's ladder
        /// length (the level ceiling).</summary>
        public static string ForHouseLevel(int houseId, int level)
        {
            if (!Ladders.TryGetValue(houseId, out var ladder))
            {
                throw new ArgumentException($"No level-model ladder for house id {houseId}.", nameof(houseId));
            }

            if (level < MinLevel || level > ladder.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(level), level, $"House level must be {MinLevel}..{ladder.Length}.");
            }

            return ladder[level - MinLevel];
        }
    }
}
