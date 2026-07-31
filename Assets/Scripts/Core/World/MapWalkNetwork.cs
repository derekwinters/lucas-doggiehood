using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// Derives the walkable <see cref="WalkNetwork"/> (#106) from the live
    /// multi-tile <see cref="TileMap"/> (#109) rather than the hardcoded
    /// starting <see cref="NeighborhoodLayout"/> (#398): every road-bearing
    /// tile contributes <see cref="Road"/> segments the same way the starting
    /// intersection does, so a newly unlocked zone's tiles knit their
    /// sidewalks and crosswalks into the same graph and dogs can wander onto
    /// them.
    ///
    /// Each tile's roads come straight from its <see cref="TileCatalog"/>
    /// road edges and <see cref="TileGeometry"/> centre — no hand-placed
    /// values. On each axis (north/south, east/west): a tile carrying a road
    /// on BOTH of that axis's edges emits one full road through the tile
    /// centre (a <see cref="TileType.FourWay"/> emits both, reproducing
    /// <see cref="NeighborhoodLayout.Roads"/> exactly); a tile carrying a
    /// road on only ONE edge emits a half-length stub from the tile centre
    /// out to that edge (a cul-de-sac / turn / tee stem). This is the
    /// straight-line graph simplification the sidewalks spec already uses for
    /// crosswalks — the rendered curve of a turn/cul-de-sac is independent of
    /// the graph edge; the sidewalk arms simply bend at the tile boundary.
    /// </summary>
    public static class MapWalkNetwork
    {
        /// <summary>A road spanning a tile centre-to-centre on one axis: half
        /// its length reaches each edge (<see cref="WorldDimensions.TileSize"/>
        /// / 2, the same value the starting map's
        /// <see cref="NeighborhoodLayout.StreetHalfLength"/> uses).</summary>
        private const float FullRoadHalfLength = WorldDimensions.TileSize / 2f;

        /// <summary>A single-edge stub reaches only from the tile centre out
        /// to one edge midpoint — half of a full road.</summary>
        private const float StubHalfLength = WorldDimensions.TileSize / 4f;

        /// <summary>Every <see cref="Road"/> the tiles in <paramref name="map"/>
        /// carry, derived from each tile's catalog road edges.</summary>
        public static IReadOnlyList<Road> RoadsFrom(TileMap map)
        {
            var roads = new List<Road>();

            foreach (var tile in map.Tiles)
            {
                var definition = TileCatalog.Get(tile.Value);
                var center = TileGeometry.CenterOf(tile.Key);

                AddAxisRoad(roads, definition, tile.Key, center,
                    StreetOrientation.NorthSouth, TileEdge.North, TileEdge.South);
                AddAxisRoad(roads, definition, tile.Key, center,
                    StreetOrientation.EastWest, TileEdge.East, TileEdge.West);
            }

            return roads;
        }

        /// <summary>
        /// The walk network for <paramref name="map"/>, attaching front
        /// walkways only for the given <paramref name="builtLots"/> — the lots
        /// of houses that actually exist. A freshly unlocked zone's lots have
        /// no <see cref="House"/> (and so no assigned art variant) yet, and
        /// <see cref="WalkNetwork.BuildFrom"/>'s walkway attach would throw on
        /// them, so callers must pass only built houses' lots.
        /// </summary>
        public static WalkNetwork BuildFrom(TileMap map, IReadOnlyList<HouseLot> builtLots)
        {
            return WalkNetwork.BuildFrom(RoadsFrom(map), builtLots);
        }

        private static void AddAxisRoad(List<Road> roads, TileTypeDefinition definition,
            TileCoordinate coordinate, GridPoint center, StreetOrientation orientation,
            TileEdge positiveEdge, TileEdge negativeEdge)
        {
            var hasPositive = definition.HasRoadOn(positiveEdge);
            var hasNegative = definition.HasRoadOn(negativeEdge);

            if (hasPositive && hasNegative)
            {
                roads.Add(new Road(orientation, center, FullRoadHalfLength));
            }
            else if (hasPositive)
            {
                roads.Add(new Road(orientation, StubCenter(coordinate, center, positiveEdge), StubHalfLength));
            }
            else if (hasNegative)
            {
                roads.Add(new Road(orientation, StubCenter(coordinate, center, negativeEdge), StubHalfLength));
            }
        }

        private static GridPoint StubCenter(TileCoordinate coordinate, GridPoint center, TileEdge edge)
        {
            var edgeMidpoint = TileGeometry.EdgeMidpoint(coordinate, edge);
            return new GridPoint((center.X + edgeMidpoint.X) / 2f, (center.Z + edgeMidpoint.Z) / 2f);
        }
    }
}
