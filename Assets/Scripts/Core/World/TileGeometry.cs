using System;
using System.Collections.Generic;
using System.Linq;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// World-space positions for a tile placed at a
    /// <see cref="TileCoordinate"/> (#109): derived from the coordinate and
    /// the #105 standard <see cref="WorldDimensions"/> only, never a
    /// separately hand-picked value.
    /// </summary>
    public static class TileGeometry
    {
        // #700: the two seed salts an open-space quadrant's cluster draws from —
        // one for its candidate points, one for the selection (count, kind and
        // per-tree scale), mirroring YardLandscaping's per-lot salts.
        private const int OpenSpaceCandidateSeedSalt = 0;
        private const int OpenSpaceSelectionSeedSalt = 1;

        // Odd multiplier that mixes the salt into the per-quadrant key, the same
        // way YardLandscaping.SeedFor mixes a lot's HouseId.
        private const int SeedSaltMultiplier = 397;

        // A tile with exactly one roaded edge is a dead end (a cul-de-sac): its
        // single arm terminates in a paved bulb at the tile centre.
        private const int DeadEndRoadEdgeCount = 1;

        /// <summary>The tile's center in world-space meters.</summary>
        public static GridPoint CenterOf(TileCoordinate coordinate)
        {
            return new GridPoint(
                coordinate.Col * WorldDimensions.TileSize,
                coordinate.Row * WorldDimensions.TileSize);
        }

        /// <summary>The midpoint of the tile's <paramref name="edge"/> in world-space meters.</summary>
        public static GridPoint EdgeMidpoint(TileCoordinate coordinate, TileEdge edge)
        {
            var center = CenterOf(coordinate);
            float half = WorldDimensions.TileSize / 2f;

            switch (edge)
            {
                case TileEdge.North: return new GridPoint(center.X, center.Z + half);
                case TileEdge.South: return new GridPoint(center.X, center.Z - half);
                case TileEdge.East: return new GridPoint(center.X + half, center.Z);
                case TileEdge.West: return new GridPoint(center.X - half, center.Z);
                default: throw new ArgumentOutOfRangeException(nameof(edge), edge, null);
            }
        }

        /// <summary>
        /// This tile's property-lot slots (<see cref="TileLotCatalog"/>) in
        /// world-space meters: each type's local offsets shifted by the
        /// tile's own <see cref="CenterOf"/>. A FourWay yields all four
        /// quadrant slots (#607); the origin FourWay's seeded lots are guarded
        /// in <see cref="GameState.LotsForUnlockedTile"/>, not here.
        /// </summary>
        public static IReadOnlyList<GridPoint> LotWorldPositionsFor(TileType type, TileCoordinate coordinate)
        {
            var center = CenterOf(coordinate);
            return TileLotCatalog.LotOffsetsFor(type)
                .Select(offset => new GridPoint(center.X + offset.X, center.Z + offset.Z))
                .ToList();
        }

        /// <summary>
        /// Every open-space tree a tile plants, across all of its
        /// open-space-with-trees quadrants (<see cref="TileLotCatalog.TreeQuadrantsFor"/>):
        /// the flattened union of <see cref="OpenSpaceTreesFor"/> per quadrant.
        /// Every quadrant with no kept house lot is planted (#614) — cul-de-sacs'
        /// two bulb-side quadrants (#385) and a bend's cupped corner AND its
        /// diagonal opposite; full-lot types (FourWay/Straight*/Tee*) and the
        /// out-of-scope GreenSpace park return an empty list (a full-lot type's
        /// unbuilt quadrants show their PREDETERMINED house's yard trees
        /// instead, #434/#461).
        ///
        /// <para><b>Invariant (#700):</b> an open-space quadrant is planted with
        /// a CLUSTER — at least <see cref="YardLandscaping.OpenSpaceSelectMin"/>
        /// spaced trees — never a single one, which read as bare grass in
        /// playtesting.</para>
        /// </summary>
        public static IReadOnlyList<YardTreePlacement> TreeWorldPositionsFor(
            TileType type, TileCoordinate coordinate)
        {
            var placements = new List<YardTreePlacement>();
            foreach (var entry in TileLotCatalog.TreeQuadrantsFor(type))
            {
                placements.AddRange(OpenSpaceTreesFor(type, coordinate, entry.Key));
            }

            return placements;
        }

        /// <summary>
        /// The cluster of open-space trees planted in ONE
        /// <paramref name="quadrant"/> of the tile at
        /// <paramref name="coordinate"/> (#700): rejection-sampled inside the
        /// quadrant's road-cleared grass (<see cref="OpenSpaceGrassFor"/>) with
        /// the same <see cref="YardLandscaping"/> machinery a house yard uses —
        /// <see cref="YardLandscaping.OpenSpaceSelectMin"/>..<see cref="YardLandscaping.OpenSpaceSelectMax"/>
        /// picks, mutually spaced by <see cref="YardLandscaping.MinSpacing"/>,
        /// each with its own #458 size variance. Seeded per (coordinate,
        /// quadrant), so a tile renders identically across sessions and saves.
        /// A quadrant that holds a house lot — or whose grass the tile's roads
        /// leave no room in — plants nothing.
        /// </summary>
        public static IReadOnlyList<YardTreePlacement> OpenSpaceTreesFor(
            TileType type, TileCoordinate coordinate, Quadrant quadrant)
        {
            if (!TileLotCatalog.TreeQuadrantsFor(type).ContainsKey(quadrant))
            {
                return new List<YardTreePlacement>();
            }

            var grass = OpenSpaceGrassFor(type, coordinate, quadrant);
            var candidates = YardLandscaping.GenerateOpenSpaceCandidates(
                grass, PavementFor(type, coordinate),
                SeedFor(coordinate, quadrant, OpenSpaceCandidateSeedSalt));
            return YardLandscaping.SelectOpenSpace(
                candidates, SeedFor(coordinate, quadrant, OpenSpaceSelectionSeedSalt));
        }

        /// <summary>
        /// The clean grass an open-space <paramref name="quadrant"/> offers:
        /// its <see cref="QuadrantWorldBounds"/> with the tile's own road
        /// corridors cleared (<see cref="LotBounds.RoadsFor"/>/
        /// <see cref="LotBounds.ClearRoadCorridors"/>, #614) — so a tree can
        /// never land in the bend's road arc, and a quadrant the roads leave no
        /// room in yields a collapsed rect that plants nothing rather than
        /// forcing a tree onto pavement.
        /// </summary>
        public static LotRect OpenSpaceGrassFor(TileType type, TileCoordinate coordinate, Quadrant quadrant)
        {
            return LotBounds.ClearRoadCorridors(
                QuadrantWorldBounds(coordinate, quadrant), LotBounds.RoadsFor(coordinate, type));
        }

        /// <summary>
        /// Every paved region of the tile at <paramref name="coordinate"/> an
        /// open-space tree must stand clear of (#700): one corridor rect per
        /// road (<see cref="LotBounds.RoadsFor"/>) reaching
        /// <see cref="LotBounds.StreetCorridorInset"/> either side of its
        /// centerline — road, verge and sidewalk — plus, for a DEAD-END type
        /// (a single roaded edge, i.e. a cul-de-sac), the bulb at the tile
        /// centre. The bulb matters because the stub's <see cref="Road"/> extent
        /// stops at the tile centre while the paved turnaround keeps going
        /// (<see cref="WorldDimensions.CulDeSacBulbRadius"/>), so the per-edge
        /// corridor trim alone cannot see it — scattering a cluster across a
        /// whole quadrant would otherwise drop trees onto it.
        /// </summary>
        private static IReadOnlyList<LotRect> PavementFor(TileType type, TileCoordinate coordinate)
        {
            var pavement = new List<LotRect>();
            foreach (var road in LotBounds.RoadsFor(coordinate, type))
            {
                pavement.Add(CorridorRect(road));
            }

            if (TileCatalog.Get(type).RoadEdges.Count == DeadEndRoadEdgeCount)
            {
                var center = CenterOf(coordinate);
                var radius = WorldDimensions.CulDeSacBulbRadius;
                pavement.Add(new LotRect(
                    center.X - radius, center.X + radius, center.Z - radius, center.Z + radius));
            }

            return pavement;
        }

        /// <summary>The paved corridor of one <paramref name="road"/> as a rect:
        /// <see cref="LotBounds.StreetCorridorInset"/> either side of its
        /// centerline, over its own finite extent.</summary>
        private static LotRect CorridorRect(Road road)
        {
            var halfWidth = LotBounds.StreetCorridorInset;
            return road.Orientation == StreetOrientation.NorthSouth
                ? new LotRect(
                    road.Center.X - halfWidth, road.Center.X + halfWidth,
                    road.Center.Z - road.HalfLength, road.Center.Z + road.HalfLength)
                : new LotRect(
                    road.Center.X - road.HalfLength, road.Center.X + road.HalfLength,
                    road.Center.Z - halfWidth, road.Center.Z + halfWidth);
        }

        /// <summary>The world-space bounds of one <paramref name="quadrant"/> of
        /// the tile at <paramref name="coordinate"/>: a
        /// <see cref="WorldDimensions.TileSize"/>/2-per-side rect on that
        /// quadrant, centred on the tile — the same tiling
        /// <see cref="LotBounds.QuadrantBounds"/> produces, keyed by coordinate
        /// rather than a house lot.</summary>
        public static LotRect QuadrantWorldBounds(TileCoordinate coordinate, Quadrant quadrant)
        {
            var center = CenterOf(coordinate);
            // The quadrant is a TileSize/2-per-side rect; its half-extent (used
            // to place and size it) is half of that quadrant side.
            var quadrantSide = WorldDimensions.TileSize / 2f;
            var half = quadrantSide / 2f;
            var (signX, signZ) = SignsFor(quadrant);
            var centerX = center.X + signX * half;
            var centerZ = center.Z + signZ * half;
            return new LotRect(centerX - half, centerX + half, centerZ - half, centerZ + half);
        }

        /// <summary>The deterministic seed for one (coordinate, quadrant)
        /// cluster (#700). <see cref="FrontierHouseId.For"/> is already an
        /// injective, pure function of exactly that pair — no two tile-quadrants
        /// on the map share a value — so it is the natural stable key here, the
        /// same way a house lot seeds its yard trees from its own
        /// <see cref="HouseLot.HouseId"/>.</summary>
        private static int SeedFor(TileCoordinate coordinate, Quadrant quadrant, int salt)
        {
            return unchecked(FrontierHouseId.For(coordinate, quadrant) * SeedSaltMultiplier + salt);
        }

        private static (float SignX, float SignZ) SignsFor(Quadrant quadrant)
        {
            switch (quadrant)
            {
                case Quadrant.NorthEast: return (1f, 1f);
                case Quadrant.NorthWest: return (-1f, 1f);
                case Quadrant.SouthEast: return (1f, -1f);
                case Quadrant.SouthWest: return (-1f, -1f);
                default: throw new ArgumentOutOfRangeException(nameof(quadrant), quadrant, null);
            }
        }
    }
}
