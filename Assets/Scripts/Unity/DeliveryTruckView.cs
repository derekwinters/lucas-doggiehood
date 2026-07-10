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

        public void DeliverTo(Vector3 housePosition, Action deliveredCallback)
        {
            // Approach along the nearest street: enter at the world edge,
            // stop by the house's front corner.
            var entry = new Vector3(0f, 0.7f, Mathf.Sign(housePosition.z) * WorldBuilder.GroundExtent);
            doorPosition = new Vector3(housePosition.x * 0.35f, 0.7f, housePosition.z * 0.8f);
            exitPosition = new Vector3(0f, 0.7f, -entry.z);
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
            package.transform.position = new Vector3(doorPosition.x, 0.3f, doorPosition.z);

            HasDelivered = true;
            var callback = onDelivered;
            onDelivered = null;
            callback?.Invoke();
        }
    }
}
