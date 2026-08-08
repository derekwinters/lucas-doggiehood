using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #546: a vehicle driving along one road claims each crosswalk it reaches
    /// (so a dog that hasn't claimed it yet must wait) and pauses at the near
    /// edge of any crosswalk a dog already holds, resuming once the dog clears
    /// it. This is the vehicle side of the first-come <see cref="RoadCrossingGate"/>
    /// rule, expressed purely in along-road coordinates so the thin Unity view
    /// only converts positions and drives.
    /// </summary>
    public class RoadCrossingTraversalTests
    {
        private const float HalfCrosswalk = 3f / 2f; // WorldDimensions.CrosswalkWidth / 2

        // #639: a stand-in front-overhang setback (pivot-to-front-bumper plus a
        // stop gap) for the vehicle-shaped occupant cases. Chosen larger than
        // the 4.75m crosswalk offset so the "began the leg already inside the
        // setback zone" case is reachable from the intersection centre.
        private const float FrontSetback = 4f;

        // #658: a stand-in rear-overhang setback (pivot-to-tail) for the
        // vehicle-shaped occupant cases, deliberately large enough that the
        // release point sits well past the band's far edge and can't be
        // confused with it.
        private const float RearSetback = 4f;

        // #658 no-deadlock simulations: the crosswalk at +/-4.75 and the road's
        // own ends are the fixed geometry every drive below runs over.
        private const float NorthCrosswalkAlong = 4.75f;
        private const float SouthCrosswalkAlong = -4.75f;
        private const float RoadEnd = 30f;

        // A drive is stepped in fixed increments rather than continuously, so a
        // vehicle that stops making progress simply stays put and the step
        // budget runs out — which is exactly what a deadlock looks like.
        private const float DriveStep = 0.1f;
        private const int DriveStepBudget = 5000;
        private const float ArrivalTolerance = 0.001f;

        // The real truck's own figures (#660 pins them), so the no-deadlock
        // cases run at the geometry the game actually ships rather than at
        // hand-picked numbers (#161).
        private static readonly float TruckBody = DeliveryTruckFootprint.NominalBodyLength;
        private static readonly float TruckFront = DeliveryTruckFootprint.FrontSetbackFor(TruckBody);
        private static readonly float TruckRear = DeliveryTruckFootprint.RearSetbackFor(TruckBody);

        private static Road NorthSouthRoad()
        {
            return NeighborhoodLayout.Roads.First(r => r.Orientation == StreetOrientation.NorthSouth);
        }

        // The starting intersection's north-south road carries two crosswalks,
        // north (+) and south (-) of the origin, at along = +/- the crossing
        // road's sidewalk offset (4.75m).
        private static WalkEdge CrosswalkAt(Road road, float along)
        {
            return NeighborhoodLayout.WalkNetwork.Edges.Single(e =>
                e.Kind == WalkEdgeKind.Crosswalk
                && System.Math.Abs(road.AlongAxis(Midpoint(e)) - along) < 0.01f
                && System.Math.Abs(Perp(road, Midpoint(e))) < 0.01f);
        }

        private static GridPoint Midpoint(WalkEdge e)
        {
            return new GridPoint((e.A.X + e.B.X) / 2f, (e.A.Z + e.B.Z) / 2f);
        }

        private static float Perp(Road road, GridPoint p)
        {
            return road.Orientation == StreetOrientation.NorthSouth
                ? p.X - road.Center.X
                : p.Z - road.Center.Z;
        }

        [Test]
        public void PausesAtTheNearEdge_WhenTheNextCrosswalkIsHeldByAnotherOccupant()
        {
            var gate = new RoadCrossingGate();
            var road = NorthSouthRoad();
            var truck = new object();
            // Truck drives from the north end (+30) to the south end (-30).
            var traversal = new RoadCrossingTraversal(gate, truck, road, NeighborhoodLayout.WalkNetwork, 30f, -30f);

            // A dog claims the north crosswalk first.
            var north = CrosswalkAt(road, 4.75f);
            Assert.That(gate.TryEnter(north, new object()), Is.True);

            // The truck, already at the near edge, must not advance into the band.
            var nearEdge = 4.75f + HalfCrosswalk; // approached travelling in -Z, near edge is on the +Z side
            var allowed = traversal.Advance(nearEdge, -30f);

            Assert.That(allowed, Is.EqualTo(nearEdge).Within(0.001f),
                "the vehicle must hold at the crosswalk's near edge while a dog holds the claim");
        }

        [Test]
        public void WithAFrontSetback_TheVehiclesBUMPERStopsAtTheNearEdge_NotItsPivot()
        {
            // #639: the returned along-coordinate becomes the vehicle's PIVOT
            // (its transform position), which sits half a body behind its front
            // bumper. Stopping the pivot at the near edge overhangs the whole
            // front half of the body into the band — and into the dog on it.
            var gate = new RoadCrossingGate();
            var road = NorthSouthRoad();
            var truck = new object();
            var traversal = new RoadCrossingTraversal(
                gate, truck, road, NeighborhoodLayout.WalkNetwork, 30f, -30f, FrontSetback);

            // A dog claims the north crosswalk first.
            var north = CrosswalkAt(road, 4.75f);
            Assert.That(gate.TryEnter(north, new object()), Is.True);

            var nearEdge = 4.75f + HalfCrosswalk; // approached travelling -Z

            // Drive down from the north end: the vehicle is held a whole front
            // setback short of the stripe rather than straddling it.
            var approach = traversal.Advance(30f, -30f);
            Assert.That(approach, Is.EqualTo(nearEdge + FrontSetback).Within(0.001f),
                "the vehicle drives up to its own stop boundary, a front setback short of the near edge");

            // Holding there, it must not creep any further forward.
            var held = traversal.Advance(approach, -30f);
            Assert.That(held, Is.EqualTo(nearEdge + FrontSetback).Within(0.001f),
                "the vehicle holds at its stop boundary while a dog holds the claim");

            // Travelling -Z, the bumper leads the pivot by the setback.
            Assert.That(held - FrontSetback, Is.GreaterThanOrEqualTo(nearEdge - 0.001f),
                "the vehicle's FRONT BUMPER must sit at or behind the crosswalk's near edge");
        }

        [Test]
        public void WithNoFrontSetback_TheStopIsTheNearEdgeExactly_AsForAPointOccupant()
        {
            // #639 regression guard: the setback defaults to 0, so occupants
            // with no body length (dogs, point vehicles) behave exactly as before.
            var road = NorthSouthRoad();
            var north = CrosswalkAt(road, 4.75f);
            var nearEdge = 4.75f + HalfCrosswalk;

            var defaultGate = new RoadCrossingGate();
            var byDefault = new RoadCrossingTraversal(
                defaultGate, new object(), road, NeighborhoodLayout.WalkNetwork, 30f, -30f);
            Assert.That(defaultGate.TryEnter(north, new object()), Is.True);

            var explicitGate = new RoadCrossingGate();
            var explicitZero = new RoadCrossingTraversal(
                explicitGate, new object(), road, NeighborhoodLayout.WalkNetwork, 30f, -30f, 0f);
            Assert.That(explicitGate.TryEnter(north, new object()), Is.True);

            Assert.That(byDefault.Advance(nearEdge, -30f), Is.EqualTo(nearEdge).Within(0.001f),
                "with no setback the stop is the crosswalk's own near edge, unchanged");
            Assert.That(explicitZero.Advance(nearEdge, -30f), Is.EqualTo(nearEdge).Within(0.001f),
                "an explicit zero setback matches the default exactly");
        }

        [Test]
        public void AFrontSetback_NeverReversesAVehicleThatBeganItsLegInsideTheSetbackZone()
        {
            // #639: a leg can begin (at an intersection waypoint) already nearer
            // a crosswalk than the setback. The stop boundary then lies BEHIND
            // the vehicle; holding position is right, backing up is not.
            var gate = new RoadCrossingGate();
            var road = NorthSouthRoad();
            var traversal = new RoadCrossingTraversal(
                gate, new object(), road, NeighborhoodLayout.WalkNetwork, 0f, -30f, FrontSetback);

            var south = CrosswalkAt(road, -4.75f);
            Assert.That(gate.TryEnter(south, new object()), Is.True);

            // Stop boundary for the south crosswalk is -4.75 + 1.5 + 4 = +0.75,
            // i.e. north of (behind) a vehicle starting at the intersection.
            var allowed = traversal.Advance(0f, -30f);

            Assert.That(allowed, Is.EqualTo(0f).Within(0.001f),
                "a blocked vehicle holds where it is — it never drives backwards to its stop boundary");
        }

        [Test]
        public void ClaimsTheWholeIntersectionAtItsBoundary_AndDrivesStraightThrough()
        {
            var gate = new RoadCrossingGate();
            var road = NorthSouthRoad();
            var truck = new object();
            var traversal = new RoadCrossingTraversal(gate, truck, road, NeighborhoodLayout.WalkNetwork, 30f, -30f);

            var north = CrosswalkAt(road, 4.75f);
            var south = CrosswalkAt(road, -4.75f);
            var nearEdge = 4.75f + HalfCrosswalk;

            // #673: the four-way's two bands are ONE manoeuvre. Reaching the
            // first band's boundary with the whole crossing free claims all of
            // it, and the vehicle drives right through without ever coming to
            // rest inside the box. (Before #673 this stopped at the second
            // band's near edge — with the vehicle sitting in the intersection.)
            var allowed = traversal.Advance(nearEdge, -30f);
            Assert.That(allowed, Is.EqualTo(-30f).Within(0.001f),
                "an available intersection is claimed whole and driven through in one move");

            // Having claimed it, a dog arriving at EITHER band is denied.
            Assert.That(gate.TryEnter(north, new object()), Is.False,
                "while the truck holds the crossing, a dog that arrives second must wait");
            Assert.That(gate.TryEnter(south, new object()), Is.False,
                "including at the far band, which the truck has already committed to");
        }

        [Test]
        public void ReleasesASingleBandCrossingOnceFullyPast_SoALaterDogMayCross()
        {
            // #546's release rule at a ONE-band crossing (a Tee's single arm, or
            // any crossing whose manoeuvre is one band): unchanged by #673 —
            // the claim is handed back the moment the vehicle is past that band.
            var gate = new RoadCrossingGate();
            var road = NorthSouthRoad();
            var truck = new object();
            var north = CrosswalkAt(road, 4.75f);
            var traversal = new RoadCrossingTraversal(
                gate, truck, road, NeighborhoodLayout.WalkNetwork, 30f, -30f, 0f, 0f,
                SingleBand(north));

            var nearEdge = 4.75f + HalfCrosswalk;

            // Claim the north crosswalk at its boundary...
            traversal.Advance(nearEdge, -30f);
            Assert.That(gate.TryEnter(north, new object()), Is.False, "truck holds it mid-crossing");

            // ...then drive well past its far edge; the claim releases.
            var pastFarEdge = 4.75f - HalfCrosswalk - 0.5f; // beyond the -Z (far) side
            traversal.Advance(pastFarEdge, -30f);

            var dog = new object();
            Assert.That(gate.TryEnter(north, dog), Is.True,
                "once the vehicle is fully past a crosswalk it releases the claim, so a dog may cross");
        }

        [Test]
        public void ReleasesAMultiBandManoeuvre_OnlyOnceItIsPastTheFINALBand()
        {
            // #673's counterpart to the single-band case above: a vehicle that
            // has committed to a whole intersection keeps every band of it until
            // it is out the far side. Letting a dog onto the band behind it
            // while it is still inside the box is the state the issue forbids.
            var gate = new RoadCrossingGate();
            var road = NorthSouthRoad();
            var truck = new object();
            var traversal = new RoadCrossingTraversal(gate, truck, road, NeighborhoodLayout.WalkNetwork, 30f, -30f);

            var north = CrosswalkAt(road, 4.75f);
            traversal.Advance(4.75f + HalfCrosswalk, -30f);

            traversal.Advance(4.75f - HalfCrosswalk - 0.5f, -30f);
            Assert.That(gate.TryEnter(north, new object()), Is.False,
                "past the FIRST band but still inside the intersection — nothing is handed back yet");

            traversal.Advance(-4.75f - HalfCrosswalk - 0.5f, -30f);
            Assert.That(gate.TryEnter(north, new object()), Is.True,
                "out the far side, the whole set releases at once");
        }

        [Test]
        public void WithARearSetback_TheClaimHoldsUntilTheVehiclesTAILClearsTheBand()
        {
            // #658: the release side is the mirror of #639's stop side. The
            // along-coordinate a vehicle reports is its PIVOT — the centre of
            // its body — so releasing the moment THAT passes the far edge lets a
            // waiting dog step onto a band the vehicle's back half is still
            // sitting on, and the dog is clipped from behind.
            var gate = new RoadCrossingGate();
            var road = NorthSouthRoad();
            var truck = new object();
            // A ONE-band crossing, so this pins the #658 rule itself rather than
            // #673's whole-manoeuvre release (covered in RoadManoeuvreTests).
            var north = CrosswalkAt(road, NorthCrosswalkAlong);
            var traversal = new RoadCrossingTraversal(
                gate, truck, road, NeighborhoodLayout.WalkNetwork,
                RoadEnd, -RoadEnd, 0f, RearSetback, SingleBand(north));

            var nearEdge = NorthCrosswalkAlong + HalfCrosswalk; // approached travelling -Z
            var farEdge = NorthCrosswalkAlong - HalfCrosswalk;

            // Claim the band on the way in.
            traversal.Advance(nearEdge, -RoadEnd);
            Assert.That(gate.TryEnter(north, new object()), Is.False, "truck holds it mid-crossing");

            // Pivot already past the far edge, tail still over the stripes.
            traversal.Advance(farEdge - 1f, -RoadEnd);
            Assert.That(gate.TryEnter(north, new object()), Is.False,
                "the vehicle's CENTRE is past the far edge but its tail is still on the band — "
                + "releasing here would let a dog step out in front of the truck's back half");

            // One tick short of the tail clearing: the release point is a whole
            // rear setback beyond the far edge.
            var releaseAlong = farEdge - RearSetback;
            traversal.Advance(releaseAlong + ArrivalTolerance * 100f, -RoadEnd);
            Assert.That(gate.TryEnter(north, new object()), Is.False,
                "still held a hair before the tail clears the far edge");

            traversal.Advance(releaseAlong, -RoadEnd);
            Assert.That(gate.TryEnter(north, new object()), Is.True,
                "once the vehicle's TAIL clears the far edge the claim releases and a dog may cross");
        }

        [Test]
        public void WithNoRearSetback_TheReleaseIsTheFarEdgeExactly_AsForAPointOccupant()
        {
            // #658 regression guard: the rear setback defaults to 0, so occupants
            // with no body length (dogs, point vehicles) release at exactly the
            // far edge, byte-for-byte as before.
            var road = NorthSouthRoad();
            var north = CrosswalkAt(road, NorthCrosswalkAlong);
            var nearEdge = NorthCrosswalkAlong + HalfCrosswalk;
            var farEdge = NorthCrosswalkAlong - HalfCrosswalk;

            // Both traversals are given the SAME one-band crossing, so this stays
            // the #658 point-occupant regression guard it was written as (#673
            // groups a four-way's two bands, which is a different rule tested in
            // RoadManoeuvreTests).
            var defaultGate = new RoadCrossingGate();
            var byDefault = new RoadCrossingTraversal(
                defaultGate, new object(), road, NeighborhoodLayout.WalkNetwork,
                RoadEnd, -RoadEnd, 0f, 0f, SingleBand(north));

            var explicitGate = new RoadCrossingGate();
            var explicitZero = new RoadCrossingTraversal(
                explicitGate, new object(), road, NeighborhoodLayout.WalkNetwork,
                RoadEnd, -RoadEnd, 0f, 0f, SingleBand(north));

            byDefault.Advance(nearEdge, -RoadEnd);
            explicitZero.Advance(nearEdge, -RoadEnd);

            // A hair short of the far edge: both still hold.
            byDefault.Advance(farEdge + ArrivalTolerance * 100f, -RoadEnd);
            explicitZero.Advance(farEdge + ArrivalTolerance * 100f, -RoadEnd);
            Assert.That(defaultGate.TryEnter(north, new object()), Is.False,
                "with no rear setback the claim still holds right up to the far edge");
            Assert.That(explicitGate.TryEnter(north, new object()), Is.False,
                "an explicit zero rear setback matches the default exactly");

            // At the far edge itself: both release, unchanged from before #658.
            byDefault.Advance(farEdge, -RoadEnd);
            explicitZero.Advance(farEdge, -RoadEnd);
            Assert.That(defaultGate.TryEnter(north, new object()), Is.True,
                "a point occupant releases at exactly the far edge, as it always has");
            Assert.That(explicitGate.TryEnter(north, new object()), Is.True,
                "an explicit zero rear setback matches the default exactly");
        }

        [Test]
        public void ATruckWithBothSetbacks_DrivesTheWholeMultiCrosswalkRoute_AndEndsHoldingNothing()
        {
            // #658 checklist item 3: holding a claim LONGER is the shape of
            // change that introduces deadlock, so pin that a truck carrying both
            // setbacks still gets all the way across an intersection's two bands.
            var gate = new RoadCrossingGate();
            var road = NorthSouthRoad();
            var truck = new RoadCrossingTraversal(
                gate, new object(), road, NeighborhoodLayout.WalkNetwork,
                RoadEnd, -RoadEnd, TruckFront, TruckRear);

            var ends = DriveAll(
                new[] { truck }, new[] { RoadEnd }, new[] { -RoadEnd });

            Assert.That(ends[0], Is.EqualTo(-RoadEnd).Within(ArrivalTolerance),
                "a truck with both setbacks must still reach the far end of the road");
            Assert.That(gate.TryEnter(CrosswalkAt(road, NorthCrosswalkAlong), new object()), Is.True,
                "it must not still be holding the north crosswalk once it has driven off");
            Assert.That(gate.TryEnter(CrosswalkAt(road, SouthCrosswalkAlong), new object()), Is.True,
                "nor the south one");
        }

        [Test]
        public void TwoOncomingTrucksWithBothSetbacks_BothCompleteTheirRoutes_NoLockOrderingCycle()
        {
            // #658, the case the issue's own checklist misses: one truck can
            // never deadlock against itself, but TWO can. If a truck's footprint
            // is long enough to hold BOTH of an intersection's bands at once,
            // two ONCOMING trucks acquire them in opposite order — A holds north
            // and is blocked at south's boundary, B holds south and is blocked at
            // north's — and neither can release without advancing. That wedge is
            // permanent and freezes every dog waiting on either band with it.
            // Oncoming trucks are explicitly permitted (they don't constrain each
            // other under car-following, #600), so this state is reachable in
            // normal play, not exotic.
            var gate = new RoadCrossingGate();
            var road = NorthSouthRoad();
            var southbound = new RoadCrossingTraversal(
                gate, new object(), road, NeighborhoodLayout.WalkNetwork,
                RoadEnd, -RoadEnd, TruckFront, TruckRear);
            var northbound = new RoadCrossingTraversal(
                gate, new object(), road, NeighborhoodLayout.WalkNetwork,
                -RoadEnd, RoadEnd, TruckFront, TruckRear);

            var ends = DriveAll(
                new[] { southbound, northbound },
                new[] { RoadEnd, -RoadEnd },
                new[] { -RoadEnd, RoadEnd });

            Assert.That(ends[0], Is.EqualTo(-RoadEnd).Within(ArrivalTolerance),
                $"the southbound truck wedged at {ends[0]} instead of driving off the road");
            Assert.That(ends[1], Is.EqualTo(RoadEnd).Within(ArrivalTolerance),
                $"the northbound truck wedged at {ends[1]} instead of driving off the road");
        }

        [Test]
        public void TwoOncomingTrucks_DoWedge_WhenRightOfWayIsScopedPerBandRatherThanPerManoeuvre()
        {
            // The teeth behind the test above. The wedge is a real, reachable
            // state — it is only unreachable because right-of-way is now scoped
            // to the whole manoeuvre (#673). Drive the identical geometry with
            // the PRE-#673 scope (each band its own independent claim) and the
            // pair locks solid: each truck takes the band it reaches first and
            // then waits forever on the one the other is sitting on.
            //
            // The over-budget setbacks are what make each truck long enough to
            // still be holding its first band when it reaches the second —
            // exactly the condition #660's bound exists to keep out.
            var overBudgetRear = DeliveryTruckFootprint.ClearGapBetweenCrosswalkBands - TruckFront + DriveStep;
            Assert.That(TruckFront + overBudgetRear,
                Is.GreaterThan(DeliveryTruckFootprint.ClearGapBetweenCrosswalkBands),
                "the probe setbacks must genuinely exceed the clear gap between the bands");

            var gate = new RoadCrossingGate();
            var road = NorthSouthRoad();
            var southbound = new RoadCrossingTraversal(
                gate, new object(), road, NeighborhoodLayout.WalkNetwork,
                RoadEnd, -RoadEnd, TruckFront, overBudgetRear, PerBandManoeuvres(road));
            var northbound = new RoadCrossingTraversal(
                gate, new object(), road, NeighborhoodLayout.WalkNetwork,
                -RoadEnd, RoadEnd, TruckFront, overBudgetRear, PerBandManoeuvres(road));

            var ends = DriveAll(
                new[] { southbound, northbound },
                new[] { RoadEnd, -RoadEnd },
                new[] { -RoadEnd, RoadEnd });

            Assert.That(ends[0], Is.Not.EqualTo(-RoadEnd).Within(ArrivalTolerance),
                "per-band right-of-way must wedge here — otherwise the no-deadlock test above proves nothing");
            Assert.That(ends[1], Is.Not.EqualTo(RoadEnd).Within(ArrivalTolerance),
                "and it wedges both trucks, each holding the band the other needs");
        }

        [Test]
        public void TheSameOverBudgetPair_ClearsFine_OnceRightOfWayIsPerManoeuvre()
        {
            // #673 removes the hold-and-wait the wedge above is built from: the
            // intersection is taken all-or-nothing, so one truck gets the whole
            // crossing and the other waits outside holding nothing. #660's
            // length bound still stands as a spec rule (a vehicle must fit
            // between the bands), but it is no longer the ONLY thing between the
            // game and a permanent lock.
            var overBudgetRear = DeliveryTruckFootprint.ClearGapBetweenCrosswalkBands - TruckFront + DriveStep;

            var gate = new RoadCrossingGate();
            var road = NorthSouthRoad();
            var southbound = new RoadCrossingTraversal(
                gate, new object(), road, NeighborhoodLayout.WalkNetwork,
                RoadEnd, -RoadEnd, TruckFront, overBudgetRear);
            var northbound = new RoadCrossingTraversal(
                gate, new object(), road, NeighborhoodLayout.WalkNetwork,
                -RoadEnd, RoadEnd, TruckFront, overBudgetRear);

            var ends = DriveAll(
                new[] { southbound, northbound },
                new[] { RoadEnd, -RoadEnd },
                new[] { -RoadEnd, RoadEnd });

            Assert.That(ends[0], Is.EqualTo(-RoadEnd).Within(ArrivalTolerance),
                $"the southbound truck wedged at {ends[0]} despite the atomic manoeuvre claim");
            Assert.That(ends[1], Is.EqualTo(RoadEnd).Within(ArrivalTolerance),
                $"the northbound truck wedged at {ends[1]} despite the atomic manoeuvre claim");
        }

        /// <summary>A manoeuvre set containing one single-band crossing — the
        /// shape a Tee's lone arm has, and the shape every crossing had before
        /// #673 grouped a four-way's two bands.</summary>
        private static IReadOnlyList<RoadManoeuvre> SingleBand(WalkEdge band)
        {
            return new[] { new RoadManoeuvre(new[] { band }) };
        }

        /// <summary>Pre-#673 scope: every band on the road as its own
        /// independent claim, with nothing tying an intersection's two together.
        /// Used to show the deadlock that scope allows.</summary>
        private static IReadOnlyList<RoadManoeuvre> PerBandManoeuvres(Road road)
        {
            var perBand = new List<RoadManoeuvre>();
            foreach (var band in RoadManoeuvre.BandsOn(road, NeighborhoodLayout.WalkNetwork))
            {
                perBand.Add(new RoadManoeuvre(new[] { band.Edge }));
            }

            return perBand;
        }

        /// <summary>Steps every traversal from its entry toward its exit in fixed
        /// increments, stopping early once they have all arrived, and returns
        /// where each one ended up. A vehicle that can never advance simply stays
        /// put until the step budget runs out — so a deadlock shows up as a final
        /// position short of the exit rather than as a hang.</summary>
        private static float[] DriveAll(
            IReadOnlyList<RoadCrossingTraversal> traversals,
            IReadOnlyList<float> entries,
            IReadOnlyList<float> exits)
        {
            var along = entries.ToArray();
            for (var step = 0; step < DriveStepBudget; step++)
            {
                var allArrived = true;
                for (var i = 0; i < traversals.Count; i++)
                {
                    var target = exits[i] < entries[i]
                        ? Math.Max(exits[i], along[i] - DriveStep)
                        : Math.Min(exits[i], along[i] + DriveStep);
                    along[i] = traversals[i].Advance(along[i], target);
                    if (Math.Abs(along[i] - exits[i]) > ArrivalTolerance)
                    {
                        allArrived = false;
                    }
                }

                if (allArrived)
                {
                    break;
                }
            }

            return along;
        }

        [Test]
        public void WithNoCrosswalkAhead_TheFullTargetIsAllowed()
        {
            var gate = new RoadCrossingGate();
            var road = NorthSouthRoad();
            var truck = new object();
            var traversal = new RoadCrossingTraversal(gate, truck, road, NeighborhoodLayout.WalkNetwork, 30f, -30f);

            // Driving from +30 down to a stop at +14: both crosswalks (+/-4.75)
            // are beyond the stop, so nothing clamps the approach.
            var allowed = traversal.Advance(30f, 14f);
            Assert.That(allowed, Is.EqualTo(14f).Within(0.001f),
                "an inbound leg that stops short of every crosswalk reaches its stop unobstructed");
        }

        [Test]
        public void ReleaseAll_DropsEveryClaimTheVehicleStillHolds()
        {
            var gate = new RoadCrossingGate();
            var road = NorthSouthRoad();
            var truck = new object();
            var traversal = new RoadCrossingTraversal(gate, truck, road, NeighborhoodLayout.WalkNetwork, 30f, -30f);

            var north = CrosswalkAt(road, 4.75f);
            traversal.Advance(4.75f + HalfCrosswalk, -30f); // claim north
            Assert.That(gate.TryEnter(north, new object()), Is.False);

            traversal.ReleaseAll();

            Assert.That(gate.TryEnter(north, new object()), Is.True,
                "ReleaseAll frees claims a vehicle abandoned (e.g. when its view is destroyed mid-route)");
        }
    }
}
