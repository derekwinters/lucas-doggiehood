using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    public class DeliveryTruckTests
    {
        [Test]
        public void TruckDrivesIn_Delivers_AndDrivesAwayAgain()
        {
            // #30: the truck animates to the house and away after delivering.
            var root = new GameObject("truck-test-root");
            try
            {
                var truck = DeliveryTruckView.Spawn(root.transform);
                var delivered = 0;
                var housePosition = new Vector3(14f, 0f, 14f);

                truck.DeliverTo(housePosition, () => delivered++);

                var reachedHouse = false;
                for (var step = 0; step < 2000 && !truck.IsGone; step++)
                {
                    truck.Tick(0.05f);
                    if (truck.HasDelivered && !reachedHouse)
                    {
                        reachedHouse = true;
                        Assert.That(delivered, Is.EqualTo(1), "delivery callback fires exactly once, at the door");
                    }
                }

                Assert.That(reachedHouse, Is.True, "truck never reached the house");
                Assert.That(truck.IsGone, Is.True, "truck never drove away");
                Assert.That(root.transform.Find("Package"), Is.Not.Null, "package left at the door");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
