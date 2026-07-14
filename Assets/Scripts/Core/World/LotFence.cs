using System;
using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>One straight fence line (#129): a segment of a lot's
    /// boundary rectangle, on the ground plane. Purely geometric — the
    /// tiling into kit pieces is <see cref="FenceTiling"/>'s job.</summary>
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
    /// Per-lot fence boundary geometry (#129): an axis-aligned square of
    /// <see cref="HalfExtent"/> around the lot center, with a
    /// <see cref="GateGapWidth"/> gate gap centered exactly where the lot's
    /// front walkway (#128) crosses the street-facing side — so the walkway
    /// always passes through the gate. Fencing is per-lot
    /// (<see cref="HouseLot.HasFence"/>, default on): a lot with the flag
    /// off contributes no fence geometry, which is the hook a later
    /// buyable-fence / house-upgrade design decision would use.
    /// </summary>
    public static class LotFence
    {
        /// <summary>
        /// Half the fence square's side, in meters from the lot center.
        /// Decision (#129, 2026-07-14): 7.5m — the widest sensible square.
        /// It must stay strictly inside the lot's street clearance
        /// (LotDistanceFromCenter 14 minus the sidewalk outer edge 5.75 =
        /// 8.25m, on BOTH streets of a corner lot; 7.5 leaves a 0.75m grass
        /// strip against the sidewalk) while containing every
        /// setback-shifted house: the #127 facade sits 5.5m street-side of
        /// the lot center (2m clearance) and the widest model at the fixed
        /// ×7 kit scale (#145, building-type-b, 12.80m) spans ±6.4m
        /// (1.1m clearance) — all test-enforced. Derek tunes it visually
        /// in the Editor check afterwards.
        /// </summary>
        public const float HalfExtent = 7.5f;

        /// <summary>
        /// Width of the gate gap the front walkway passes through.
        /// Decision (#129, 2026-07-14): the walkway's visual width
        /// (SidewalkWidth, 2m) plus 0.5m clearance per side, so the fence
        /// ends read as gate posts flanking the walkway instead of
        /// touching the pavers.
        /// </summary>
        public const float GateGapWidth = WorldDimensions.SidewalkWidth + 1f;

        private const float Epsilon = 0.001f;

        /// <summary>
        /// The fence lines for <paramref name="lot"/>: empty when the lot's
        /// fence is disabled; otherwise the four boundary sides, with the
        /// side the lot's front walkway crosses split around the gate gap
        /// (centered on the walkway line). A lot with no front walkway —
        /// which no starting lot is — gets the full ungated rectangle.
        /// Reads <see cref="NeighborhoodLayout.WalkNetwork"/> for the
        /// walkway, like <see cref="HousePlacement.Position"/> does; it is
        /// only ever called after the network is built.
        /// </summary>
        public static IReadOnlyList<FenceRun> RunsFor(HouseLot lot)
        {
            if (!lot.HasFence)
            {
                return Array.Empty<FenceRun>();
            }

            var minX = lot.Position.X - HalfExtent;
            var maxX = lot.Position.X + HalfExtent;
            var minZ = lot.Position.Z - HalfExtent;
            var maxZ = lot.Position.Z + HalfExtent;

            var hasGate = NeighborhoodLayout.WalkNetwork.TryGetFrontWalkway(lot.HouseId, out var walkway);

            var runs = new List<FenceRun>();

            // South and north sides (along X at constant Z), then west and
            // east sides (along Z at constant X).
            AddSide(runs, new GridPoint(minX, minZ), new GridPoint(maxX, minZ),
                hasGate && GateIsOnZSide(walkway, lot, minZ) ? walkway.A.X : (float?)null);
            AddSide(runs, new GridPoint(minX, maxZ), new GridPoint(maxX, maxZ),
                hasGate && GateIsOnZSide(walkway, lot, maxZ) ? walkway.A.X : (float?)null);
            AddSide(runs, new GridPoint(minX, minZ), new GridPoint(minX, maxZ),
                hasGate && GateIsOnXSide(walkway, lot, minX) ? walkway.A.Z : (float?)null);
            AddSide(runs, new GridPoint(maxX, minZ), new GridPoint(maxX, maxZ),
                hasGate && GateIsOnXSide(walkway, lot, maxX) ? walkway.A.Z : (float?)null);

            return runs;
        }

        /// <summary>True when the walkway (door → sidewalk) runs along Z
        /// and exits the rectangle through the side at
        /// <paramref name="sideZ"/>.</summary>
        private static bool GateIsOnZSide(WalkEdge walkway, HouseLot lot, float sideZ)
        {
            if (Math.Abs(walkway.A.X - walkway.B.X) > Epsilon)
            {
                return false; // walkway runs along X, crosses an X side
            }

            return Math.Sign(walkway.B.Z - lot.Position.Z) == Math.Sign(sideZ - lot.Position.Z);
        }

        /// <summary>True when the walkway runs along X and exits the
        /// rectangle through the side at <paramref name="sideX"/>.</summary>
        private static bool GateIsOnXSide(WalkEdge walkway, HouseLot lot, float sideX)
        {
            if (Math.Abs(walkway.A.X - walkway.B.X) <= Epsilon)
            {
                return false;
            }

            return Math.Sign(walkway.B.X - lot.Position.X) == Math.Sign(sideX - lot.Position.X);
        }

        /// <summary>One rectangle side from <paramref name="a"/> to
        /// <paramref name="b"/> (axis-aligned, min → max), whole — or split
        /// around the gate gap centered at <paramref name="gateCenter"/>
        /// (a coordinate on the side's own axis) when one is given.</summary>
        private static void AddSide(List<FenceRun> runs, GridPoint a, GridPoint b, float? gateCenter)
        {
            if (!gateCenter.HasValue)
            {
                runs.Add(new FenceRun(a, b));
                return;
            }

            var alongX = Math.Abs(a.Z - b.Z) <= Epsilon;
            var gapMin = gateCenter.Value - GateGapWidth / 2f;
            var gapMax = gateCenter.Value + GateGapWidth / 2f;

            var first = alongX ? new GridPoint(gapMin, a.Z) : new GridPoint(a.X, gapMin);
            var second = alongX ? new GridPoint(gapMax, a.Z) : new GridPoint(a.X, gapMax);

            if ((alongX ? first.X - a.X : first.Z - a.Z) > Epsilon)
            {
                runs.Add(new FenceRun(a, first));
            }

            if ((alongX ? b.X - second.X : b.Z - second.Z) > Epsilon)
            {
                runs.Add(new FenceRun(second, b));
            }
        }
    }
}
