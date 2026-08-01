using System;
using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>An axis-aligned rectangle on the ground plane, in meters
    /// (#222). Facing directions in this codebase are always cardinal
    /// (<see cref="HousePlacement.FacingToward"/> snaps to the dominant
    /// axis), so every shape <see cref="LotBounds"/> works with stays
    /// axis-aligned — no rotation needed.</summary>
    public readonly struct LotRect
    {
        public float MinX { get; }
        public float MaxX { get; }
        public float MinZ { get; }
        public float MaxZ { get; }

        public LotRect(float minX, float maxX, float minZ, float maxZ)
        {
            if (maxX < minX)
            {
                throw new ArgumentException("maxX must be >= minX.", nameof(maxX));
            }

            if (maxZ < minZ)
            {
                throw new ArgumentException("maxZ must be >= minZ.", nameof(maxZ));
            }

            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
        }

        public float Width
        {
            get { return MaxX - MinX; }
        }

        public float Depth
        {
            get { return MaxZ - MinZ; }
        }

        public GridPoint Center
        {
            get { return new GridPoint((MinX + MaxX) / 2f, (MinZ + MaxZ) / 2f); }
        }

        /// <summary>Whether <paramref name="point"/> sits inside this rect
        /// (inclusive of the edges).</summary>
        public bool Contains(GridPoint point)
        {
            return point.X >= MinX && point.X <= MaxX && point.Z >= MinZ && point.Z <= MaxZ;
        }

        /// <summary>Whether this rect fully contains <paramref name="other"/>.</summary>
        public bool Contains(LotRect other)
        {
            return other.MinX >= MinX && other.MaxX <= MaxX && other.MinZ >= MinZ && other.MaxZ <= MaxZ;
        }

        /// <summary>The shortest distance from <paramref name="point"/> to
        /// this rect, in meters: 0 when the point is inside or on an edge,
        /// otherwise the straight-line distance to the nearest edge. The
        /// engine-free primitive both yard landscaping (#170) and quest
        /// hidden-item placement (#290) use to keep points clear of a house
        /// footprint.</summary>
        public float DistanceTo(GridPoint point)
        {
            var dx = Math.Max(MinX - point.X, Math.Max(0f, point.X - MaxX));
            var dz = Math.Max(MinZ - point.Z, Math.Max(0f, point.Z - MaxZ));
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Whether this rect shares any positive-area overlap with
        /// <paramref name="other"/> — rects that only touch at an edge
        /// (e.g. two lot quadrants meeting at the road centerline) do NOT
        /// overlap.</summary>
        public bool Overlaps(LotRect other)
        {
            return MinX < other.MaxX && MaxX > other.MinX && MinZ < other.MaxZ && MaxZ > other.MinZ;
        }
    }

    /// <summary>
    /// Lot bounds (#222). Decision (conversation with Derek, 2026-07-20): a
    /// property (lot) is one QUADRANT of a tile — on the starting FourWay
    /// intersection each of the 4 quadrants is a property, and that same
    /// quadrant size (<see cref="WorldDimensions.TileSize"/> / 2 per side)
    /// is the standard for every layout.
    ///
    /// <see cref="QuadrantBounds"/> derives purely from the lot's own
    /// <see cref="HouseLot.Quadrant"/> and <see cref="WorldDimensions.TileSize"/>
    /// — NOT from <see cref="HouseLot.Position"/> (a separate, hand-picked
    /// house-placement choice, <see cref="NeighborhoodLayout.LotDistanceFromCenter"/>).
    /// Sizing bounds to a full tile-quadrant (half = TileSize/4 = 15m) and
    /// centering them on the hand-picked position (14m) would overlap the
    /// opposite lot's bounds across the road by 1m; centering each
    /// quadrant on the tile's own quadrant split (0m/±30m per lot) instead
    /// makes the 4 lots' bounds exactly tile the 60m tile with no gap or
    /// overlap, satisfying the "no spill into the neighbouring quadrant"
    /// requirement by construction.
    ///
    /// <see cref="FrontYard"/>/<see cref="BackYard"/> split those bounds
    /// relative to <see cref="HousePlacement.FrontFacing"/>, excluding the
    /// house footprint — the regions #170 scatters trees into.
    /// </summary>
    public static class LotBounds
    {
        /// <summary>
        /// How far the paved street corridor reaches from a road's
        /// centerline into the front yard that faces it: the road
        /// half-width plus the grass verge plus the full sidewalk width —
        /// i.e. the sidewalk's OUTER edge, the same 5.75m boundary
        /// <see cref="HousePlacement"/> sets front facades back from.
        /// Derived from <see cref="WorldDimensions"/> alone (#244), no
        /// hand-tuned magic number.
        ///
        /// A lot's quadrant bounds tile the whole 60m tile with no gap, so
        /// the front yard's street-side edge sits ON the tile centerline
        /// where the road runs — <see cref="FrontYard"/> pulls that edge
        /// inward by this distance so the region ends at the yard/pavement
        /// line instead of reaching into the road and sidewalk (#244:
        /// procedurally placed trees were landing in the road because the
        /// region included it).
        /// </summary>
        public const float StreetCorridorInset =
            WorldDimensions.RoadWidth / 2f + WorldDimensions.GrassVergeWidth + WorldDimensions.SidewalkWidth;

        /// <summary>Tolerance for deciding a yard edge sits ON a road's
        /// centerline (#272). Quadrant edges on an inner road land on an
        /// exact 0, and an already-inset edge is a full
        /// <see cref="StreetCorridorInset"/> (5.75m) away — far larger than
        /// this — so a tiny tolerance both matches the real border edges and
        /// never re-insets an edge #244 already pulled off the centerline.</summary>
        private const float RoadCenterlineTolerance = 0.001f;

        /// <summary>The lot's rectangular bounds: one tile-quadrant
        /// (<see cref="WorldDimensions.TileSize"/> / 2 per side), positioned
        /// on the lot's own <see cref="HouseLot.Quadrant"/> within the tile the
        /// lot sits on. The quadrant is centred on THAT tile (not always the
        /// origin): a starting lot rounds to the origin tile and is unchanged,
        /// but a zone lot (#405) sits on its own tile (e.g. the first zone's
        /// cul-de-sac at world Z ~= 60), so its bounds surround its house
        /// position there — otherwise <see cref="YardSplit"/> would slice around
        /// a house that lies outside origin-centred bounds and produce an
        /// inverted rect. The hand-picked lot distance within a tile still does
        /// not affect the bounds (two lots in the same tile-quadrant share
        /// them); only which tile the lot is on shifts them.</summary>
        public static LotRect QuadrantBounds(HouseLot lot)
        {
            var half = WorldDimensions.TileSize / 4f;
            var (signX, signZ) = SignsFor(lot.Quadrant);
            var tileCenter = NearestTileCenter(lot.Position);
            var centerX = tileCenter.X + signX * half;
            var centerZ = tileCenter.Z + signZ * half;
            return new LotRect(centerX - half, centerX + half, centerZ - half, centerZ + half);
        }

        /// <summary>The centre of the <see cref="WorldDimensions.TileSize"/>
        /// grid tile that <paramref name="position"/> falls in — the same
        /// col*size / row*size convention <see cref="TileGeometry.CenterOf"/>
        /// and <see cref="Zone"/> use to place lots, inverted by rounding to
        /// the nearest tile.</summary>
        private static GridPoint NearestTileCenter(GridPoint position)
        {
            return TileGeometry.CenterOf(NearestTileCoordinate(position));
        }

        /// <summary>The integer grid <see cref="TileCoordinate"/> of the
        /// <see cref="WorldDimensions.TileSize"/> tile that
        /// <paramref name="position"/> falls in — the inverse of
        /// <see cref="TileGeometry.CenterOf"/>, rounding to the nearest tile.
        /// The tile whose road geometry (<see cref="RoadsFor"/>) and, for the
        /// Unity layer, whose <see cref="TileMap"/> type a lot's yards are
        /// resolved against (#455).</summary>
        public static TileCoordinate NearestTileCoordinate(GridPoint position)
        {
            var size = WorldDimensions.TileSize;
            var col = (int)Math.Round(position.X / size, MidpointRounding.AwayFromZero);
            var row = (int)Math.Round(position.Z / size, MidpointRounding.AwayFromZero);
            return new TileCoordinate(col, row);
        }

        /// <summary>The portion of <see cref="QuadrantBounds"/> on the
        /// street side of the house (the direction
        /// <see cref="HousePlacement.FrontFacing"/> points), excluding the
        /// house footprint AND the paved street corridor: the street-side
        /// edge is pulled in by <see cref="StreetCorridorInset"/> so the
        /// region ends at the yard/pavement line rather than reaching into
        /// the road and sidewalk it faces (#244).</summary>
        public static LotRect FrontYard(HouseLot lot)
        {
            return YardSplit(lot, NeighborhoodLayout.Roads).Front;
        }

        /// <summary>
        /// <see cref="FrontYard(HouseLot)"/> clipped against the lot's OWN
        /// tile's road as well as the origin's fixed streets (#455). A zone
        /// tile's road is built as a <see cref="TileRoadSegment"/> and never
        /// turned into a <see cref="Road"/>, so the single-arg overload — which
        /// only knows <see cref="NeighborhoodLayout.Roads"/> — never trimmed the
        /// road a cul-de-sac kept-quadrant lot borders, and trees landed in its
        /// paved strip. This threads that tile's road geometry in via
        /// <see cref="RoadsFor"/>; the 4 starting FourWay lots are unaffected
        /// (their tile arms are coincident with the origin streets).
        /// </summary>
        public static LotRect FrontYard(HouseLot lot, TileType tileType)
        {
            return YardSplit(lot, RoadsFor(lot, tileType)).Front;
        }

        /// <summary>The portion of <see cref="QuadrantBounds"/> behind the
        /// house (away from the faced street), excluding the house
        /// footprint.</summary>
        public static LotRect BackYard(HouseLot lot)
        {
            return YardSplit(lot, NeighborhoodLayout.Roads).Back;
        }

        /// <summary><see cref="BackYard(HouseLot)"/> clipped against the lot's
        /// own tile's road as well as the origin's fixed streets — see
        /// <see cref="FrontYard(HouseLot, TileType)"/> (#455).</summary>
        public static LotRect BackYard(HouseLot lot, TileType tileType)
        {
            return YardSplit(lot, RoadsFor(lot, tileType)).Back;
        }

        /// <summary>
        /// The roads a lot's yards clip against when its tile <paramref name="tileType"/>
        /// is known (#455): <see cref="NeighborhoodLayout.Roads"/> (the origin
        /// FourWay's fixed streets) PLUS the lot's own tile's road geometry,
        /// converted from <see cref="TileRoadGeometry.SegmentsFor"/> to
        /// <see cref="Road"/> (a segment's <see cref="TileRoadSegment.Center"/>/
        /// <see cref="TileRoadSegment.Orientation"/> map directly;
        /// <see cref="Road.HalfLength"/> is half the segment length). The tile
        /// coordinate comes from <see cref="NearestTileCoordinate"/> over the
        /// lot's position. Combining with the origin roads keeps the 4 starting
        /// FourWay lots byte-identical — their own tile's four arms lie on the
        /// same centerlines as those fixed streets, and the inset is not applied
        /// twice to an already-inset edge.
        /// </summary>
        public static IReadOnlyList<Road> RoadsFor(HouseLot lot, TileType tileType)
        {
            var roads = new List<Road>(NeighborhoodLayout.Roads);
            var coordinate = NearestTileCoordinate(lot.Position);
            foreach (var segment in TileRoadGeometry.SegmentsFor(coordinate, tileType))
            {
                roads.Add(new Road(segment.Orientation, segment.Center, segment.Length / 2f));
            }

            return roads;
        }

        /// <summary>
        /// Pulls every edge of <paramref name="yard"/> that lies on a road
        /// centerline inward by <see cref="StreetCorridorInset"/>, so no
        /// part of the region reaches into a road it borders (#272).
        ///
        /// Each lot is one tile quadrant, so on the FourWay every quadrant
        /// borders TWO roads — one on each inner edge. The #244 inset only
        /// cleared the FACED road; the PERPENDICULAR road's centerline still
        /// ran along the yard's other inner edge, and trees landed in it.
        /// This trims BOTH, and derives which edges border a road generically
        /// from <paramref name="roads"/> (an edge is only trimmed when it lies
        /// on a road's centerline AND overlaps that road's finite extent), so
        /// the rule survives map expansion rather than hard-coding the two
        /// inner quadrant edges. Already-inset edges (#244) sit a full
        /// <see cref="StreetCorridorInset"/> off the centerline and are left
        /// untouched, so this composes with the faced-road trim without
        /// double-insetting.
        /// </summary>
        public static LotRect ClearRoadCorridors(LotRect yard, IReadOnlyList<Road> roads)
        {
            if (roads == null)
            {
                throw new ArgumentNullException(nameof(roads));
            }

            var minX = yard.MinX;
            var maxX = yard.MaxX;
            var minZ = yard.MinZ;
            var maxZ = yard.MaxZ;

            foreach (var road in roads)
            {
                if (road.Orientation == StreetOrientation.NorthSouth)
                {
                    // Centerline at constant X, running along Z over its extent.
                    var extentMin = road.Center.Z - road.HalfLength;
                    var extentMax = road.Center.Z + road.HalfLength;
                    if (!SpansOverlap(minZ, maxZ, extentMin, extentMax))
                    {
                        continue;
                    }

                    if (OnCenterline(minX, road.Center.X))
                    {
                        minX = Math.Min(minX + StreetCorridorInset, maxX);
                    }

                    if (OnCenterline(maxX, road.Center.X))
                    {
                        maxX = Math.Max(maxX - StreetCorridorInset, minX);
                    }
                }
                else
                {
                    // Centerline at constant Z, running along X over its extent.
                    var extentMin = road.Center.X - road.HalfLength;
                    var extentMax = road.Center.X + road.HalfLength;
                    if (!SpansOverlap(minX, maxX, extentMin, extentMax))
                    {
                        continue;
                    }

                    if (OnCenterline(minZ, road.Center.Z))
                    {
                        minZ = Math.Min(minZ + StreetCorridorInset, maxZ);
                    }

                    if (OnCenterline(maxZ, road.Center.Z))
                    {
                        maxZ = Math.Max(maxZ - StreetCorridorInset, minZ);
                    }
                }
            }

            return new LotRect(minX, maxX, minZ, maxZ);
        }

        private static (LotRect Front, LotRect Back) YardSplit(HouseLot lot, IReadOnlyList<Road> roads)
        {
            var bounds = QuadrantBounds(lot);
            var facing = HousePlacement.FrontFacing(lot);
            var house = HousePlacement.Position(lot, HousePlacement.KitScale);
            var halfDepth = HalfDepthOf(lot);

            if (facing.X > 0f)
            {
                var facadeX = house.X + halfDepth;
                var rearX = house.X - halfDepth;
                var streetEdgeX = Math.Max(bounds.MaxX - StreetCorridorInset, facadeX);
                return ClearRoadCorridors(
                    new LotRect(facadeX, streetEdgeX, bounds.MinZ, bounds.MaxZ),
                    new LotRect(bounds.MinX, rearX, bounds.MinZ, bounds.MaxZ),
                    roads);
            }

            if (facing.X < 0f)
            {
                var facadeX = house.X - halfDepth;
                var rearX = house.X + halfDepth;
                var streetEdgeX = Math.Min(bounds.MinX + StreetCorridorInset, facadeX);
                return ClearRoadCorridors(
                    new LotRect(streetEdgeX, facadeX, bounds.MinZ, bounds.MaxZ),
                    new LotRect(rearX, bounds.MaxX, bounds.MinZ, bounds.MaxZ),
                    roads);
            }

            if (facing.Z > 0f)
            {
                var facadeZ = house.Z + halfDepth;
                var rearZ = house.Z - halfDepth;
                var streetEdgeZ = Math.Max(bounds.MaxZ - StreetCorridorInset, facadeZ);
                return ClearRoadCorridors(
                    new LotRect(bounds.MinX, bounds.MaxX, facadeZ, streetEdgeZ),
                    new LotRect(bounds.MinX, bounds.MaxX, bounds.MinZ, rearZ),
                    roads);
            }

            if (facing.Z < 0f)
            {
                var facadeZ = house.Z - halfDepth;
                var rearZ = house.Z + halfDepth;
                var streetEdgeZ = Math.Min(bounds.MinZ + StreetCorridorInset, facadeZ);
                return ClearRoadCorridors(
                    new LotRect(bounds.MinX, bounds.MaxX, streetEdgeZ, facadeZ),
                    new LotRect(bounds.MinX, bounds.MaxX, rearZ, bounds.MaxZ),
                    roads);
            }

            throw new ArgumentException("Lot facing must be a nonzero cardinal direction.", nameof(lot));
        }

        private static (LotRect Front, LotRect Back) ClearRoadCorridors(
            LotRect front, LotRect back, IReadOnlyList<Road> roads)
        {
            return (ClearRoadCorridors(front, roads), ClearRoadCorridors(back, roads));
        }

        private static bool OnCenterline(float edge, float centerline)
        {
            return Math.Abs(edge - centerline) <= RoadCenterlineTolerance;
        }

        private static bool SpansOverlap(float aMin, float aMax, float bMin, float bMax)
        {
            return aMin < bMax && aMax > bMin;
        }

        private static float HalfDepthOf(HouseLot lot)
        {
            var model = HouseModelCatalog.ForHouse(lot.HouseId);
            return HousePlacement.KitScale * model.FootprintZ / 2f;
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
