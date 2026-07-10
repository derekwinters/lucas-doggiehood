using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Art;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Doggiehood.Unity.EditModeTests
{
    public class WorldBuilderTests
    {
        private GameObject root;

        [SetUp]
        public void BuildWorld()
        {
            root = WorldBuilder.Build(GameState.CreateNew());
        }

        [TearDown]
        public void DestroyWorld()
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        private IEnumerable<Transform> Children()
        {
            return root.transform.Cast<Transform>();
        }

        [Test]
        public void BuildsExactlyFourHouses_OnTheirLayoutLots()
        {
            // #38: the scene contains exactly 4 houses positioned per #7.
            var houses = Children().Where(t => t.name.StartsWith(WorldBuilder.HouseNamePrefix)).ToList();

            Assert.That(houses.Count, Is.EqualTo(4));

            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var house = houses.SingleOrDefault(h => h.name == WorldBuilder.HouseNamePrefix + lot.HouseId);
                Assert.That(house, Is.Not.Null, $"missing house {lot.HouseId}");
                Assert.That(house.position.x, Is.EqualTo(lot.Position.X).Within(0.001f));
                Assert.That(house.position.z, Is.EqualTo(lot.Position.Z).Within(0.001f));
            }
        }

        [Test]
        public void EveryHouse_HasAViewWithItsIdAndDistinctStyleGeometry()
        {
            var views = root.GetComponentsInChildren<HouseView>();

            Assert.That(views.Length, Is.EqualTo(4));
            Assert.That(views.Select(v => v.HouseId), Is.EquivalentTo(new[] { 1, 2, 3, 4 }));

            // #64: gambrel is the only two-block roof; porches only where styled.
            foreach (var view in views)
            {
                var style = HouseStyleTable.ForHouse(view.HouseId);
                var roofBlocks = view.GetComponentsInChildren<Transform>().Count(t => t.name == "Roof");
                Assert.That(roofBlocks, Is.EqualTo(style.RoofShape == RoofShape.Gambrel ? 2 : 1));

                var hasPorch = view.GetComponentsInChildren<Transform>().Any(t => t.name == "Porch");
                Assert.That(hasPorch, Is.EqualTo(style.HasPorch));
            }
        }

        [Test]
        public void BuildsBothStreets()
        {
            var streets = Children().Where(t => t.name.StartsWith(WorldBuilder.StreetNamePrefix)).ToList();

            Assert.That(streets.Count, Is.EqualTo(2));
        }

        [Test]
        public void SunMatchesTheDaytimeLightingPreset()
        {
            // #39: single fixed daytime setup.
            var sun = root.GetComponentsInChildren<Light>().Single();

            Assert.That(sun.type, Is.EqualTo(LightType.Directional));
            Assert.That(sun.intensity, Is.EqualTo(LightingPreset.SunIntensity).Within(0.001f));

            var euler = sun.transform.rotation.eulerAngles;
            Assert.That(euler.x, Is.EqualTo(LightingPreset.SunPitchDegrees).Within(0.01f));
            Assert.That(euler.y, Is.EqualTo(LightingPreset.SunYawDegrees).Within(0.01f));

            var expected = CoreColors.FromHex(LightingPreset.SunColorHex);
            Assert.That(sun.color.r, Is.EqualTo(expected.r).Within(0.001f));
            Assert.That(sun.color.g, Is.EqualTo(expected.g).Within(0.001f));
            Assert.That(sun.color.b, Is.EqualTo(expected.b).Within(0.001f));
        }

        [Test]
        public void AmbientLighting_IsTheFlatDaytimeAmbient()
        {
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Flat));

            var expected = CoreColors.FromHex(LightingPreset.AmbientColorHex);
            Assert.That(RenderSettings.ambientLight.r, Is.EqualTo(expected.r).Within(0.001f));
            Assert.That(RenderSettings.ambientLight.g, Is.EqualTo(expected.g).Within(0.001f));
            Assert.That(RenderSettings.ambientLight.b, Is.EqualTo(expected.b).Within(0.001f));
        }

        [Test]
        public void WorldContainsNoPlayerObjects()
        {
            // #19: no player avatar anywhere in the built world.
            var offenders = root.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.ToLowerInvariant().Contains("player")
                    || t.name.ToLowerInvariant().Contains("avatar"))
                .Select(t => t.name)
                .ToList();

            Assert.That(offenders, Is.Empty);
        }
    }
}
