using System.Linq;
using Doggiehood.Core.Art;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #740: the graybox backyard pool view — a gray open-topped shell with a
    /// blue water surface inset within it and sitting slightly below the rim,
    /// at the position Core assigned.
    /// </summary>
    public class PoolViewTests
    {
        private const int HouseId = 2;
        private const float Tolerance = 0.001f;

        private GameObject parent;
        private PoolView view;

        [SetUp]
        public void SpawnPool()
        {
            parent = new GameObject("pool-parent");
            view = PoolView.Spawn(HouseId, new GridPoint(3f, -5f), parent.transform);
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(parent);
        }

        [Test]
        public void Spawn_PlacesThePoolOnTheGround_AtItsCoreAssignedPosition()
        {
            Assert.That(view.HouseId, Is.EqualTo(HouseId));
            Assert.That(view.name, Is.EqualTo(PoolView.NamePrefix + HouseId));
            Assert.That(view.transform.position.x, Is.EqualTo(3f).Within(Tolerance));
            Assert.That(view.transform.position.z, Is.EqualTo(-5f).Within(Tolerance));
            Assert.That(view.transform.position.y, Is.EqualTo(0f).Within(Tolerance),
                "the pool stands on the lawn");
        }

        [Test]
        public void Spawn_BuildsAGrayShellAboutOneAdultDogTall_AndTwoAdultDogsWide()
        {
            var shell = view.transform.Find(PoolView.ShellName);
            Assert.That(shell, Is.Not.Null, "the pool has a gray outer shell");

            var bounds = shell.GetComponent<Renderer>().bounds;
            Assert.That(bounds.size.y, Is.EqualTo(PoolPlacement.PoolHeight).Within(Tolerance),
                "the shell stands about one adult dog tall");
            Assert.That(bounds.size.x, Is.EqualTo(PoolPlacement.PoolOuterDiameter).Within(Tolerance),
                "the shell is about two adult dogs wide");
            Assert.That(bounds.size.z, Is.EqualTo(PoolPlacement.PoolOuterDiameter).Within(Tolerance));

            Assert.That(shell.GetComponent<Renderer>().sharedMaterial.color,
                Is.EqualTo(CoreColors.FromHex(Palette.PoolShellHex)));
        }

        [Test]
        public void Spawn_BuildsABlueInterior_InsetWithinTheShell_AndBelowItsRim()
        {
            // Derek: "blue interior that is slightly lower than the rest of the
            // cylinder" — so the pool reads as an open container from the
            // game's fixed camera angle, not a solid gray drum.
            var water = view.transform.Find(PoolView.WaterName);
            Assert.That(water, Is.Not.Null, "the pool has a blue interior");

            var bounds = water.GetComponent<Renderer>().bounds;
            Assert.That(bounds.size.x, Is.EqualTo(PoolPlacement.PoolInnerDiameter).Within(Tolerance),
                "the water surface is inset within the shell wall");
            Assert.That(bounds.max.y, Is.EqualTo(PoolPlacement.PoolWaterSurfaceHeight).Within(Tolerance));
            Assert.That(bounds.max.y, Is.LessThan(PoolPlacement.PoolHeight - Tolerance),
                "the water sits below the shell rim");

            Assert.That(water.GetComponent<Renderer>().sharedMaterial.color,
                Is.EqualTo(CoreColors.FromHex(Palette.PoolWaterHex)));
        }

        [Test]
        public void Spawn_LeavesTheShellOpenAtTheTop_SoTheWaterIsVisible()
        {
            // A Unity Cylinder primitive is capped, so a gray cylinder would
            // hide the blue interior beneath it entirely. The shell is a
            // generated open-topped ring wall instead (the same reason #602
            // generated GroundRingMesh's annulus): nothing of the shell's mesh
            // spans the middle above the water.
            var shell = view.transform.Find(PoolView.ShellName);
            var mesh = shell.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh, Is.Not.Null);

            var innerRadius = PoolPlacement.PoolInnerDiameter / 2f;
            var localInnerRadius = innerRadius / PoolPlacement.PoolOuterDiameter;
            foreach (var vertex in mesh.vertices)
            {
                var radius = Mathf.Sqrt(vertex.x * vertex.x + vertex.z * vertex.z);
                Assert.That(radius, Is.GreaterThanOrEqualTo(localInnerRadius - Tolerance),
                    "no shell geometry reaches across the open interior");
            }
        }

        [Test]
        public void Spawn_IsPurelyDecorative_WithNoCollider()
        {
            // #740 out of scope: no tap target and no physics on the pool. A
            // stray collider on a yard object quietly swallows taps meant for
            // what is underneath it (the #703 delivered-package lesson).
            Assert.That(view.GetComponentsInChildren<Collider>(), Is.Empty);
        }

        [Test]
        public void Spawn_ParentsThePoolToTheWorldRoot()
        {
            Assert.That(view.transform.parent, Is.EqualTo(parent.transform));
            Assert.That(parent.transform.Cast<Transform>().Count(), Is.EqualTo(1));
        }
    }
}
