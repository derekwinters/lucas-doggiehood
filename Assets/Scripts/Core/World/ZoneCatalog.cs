using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// The authored zones the player unlocks in sequence (#56,
    /// docs/specs/expansion.md "Map shape"). Only the first zone is
    /// authored so far — later zones are future hand-authored content, not
    /// procedural (explicitly out of scope, #109).
    /// </summary>
    public static class ZoneCatalog
    {
        /// <summary>
        /// The first house id this zone's lots use. Continues on from the
        /// 4 starting houses (<see cref="NeighborhoodLayout.HouseLots"/>,
        /// ids 1-4) so every house id in the game stays unique.
        /// </summary>
        private const int FirstZoneFirstHouseId = 5;

        /// <summary>
        /// The confirmed first-zone layout (2026-07-29, Derek, on #360): a
        /// single cul-de-sac tile directly north of the starting
        /// intersection — from the starting FourWay at (0,0), CulDeSacSouth
        /// at (0,1). Its road enters from the south edge (meeting the
        /// FourWay origin's north road) and ends in a bulb pointing north,
        /// so the whole zone sits due north with no dangling roads. This
        /// supersedes the #56 two-tile northwest layout (TurnSW at (0,1) +
        /// CulDeSacEast at (-1,1)), not a silent change. The FourWay origin
        /// tile itself isn't part of this zone's placements — it's the
        /// pre-existing starting intersection
        /// (<see cref="NeighborhoodLayout"/>), already on the map before any
        /// zone unlocks.
        /// </summary>
        public static Zone FirstZone { get; } = new Zone(
            new[]
            {
                new ZoneTilePlacement(new TileCoordinate(0, 1), TileType.CulDeSacSouth),
            },
            FirstZoneFirstHouseId);

        /// <summary>Every authored zone, in unlock order.</summary>
        public static IReadOnlyList<Zone> Zones { get; } = new[] { FirstZone };
    }
}
