using System.Linq;
using Doggiehood.Core.World;
using Doggiehood.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #126 bonus: Editor-only door-position overlays for the real
    /// neighborhood houses. The drawing itself is a gizmo callback (not
    /// headless-testable); these tests pin the position logic it renders —
    /// which must come from the same Core API the gallery and game use.
    /// </summary>
    public class HouseViewGizmosTests
    {
        private GameObject root;

        [SetUp]
        public void BuildWorld()
        {
            WorldBuilder.ForcePrimitiveFallback = false;
            root = WorldBuilder.Build(GameState.CreateNew());
        }

        [TearDown]
        public void DestroyWorld()
        {
            WorldBuilder.ForcePrimitiveFallback = false;
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryGetDoorWorldPosition_ForEveryKitHouse_ReturnsTheCoreCatalogDoor()
        {
            var views = root.GetComponentsInChildren<HouseView>();
            Assert.That(views.Length, Is.EqualTo(4));

            foreach (var view in views)
            {
                Assert.That(HouseViewGizmos.TryGetDoorWorldPosition(view, out var door), Is.True,
                    $"house {view.HouseId} has no door overlay");

                // The door sits on the ground plane, away from the house
                // center by exactly the scaled Core door-offset magnitude.
                var model = HouseModelCatalog.ForHouse(view.HouseId);
                var visual = view.transform.Find("Model");
                var local = model.FrontDoorLocalPosition;
                var expectedDistance = visual.localScale.x
                    * Mathf.Sqrt(local.X * local.X + local.Z * local.Z);

                Assert.That(door.y, Is.EqualTo(0f).Within(0.001f));
                var offset = door - view.transform.position;
                Assert.That(offset.magnitude, Is.EqualTo(expectedDistance).Within(0.001f),
                    $"house {view.HouseId} door distance");
            }
        }

        [Test]
        public void TryGetDoorWorldPosition_PointsTheDoorTowardTheWalkwaySidewalk()
        {
            // Behavioral check that the yaw handling is right: the gizmo
            // reads the placed Unity transform, and Core's walkway edge
            // (#128) starts at the catalog door — a wrong yaw would put
            // the transform-derived door somewhere else entirely, so the
            // two must coincide. (Doors can be recessed or lateral since
            // the gallery pass, so no closer-than-the-center geometry
            // assumption survives — equality with the walkway's door node
            // is the exact contract.)
            foreach (var view in root.GetComponentsInChildren<HouseView>())
            {
                Assert.That(NeighborhoodLayout.WalkNetwork.TryGetFrontWalkway(view.HouseId, out var walkway),
                    Is.True, $"house {view.HouseId} has no front walkway");
                var doorNode = new Vector3(walkway.A.X, 0f, walkway.A.Z);

                Assert.That(HouseViewGizmos.TryGetDoorWorldPosition(view, out var door), Is.True);
                Assert.That(Vector3.Distance(new Vector3(door.x, 0f, door.z), doorNode),
                    Is.LessThan(0.001f),
                    $"house {view.HouseId} gizmo door {door} must sit on the walkway's door node {doorNode}");
            }
        }

        [Test]
        public void TryGetDoorWorldPosition_IsFalseForGrayboxFallbackHouses()
        {
            // The graybox primitives have no catalog model transform to
            // read, so no overlay is drawn (and nothing throws).
            Object.DestroyImmediate(root);
            WorldBuilder.ForcePrimitiveFallback = true;
            root = WorldBuilder.Build(GameState.CreateNew());

            foreach (var view in root.GetComponentsInChildren<HouseView>())
            {
                Assert.That(HouseViewGizmos.TryGetDoorWorldPosition(view, out _), Is.False);
            }
        }
    }
}
