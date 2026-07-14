using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #129: per-lot fence boundary geometry — a rectangle around each
    /// house lot with a gate gap exactly where the lot's front walkway
    /// (#128) crosses it. Fencing is a per-lot on/off flag on
    /// <see cref="HouseLot"/> (all four starting lots fenced today) so a
    /// later design pass can make fences a buyable decoration or
    /// house-level upgrade without reshaping this geometry.
    /// </summary>
    public class LotFenceTests
    {
        [Test]
        public void HouseLots_AllHaveFencesEnabledByDefault()
        {
            // Derek's #129 request: all four starting lots render fenced so
            // the Editor check shows the feature everywhere. The flag (not
            // the constant-on state) is the mechanism a later buyable-fence
            // decision would flip.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                Assert.That(lot.HasFence, Is.True,
                    $"lot {lot.HouseId} should be fenced by default");
            }
        }

        [Test]
        public void HasFence_CanBeDisabledPerLot()
        {
            var lot = new HouseLot(1, Quadrant.NorthEast, new GridPoint(14f, 14f), hasFence: false);
            Assert.That(lot.HasFence, Is.False);
        }

        [Test]
        public void RunsFor_LotWithFenceDisabled_ContributesNoFenceGeometry()
        {
            var lot = new HouseLot(1, Quadrant.NorthEast,
                NeighborhoodLayout.GetHouseLot(1).Position, hasFence: false);
            Assert.That(LotFence.RunsFor(lot), Is.Empty);
        }

        [Test]
        public void RunsFor_FormTheLotBoundary_WithAGapExactlyWhereTheWalkwayCrosses()
        {
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var runs = LotFence.RunsFor(lot);
                Assert.That(runs, Is.Not.Empty, $"lot {lot.HouseId} has no fence runs");

                var minX = lot.Position.X - LotFence.HalfExtent;
                var maxX = lot.Position.X + LotFence.HalfExtent;
                var minZ = lot.Position.Z - LotFence.HalfExtent;
                var maxZ = lot.Position.Z + LotFence.HalfExtent;

                foreach (var run in runs)
                {
                    AssertOnBoundary(run.A, minX, maxX, minZ, maxZ, lot.HouseId);
                    AssertOnBoundary(run.B, minX, maxX, minZ, maxZ, lot.HouseId);
                    Assert.That(
                        Math.Abs(run.A.X - run.B.X) < 0.001f || Math.Abs(run.A.Z - run.B.Z) < 0.001f,
                        Is.True, $"lot {lot.HouseId}: run must be axis-aligned");
                    Assert.That(
                        SameSideLine(run, minX, maxX, minZ, maxZ),
                        Is.True, $"lot {lot.HouseId}: run must lie on a single side of the rectangle");
                }

                // Together the runs cover the whole rectangle perimeter
                // except exactly one gate gap.
                var perimeter = 8f * LotFence.HalfExtent;
                Assert.That(runs.Sum(r => r.Length),
                    Is.EqualTo(perimeter - LotFence.GateGapWidth).Within(0.001f),
                    $"lot {lot.HouseId}: fence must cover the boundary minus one gate gap");

                // The walkway (#128) crosses the fence rectangle: door
                // inside, sidewalk attach outside.
                Assert.That(NeighborhoodLayout.WalkNetwork.TryGetFrontWalkway(lot.HouseId, out var walkway),
                    Is.True, $"lot {lot.HouseId} has no front walkway");
                Assert.That(IsStrictlyInside(walkway.A, minX, maxX, minZ, maxZ), Is.True,
                    $"lot {lot.HouseId}: the door must be inside the fence");
                Assert.That(IsStrictlyInside(walkway.B, minX, maxX, minZ, maxZ), Is.False,
                    $"lot {lot.HouseId}: the sidewalk attach must be outside the fence");

                var crossing = WalkwayBoundaryCrossing(walkway, minX, maxX, minZ, maxZ);

                // The gap is centered on the walkway line: no fence within
                // half a gate gap of the crossing point...
                foreach (var run in runs)
                {
                    Assert.That(DistanceToSegment(crossing, run),
                        Is.GreaterThanOrEqualTo(LotFence.GateGapWidth / 2f - 0.001f),
                        $"lot {lot.HouseId}: a fence run intrudes into the gate gap");
                }

                // ...and the fence resumes exactly at both gap edges (the
                // gap is no wider than GateGapWidth).
                foreach (var sign in new[] { -1f, 1f })
                {
                    var gapEdge = PointAlongGateSide(crossing, walkway, sign * LotFence.GateGapWidth / 2f);
                    Assert.That(runs.Any(r =>
                            PointsNearlyEqual(r.A, gapEdge) || PointsNearlyEqual(r.B, gapEdge)),
                        Is.True,
                        $"lot {lot.HouseId}: fence must resume exactly at the gap edge {sign}");
                }
            }
        }

        [Test]
        public void FenceRectangle_StaysWithinTheLot_AndClearOfEveryHouseFootprint()
        {
            // Street side: the fence must never reach the sidewalk. The
            // sidewalk's OUTER edge sits RoadWidth/2 + GrassVergeWidth +
            // SidewalkWidth from the road centerline, and lot centers sit
            // LotDistanceFromCenter out — a corner lot has a street on two
            // sides, so the half-extent must stay strictly inside that
            // clearance.
            var sidewalkOuterEdge = WorldDimensions.RoadWidth / 2f
                + WorldDimensions.GrassVergeWidth + WorldDimensions.SidewalkWidth;
            Assert.That(LotFence.HalfExtent,
                Is.LessThan(NeighborhoodLayout.LotDistanceFromCenter - sidewalkOuterEdge),
                "fence must stay strictly clear of the sidewalk");

            // Away sides: stay inside the starting 60m tile's quadrant.
            Assert.That(NeighborhoodLayout.LotDistanceFromCenter + LotFence.HalfExtent,
                Is.LessThanOrEqualTo(WorldDimensions.TileSize / 2f),
                "fence must stay inside the starting tile quadrant");

            // Every lot's setback-shifted house (#127) — its full scaled
            // #125 catalog footprint at the game's placement and yaw —
            // stays inside the fence with margin.
            const float requiredMargin = 0.5f;
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var model = HouseModelCatalog.ForHouse(lot.HouseId);
                var scale = HousePlacement.HouseTargetFootprint / model.MaxFootprint;
                var position = HousePlacement.Position(lot, HousePlacement.HouseTargetFootprint);
                var yaw = HousePlacement.ModelYawDegrees(HousePlacement.FrontFacing(lot));

                foreach (var corner in FootprintWorldCorners(model, position, yaw, scale))
                {
                    Assert.That(Math.Abs(corner.X - lot.Position.X),
                        Is.LessThanOrEqualTo(LotFence.HalfExtent - requiredMargin),
                        $"lot {lot.HouseId}: house corner {corner} too close to the fence (X)");
                    Assert.That(Math.Abs(corner.Z - lot.Position.Z),
                        Is.LessThanOrEqualTo(LotFence.HalfExtent - requiredMargin),
                        $"lot {lot.HouseId}: house corner {corner} too close to the fence (Z)");
                }
            }
        }

        private static IEnumerable<GridPoint> FootprintWorldCorners(
            HouseModel model, GridPoint position, float yawDegrees, float scale)
        {
            var radians = yawDegrees * Math.PI / 180.0;
            var cos = (float)Math.Cos(radians);
            var sin = (float)Math.Sin(radians);

            foreach (var signX in new[] { -1f, 1f })
            {
                foreach (var signZ in new[] { -1f, 1f })
                {
                    var localX = signX * model.FootprintX / 2f;
                    var localZ = signZ * model.FootprintZ / 2f;

                    // Unity yaw rotates +Z toward +X — the same convention
                    // as HouseModel.FrontDoorWorldPosition.
                    var rotatedX = localX * cos + localZ * sin;
                    var rotatedZ = -localX * sin + localZ * cos;

                    yield return new GridPoint(
                        position.X + scale * rotatedX,
                        position.Z + scale * rotatedZ);
                }
            }
        }

        private static void AssertOnBoundary(GridPoint point, float minX, float maxX, float minZ, float maxZ,
            int houseId)
        {
            var onXSide = (Math.Abs(point.X - minX) < 0.001f || Math.Abs(point.X - maxX) < 0.001f)
                && point.Z >= minZ - 0.001f && point.Z <= maxZ + 0.001f;
            var onZSide = (Math.Abs(point.Z - minZ) < 0.001f || Math.Abs(point.Z - maxZ) < 0.001f)
                && point.X >= minX - 0.001f && point.X <= maxX + 0.001f;
            Assert.That(onXSide || onZSide, Is.True,
                $"lot {houseId}: fence point {point} is not on the lot boundary rectangle");
        }

        private static bool SameSideLine(FenceRun run, float minX, float maxX, float minZ, float maxZ)
        {
            foreach (var side in new[] { minX, maxX })
            {
                if (Math.Abs(run.A.X - side) < 0.001f && Math.Abs(run.B.X - side) < 0.001f)
                {
                    return true;
                }
            }

            foreach (var side in new[] { minZ, maxZ })
            {
                if (Math.Abs(run.A.Z - side) < 0.001f && Math.Abs(run.B.Z - side) < 0.001f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsStrictlyInside(GridPoint point, float minX, float maxX, float minZ, float maxZ)
        {
            return point.X > minX + 0.001f && point.X < maxX - 0.001f
                && point.Z > minZ + 0.001f && point.Z < maxZ - 0.001f;
        }

        /// <summary>Where the axis-aligned walkway segment (door inside →
        /// sidewalk attach outside) crosses the rectangle boundary.</summary>
        private static GridPoint WalkwayBoundaryCrossing(WalkEdge walkway,
            float minX, float maxX, float minZ, float maxZ)
        {
            if (Math.Abs(walkway.A.X - walkway.B.X) < 0.001f)
            {
                // Runs along Z; crosses a Z side.
                var side = walkway.B.Z > walkway.A.Z ? maxZ : minZ;
                return new GridPoint(walkway.A.X, side);
            }

            var sideX = walkway.B.X > walkway.A.X ? maxX : minX;
            return new GridPoint(sideX, walkway.A.Z);
        }

        /// <summary>A point on the gate side's line, offset along the side
        /// (perpendicular to the walkway) from the crossing point.</summary>
        private static GridPoint PointAlongGateSide(GridPoint crossing, WalkEdge walkway, float offset)
        {
            return Math.Abs(walkway.A.X - walkway.B.X) < 0.001f
                ? new GridPoint(crossing.X + offset, crossing.Z)
                : new GridPoint(crossing.X, crossing.Z + offset);
        }

        private static float DistanceToSegment(GridPoint point, FenceRun run)
        {
            var abx = run.B.X - run.A.X;
            var abz = run.B.Z - run.A.Z;
            var lengthSquared = abx * abx + abz * abz;
            var t = lengthSquared < 0.000001f
                ? 0f
                : Math.Max(0f, Math.Min(1f,
                    ((point.X - run.A.X) * abx + (point.Z - run.A.Z) * abz) / lengthSquared));

            var dx = run.A.X + t * abx - point.X;
            var dz = run.A.Z + t * abz - point.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        private static bool PointsNearlyEqual(GridPoint a, GridPoint b)
        {
            return Math.Abs(a.X - b.X) < 0.001f && Math.Abs(a.Z - b.Z) < 0.001f;
        }
    }
}
