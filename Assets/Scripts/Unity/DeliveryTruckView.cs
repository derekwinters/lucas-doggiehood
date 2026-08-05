using System;
using System.Linq;
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

        private enum Phase
        {
            Idle,
            DrivingIn,
            DrivingOut,
        }

        private Phase phase = Phase.Idle;
        private Vector3 doorPosition;
        private Vector3 stopPosition;
        private Vector3 exitPosition;
        private Action onDelivered;

        // #546: right-of-way as the truck drives its road. The traversal claims
        // each crosswalk the truck reaches (so a dog that arrives second waits)
        // and reports how far the truck may advance without entering a crosswalk a
        // dog already holds. crossingRoad is the single road the whole route runs
        // along, used to convert the truck's position to along-road coordinates.
        private RoadCrossingTraversal crossing;
        private Road crossingRoad;

        public bool HasDelivered { get; private set; }
        public bool IsGone { get; private set; }

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

        public void DeliverTo(Vector3 doorTarget, Action deliveredCallback)
        {
            // #538: route the truck ALONG THE ROAD, not in a bee-line across
            // the yard. DeliveryTruckRoute derives entry/stop/exit from the
            // real road geometry: the truck enters at a road end, stops at the
            // road point nearest the door (short of the waiting dog — it never
            // drives onto the sidewalk, yard, or lot), and leaves by the far
            // road end. #471: doorTarget is the dog's actual front-walkway node
            // (WalkDogHome passes the exact point the dog sits at); the PACKAGE
            // is still dropped there, but the TRUCK stays on the road.
            var route = DeliveryTruckRoute.ToDoor(
                NeighborhoodLayout.Roads, new GridPoint(doorTarget.x, doorTarget.z));

            // #546: the whole entry -> stop -> exit route lies on one road's
            // centerline. Resolve that road and set up the crossing traversal so
            // the truck yields to dogs already on a crosswalk it drives over.
            crossingRoad = NeighborhoodLayout.Roads.First(
                r => r.Contains(route.Entry) && r.Contains(route.Exit));
            crossing = new RoadCrossingTraversal(
                RoadCrossingGate.Shared, this, crossingRoad, NeighborhoodLayout.WalkNetwork,
                crossingRoad.AlongAxis(route.Entry), crossingRoad.AlongAxis(route.Exit));

            var entry = new Vector3(route.Entry.X, TruckHeight, route.Entry.Z);
            doorPosition = new Vector3(doorTarget.x, TruckHeight, doorTarget.z);
            stopPosition = new Vector3(route.Stop.X, TruckHeight, route.Stop.Z);
            exitPosition = new Vector3(route.Exit.X, TruckHeight, route.Exit.Z);
            transform.position = entry;
            onDelivered = deliveredCallback;
            phase = Phase.DrivingIn;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Advances the drive; called by Update at runtime and
        /// directly by EditMode tests.</summary>
        public void Tick(float deltaTime)
        {
            switch (phase)
            {
                case Phase.DrivingIn:
                    Drive(ClampToCrossing(stopPosition), deltaTime);
                    if (Vector3.Distance(transform.position, stopPosition) <= ArriveDistance)
                    {
                        DropPackage();
                        phase = Phase.DrivingOut;
                    }

                    break;
                case Phase.DrivingOut:
                    Drive(ClampToCrossing(exitPosition), deltaTime);
                    if (Vector3.Distance(transform.position, exitPosition) <= ArriveDistance)
                    {
                        IsGone = true;
                        phase = Phase.Idle;
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

                    break;
            }
        }

        /// <summary>#546: clamps this tick's drive target so the truck never
        /// enters a crosswalk it may not (one a dog holds, or one it hasn't yet
        /// reached the boundary of to claim). The traversal works in along-road
        /// coordinates; convert to/from the road centerline here. The clamp keeps
        /// the target's Y so the truck stays at its ride height.</summary>
        private Vector3 ClampToCrossing(Vector3 target)
        {
            if (crossing == null)
            {
                return target;
            }

            var currentAlong = crossingRoad.AlongAxis(new GridPoint(transform.position.x, transform.position.z));
            var targetAlong = crossingRoad.AlongAxis(new GridPoint(target.x, target.z));
            var allowedAlong = crossing.Advance(currentAlong, targetAlong);
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
