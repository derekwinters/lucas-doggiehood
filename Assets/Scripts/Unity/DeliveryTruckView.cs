using System;
using System.Collections.Generic;
using Doggiehood.Core.Art;
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
    ///
    /// #672: it also keeps to the RIGHT-hand lane. The route's waypoints stay on
    /// the road centerline (an intersection waypoint belongs to two roads with two
    /// different right-hand sides, so a lane baked into the graph would be
    /// ambiguous); the lane is resolved per LEG in Core (<see cref="RoadLeg"/> /
    /// <see cref="RoadLane"/>) and applied to every drive target and clamp here.
    ///
    /// #673: right-of-way, by contrast, is NOT per leg. Crossing an intersection
    /// is always two legs meeting at its centre, so a per-leg claim could not see
    /// the whole crossing — the truck drove to the turn point, released what it
    /// held, and only then looked at the crosswalk it was about to cross. It now
    /// acquires every band of the crossing at once, before entering it
    /// (<see cref="RouteManoeuvres"/> / <see cref="RoadManoeuvre"/> in Core), and
    /// holds them until its tail is clear of the far side.
    /// </summary>
    public sealed class DeliveryTruckView : MonoBehaviour
    {
        private const float Speed = 8f;
        private const float ArriveDistance = 0.2f;

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
        // to the house kit rather than per-axis stretching. #660: the figure now
        // lives in Core, because it is not a free visual dial — it is bounded by
        // DeliveryTruckFootprint.MaxBodyLength (the truck must fit between an
        // intersection's two crosswalk bands, or two oncoming trucks deadlock),
        // and that bound is pinned by Core tests. Still subject to an on-device
        // look against the houses (#547).
        public const float ModelScale = DeliveryTruckFootprint.ModelScale;

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
        // truck is never invisible when the model can't load. #660: left at its
        // authored size — at 2.6m it is comfortably inside
        // DeliveryTruckFootprint.MaxBodyLength, and halving the kit model's
        // scale brought the two footprints CLOSER together (9.75m vs 2.6m
        // before, 4.875m vs 2.6m now), so nothing here needs to move.
        public static readonly Vector3 FallbackScale = new Vector3(1.4f, 1.4f, 2.6f);

        // A Bounds has 8 corners; MeasureBodyLength walks all of them to bring a
        // world-space renderer bound back into the truck root's local space.
        private const int BoundsCornerCount = 8;

        // Below this the measurement is degenerate (a body that contributed no
        // renderers), and the graybox footprint is the better answer.
        private const float MinMeasurableBodyLength = 0.01f;

        /// <summary>#639: the truck's length along its own travel axis, measured
        /// at spawn from the body it actually got — the kit model's renderer
        /// bounds (already scaled by <see cref="ModelScale"/>), or
        /// <see cref="FallbackScale"/>.z on the graybox path. The crosswalk
        /// setback derives from this rather than a hand-tuned literal (#161), so
        /// it tracks the real model if the kit or its scale ever changes.</summary>
        public float BodyLength { get; private set; }

        /// <summary>#639: how far ahead of the truck's pivot its front bumper
        /// sits (half a body), plus the crosswalk stop gap. Handed to
        /// <see cref="RoadCrossingTraversal"/> so the yield stop is measured at
        /// the BUMPER: <c>transform.position</c> is the centre of the body, so
        /// stopping that at a crosswalk's near edge left the whole front half
        /// overhanging the band — and clipping the dog crossing it. #660: the
        /// derivation itself lives in Core, alongside the budget both this and
        /// the matching rear setback have to fit inside.</summary>
        public float CrosswalkFrontSetback => DeliveryTruckFootprint.FrontSetbackFor(BodyLength);

        /// <summary>#658: how far BEHIND the truck's pivot its tail trails (half a
        /// body). Handed to <see cref="RoadCrossingTraversal"/> so a crosswalk is
        /// only released once the TAIL has cleared the band: the release used to
        /// be measured at the pivot, so the truck handed the crosswalk back — and
        /// a waiting dog stepped onto it — while its whole back half was still
        /// over the stripes. Derived from the same measured <see cref="BodyLength"/>
        /// as the front setback (#161), inside the shared budget
        /// <see cref="DeliveryTruckFootprint.FitsBetweenCrosswalkBands"/> pins.</summary>
        public float CrosswalkRearSetback => DeliveryTruckFootprint.RearSetbackFor(BodyLength);

        /// <summary>
        /// #547: test seam mirroring <see cref="WorldBuilder.ForcePrimitiveFallback"/>
        /// — forces <see cref="Spawn"/> down the graybox-cube branch (the same
        /// branch a null <c>Resources.Load</c> takes) so EditMode can exercise
        /// the fallback without removing the staged asset. Left <c>false</c> in
        /// normal play.
        /// </summary>
        public static bool ForcePrimitiveFallback { get; set; }

        // #601: per-spawn car color. Trucks are transient and unsaved, so the
        // color is NOT a persisted per-id assignment like the houses' tint — it
        // is picked at spawn from an incrementing spawn seed (deterministic in
        // Core so it's testable), avoiding the colors currently on the road so
        // two concurrent trucks differ. The seed counter and the in-use color
        // set are process-static because the "distinct from active" rule spans
        // all live trucks; the set is maintained across the spawn/OnDestroy
        // lifetime of each view.
        private static int nextSpawnSeed;
        private static readonly HashSet<int> ActiveCarColorIndices = new HashSet<int>();

        /// <summary>#601: the curated standard-car-color index this truck drew
        /// at spawn (0..<see cref="CarColorAssignment.CarColorCount"/>-1).</summary>
        public int CarColorIndex { get; private set; }

        /// <summary>#601: the hex of this truck's assigned standard car color —
        /// <see cref="Palette.CarColorHex"/> at <see cref="CarColorIndex"/>.</summary>
        public string CarColorHex => Palette.CarColorHex(CarColorIndex);

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

        // #673: right-of-way is scoped to the whole MANOEUVRE through an
        // intersection, not to one leg of it. A route's waypoints are junctions,
        // so crossing an intersection is always two legs meeting at its centre —
        // which is why the per-leg traversal could never see a whole crossing.
        // These are resolved once for the route and SHARED by both legs, so the
        // claim taken on the approach survives the turn and is released only when
        // the truck's tail is clear of the far side.
        private RouteManoeuvres routeManoeuvres = RouteManoeuvres.None;
        private readonly List<GridPoint> routePoints = new List<GridPoint>();

        // #672: the lane the truck keeps to on the current leg — the signed
        // perpendicular offset from that leg's road centerline to the centre of
        // its RIGHT-hand lane. The decision is Core's (RoadLeg/RoadLane); this
        // only remembers the resolved figure so every drive target and clamp on
        // the leg is expressed on the lane line rather than the centerline.
        private float legLaneOffset;

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

        /// <summary>#672: the signed perpendicular offset from the current leg's
        /// road centerline to the centre of the lane the truck keeps to — the
        /// companion to <see cref="CurrentAlong"/> on the road's other axis.
        /// <c>RoadLane.Offset</c> in magnitude, its sign set by the leg's travel
        /// direction (<see cref="RoadLane"/>). Zero when not on a resolved road
        /// leg.</summary>
        public float LaneOffset => legLaneOffset;

        /// <summary>#672: where the truck actually IS across its current leg's
        /// road, signed the same way as <see cref="LaneOffset"/> — so a test (or a
        /// later road user) can check it is keeping right without re-deriving the
        /// road's perpendicular axis. Zero when not on a resolved road leg.</summary>
        public float CurrentLateral
        {
            get
            {
                if (crossingRoad == null)
                {
                    return 0f;
                }

                return crossingRoad.Orientation == StreetOrientation.NorthSouth
                    ? transform.position.x - crossingRoad.Center.X
                    : transform.position.z - crossingRoad.Center.Z;
            }
        }

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

            // #601: draw this truck's standard car color at spawn, avoiding the
            // colors already on the road so concurrent trucks differ. Reserved
            // in ActiveCarColorIndices for the truck's lifetime (released in
            // OnDestroy). Decision lives in Core; this only supplies the seed.
            view.CarColorIndex = CarColorAssignment.IndexFor(nextSpawnSeed++, ActiveCarColorIndices);
            ActiveCarColorIndices.Add(view.CarColorIndex);

            var model = ForcePrimitiveFallback ? null : Resources.Load<GameObject>(ModelResourceName);
            if (model != null)
            {
                var visual = Instantiate(model, truck.transform);
                visual.name = "Model";
                visual.transform.localPosition = new Vector3(0f, ModelGroundOffsetY, 0f);
                visual.transform.localRotation = Quaternion.Euler(0f, ModelForwardYawOffsetDegrees, 0f);
                visual.transform.localScale = Vector3.one * ModelScale;
                ApplyCarColor(visual, view.CarColorHex);

                // #639: measure the real body now, while the root is still
                // unrotated and before Drive() starts aiming it.
                view.BodyLength = MeasureBodyLength(truck.transform, visual);
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

                // #639: on this path the graybox IS the body, so its footprint
                // length is the measurement the crosswalk setback derives from.
                view.BodyLength = FallbackScale.z;
            }

            return view;
        }

        /// <summary>#639: the spawned body's extent along the truck root's own
        /// forward (local +Z) axis — the length that decides how far ahead of the
        /// pivot the front bumper sits. <see cref="Renderer.bounds"/> is a
        /// world-space AABB, so each one's corners are brought back into the
        /// root's local space before measuring; the root is unrotated at spawn,
        /// but this keeps the figure right whatever the parent's orientation.
        /// Falls back to the graybox footprint when the body contributes no
        /// renderers at all.</summary>
        private static float MeasureBodyLength(Transform root, GameObject visual)
        {
            var minZ = float.MaxValue;
            var maxZ = float.MinValue;

            foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
            {
                var bounds = renderer.bounds;
                for (var corner = 0; corner < BoundsCornerCount; corner++)
                {
                    var offset = new Vector3(
                        (corner & 1) == 0 ? -bounds.extents.x : bounds.extents.x,
                        (corner & 2) == 0 ? -bounds.extents.y : bounds.extents.y,
                        (corner & 4) == 0 ? -bounds.extents.z : bounds.extents.z);
                    var local = root.InverseTransformPoint(bounds.center + offset);
                    minZ = Mathf.Min(minZ, local.z);
                    maxZ = Mathf.Max(maxZ, local.z);
                }
            }

            var length = maxZ - minZ;
            return length > MinMeasurableBodyLength ? length : FallbackScale.z;
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
            routePoints.Clear();
            foreach (var point in route.Inbound)
            {
                waypoints.Add(new Vector3(point.X, TruckHeight, point.Z));
                routePoints.Add(point);
            }

            // Outbound[0] is the stop, already the last inbound waypoint — skip it.
            for (var i = 1; i < route.Outbound.Count; i++)
            {
                var point = route.Outbound[i];
                waypoints.Add(new Vector3(point.X, TruckHeight, point.Z));
                routePoints.Add(point);
            }

            // #673: resolve the whole route's intersection manoeuvres up front —
            // the decision is Core's, this only supplies the geometry.
            routeManoeuvres = RouteManoeuvres.Resolve(roads, network, routePoints);

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

            // #672: enter off-map already in the right-hand lane rather than
            // sliding across from the centerline on the first tick.
            transform.position = OnLegLane(transform.position);
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

            // #672: drive the leg's LANE line, not the road centerline the
            // waypoint sits on.
            var target = OnLegLane(waypoints[targetIndex]);
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

            // #673: reaching a waypoint must NOT release anything. At an
            // intersection the waypoint IS the turn point, so releasing here left
            // the truck sitting in the middle of the box holding nothing — and
            // only then looking at the crosswalk on its outgoing leg. The release
            // is driven by "my tail has cleared the last band of this manoeuvre"
            // instead, inside RoadCrossingTraversal.
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
            ReleaseEveryClaim();
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
        /// exactly what RoadCrossingTraversal expects, now applied per-segment.
        /// #672: the same resolution also fixes the lane the truck keeps to for
        /// this leg, since "right" depends on the direction it is driven.</summary>
        private void BeginLeg()
        {
            crossing = null;
            crossingRoad = null;
            following = null;
            legLaneOffset = 0f;
            if (targetIndex <= 0 || targetIndex >= waypoints.Count)
            {
                return;
            }

            var from = waypoints[targetIndex - 1];
            var to = waypoints[targetIndex];

            // #672: the leg — which road it runs along, which way, and therefore
            // which lane the truck keeps to — is resolved in Core (RoadLeg), so
            // the lane rule and its sign convention are testable without the
            // engine and shared with any future road user.
            if (!RoadLeg.TryResolve(
                    roads, new GridPoint(from.x, from.z), new GridPoint(to.x, to.z), out var leg))
            {
                return;
            }

            crossingRoad = leg.Road;
            legTravelSign = leg.TravelSign;
            legLaneOffset = leg.LaneOffset;
            var entryAlong = leg.EntryAlong;
            var exitAlong = leg.ExitAlong;

            if (network == null)
            {
                return;
            }

            // #673: the manoeuvres this leg has to reason about — the
            // intersection it is driving INTO (acquired whole, before it enters)
            // and the one it has just come out of (released once its tail is
            // clear). Both legs of a crossing share the same manoeuvre objects,
            // so the claim carries across the turn.
            var manoeuvres = routeManoeuvres.ForLeg(targetIndex);

            // #639/#658: the truck is not a point — it hands the traversal its
            // own pivot-to-bumper and pivot-to-tail setbacks, so the yield stop
            // lands at its FRONT and the release only happens once its TAIL is
            // clear. Between them the whole footprint stays off a band it does
            // not hold. #660 bounds the pair so both fit in the clear gap between
            // an intersection's two bands. (#673 made that bound belt-and-braces
            // rather than load-bearing: the truck now takes an intersection's
            // bands all-or-nothing, so holding two at once can no longer produce
            // the lock-ordering cycle #660 was raised for.)
            crossing = new RoadCrossingTraversal(
                RoadCrossingGate.Shared, this, crossingRoad, network, entryAlong, exitAlong,
                CrosswalkFrontSetback, CrosswalkRearSetback, manoeuvres);

            // #600: a fresh follower for this leg's road and travel direction.
            following = new CarFollowing(legTravelSign);
        }

        /// <summary>#673: drops every crosswalk claim this truck still holds
        /// anywhere on its route. The per-leg traversal only knows the manoeuvres
        /// of its own leg, and a manoeuvre acquired on an approach leg outlives
        /// that leg by design — so teardown has to go through the route.</summary>
        private void ReleaseEveryClaim()
        {
            crossing?.ReleaseAll();
            routeManoeuvres.ReleaseAll(RoadCrossingGate.Shared, this);
        }

        /// <summary>#672: the point on the current leg's LANE line that a
        /// centerline waypoint corresponds to — the truck aims at, and snaps onto,
        /// its own lane rather than the middle of the road. The route's waypoints
        /// deliberately stay on the centerline (#538/#599), because an
        /// intersection waypoint belongs to two roads with two different
        /// right-hand sides; the lane is derived per leg here instead. Off a
        /// resolved road leg (a turnaround pivot) the raw waypoint stands.</summary>
        private Vector3 OnLegLane(Vector3 waypoint)
        {
            if (crossingRoad == null)
            {
                return waypoint;
            }

            var along = crossingRoad.AlongAxis(new GridPoint(waypoint.x, waypoint.z));
            var point = crossingRoad.PointAt(along, legLaneOffset);
            return new Vector3(point.X, waypoint.y, point.Z);
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
            var point = crossingRoad.PointAt(allowedAlong, legLaneOffset);
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
            var point = crossingRoad.PointAt(allowedAlong, legLaneOffset);
            return new Vector3(point.X, target.y, point.Z);
        }

        private void OnDestroy()
        {
            // #546: never leave a crosswalk claimed if the truck is torn down
            // mid-route (it normally releases each as it passes).
            ReleaseEveryClaim();

            // #601: release this truck's car color back to the pool so a later
            // spawn can reuse it (the "distinct from active" rule only avoids
            // colors still on the road).
            ActiveCarColorIndices.Remove(CarColorIndex);
        }

        /// <summary>#601 test seam: the per-spawn car-color pick keeps process-
        /// static state (the seed counter and the in-use color set) because the
        /// "distinct from active" rule spans every live truck. In play that set
        /// drains as each truck's <see cref="OnDestroy"/> runs, but EditMode
        /// fixtures share the process and don't reliably fire OnDestroy on
        /// <c>DestroyImmediate</c>, so trucks from earlier tests can leave colors
        /// reserved and fill the set — degrading the distinctness guarantee for a
        /// later test. A fixture resets this between cases so the in-use set can't
        /// bleed across tests.</summary>
        public static void ResetSpawnColorStateForTests()
        {
            nextSpawnSeed = 0;
            ActiveCarColorIndices.Clear();
        }

        /// <summary>#601: applies the chosen standard car color as a material
        /// color-multiply over the truck model's renderers — the exact
        /// <c>WorldBuilder.ApplyPaletteTint</c> technique the houses use (clone
        /// each renderer's <c>sharedMaterial</c> and set its <c>.color</c>), so
        /// the kit model's light base reads as the assigned color.</summary>
        private static void ApplyCarColor(GameObject visual, string colorHex)
        {
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
            {
                var material = renderer.sharedMaterial != null
                    ? new Material(renderer.sharedMaterial)
                    : new Material(Shader.Find("Standard"));
                material.color = CoreColors.FromHex(colorHex);
                renderer.sharedMaterial = material;
            }
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
            // #703: PackageView also owns the box's short visible beat and then
            // removes it. The package is parented to the world root above so it
            // can outlive the truck (deliberate); nothing here destroys it, and
            // nothing here should — a truck that is torn down mid-beat must not
            // be able to strand a permanent tap-swallowing cube at the door.
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
