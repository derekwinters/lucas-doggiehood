using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #599: when the only reachable off-map opening is the entry (a spur or
    /// cul-de-sac), the truck turns around and retraces. For v1 the turnaround
    /// is a simple in-place reorient — but it is driven by a named, tunable
    /// radius so it can later swing around an arc (e.g. following the cul-de-sac
    /// bulb loop) without touching the routing logic.
    /// </summary>
    public class TruckTurnaroundTests
    {
        [Test]
        public void InPlaceReorientRadius_IsZero_AndIsANamedConstant()
        {
            Assert.That(TruckTurnaround.InPlaceReorientRadius, Is.EqualTo(0f));
        }

        [Test]
        public void RequiresManeuver_IsFalseInPlace_TrueForAPositiveRadius()
        {
            Assert.That(TruckTurnaround.RequiresManeuver(TruckTurnaround.InPlaceReorientRadius), Is.False);
            Assert.That(TruckTurnaround.RequiresManeuver(8f), Is.True);
        }

        [Test]
        public void Waypoints_InPlace_IsJustThePivot_NoLateralDisplacement()
        {
            var pivot = new GridPoint(0f, 45f);
            var heading = new GridPoint(0f, 1f);

            var waypoints = TruckTurnaround.Waypoints(pivot, heading, TruckTurnaround.InPlaceReorientRadius);

            Assert.That(waypoints.Count, Is.EqualTo(1));
            Assert.That(waypoints[0].X, Is.EqualTo(pivot.X).Within(0.0001f));
            Assert.That(waypoints[0].Z, Is.EqualTo(pivot.Z).Within(0.0001f));
        }

        [Test]
        public void Waypoints_PositiveRadius_SwingsOffThePivot()
        {
            var pivot = new GridPoint(0f, 45f);
            var heading = new GridPoint(0f, 1f);

            var waypoints = TruckTurnaround.Waypoints(pivot, heading, 8f);

            Assert.That(waypoints.Count, Is.GreaterThan(1),
                "a positive radius must produce a swept arc, proving the seam is tunable");
            Assert.That(
                waypoints.Any(p => System.Math.Abs(p.X - pivot.X) > 0.5f),
                Is.True,
                "the arc must displace off the pivot's centerline");
        }
    }
}
