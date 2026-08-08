using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #673: right-of-way is scoped to the whole MANOEUVRE through an
    /// intersection, not to one leg of it. A vehicle acquires every crosswalk
    /// band its pass through an intersection will cross — all-or-nothing —
    /// before it enters the first one, so it can never strand itself in the
    /// middle of the box waiting on a band it never checked.
    ///
    /// The bug these tests exist for: the truck drove into the centre of a
    /// four-way, turned, and only THEN looked at the crosswalk on its outgoing
    /// leg. A dog held it, so the truck stopped dead in the middle of the
    /// intersection — holding nothing, because reaching the turn waypoint had
    /// released the claims from the leg it had just finished.
    /// </summary>
    public class RoadManoeuvreTests
    {
        private const float HalfCrosswalk = 3f / 2f; // WorldDimensions.CrosswalkWidth / 2

        // The starting intersection's bands sit at along = +/-4.75 on both
        // roads (the crossing road's sidewalk-centre offset).
        private const float BandAlong = 4.75f;

        // The intersection BOX: everything between the two bands' outer (near)
        // edges. "Outside the intersection entirely" means clear of this.
        private const float BoxEdge = BandAlong + HalfCrosswalk; // 6.25

        private const float RoadEnd = 30f;

        // A drive is stepped in fixed increments, so a vehicle that stops
        // making progress simply stays put and the step budget runs out —
        // which is exactly what a deadlock looks like.
        private const float DriveStep = 0.1f;
        private const int DriveStepBudget = 5000;
        private const float ArrivalTolerance = 0.001f;

        // The real truck's own figures (#660 pins them), so every case below
        // runs at the geometry the game actually ships rather than at
        // hand-picked numbers (#161).
        private static readonly float TruckBody = DeliveryTruckFootprint.NominalBodyLength;
        private static readonly float TruckFront = DeliveryTruckFootprint.FrontSetbackFor(TruckBody);
        private static readonly float TruckRear = DeliveryTruckFootprint.RearSetbackFor(TruckBody);
        private static readonly float TruckHalfBody = TruckBody / 2f;

        private static IReadOnlyList<Road> Roads => NeighborhoodLayout.Roads;

        private static WalkNetwork Network => NeighborhoodLayout.WalkNetwork;

        /// <summary>North -> centre -> east: the reported manoeuvre, and a LEFT
        /// turn (heading south, leaving east). The truck enters southbound on the
        /// north-south road, turns at the origin, and leaves eastbound on the
        /// east-west road. #672 flagged the left turn specifically: its new leg
        /// can start up to a lane offset deeper into the intersection, which used
        /// to narrow the yield margin. Here it cannot matter — the truck does not
        /// enter at all until the whole turn is clear.</summary>
        private static IReadOnlyList<GridPoint> NorthToEastTurn()
        {
            return new[]
            {
                new GridPoint(0f, RoadEnd),
                new GridPoint(0f, 0f),
                new GridPoint(RoadEnd, 0f),
            };
        }

        /// <summary>North -> centre -> south: straight through the four-way,
        /// which crosses TWO bands just like a turn does.</summary>
        private static IReadOnlyList<GridPoint> StraightThrough()
        {
            return new[]
            {
                new GridPoint(0f, RoadEnd),
                new GridPoint(0f, 0f),
                new GridPoint(0f, -RoadEnd),
            };
        }

        /// <summary>North -> centre -> west: the mirror RIGHT turn, which crosses
        /// a DIFFERENT band pair (the west band, not the east one) — the
        /// distinction #672 introduced when it put vehicles in lanes.</summary>
        private static IReadOnlyList<GridPoint> NorthToWestTurn()
        {
            return new[]
            {
                new GridPoint(0f, RoadEnd),
                new GridPoint(0f, 0f),
                new GridPoint(-RoadEnd, 0f),
            };
        }

        /// <summary>East -> centre -> north: a second approach to the same
        /// intersection, for the two-vehicle cases.</summary>
        private static IReadOnlyList<GridPoint> EastToNorthTurn()
        {
            return new[]
            {
                new GridPoint(RoadEnd, 0f),
                new GridPoint(0f, 0f),
                new GridPoint(0f, RoadEnd),
            };
        }

        private static WalkEdge BandAt(GridPoint midpoint)
        {
            return Network.Edges.Single(e =>
                e.Kind == WalkEdgeKind.Crosswalk
                && Math.Abs(((e.A.X + e.B.X) / 2f) - midpoint.X) < 0.01f
                && Math.Abs(((e.A.Z + e.B.Z) / 2f) - midpoint.Z) < 0.01f);
        }

        private static WalkEdge NorthBand => BandAt(new GridPoint(0f, BandAlong));

        private static WalkEdge SouthBand => BandAt(new GridPoint(0f, -BandAlong));

        private static WalkEdge EastBand => BandAt(new GridPoint(BandAlong, 0f));

        [Test]
        public void ATurnWhoseOutgoingBandIsHeld_LeavesTheTruckOutsideTheIntersectionEntirely()
        {
            // THE reported bug. A dog holds the crosswalk on the OUTGOING leg
            // of the turn. Per-leg right-of-way never looked at that band until
            // the truck had already driven to the turn point, so it stopped in
            // the middle of the box. The manoeuvre is one unit: the truck must
            // work this out before it enters the first band at all.
            var gate = new RoadCrossingGate();
            var dog = new object();
            Assert.That(gate.TryEnter(EastBand, dog), Is.True);

            var truck = new ManoeuvreDrive(gate, Roads, Network, NorthToEastTurn(), TruckFront, TruckRear);
            truck.Drive(DriveStepBudget, DriveStep);

            Assert.That(truck.Finished, Is.False,
                "the truck cannot complete the turn while a dog holds its outgoing crosswalk");

            // Southbound, the leading edge is half a body ahead in -Z.
            var leadingEdgeZ = truck.Position.Z - TruckHalfBody;
            Assert.That(leadingEdgeZ, Is.GreaterThanOrEqualTo(BoxEdge - ArrivalTolerance),
                $"the truck came to rest at z={truck.Position.Z} — its front reached {leadingEdgeZ}, "
                + $"inside the intersection box (outer edge {BoxEdge}). It must wait BEHIND the first "
                + "band, fully outside the intersection.");
        }

        [Test]
        public void ARightTurn_CrossesTheOTHERBandPair_AndIsGatedOnThatOne()
        {
            // A left and a right turn out of the same approach cross different
            // outgoing bands, so the manoeuvre has to be resolved from the ROUTE
            // rather than from the road. A dog on the EAST band must not hold up
            // a truck turning WEST — and one on the WEST band must.
            var westBand = BandAt(new GridPoint(-BandAlong, 0f));

            var unaffectedGate = new RoadCrossingGate();
            Assert.That(unaffectedGate.TryEnter(EastBand, new object()), Is.True);
            var turningWest = new ManoeuvreDrive(
                unaffectedGate, Roads, Network, NorthToWestTurn(), TruckFront, TruckRear);
            turningWest.Drive(DriveStepBudget, DriveStep);
            Assert.That(turningWest.Finished, Is.True,
                "a dog on the east band is not on this truck's manoeuvre at all");

            var blockedGate = new RoadCrossingGate();
            Assert.That(blockedGate.TryEnter(westBand, new object()), Is.True);
            var blocked = new ManoeuvreDrive(
                blockedGate, Roads, Network, NorthToWestTurn(), TruckFront, TruckRear);
            blocked.Drive(DriveStepBudget, DriveStep);
            Assert.That(blocked.Finished, Is.False, "a dog on the west band is");
            Assert.That(blocked.Position.Z - TruckHalfBody, Is.GreaterThanOrEqualTo(BoxEdge - ArrivalTolerance),
                "and it waits outside the intersection, same as for a left turn");
        }

        [Test]
        public void AStraightThroughCrossingWhoseFarBandIsHeld_AlsoStopsBeforeTheNearBand()
        {
            // Derek's answer (2026-08-07): one rule for both. A four-way has two
            // bands on a straight run too, so "don't block the box" applies
            // there as well — there is no "is this a turn?" predicate anywhere.
            var gate = new RoadCrossingGate();
            var dog = new object();
            Assert.That(gate.TryEnter(SouthBand, dog), Is.True);

            var truck = new ManoeuvreDrive(gate, Roads, Network, StraightThrough(), TruckFront, TruckRear);
            truck.Drive(DriveStepBudget, DriveStep);

            Assert.That(truck.Finished, Is.False,
                "the truck cannot cross while a dog holds the far band");
            Assert.That(truck.Position.Z - TruckHalfBody, Is.GreaterThanOrEqualTo(BoxEdge - ArrivalTolerance),
                "a straight-through crossing must also stop before the NEAR band, never between the two");
        }

        [Test]
        public void WhenAnyBandIsUnavailable_TheTruckHoldsNone_SoAThirdOccupantMayTakeTheFirst()
        {
            // All-or-nothing: a partial acquire is hold-and-wait, which is the
            // ingredient a deadlock needs.
            var gate = new RoadCrossingGate();
            var dog = new object();
            Assert.That(gate.TryEnter(EastBand, dog), Is.True);

            var truck = new ManoeuvreDrive(gate, Roads, Network, NorthToEastTurn(), TruckFront, TruckRear);
            truck.Drive(DriveStepBudget, DriveStep);

            var thirdParty = new object();
            Assert.That(gate.TryEnter(NorthBand, thirdParty), Is.True,
                "a blocked truck must be holding NO bands at all — it may not sit on the incoming "
                + "band while waiting for the outgoing one");
        }

        [Test]
        public void OnceEveryBandIsFree_TheManoeuvreIsTakenTogether_AndDrivenWithoutStopping()
        {
            var gate = new RoadCrossingGate();
            var truck = new ManoeuvreDrive(gate, Roads, Network, NorthToEastTurn(), TruckFront, TruckRear);

            // Step until the truck first commits to the intersection (its
            // leading edge crosses the box edge), then check it holds BOTH.
            var committed = false;
            for (var step = 0; step < DriveStepBudget && !committed; step++)
            {
                truck.Step(DriveStep);
                committed = truck.Position.Z - TruckHalfBody < BoxEdge - ArrivalTolerance;
            }

            Assert.That(committed, Is.True, "the truck should have entered the intersection");
            Assert.That(gate.TryEnter(NorthBand, new object()), Is.False,
                "on entering, the truck holds the incoming band");
            Assert.That(gate.TryEnter(EastBand, new object()), Is.False,
                "and it holds the OUTGOING band too — acquired before it entered, not after it turned");

            truck.Drive(DriveStepBudget, DriveStep);
            Assert.That(truck.Finished, Is.True, "with both bands free the truck drives the whole manoeuvre");
        }

        [Test]
        public void TheClaimSetIsReleasedOnlyOnceTheTailClearsTheFINALBandOfTheManoeuvre()
        {
            // #658's rule, carried across a turn: the release is driven by "my
            // tail has cleared the last band of this manoeuvre", never by "I
            // reached a waypoint".
            var gate = new RoadCrossingGate();
            var truck = new ManoeuvreDrive(gate, Roads, Network, NorthToEastTurn(), TruckFront, TruckRear);

            // Drive to the turn point itself. Reaching it must NOT release
            // anything — that release is the bug (DeliveryTruckView.cs:363).
            for (var step = 0; step < DriveStepBudget && truck.Position.Z > ArrivalTolerance; step++)
            {
                truck.Step(DriveStep);
            }

            Assert.That(gate.TryEnter(NorthBand, new object()), Is.False,
                "arriving at the turn waypoint must not release the incoming band");
            Assert.That(gate.TryEnter(EastBand, new object()), Is.False,
                "nor the outgoing one");

            // The final band of the manoeuvre is the east band; its far edge is
            // at x = 4.75 + 1.5, and the tail trails a rear setback behind.
            var releaseX = BandAlong + HalfCrosswalk + TruckRear;
            for (var step = 0; step < DriveStepBudget && truck.Position.X < releaseX - DriveStep; step++)
            {
                truck.Step(DriveStep);
                Assert.That(gate.TryEnter(NorthBand, new object()), Is.False,
                    $"the manoeuvre must stay held until the tail clears the FINAL band (x={truck.Position.X})");
            }

            truck.Drive(DriveStepBudget, DriveStep);
            Assert.That(gate.TryEnter(NorthBand, new object()), Is.True,
                "once the tail clears the final band the whole set releases at once");
            Assert.That(gate.TryEnter(EastBand, new object()), Is.True,
                "every band of the set, together");
        }

        [Test]
        public void TwoTrucksTurningThroughOneIntersection_BothClear_AndNeitherDeadlocks()
        {
            // The hazard all-or-nothing acquisition is bought to avoid: two
            // vehicles each holding half of the intersection and waiting on the
            // other. With release-on-failure there is no hold-and-wait, so one
            // wins outright and the other follows it through.
            var gate = new RoadCrossingGate();
            var fromNorth = new ManoeuvreDrive(gate, Roads, Network, NorthToEastTurn(), TruckFront, TruckRear);
            var fromEast = new ManoeuvreDrive(gate, Roads, Network, EastToNorthTurn(), TruckFront, TruckRear);

            DriveTogether(DriveStepBudget, DriveStep, fromNorth, fromEast);

            Assert.That(fromNorth.Finished, Is.True,
                $"the northbound-entering truck wedged at {fromNorth.Position.X},{fromNorth.Position.Z}");
            Assert.That(fromEast.Finished, Is.True,
                $"the eastbound-entering truck wedged at {fromEast.Position.X},{fromEast.Position.Z}");
        }

        [Test]
        public void TwoCollidingTrucks_ResolveInAFixedOrder_SoTheyCannotRetryInLockstepForever()
        {
            // Livelock is the residual hazard once hold-and-wait is gone: two
            // vehicles retrying in step, each releasing what the other needs.
            // Bands are claimed in ONE global order (by band identity), so the
            // same vehicle wins every time rather than the pair trading.
            var firstRunWinner = FirstThroughTheIntersection();
            var secondRunWinner = FirstThroughTheIntersection();

            Assert.That(firstRunWinner, Is.Not.Null, "one of the two trucks must get through");
            Assert.That(secondRunWinner, Is.EqualTo(firstRunWinner),
                "the same approach must win both times — a tie broken by timing can livelock");
        }

        [Test]
        public void ADogHoldingOneBand_NeverDeadlocksAgainstATurningTruck()
        {
            var gate = new RoadCrossingGate();
            var dog = new object();
            Assert.That(gate.TryEnter(EastBand, dog), Is.True);

            var truck = new ManoeuvreDrive(gate, Roads, Network, NorthToEastTurn(), TruckFront, TruckRear);
            truck.Drive(DriveStepBudget, DriveStep);
            Assert.That(truck.Finished, Is.False, "held up by the dog, as it should be");

            gate.Exit(EastBand, dog);
            truck.Drive(DriveStepBudget, DriveStep);

            Assert.That(truck.Finished, Is.True,
                "once the dog clears, the truck completes the whole manoeuvre — no deadlock");
        }

        [Test]
        public void TheFrontAndRearSetbackBound_StillHolds_WhenAManoeuvreSpansTwoBands()
        {
            // #660's budget is unchanged by this issue: the truck still has to
            // fit in the clear roadway between an intersection's two bands.
            Assert.That(DeliveryTruckFootprint.FitsBetweenCrosswalkBands(TruckBody), Is.True);

            var gate = new RoadCrossingGate();
            var truck = new ManoeuvreDrive(gate, Roads, Network, StraightThrough(), TruckFront, TruckRear);
            truck.Drive(DriveStepBudget, DriveStep);

            Assert.That(truck.Finished, Is.True,
                "a truck inside the #660 budget drives a two-band manoeuvre end to end");
            Assert.That(gate.TryEnter(NorthBand, new object()), Is.True, "holding nothing afterwards");
            Assert.That(gate.TryEnter(SouthBand, new object()), Is.True, "holding nothing afterwards");
        }

        [Test]
        public void ASingleBandCrossing_BehavesExactlyAsBefore_TheRegressionGuard()
        {
            // A Tee's lone arm is a genuine one-band crossing: nothing to group,
            // so #546/#639/#658 must be untouched there — stop with the bumper at
            // the near edge, release the moment the tail clears the far edge.
            var map = new TileMap(new TileCoordinate(0, 0), TileType.TeeNorth);
            var roads = MapWalkNetwork.RoadsFrom(map);
            var network = MapWalkNetwork.BuildFrom(map, new HouseLot[0]);
            var arm = roads.Single(r =>
                r.Orientation == StreetOrientation.NorthSouth
                && RoadManoeuvre.BandsOn(r, network).Count == 1);
            var band = RoadManoeuvre.BandsOn(arm, network).Single();

            var gate = new RoadCrossingGate();
            var dog = new object();
            Assert.That(gate.TryEnter(band.Edge, dog), Is.True);

            var truck = new object();
            var traversal = new RoadCrossingTraversal(
                gate, truck, arm, network, arm.HalfLength, -arm.HalfLength, TruckFront, TruckRear);

            // Blocked: the bumper comes to rest a front setback short of the
            // band's near edge, exactly as before.
            var nearEdge = band.Along + HalfCrosswalk; // approached driving -along
            var stopped = traversal.Advance(arm.HalfLength, -arm.HalfLength);
            Assert.That(stopped, Is.EqualTo(nearEdge + TruckFront).Within(ArrivalTolerance),
                "a one-band crossing still stops at the #639 boundary");

            // Released: the moment the tail clears THAT band's far edge — there
            // is no later band to wait for.
            gate.Exit(band.Edge, dog);
            traversal.Advance(stopped, -arm.HalfLength);
            var release = band.Along - HalfCrosswalk - TruckRear;
            traversal.Advance(release + ArrivalTolerance * 100f, -arm.HalfLength);
            Assert.That(gate.TryEnter(band.Edge, new object()), Is.False,
                "still held a hair before the tail clears");
            traversal.Advance(release, -arm.HalfLength);
            Assert.That(gate.TryEnter(band.Edge, new object()), Is.True,
                "a one-band crossing releases at the #658 point, unchanged");
        }

        [Test]
        public void ABandCrossedBetweenTwoNonJunctionWaypoints_IsStillClaimedFirstCome()
        {
            // Scoping right-of-way to intersections must not NARROW what gets
            // claimed. A leg whose ends are both ordinary points (an opening and
            // a stop, say) can still drive over a band, and that band is still a
            // first-come claim — as its own one-band manoeuvre.
            var waypoints = new[] { new GridPoint(0f, RoadEnd), new GridPoint(0f, 2f) };

            var gate = new RoadCrossingGate();
            var dog = new object();
            Assert.That(gate.TryEnter(NorthBand, dog), Is.True);

            var truck = new ManoeuvreDrive(gate, Roads, Network, waypoints, TruckFront, TruckRear);
            truck.Drive(DriveStepBudget, DriveStep);

            Assert.That(truck.Finished, Is.False,
                "the truck must still yield to a dog on a band no intersection manoeuvre covers");
            Assert.That(truck.Position.Z - TruckHalfBody,
                Is.GreaterThanOrEqualTo(BandAlong + HalfCrosswalk - ArrivalTolerance),
                "and it stops with its bumper at that band's near edge, as it always did");
        }

        [Test]
        public void ARouteThatPassesOneIntersectionTwice_YieldsCorrectlyOnBOTHPasses()
        {
            // A cul-de-sac retrace comes back through the crossing it went out
            // by, crossing the same two bands in the OPPOSITE order. Each pass is
            // its own manoeuvre, so each releases at the band it leaves by — not
            // at the one it entered by, which would drop the claim mid-box.
            var outAndBack = new[]
            {
                new GridPoint(0f, RoadEnd),
                new GridPoint(0f, 0f),
                new GridPoint(-RoadEnd, 0f),
                new GridPoint(0f, 0f),
                new GridPoint(0f, RoadEnd),
            };

            var gate = new RoadCrossingGate();
            var truck = new ManoeuvreDrive(gate, Roads, Network, outAndBack, TruckFront, TruckRear);

            // Drive out west through the crossing, far enough that the outbound
            // manoeuvre's tail is clear of its final band.
            var clearOfTheCrossing = -(BoxEdge + TruckRear + 1f);
            for (var step = 0; step < DriveStepBudget && truck.Position.X > clearOfTheCrossing; step++)
            {
                truck.Step(DriveStep);
            }

            Assert.That(truck.Position.X, Is.LessThan(-BoxEdge),
                "the truck should have driven out through the crossing unobstructed");

            // Now a dog takes the band the RETURN pass will leave by.
            var dog = new object();
            Assert.That(gate.TryEnter(NorthBand, dog), Is.True,
                "the outbound pass released the whole set once it was clear of the far side");

            truck.Drive(DriveStepBudget, DriveStep);
            Assert.That(truck.Finished, Is.False, "the return pass is blocked by the dog");
            Assert.That(truck.Position.X, Is.LessThanOrEqualTo(-(BoxEdge + TruckHalfBody) + ArrivalTolerance),
                "and it waits OUTSIDE the intersection on the way back, not inside it");
        }

        [Test]
        public void AManoeuvresClaimOrder_IsTheSameWhicheverWayItsBandsAreListed()
        {
            // The deterministic tie-break, at the unit level: two vehicles
            // meeting the same bands in opposite route order still ATTEMPT them
            // in the same order, so one of them always wins outright.
            var forward = new RoadManoeuvre(new[] { NorthBand, EastBand });
            var backward = new RoadManoeuvre(new[] { EastBand, NorthBand });

            Assert.That(forward.ClaimOrder.Select(Midpoint).ToArray(),
                Is.EqualTo(backward.ClaimOrder.Select(Midpoint).ToArray()),
                "the claim order is a property of the bands, not of the route through them");
            Assert.That(forward.Bands.Select(Midpoint).ToArray(),
                Is.Not.EqualTo(backward.Bands.Select(Midpoint).ToArray()),
                "while the ROUTE order still differs — that is what decides which band is released last");
        }

        private static string Midpoint(WalkEdge edge)
        {
            return $"{(edge.A.X + edge.B.X) / 2f:0.00},{(edge.A.Z + edge.B.Z) / 2f:0.00}";
        }

        /// <summary>Runs the two-approach collision and reports which route got
        /// through the intersection first, or null if neither did.</summary>
        private static string FirstThroughTheIntersection()
        {
            var gate = new RoadCrossingGate();
            var fromNorth = new ManoeuvreDrive(gate, Roads, Network, NorthToEastTurn(), TruckFront, TruckRear);
            var fromEast = new ManoeuvreDrive(gate, Roads, Network, EastToNorthTurn(), TruckFront, TruckRear);

            for (var step = 0; step < DriveStepBudget; step++)
            {
                fromNorth.Step(DriveStep);
                fromEast.Step(DriveStep);

                if (fromNorth.Position.Z - TruckHalfBody < BoxEdge - ArrivalTolerance)
                {
                    return "north";
                }

                if (fromEast.Position.X - TruckHalfBody < BoxEdge - ArrivalTolerance)
                {
                    return "east";
                }
            }

            return null;
        }

        private static void DriveTogether(int budget, float step, params ManoeuvreDrive[] drives)
        {
            for (var i = 0; i < budget; i++)
            {
                var allFinished = true;
                foreach (var drive in drives)
                {
                    drive.Step(step);
                    allFinished &= drive.Finished;
                }

                if (allFinished)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// A Core stand-in for the way <c>DeliveryTruckView</c> drives: a
        /// waypoint route, one <see cref="RoadCrossingTraversal"/> per leg, and
        /// a hand-off at each waypoint. It exists because the bug is precisely a
        /// BETWEEN-legs bug — a single-leg traversal can't express it.
        /// </summary>
        private sealed class ManoeuvreDrive
        {
            private readonly RoadCrossingGate gate;
            private readonly IReadOnlyList<Road> roads;
            private readonly WalkNetwork network;
            private readonly IReadOnlyList<GridPoint> waypoints;
            private readonly RouteManoeuvres manoeuvres;
            private readonly float frontSetback;
            private readonly float rearSetback;
            private readonly object occupant = new object();

            private RoadCrossingTraversal crossing;
            private RoadLeg leg;
            private bool onRoad;
            private int targetIndex = 1;

            public ManoeuvreDrive(
                RoadCrossingGate gate, IReadOnlyList<Road> roads, WalkNetwork network,
                IReadOnlyList<GridPoint> waypoints, float frontSetback, float rearSetback)
            {
                this.gate = gate;
                this.roads = roads;
                this.network = network;
                this.waypoints = waypoints;
                this.frontSetback = frontSetback;
                this.rearSetback = rearSetback;
                manoeuvres = RouteManoeuvres.Resolve(roads, network, waypoints);
                Position = waypoints[0];
                BeginLeg();
            }

            public GridPoint Position { get; private set; }

            public bool Finished { get; private set; }

            public void Drive(int budget, float step)
            {
                for (var i = 0; i < budget && !Finished; i++)
                {
                    Step(step);
                }
            }

            public void Step(float step)
            {
                if (Finished)
                {
                    return;
                }

                if (!onRoad)
                {
                    Position = waypoints[targetIndex];
                    AdvanceWaypoint();
                    return;
                }

                var currentAlong = leg.Road.AlongAxis(Position);
                var stepped = currentAlong + (leg.TravelSign * step);
                var desired = leg.TravelSign > 0f
                    ? Math.Min(stepped, leg.ExitAlong)
                    : Math.Max(stepped, leg.ExitAlong);
                var allowed = crossing == null ? desired : crossing.Advance(currentAlong, desired);
                Position = leg.Road.PointAt(allowed, 0f);

                if (Math.Abs(allowed - leg.ExitAlong) <= ArrivalTolerance)
                {
                    AdvanceWaypoint();
                }
            }

            private void AdvanceWaypoint()
            {
                targetIndex++;
                if (targetIndex >= waypoints.Count)
                {
                    Finished = true;
                    return;
                }

                BeginLeg();
            }

            private void BeginLeg()
            {
                crossing = null;
                onRoad = false;
                if (targetIndex <= 0 || targetIndex >= waypoints.Count)
                {
                    return;
                }

                if (!RoadLeg.TryResolve(roads, waypoints[targetIndex - 1], waypoints[targetIndex], out leg))
                {
                    return;
                }

                onRoad = true;
                crossing = new RoadCrossingTraversal(
                    gate, occupant, leg.Road, network, leg.EntryAlong, leg.ExitAlong,
                    frontSetback, rearSetback, manoeuvres.ForLeg(targetIndex));
            }
        }
    }
}
