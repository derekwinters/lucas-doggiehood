using System;
using System.Collections.Generic;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// The delivery truck (#30, #538): drives in along the ROAD, stops at the
    /// road point nearest the dog's front door to drop a package cube at that
    /// door, and drives away along the same road (the view destroys itself
    /// off-screen). It never leaves the roadway — the approach path comes from
    /// <see cref="DeliveryTruckRoute"/> over the real road geometry, not a
    /// bee-line across yards, and the truck stops short of the waiting dog
    /// rather than driving into it. Tick is separated from Update so EditMode
    /// tests can step the animation deterministically.
    /// </summary>
    public sealed class DeliveryTruckView : MonoBehaviour
    {
        private const float Speed = 8f;
        private const float ArriveDistance = 0.2f;

        // A route leg is axis-aligned on a road centerline; this tolerance
        // decides which axis (N-S vs E-W) a leg runs along when matching it to
        // the road it lies on (#161: no bare geometry literals in method bodies).
        private const float LegAxisEpsilon = 0.01f;

        // Fixed vertical offsets (unrelated to the #471 door-position bug):
        // the truck body rides at TruckHeight; the dropped package rests at
        // PackageHeight so it sits on the ground rather than clipping through.
        private const float TruckHeight = 0.7f;
        private const float PackageHeight = 0.3f;

        // #547: the truck body is the staged Kenney Car Kit "delivery" model,
        // loaded by name like every other kit model (road/house/fence/tree).
        private const string ModelResourceName = "delivery";

        // Uniform kit scale, mirroring WorldBuilder.HouseKitScale's discipline
        // (#145): one fixed number so the truck reads at a believable size next
        // to the house kit rather than per-axis stretching. First-pass value —
        // the exact figure is confirmed on-device against the houses (the issue
        // flags the kit pivot/scale as needing a visual check).
        public const float ModelScale = 3f;

        // The Kenney kit models carry ground-level (base) pivots — the road and
        // house loaders place them straight at the surface (WorldBuilder). The
        // truck ROOT rides at TruckHeight (DeliverTo), so the model child is
        // lowered by that much to seat its wheels on the ground.
        private const float ModelGroundOffsetY = -TruckHeight;

        // Drive() aims the root's +Z down the travel direction each tick; this
        // yaw aligns the model's front with that +Z. Confirmed on-device (a kit
        // model authored facing the opposite local axis would drive tail-first —
        // a one-constant correction, e.g. 180f).
        private const float ModelForwardYawOffsetDegrees = 0f;

        // The pre-#547 graybox footprint, kept for the fallback path so the
        // truck is never invisible when the model can't load.
        public static readonly Vector3 FallbackScale = new Vector3(1.4f, 1.4f, 2.6f);

        /// <summary>
        /// #547: test seam mirroring <see cref="WorldBuilder.ForcePrimitiveFallback"/>
        /// — forces <see cref="Spawn"/> down the graybox-cube branch (the same
        /// branch a null <c>Resources.Load</c> takes) so EditMode can exercise
        /// the fallback without removing the staged asset. Left <c>false</c> in
        /// normal play.
        /// </summary>
        public static bool ForcePrimitiveFallback { get; set; }

        // #599: the truck now drives a multi-segment waypoint path over the LIVE
        // road network (in from an off-map opening, out by another), so the route
        // is a list of centerline waypoints rather than a single road's ends.
        private readonly List<Vector3> waypoints = new List<Vector3>();
        private int stopIndex;
        private int targetIndex;
        private bool routeActive;
        private IReadOnlyList<Road> roads;
        private WalkNetwork network;
        private Vector3 doorPosition;
        private Action onDelivered;

        // #546: right-of-way as the truck drives. Each route leg lies on one road
        // centerline; the per-leg traversal claims each crosswalk the truck
        // reaches (so a dog that arrives second waits) and reports how far the
        // truck may advance without entering a crosswalk a dog already holds.
        // crossingRoad is the current leg's road, used to convert the truck's
        // position to along-road coordinates.
        private RoadCrossingTraversal crossing;
        private Road crossingRoad;

        // #600: 1-D car-following on the current leg's road. The crosswalk gate
        // above arbitrates only the crosswalk claim, so this is what keeps the
        // truck one car length behind whatever truck is ahead of it on the
        // segment. Created per leg (each leg is one road) with that leg's travel
        // direction; the immediate leader's along-coordinate is supplied by the
        // owning QuestDirector each tick (null on an open road).
        private CarFollowing following;
        private float legTravelSign = 1f;

        public bool HasDelivered { get; private set; }
        public bool IsGone { get; private set; }

        /// <summary>#600: true while the truck is driving a road leg — i.e. it
        /// participates in car-following and can be a leader/follower this tick.
        /// False before <see cref="DeliverTo"/>, once <see cref="IsGone"/>, or on
        /// a leg with no resolved road (e.g. a turnaround maneuver waypoint).</summary>
        public bool IsDriving => routeActive && !IsGone && crossingRoad != null;

        /// <summary>#600: a value key identifying the physical road this truck is
        /// currently on — equal across the separate <see cref="Road"/> instances
        /// two trucks derive for the same segment, so the owner can group
        /// same-segment trucks. Null when not on a resolved road leg.</summary>
        public object CurrentSegmentKey => crossingRoad == null
            ? null
            : (object)(crossingRoad.Orientation, crossingRoad.Center.X, crossingRoad.Center.Z, crossingRoad.HalfLength);

        /// <summary>#600: +1 or -1, the direction this truck drives along its
        /// current leg's road axis (matching <see cref="CurrentAlong"/>).</summary>
        public float TravelSign => legTravelSign;

        /// <summary>#600: this truck's position on its current leg's road, in the
        /// road's own along-coordinate — the common number line on which the
        /// owner compares trucks to find each follower's immediate leader.</summary>
        public float CurrentAlong => crossingRoad == null
            ? 0f
            : crossingRoad.AlongAxis(new GridPoint(transform.position.x, transform.position.z));

        public static DeliveryTruckView Spawn(Transform parent)
        {
            // The moving root carries the view + the drive rotation; the visible
            // body (kit model, or graybox fallback) hangs off it as a child so
            // Drive()'s LookRotation aims the whole truck without touching the
            // model's own seat/scale/yaw. Scope note (#547): only the spawned
            // body changes here — DeliverTo/Tick/Drive are untouched.
            var truck = new GameObject("DeliveryTruck");
            truck.transform.SetParent(parent);
            var view = truck.AddComponent<DeliveryTruckView>();

            var model = ForcePrimitiveFallback ? null : Resources.Load<GameObject>(ModelResourceName);
            if (model != null)
            {
                var visual = Instantiate(model, truck.transform);
                visual.name = "Model";
                visual.transform.localPosition = new Vector3(0f, ModelGroundOffsetY, 0f);
                visual.transform.localRotation = Quaternion.Euler(0f, ModelForwardYawOffsetDegrees, 0f);
                visual.transform.localScale = Vector3.one * ModelScale;
            }
            else
            {
                // Graybox fallback (only reached when the kit model can't load):
                // the original primitive cube, centred on the root so its base
                // still sits on the ground at the pre-#547 footprint.
                var graybox = GameObject.CreatePrimitive(PrimitiveType.Cube);
                graybox.name = "Graybox";
                graybox.transform.SetParent(truck.transform);
                graybox.transform.localPosition = Vector3.zero;
                graybox.transform.localScale = FallbackScale;
            }

            return view;
        }

        public void DeliverTo(Vector3 doorTarget, TileMap map, WalkNetwork walkNetwork, Action deliveredCallback)
        {
            // #599: route the truck IN from an off-map opening, over the LIVE
            // multi-tile road network, to the road point nearest the door, then
            // OUT by a different opening (or a retrace on a spur/cul-de-sac).
            // DeliveryTruckRoute derives the whole waypoint path from the real
            // map geometry — the truck never leaves the roadway. #471: doorTarget
            // is the dog's actual front-walkway node (WalkDogHome passes the exact
            // point the dog sits at); the PACKAGE is dropped there, the TRUCK
            // stays on the road.
            roads = MapWalkNetwork.RoadsFrom(map);
            network = walkNetwork;
            var route = DeliveryTruckRoute.ToDoor(map, new GridPoint(doorTarget.x, doorTarget.z));

            waypoints.Clear();
            foreach (var point in route.Inbound)
            {
                waypoints.Add(new Vector3(point.X, TruckHeight, point.Z));
            }

            // Outbound[0] is the stop, already the last inbound waypoint — skip it.
            for (var i = 1; i < route.Outbound.Count; i++)
            {
                var point = route.Outbound[i];
                waypoints.Add(new Vector3(point.X, TruckHeight, point.Z));
            }

            stopIndex = route.Inbound.Count - 1;
            doorPosition = new Vector3(doorTarget.x, TruckHeight, doorTarget.z);
            onDelivered = deliveredCallback;

            transform.position = waypoints[0];
            targetIndex = 1;
            routeActive = true;

            // Degenerate case: the door's nearest road point is the entry opening
            // itself, so the truck starts already at its stop.
            if (stopIndex == 0 && !HasDelivered)
            {
                DropPackage();
            }

            BeginLeg();
        }

        /// <summary>Advances the drive along the waypoint path with no truck
        /// ahead (open road) — kept for callers/tests driving a single truck.
        /// #600: at runtime the owning <see cref="QuestDirector"/> drives every
        /// truck through the leader-aware overload instead, so the view no longer
        /// self-ticks in Update.</summary>
        public void Tick(float deltaTime)
        {
            Tick(deltaTime, null);
        }

        /// <summary>#600: advances the drive one tick, held behind the immediate
        /// leader on this leg's road when <paramref name="leaderAlong"/> is set
        /// (its along-coordinate on the same road), or unobstructed by following
        /// when null. Called by <see cref="QuestDirector"/> at runtime and
        /// directly by EditMode tests.</summary>
        public void Tick(float deltaTime, float? leaderAlong)
        {
            if (!routeActive)
            {
                return;
            }

            if (targetIndex >= waypoints.Count)
            {
                Finish();
                return;
            }

            var target = waypoints[targetIndex];
            Drive(ClampToLeader(ClampToCrossing(target), leaderAlong, deltaTime), deltaTime);

            if (Vector3.Distance(transform.position, target) > ArriveDistance)
            {
                return;
            }

            // Snap exactly onto the waypoint on arrival, drop at the stop, and
            // advance to the next leg (releasing the finished leg's claims).
            transform.position = target;
            if (targetIndex == stopIndex && !HasDelivered)
            {
                DropPackage();
            }

            crossing?.ReleaseAll();
            targetIndex++;
            if (targetIndex >= waypoints.Count)
            {
                Finish();
                return;
            }

            BeginLeg();
        }

        /// <summary>The truck has reached its exit opening: tear the view down
        /// off-screen, releasing any crosswalk claim it still holds.</summary>
        private void Finish()
        {
            IsGone = true;
            routeActive = false;
            crossing?.ReleaseAll();
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }

        /// <summary>#546/#599: sets up the crosswalk-yield traversal for the leg
        /// the truck is about to drive (from the previous waypoint to the target),
        /// bound to that leg's road on the LIVE network. Each leg is axis-aligned
        /// on one road centerline, so crosswalks are met in a single fixed order —
        /// exactly what RoadCrossingTraversal expects, now applied per-segment.</summary>
        private void BeginLeg()
        {
            crossing = null;
            crossingRoad = null;
            following = null;
            if (targetIndex <= 0 || targetIndex >= waypoints.Count)
            {
                return;
            }

            var from = waypoints[targetIndex - 1];
            var to = waypoints[targetIndex];
            crossingRoad = ResolveLegRoad(from, to);
            if (crossingRoad == null || network == null)
            {
                return;
            }

            var entryAlong = crossingRoad.AlongAxis(new GridPoint(from.x, from.z));
            var exitAlong = crossingRoad.AlongAxis(new GridPoint(to.x, to.z));
            legTravelSign = exitAlong - entryAlong < 0f ? -1f : 1f;

            crossing = new RoadCrossingTraversal(
                RoadCrossingGate.Shared, this, crossingRoad, network, entryAlong, exitAlong);

            // #600: a fresh follower for this leg's road and travel direction.
            following = new CarFollowing(legTravelSign);
        }

        /// <summary>The road a leg runs along: the one whose centerline contains
        /// both endpoints and whose orientation matches the leg's axis.</summary>
        private Road ResolveLegRoad(Vector3 from, Vector3 to)
        {
            if (roads == null)
            {
                return null;
            }

            var a = new GridPoint(from.x, from.z);
            var b = new GridPoint(to.x, to.z);
            var runsNorthSouth = Mathf.Abs(from.x - to.x) < LegAxisEpsilon;
            var runsEastWest = Mathf.Abs(from.z - to.z) < LegAxisEpsilon;

            foreach (var road in roads)
            {
                if (!road.Contains(a) || !road.Contains(b))
                {
                    continue;
                }

                if (runsNorthSouth && road.Orientation == StreetOrientation.NorthSouth)
                {
                    return road;
                }

                if (runsEastWest && road.Orientation == StreetOrientation.EastWest)
                {
                    return road;
                }
            }

            return null;
        }

        /// <summary>#546: clamps this tick's drive target so the truck never
        /// enters a crosswalk it may not (one a dog holds, or one it hasn't yet
        /// reached the boundary of to claim). The traversal works in along-road
        /// coordinates; convert to/from the current leg's road centerline here.
        /// The clamp keeps the target's Y so the truck stays at its ride height.</summary>
        private Vector3 ClampToCrossing(Vector3 target)
        {
            if (crossing == null || crossingRoad == null)
            {
                return target;
            }

            var currentAlong = crossingRoad.AlongAxis(new GridPoint(transform.position.x, transform.position.z));
            var targetAlong = crossingRoad.AlongAxis(new GridPoint(target.x, target.z));
            var allowedAlong = crossing.Advance(currentAlong, targetAlong);
            var point = crossingRoad.PointAt(allowedAlong, 0f);
            return new Vector3(point.X, target.y, point.Z);
        }

        /// <summary>#600: clamps this tick's drive target so the truck never
        /// advances closer than one car length behind the truck ahead of it on
        /// this leg's road (<paramref name="leaderAlong"/>, or null on an open
        /// road), and holds for a second after a stopped leader begins to move.
        /// The decision lives in Core (<see cref="CarFollowing"/>); this only
        /// converts positions to/from the current leg's along-road coordinate.
        /// Applied on top of <see cref="ClampToCrossing"/>, so the truck obeys
        /// whichever of the two limits (body gap, crosswalk claim) is nearer.</summary>
        private Vector3 ClampToLeader(Vector3 target, float? leaderAlong, float deltaTime)
        {
            if (following == null || crossingRoad == null)
            {
                return target;
            }

            var currentAlong = crossingRoad.AlongAxis(new GridPoint(transform.position.x, transform.position.z));
            var targetAlong = crossingRoad.AlongAxis(new GridPoint(target.x, target.z));
            var allowedAlong = following.Advance(currentAlong, targetAlong, leaderAlong, deltaTime);
            var point = crossingRoad.PointAt(allowedAlong, 0f);
            return new Vector3(point.X, target.y, point.Z);
        }

        private void OnDestroy()
        {
            // #546: never leave a crosswalk claimed if the truck is torn down
            // mid-route (it normally releases each as it passes).
            crossing?.ReleaseAll();
        }

        private void Drive(Vector3 target, float deltaTime)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, Speed * deltaTime);
            var direction = target - transform.position;
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        private void DropPackage()
        {
            var package = GameObject.CreatePrimitive(PrimitiveType.Cube);
            package.name = "Package";
            package.transform.SetParent(transform.parent);
            package.transform.localScale = Vector3.one * 0.6f;
            package.transform.position = new Vector3(doorPosition.x, PackageHeight, doorPosition.z);

            // #471: make the delivered package routable/tappable — it previously
            // carried no IInteractable, so TapRouter swallowed taps on it.
            package.AddComponent<PackageView>();

            HasDelivered = true;
            Doggiehood.Core.Audio.AudioEventBus.Publish(Doggiehood.Core.Audio.SfxEvent.TruckArrival);
            Doggiehood.Core.Audio.AudioEventBus.Publish(Doggiehood.Core.Audio.SfxEvent.ItemDelivered);
            var callback = onDelivered;
            onDelivered = null;
            callback?.Invoke();
        }
    }
}
