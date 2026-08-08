using System;
using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// The vehicle side of road right-of-way (#546): as a vehicle drives
    /// monotonically along one <see cref="Road"/> from an entry along-coordinate
    /// to an exit along-coordinate, it claims the crosswalks it reaches through a
    /// shared <see cref="RoadCrossingGate"/> (so a dog that arrives second must
    /// wait) and pauses short of any crosswalk it may not take, resuming once
    /// that dog clears it. The pause is measured at the occupant's LEADING
    /// EDGE, not its pivot: a caller with a body length passes its own
    /// pivot-to-front-bumper setback so the whole footprint stays clear of the
    /// band (#639), while a point occupant passes nothing and stops at the near
    /// edge exactly as before. The RELEASE is the mirror image, measured at the
    /// occupant's TRAILING EDGE (#658): a body-shaped caller also passes its
    /// pivot-to-tail setback, so it keeps the claim until its back end is off
    /// the band rather than handing it to a dog that then gets clipped from
    /// behind. Both setbacks are bounded by #660 — together they must fit inside
    /// the clear gap between an intersection's two bands.
    /// Everything is expressed in along-road
    /// coordinates (<see cref="Road.AlongAxis"/>), so the Unity delivery-truck
    /// view only converts positions to/from along and drives — no decision logic
    /// leaks into the engine layer.
    ///
    /// #673: the unit of right-of-way is a whole <see cref="RoadManoeuvre"/>,
    /// not a single band. A vehicle stops at the boundary of a manoeuvre's FIRST
    /// band until it can take EVERY band of that manoeuvre at once, and it holds
    /// the set until its tail clears the manoeuvre's LAST band. That is what
    /// keeps it from driving into the middle of an intersection and only then
    /// discovering it cannot get out the other side. A manoeuvre may span two
    /// roads (a turn), in which case the same manoeuvre object is shared by both
    /// legs' traversals: the approach leg acquires it, the departure leg releases
    /// it. When no manoeuvres are supplied, this type groups the bands on its own
    /// road by intersection — enough for a straight run, which is the only shape
    /// a single road can describe.
    ///
    /// A vehicle's leg is monotonic in along, so bands are met in a single fixed
    /// order; this type does not attempt to handle a mid-leg reversal.
    /// </summary>
    public sealed class RoadCrossingTraversal
    {
        /// <summary>Half the along-road thickness of a crosswalk stripe the
        /// vehicle must stop short of — derived from the locked crosswalk width
        /// (#161: no bare geometry literals).</summary>
        public const float HalfCrosswalkAlong = WorldDimensions.CrosswalkWidth / 2f;

        private const float Epsilon = 0.0001f;

        private readonly RoadCrossingGate gate;
        private readonly object occupant;
        private readonly float travelSign;
        private readonly float frontSetback;
        private readonly float rearSetback;
        private readonly Crossing[] crossings;

        public RoadCrossingTraversal(
            RoadCrossingGate gate, object occupant, Road road, WalkNetwork network,
            float entryAlong, float exitAlong, float frontSetback = 0f, float rearSetback = 0f,
            IReadOnlyList<RoadManoeuvre> manoeuvres = null)
        {
            // #639: frontSetback is the occupant's own pivot-to-leading-edge
            // distance (plus whatever stop gap it wants). It stays a caller-
            // supplied number so this type remains occupant-agnostic (#546): a
            // point occupant passes nothing and gets exactly the old behaviour.
            //
            // #658: rearSetback is the mirror on the release side — the
            // occupant's own pivot-to-tail distance, so a band is only handed
            // back once its whole body is off it. Same deal: caller-supplied,
            // defaulting to 0, so a point occupant is untouched.
            //
            // #673: manoeuvres are the route's own grouping of bands into whole
            // intersection crossings. Supplied by a caller that knows the route
            // (so a turn's two roads land in one manoeuvre); inferred from this
            // road alone when it isn't.
            this.gate = gate ?? throw new ArgumentNullException(nameof(gate));
            this.occupant = occupant ?? throw new ArgumentNullException(nameof(occupant));
            this.frontSetback = frontSetback;
            this.rearSetback = rearSetback;
            if (road == null)
            {
                throw new ArgumentNullException(nameof(road));
            }

            if (network == null)
            {
                throw new ArgumentNullException(nameof(network));
            }

            var direction = exitAlong - entryAlong;
            travelSign = direction < 0f ? -1f : 1f;

            var groups = manoeuvres
                ?? RoadManoeuvre.GroupByIntersection(RoadManoeuvre.BandsOn(road, network), travelSign);

            // Only the bands that lie on THIS road can be reasoned about in this
            // road's along-coordinates; a turn's other band is the sibling leg's
            // to gate on and to release.
            var found = new List<Crossing>();
            foreach (var manoeuvre in groups)
            {
                for (var i = 0; i < manoeuvre.Bands.Count; i++)
                {
                    var band = manoeuvre.Bands[i];
                    if (!RoadManoeuvre.TryAlongOn(road, band, out var along))
                    {
                        continue;
                    }

                    found.Add(new Crossing(along, manoeuvre, i == manoeuvre.Bands.Count - 1));
                }
            }

            // Order the bands in the direction of travel, so the vehicle meets
            // them front to back.
            found.Sort((a, b) => (a.Along * travelSign).CompareTo(b.Along * travelSign));
            crossings = found.ToArray();
        }

        /// <summary>
        /// Given the vehicle's current along-coordinate and the along-coordinate
        /// it intends to reach this tick, returns the along-coordinate it may
        /// actually advance to: the full target when the way is clear, or the
        /// boundary of the next manoeuvre it may not yet enter. Acquires whole
        /// manoeuvres it reaches, and releases ones it has fully passed, as a
        /// side effect.
        /// </summary>
        public float Advance(float currentAlong, float targetAlong)
        {
            ReleaseCleared(currentAlong);

            var allowed = targetAlong;
            for (var i = 0; i < crossings.Length; i++)
            {
                var manoeuvre = crossings[i].Manoeuvre;
                if (manoeuvre.IsHeld)
                {
                    // The whole crossing is already this vehicle's — drive it.
                    continue;
                }

                var along = crossings[i].Along;
                if (!IsAhead(currentAlong, along))
                {
                    continue;
                }

                var boundary = StopBoundary(along);
                if (HasReached(currentAlong, boundary))
                {
                    // #673: all-or-nothing. Either the ENTIRE manoeuvre through
                    // this intersection is available — including the bands on
                    // the leg after the turn — or the vehicle takes none of it
                    // and waits here, outside the intersection.
                    if (manoeuvre.TryAcquire(gate, occupant))
                    {
                        continue;
                    }

                    allowed = ClampAhead(allowed, boundary);
                    break;
                }

                // Not yet at the boundary: drive up to it, but no further, until
                // the claim can be resolved there.
                allowed = ClampAhead(allowed, boundary);
                break;
            }

            // #639: with a non-zero setback the stop boundary can sit BEHIND an
            // occupant that began its leg already inside the setback zone (a leg
            // starting at an intersection waypoint, say). Holding position is
            // right there; reversing out of the zone is not.
            return NoFurtherBackThan(allowed, currentAlong);
        }

        /// <summary>
        /// The along-coordinate at which an occupant must stop for the crosswalk
        /// centred on <paramref name="along"/>: the stripe's own near edge pushed
        /// back by the occupant's front setback, so it is the occupant's LEADING
        /// EDGE — not its pivot — that comes to rest at the edge of the band
        /// (#639). With the default zero setback this is the near edge itself.
        /// </summary>
        private float StopBoundary(float along)
        {
            return along - travelSign * (HalfCrosswalkAlong + frontSetback);
        }

        /// <summary>Releases every manoeuvre this vehicle still holds — used
        /// when its view is torn down mid-route so the claim can't strand a
        /// dog.</summary>
        public void ReleaseAll()
        {
            for (var i = 0; i < crossings.Length; i++)
            {
                if (crossings[i].Manoeuvre.IsHeld)
                {
                    crossings[i].Manoeuvre.Release(gate, occupant);
                }
            }
        }

        /// <summary>
        /// #673: a manoeuvre is handed back as one set, once the vehicle's TAIL
        /// has cleared its FINAL band — never when a waypoint is reached. Only
        /// the leg that actually drives that final band can make the call, which
        /// is why a turn releases on its departure leg rather than at the turn
        /// point.
        /// </summary>
        private void ReleaseCleared(float currentAlong)
        {
            for (var i = 0; i < crossings.Length; i++)
            {
                if (!crossings[i].IsFinalBand || !crossings[i].Manoeuvre.IsHeld)
                {
                    continue;
                }

                var farEdge = crossings[i].Along + travelSign * HalfCrosswalkAlong;
                if ((farEdge - TrailingEdge(currentAlong)) * travelSign <= Epsilon)
                {
                    crossings[i].Manoeuvre.Release(gate, occupant);
                }
            }
        }

        /// <summary>
        /// Where the occupant's TAIL is, given where its pivot is: a rear
        /// setback back down the road it came from (#658). A crosswalk is only
        /// released once this — not the pivot — is past the far edge, so a
        /// waiting dog is never let onto a band the vehicle's back half is still
        /// covering. With the default zero setback this is the pivot itself, so
        /// a point occupant releases at exactly the far edge as before.
        /// </summary>
        private float TrailingEdge(float currentAlong)
        {
            return currentAlong - travelSign * rearSetback;
        }

        private bool IsAhead(float currentAlong, float along)
        {
            return (along - currentAlong) * travelSign > Epsilon;
        }

        private bool HasReached(float currentAlong, float boundary)
        {
            return (boundary - currentAlong) * travelSign <= Epsilon;
        }

        private float ClampAhead(float value, float cap)
        {
            return travelSign > 0f ? Math.Min(value, cap) : Math.Max(value, cap);
        }

        /// <summary>#639: never hand back an along-coordinate behind where the
        /// occupant already is — a clamp may only slow it down or hold it.</summary>
        private float NoFurtherBackThan(float value, float currentAlong)
        {
            return travelSign > 0f ? Math.Max(value, currentAlong) : Math.Min(value, currentAlong);
        }

        /// <summary>One band of a manoeuvre, as this leg sees it: where it sits
        /// on this road, which manoeuvre it belongs to, and whether it is that
        /// manoeuvre's last — the band whose far edge drives the release.</summary>
        private readonly struct Crossing
        {
            public readonly float Along;
            public readonly RoadManoeuvre Manoeuvre;
            public readonly bool IsFinalBand;

            public Crossing(float along, RoadManoeuvre manoeuvre, bool isFinalBand)
            {
                Along = along;
                Manoeuvre = manoeuvre;
                IsFinalBand = isFinalBand;
            }
        }
    }
}
