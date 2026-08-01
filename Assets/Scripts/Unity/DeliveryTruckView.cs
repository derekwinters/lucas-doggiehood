using System;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// The delivery truck (#30): drives in from the street edge, drops a
    /// package cube at the house's front door, and drives away (the view
    /// destroys itself off-screen). Tick is separated from Update so
    /// EditMode tests can step the animation deterministically.
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

        private enum Phase
        {
            Idle,
            DrivingIn,
            DrivingOut,
        }

        private Phase phase = Phase.Idle;
        private Vector3 doorPosition;
        private Vector3 exitPosition;
        private Action onDelivered;

        public bool HasDelivered { get; private set; }
        public bool IsGone { get; private set; }

        public static DeliveryTruckView Spawn(Transform parent)
        {
            var truck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            truck.name = "DeliveryTruck";
            truck.transform.SetParent(parent);
            truck.transform.localScale = new Vector3(1.4f, 1.4f, 2.6f);
            return truck.AddComponent<DeliveryTruckView>();
        }

        public void DeliverTo(Vector3 doorTarget, Action deliveredCallback)
        {
            // Approach along the nearest street: enter at the world edge,
            // stop at the dog's door. #471: doorTarget is already the dog's
            // actual front-walkway node (WalkDogHome passes the exact point the
            // dog sits at) — use it directly. The old * 0.35f / * 0.8f scaling
            // was a leftover from when the caller passed a lot-center, and it
            // dropped the package away from the sitting dog.
            var entry = new Vector3(0f, TruckHeight, Mathf.Sign(doorTarget.z) * WorldBuilder.GroundExtent);
            doorPosition = new Vector3(doorTarget.x, TruckHeight, doorTarget.z);
            exitPosition = new Vector3(0f, TruckHeight, -entry.z);
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
                    Drive(doorPosition, deltaTime);
                    if (Vector3.Distance(transform.position, doorPosition) <= ArriveDistance)
                    {
                        DropPackage();
                        phase = Phase.DrivingOut;
                    }

                    break;
                case Phase.DrivingOut:
                    Drive(exitPosition, deltaTime);
                    if (Vector3.Distance(transform.position, exitPosition) <= ArriveDistance)
                    {
                        IsGone = true;
                        phase = Phase.Idle;
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
