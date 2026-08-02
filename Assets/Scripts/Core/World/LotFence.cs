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
        /// #318: the fence lines a lot contributes given persisted world state:
        /// a lot is fenced when either its static <see cref="HouseLot.HasFence"/>
        /// flag is on OR a completed fence-purchase quest recorded a
        /// <see cref="Economy.ItemCatalog.FenceItemName"/> <see cref="PlacedItem"/>
        /// for that house. This is the flag-respecting API WorldBuilder consumes
        /// once fences can be bought — empty while a lot has neither source,
        /// otherwise <see cref="GeometryFor"/>.
        /// </summary>
        public static IReadOnlyList<FenceRun> RunsFor(HouseLot lot, GameState state)
        {
            return IsFenced(lot, state) ? GeometryFor(lot, state) : Array.Empty<FenceRun>();
        }

        /// <summary>
        /// #318: whether a lot's backyard fence should render given persisted
        /// state — its static <see cref="HouseLot.HasFence"/> flag OR a
        /// purchased fence recorded in <see cref="GameState.PlacedItems"/> for
        /// that house (the fence-purchase quest's completion effect). Additive:
        /// either source enables the fence.
        /// </summary>
        public static bool IsFenced(HouseLot lot, GameState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (lot.HasFence)
            {
                return true;
            }

            foreach (var item in state.PlacedItems)
            {
                if (item.HouseId == lot.HouseId
                    && item.ItemName == Economy.ItemCatalog.FenceItemName)
                {
                    return true;
                }
            }

            return false;
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
            // #223: a manual per-lot override replaces the model-derived
            // geometry verbatim. The override was validated as a continuous
            // open polyline at HouseLot construction, so returning it here
            // preserves the same continuous-runs invariant. With no override
            // (the shipping default) the auto-derivation below runs unchanged.
            if (lot.HasFenceOverride)
            {
                return lot.FenceOverride;
            }

            var model = HouseModelCatalog.ForHouse(lot.HouseId);
            var facing = HousePlacement.FrontFacing(lot);
            var position = HousePlacement.Position(lot, HousePlacement.KitScale);
            var quadrant = LotBounds.QuadrantBounds(lot);
            return BackyardRuns(quadrant, model, position, facing, HousePlacement.KitScale,
                NeighborhoodLayout.Roads);
        }

        /// <summary>
        /// #460: the lot's backyard fence geometry sized to the house's CURRENT
        /// upgrade level, so the two connectors reach the actual (upgraded) mesh's
        /// side-wall midpoints instead of staying pinned to the level-1 half-width
        /// (which lands inside a wider upgraded house — a collision — or falls
        /// short — a gap). It mirrors the level-blind <see cref="GeometryFor(HouseLot)"/>
        /// exactly but resolves the connector footprint through the level-aware
        /// <see cref="HouseModelCatalog.ForHouse(int, int)"/> (#454) at
        /// <see cref="GameState.GetHouseLevel"/>. ONLY the connector
        /// <c>halfWidth</c> changes: <see cref="HousePlacement.Position"/> /
        /// <see cref="HousePlacement.FrontFacing"/> stay on their existing
        /// (level-blind) resolution — house placement itself is #454/#462's
        /// territory — and the lot-based offset boundary rectangle is unaffected.
        /// A lot with a manual <see cref="HouseLot.FenceOverride"/> (#223) still
        /// returns the override verbatim. At
        /// <see cref="Doggiehood.Core.Art.HouseLevelModelTable.MinLevel"/>
        /// it is byte-identical to <see cref="GeometryFor(HouseLot)"/>.
        /// </summary>
        public static IReadOnlyList<FenceRun> GeometryFor(HouseLot lot, GameState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            // #461: resolve the fence's facing/position from the live map-spanning
            // GameState.WalkNetwork, not the starting-tile singleton — so a zone
            // house's fence rotates with the house's REAL street-ward facing
            // instead of the pre-rotation Z-sign fallback. #460's per-level
            // connector footprint (GetHouseLevel) rides the same call.
            // #509: the lot's live tile type rides too, so BackyardRuns
            // corridor-clears against the tile's OWN roads (LotBounds.RoadsFor),
            // not just the origin FourWay arms — the same map-spanning source the
            // facing/position resolve from. Derived from state.Map exactly like the
            // network and level above, so no per-lot road plumbing is needed.
            var tileType = state.Map.GetTileAt(LotBounds.NearestTileCoordinate(lot.Position));
            return GeometryFrom(
                lot, state.WalkNetwork, state.GetHouseLevel(lot.HouseId), tileType);
        }

        /// <summary>
        /// #461: the lot's backyard fence geometry oriented to the facing/position
        /// resolved from an explicit map-spanning <paramref name="network"/> (via
        /// <see cref="HousePlacement.PredeterminedFrontFacing"/>/
        /// <see cref="HousePlacement.PredeterminedPosition"/>) rather than the
        /// starting-tile singleton. Used by the yard-tree pre-bake (#434/#450),
        /// which rejection-samples back candidates against the fence line before
        /// the house is built: the network still knows the lot's tile sidewalks,
        /// so the fence orients to the house's real (predetermined) facing.
        /// #509: <paramref name="tileType"/> (the caller's own tile) feeds
        /// <see cref="LotBounds.RoadsFor"/>, so the fence corridor-clears against
        /// the lot's own tile roads rather than only the origin arms.
        /// Level-blind (level 1), like the queryable
        /// <see cref="GeometryFor(HouseLot)"/>; the level-aware fence tracking is
        /// <see cref="GeometryFor(HouseLot, GameState)"/>'s job. A manual
        /// <see cref="HouseLot.FenceOverride"/> (#223) still returns verbatim.
        /// </summary>
        public static IReadOnlyList<FenceRun> GeometryFor(HouseLot lot, WalkNetwork network, TileType tileType)
        {
            return GeometryFrom(lot, network, Doggiehood.Core.Art.HouseLevelModelTable.MinLevel, tileType);
        }

        /// <summary>
        /// #461: shared builder behind the network/state-aware
        /// <see cref="GeometryFor(HouseLot, GameState)"/> and
        /// <see cref="GeometryFor(HouseLot, WalkNetwork, TileType)"/> — traces the
        /// five-run backyard shape with the connector footprint at
        /// <paramref name="level"/> and the facing/position resolved from
        /// <paramref name="network"/>. #509: the road-corridor clear uses
        /// <see cref="LotBounds.RoadsFor"/> for <paramref name="tileType"/> — the
        /// origin FourWay arms PLUS the lot's own tile's roads — so a
        /// road-bordering edge on an expansion tile (whose road is absent from
        /// <see cref="NeighborhoodLayout.Roads"/>) is corridor-cleared and the
        /// fence stays out of the paved road.
        /// </summary>
        private static IReadOnlyList<FenceRun> GeometryFrom(
            HouseLot lot, WalkNetwork network, int level, TileType tileType)
        {
            if (lot.HasFenceOverride)
            {
                return lot.FenceOverride;
            }

            var model = HouseModelCatalog.ForHouse(lot.HouseId, level);
            var facing = HousePlacement.PredeterminedFrontFacing(lot, network);
            var position = HousePlacement.PredeterminedPosition(
                lot, HousePlacement.KitScale, network, Doggiehood.Core.Art.HouseLevelModelTable.MinLevel);
            var quadrant = LotBounds.QuadrantBounds(lot);
            return BackyardRuns(quadrant, model, position, facing, HousePlacement.KitScale,
                LotBounds.RoadsFor(lot, tileType));
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

        /// <summary>
        /// #223: guards a manual per-lot fence override
        /// (<see cref="HouseLot.FenceOverride"/>) before it replaces the
        /// auto-derived geometry. Mirroring <see cref="BackyardRuns(LotRect, HouseModel, GridPoint, GridPoint, float, IReadOnlyList{Road})"/>'s
        /// argument guards, the override must be a continuous open polyline —
        /// a non-empty chain of runs where each run connects to the next with
        /// no gap — so an override preserves the same continuous-runs
        /// invariant the auto-derived geometry guarantees (the shape is
        /// count-agnostic: it may carry the current five-run backyard geometry
        /// or any other continuous run of side/rear anchors). Throws
        /// <see cref="ArgumentException"/> on a null/empty override or a gap
        /// between consecutive runs.
        /// </summary>
        public static void ValidateFenceOverride(IReadOnlyList<FenceRun> runs)
        {
            if (runs == null)
            {
                throw new ArgumentNullException(nameof(runs));
            }

            if (runs.Count == 0)
            {
                throw new ArgumentException(
                    "A fence override must contain at least one run.", nameof(runs));
            }

            for (var i = 0; i < runs.Count - 1; i++)
            {
                if (!SharesEndpoint(runs[i], runs[i + 1]))
                {
                    throw new ArgumentException(
                        "A fence override must be a continuous chain of runs with no gap "
                        + $"(run {i} does not connect to run {i + 1}).", nameof(runs));
                }
            }
        }

        /// <summary>Whether two runs meet at a shared endpoint (within
        /// <see cref="Epsilon"/>) — the no-gap continuity test #223's override
        /// validation applies to consecutive runs.</summary>
        private static bool SharesEndpoint(FenceRun a, FenceRun b)
        {
            return NearlyEqual(a.B, b.A) || NearlyEqual(a.B, b.B)
                || NearlyEqual(a.A, b.A) || NearlyEqual(a.A, b.B);
        }

        private static bool NearlyEqual(GridPoint a, GridPoint b)
        {
            return Math.Abs(a.X - b.X) < Epsilon && Math.Abs(a.Z - b.Z) < Epsilon;
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
