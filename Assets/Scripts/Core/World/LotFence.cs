using System;
using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>One straight fence line (#129, reshaped by #146/#342): a
    /// segment of a lot's backyard fence, on the ground plane. Purely
    /// geometric — the tiling into kit pieces is
    /// <see cref="FenceTiling"/>'s job.</summary>
    public readonly struct FenceRun
    {
        public GridPoint A { get; }
        public GridPoint B { get; }

        public FenceRun(GridPoint a, GridPoint b)
        {
            A = a;
            B = b;
        }

        /// <summary>Straight-line length of the run.</summary>
        public float Length
        {
            get
            {
                var dx = A.X - B.X;
                var dz = A.Z - B.Z;
                return (float)Math.Sqrt(dx * dx + dz * dz);
            }
        }
    }

    /// <summary>
    /// Per-lot backyard fence geometry (#342, adopting #147's settled
    /// rules; supersedes #146's house-footprint-width fence). The fence
    /// line traces the LOT BOUNDARY rather than the house, leaving one
    /// <see cref="BoundaryOffset"/> (a sidewalk-width) strip of grass beyond
    /// the pavement on every edge (#147 offset fix). A quadrant edge that
    /// lies ON a road centerline (a road-bordering edge) is offset from the
    /// sidewalk's OUTER edge (<see cref="LotBounds.StreetCorridorInset"/>)
    /// by one <see cref="BoundaryOffset"/>; a neighbour-yard / map edge is
    /// offset from the <see cref="LotBounds.QuadrantBounds"/> boundary by
    /// that same <see cref="BoundaryOffset"/> (so #146's corner-lot
    /// house-anchoring dissolves). Which edges border a road is derived
    /// generically from the road network, mirroring
    /// <see cref="LotBounds.ClearRoadCorridors"/>.
    ///
    /// The shape is FIVE runs, front open: two side runs plus one rear run
    /// trace the offset boundary rectangle around the back yard, and two
    /// short connectors turn perpendicular-inward from each side run's front
    /// end (truncated at the house's depth midpoint) to the house side-wall
    /// midpoints. The front yard stays open — no fence line ever crosses the
    /// front, so the walkway (#128, door → sidewalk) needs no gate. The
    /// whole shape rotates with the house facing
    /// (<see cref="HousePlacement.FrontFacing"/>).
    ///
    /// Fences are defined for every lot but HIDDEN by default
    /// (<see cref="HouseLot.HasFence"/> defaults false since #146; a future
    /// fence-purchase quest, #147/#318, purchases them). The flag-respecting
    /// <see cref="RunsFor"/> is what WorldBuilder consumes — empty while
    /// hidden — while <see cref="GeometryFor"/> keeps the geometry queryable
    /// for a disabled lot.
    /// </summary>
    public static class LotFence
    {
        /// <summary>
        /// The sidewalk-width strip of grass left between the pavement and the
        /// fence line, in meters. Decision (#147 "Settled", adopted by #342):
        /// one raised <see cref="WorldDimensions.SidewalkWidth"/> (2m). It is
        /// measured off the sidewalk's OUTER edge
        /// (<see cref="LotBounds.StreetCorridorInset"/>) on a road-bordering
        /// edge and off the <see cref="LotBounds.QuadrantBounds"/> boundary on
        /// a neighbour-yard / map edge (#147 offset fix), so the same grass
        /// strip sits between the sidewalk and the fence everywhere. Named
        /// rather than a bare literal (#161).
        /// </summary>
        public const float BoundaryOffset = WorldDimensions.SidewalkWidth;

        private const float Epsilon = 0.001f;

        /// <summary>
        /// The lot-free / #126-gallery standard quadrant's front edge sits
        /// this far in front of the house facade, in meters: the same
        /// street corridor + front setback a real lot's quadrant front edge
        /// (on the faced-road centerline) sits ahead of the facade. Derived
        /// from <see cref="LotBounds.StreetCorridorInset"/> +
        /// <see cref="HousePlacement.FrontSetback"/> (#127/#244), identical
        /// for every model, so the gallery reproduces a real lot's fence
        /// outline without a real quadrant.
        /// </summary>
        public static float StandardQuadrantFrontEdgeAheadOfFacade
        {
            get { return LotBounds.StreetCorridorInset + HousePlacement.FrontSetback; }
        }

        /// <summary>
        /// The fence lines <paramref name="lot"/> contributes to the built
        /// world: empty while the lot's fence is hidden
        /// (<see cref="HouseLot.HasFence"/> off — every starting lot's
        /// default), otherwise <see cref="GeometryFor"/>.
        /// </summary>
        public static IReadOnlyList<FenceRun> RunsFor(HouseLot lot)
        {
            return lot.HasFence ? GeometryFor(lot) : Array.Empty<FenceRun>();
        }

        /// <summary>
        /// The lot's backyard fence geometry regardless of the
        /// <see cref="HouseLot.HasFence"/> flag — queryable for a disabled
        /// lot (the #147 purchase flow needs to describe what it sells).
        /// Traces the lot's actual <see cref="LotBounds.QuadrantBounds"/> with
        /// the road-aware offset, passing <see cref="NeighborhoodLayout.Roads"/>
        /// so road-bordering edges sit off the sidewalk's outer edge. Reads
        /// <see cref="NeighborhoodLayout.WalkNetwork"/> via HousePlacement,
        /// like #129's version did; it is only ever called after the network
        /// is built.
        /// </summary>
        public static IReadOnlyList<FenceRun> GeometryFor(HouseLot lot)
        {
            var model = HouseModelCatalog.ForHouse(lot.HouseId);
            var facing = HousePlacement.FrontFacing(lot);
            var position = HousePlacement.Position(lot, HousePlacement.KitScale);
            var quadrant = LotBounds.QuadrantBounds(lot);
            return BackyardRuns(quadrant, model, position, facing, HousePlacement.KitScale,
                NeighborhoodLayout.Roads);
        }

        /// <summary>
        /// Lot-free form (no quadrant — the #126 gallery reuses it with its
        /// own placement): the backyard fence for a house model at
        /// <paramref name="housePosition"/> facing the unit cardinal
        /// <paramref name="facing"/> at <paramref name="uniformScale"/>. Per
        /// #147's approved resolution, the <see cref="BoundaryOffset"/> is
        /// applied against a STANDARD quadrant (<see cref="WorldDimensions.TileSize"/>/2
        /// per side, identical for every lot) synthesised around the house
        /// so the gallery shows the real offset outline without a per-model
        /// boundary.
        /// </summary>
        public static IReadOnlyList<FenceRun> BackyardRuns(HouseModel model, GridPoint housePosition,
            GridPoint facing, float uniformScale)
        {
            if (uniformScale <= 0f)
            {
                throw new ArgumentException("Uniform scale must be positive.", nameof(uniformScale));
            }

            var quadrant = StandardQuadrantAround(model, housePosition, facing, uniformScale);
            // The standard gallery quadrant has no adjacent road network, so
            // the offset is a plain BoundaryOffset on every edge.
            return BackyardRuns(quadrant, model, housePosition, facing, uniformScale,
                Array.Empty<Road>());
        }

        /// <summary>
        /// Pure builder: the five backyard fence runs tracing the road-aware
        /// inset of <paramref name="quadrantBounds"/> (edges on a road
        /// centerline in <paramref name="roads"/> pulled off the sidewalk's
        /// outer edge via <see cref="LotBounds.ClearRoadCorridors"/>, then
        /// every edge inset by <see cref="BoundaryOffset"/>), for a house of
        /// <paramref name="model"/> at <paramref name="housePosition"/>
        /// facing the unit cardinal <paramref name="facing"/> at
        /// <paramref name="uniformScale"/>. Pass an empty
        /// <paramref name="roads"/> list for a lot with no adjacent road (the
        /// gallery standard quadrant) to get a plain uniform offset. Runs chain
        /// sideWallMidpoint → (connector) → side-run front end →
        /// (side run) → rear corner → (rear run) → rear corner →
        /// (side run) → side-run front end → (connector) → sideWallMidpoint,
        /// continuous with the front left open.
        /// </summary>
        public static IReadOnlyList<FenceRun> BackyardRuns(LotRect quadrantBounds, HouseModel model,
            GridPoint housePosition, GridPoint facing, float uniformScale, IReadOnlyList<Road> roads)
        {
            if (uniformScale <= 0f)
            {
                throw new ArgumentException("Uniform scale must be positive.", nameof(uniformScale));
            }

            if (roads == null)
            {
                throw new ArgumentNullException(nameof(roads));
            }

            var halfWidth = uniformScale * model.FootprintX / 2f;

            // Facing basis: f along the facing (front toward +f), p across it.
            var f = facing;
            var p = new GridPoint(-f.Z, f.X);

            // Road-aware offset boundary rectangle (#147 offset fix). A
            // quadrant edge that lies ON a road centerline sits inside the
            // paved street corridor, not on a yard boundary, so insetting it by
            // a plain BoundaryOffset would put the fence line in the road. Pull
            // every such edge off the sidewalk's OUTER edge first — mirroring
            // the tree-placement precedent in
            // LotBounds.ClearRoadCorridors (road edges derived generically
            // from the network, not hard-coded) — then inset EVERY edge by one
            // BoundaryOffset. The result: a sidewalk-width strip of grass
            // between the sidewalk and the fence on road-bordering edges, and
            // the same strip off the quadrant boundary on neighbour-yard / map
            // edges.
            var corridorCleared = LotBounds.ClearRoadCorridors(quadrantBounds, roads);
            var inset = new LotRect(
                corridorCleared.MinX + BoundaryOffset, corridorCleared.MaxX - BoundaryOffset,
                corridorCleared.MinZ + BoundaryOffset, corridorCleared.MaxZ - BoundaryOffset);

            // Project the inset corners onto the facing basis: the rear edge
            // is the least-along boundary; the two side edges are the cross
            // extremes.
            var corners = new[]
            {
                new GridPoint(inset.MinX, inset.MinZ),
                new GridPoint(inset.MinX, inset.MaxZ),
                new GridPoint(inset.MaxX, inset.MinZ),
                new GridPoint(inset.MaxX, inset.MaxZ),
            };

            var alongRear = float.PositiveInfinity;
            var crossLo = float.PositiveInfinity;
            var crossHi = float.NegativeInfinity;
            foreach (var corner in corners)
            {
                var along = Dot(corner, f);
                var cross = Dot(corner, p);
                if (along < alongRear)
                {
                    alongRear = along;
                }

                if (cross < crossLo)
                {
                    crossLo = cross;
                }

                if (cross > crossHi)
                {
                    crossHi = cross;
                }
            }

            // The side runs are truncated at the house's depth midpoint; the
            // connectors reach inward from there to the side-wall midpoints.
            var alongHouse = Dot(housePosition, f);
            var houseCross = Dot(housePosition, p);

            if (alongHouse - alongRear <= Epsilon)
            {
                throw new ArgumentException(
                    "The house must sit in front of the offset rear boundary.", nameof(housePosition));
            }

            var midLow = Reconstruct(f, alongHouse, p, houseCross - halfWidth);
            var midHigh = Reconstruct(f, alongHouse, p, houseCross + halfWidth);
            var sideFrontLow = Reconstruct(f, alongHouse, p, crossLo);
            var sideFrontHigh = Reconstruct(f, alongHouse, p, crossHi);
            var rearLow = Reconstruct(f, alongRear, p, crossLo);
            var rearHigh = Reconstruct(f, alongRear, p, crossHi);

            return new[]
            {
                new FenceRun(midLow, sideFrontLow),   // connector (perpendicular-inward)
                new FenceRun(sideFrontLow, rearLow),  // side run
                new FenceRun(rearLow, rearHigh),      // rear run tracing the offset boundary
                new FenceRun(rearHigh, sideFrontHigh), // side run
                new FenceRun(sideFrontHigh, midHigh), // connector (perpendicular-inward)
            };
        }

        /// <summary>
        /// The standard quadrant (<see cref="WorldDimensions.TileSize"/>/2
        /// per side) synthesised around a lot-free placement, matching how a
        /// real lot's quadrant sits relative to its house: cross-centred on
        /// the house, with the front edge
        /// <see cref="StandardQuadrantFrontEdgeAheadOfFacade"/> in front of
        /// the scaled facade.
        /// </summary>
        private static LotRect StandardQuadrantAround(HouseModel model, GridPoint housePosition,
            GridPoint facing, float uniformScale)
        {
            var half = WorldDimensions.TileSize / 4f;
            var halfDepth = uniformScale * model.FootprintZ / 2f;
            var frontEdgeAheadOfCenter = halfDepth + StandardQuadrantFrontEdgeAheadOfFacade;

            // Quadrant centre: back from the front edge by half a quadrant,
            // along the facing axis only (so it stays cross-centred on the
            // house).
            var centreAlongOffset = frontEdgeAheadOfCenter - half;
            var centre = new GridPoint(
                housePosition.X + facing.X * centreAlongOffset,
                housePosition.Z + facing.Z * centreAlongOffset);

            return new LotRect(centre.X - half, centre.X + half, centre.Z - half, centre.Z + half);
        }

        private static float Dot(GridPoint a, GridPoint unit)
        {
            return a.X * unit.X + a.Z * unit.Z;
        }

        /// <summary>Rebuild a world point from its projections onto the
        /// orthonormal cardinal basis {f, p}.</summary>
        private static GridPoint Reconstruct(GridPoint f, float along, GridPoint p, float cross)
        {
            return new GridPoint(f.X * along + p.X * cross, f.Z * along + p.Z * cross);
        }
    }
}
