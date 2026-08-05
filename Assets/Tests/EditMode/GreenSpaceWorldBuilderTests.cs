using System.Linq;
using Doggiehood.Core.Onboarding;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #539: the Unity world-build's no-op handling of a placed
    /// <see cref="TileType.GreenSpace"/> tile. A green space carries no road art
    /// and no lots and is never recorded in <see cref="GameState.UnlockedTiles"/>
    /// (the road-art and lot passes iterate that list), so it contributes no
    /// mesh, no lot marker, and no empty-lot slab — while the grass ground plane
    /// still grows to cover it because <see cref="WorldBuilder.ResizeGroundToMap"/>
    /// sizes from the whole <see cref="GameState.Map"/>.
    /// </summary>
    public class GreenSpaceWorldBuilderTests
    {
        private static readonly TileCoordinate GreenOne = new TileCoordinate(1, 1);
        private static readonly TileCoordinate GreenTwo = new TileCoordinate(2, 1);

        private GameObject root;

        [TearDown]
        public void Cleanup()
        {
            WorldBuilder.ForcePrimitiveFallback = false;
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        // A synthetic target map (the shipped authored map has no green space
        // yet): an origin FourWay, an L of roads, and two green spaces arranged
        // so unlocking the roads auto-activates both onto the live Map.
        private static GameState StateWithActivatedGreenSpaces()
        {
            var target = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            target.Place(new TileCoordinate(0, 1), TileType.StraightNS);
            target.Place(new TileCoordinate(1, 0), TileType.StraightEW);
            target.Place(new TileCoordinate(2, 0), TileType.StraightEW);
            target.Place(GreenOne, TileType.GreenSpace);
            target.Place(GreenTwo, TileType.GreenSpace);

            var state = GameState.CreateNew();
            state.RestoreRewardChainStep(OnboardingRewardStep.Done);
            state.SetTargetMap(target);
            state.Wallet.Deposit(10000);
            state.TryUnlockTile(new TileCoordinate(1, 0));
            state.TryUnlockTile(new TileCoordinate(2, 0));
            state.TryUnlockTile(new TileCoordinate(0, 1));
            return state;
        }

        [Test]
        public void Build_WithPlacedGreenSpace_DoesNotThrow_AndRendersNoRoadOrLotForIt()
        {
            var state = StateWithActivatedGreenSpaces();
            Assert.That(state.Map.HasTileAt(GreenOne), Is.True, "precondition: green#1 activated onto the map");
            Assert.That(state.Map.HasTileAt(GreenTwo), Is.True, "precondition: green#2 activated onto the map");

            // The build itself must not throw on a Map that carries a roadless,
            // lotless green-space tile.
            root = WorldBuilder.Build(state);

            var greenOneRoad = WorldBuilder.ZoneRoadNamePrefix + GreenOne.Col + "," + GreenOne.Row;
            var greenTwoRoad = WorldBuilder.ZoneRoadNamePrefix + GreenTwo.Col + "," + GreenTwo.Row;
            var names = root.transform.Cast<Transform>().Select(t => t.name).ToList();
            Assert.That(names, Does.Not.Contain(greenOneRoad),
                "a green space contributes no road-art container");
            Assert.That(names, Does.Not.Contain(greenTwoRoad));

            Assert.That(state.LotsForUnlockedTile(GreenOne), Is.Empty,
                "a green space offers no buildable lots, so no empty-lot slab is built");
        }

        [Test]
        public void ResizeGroundToMap_GrowsToCoverAPlacedGreenSpaceTile()
        {
            var state = StateWithActivatedGreenSpaces();
            root = WorldBuilder.Build(state);

            var ground = root.transform.Find(WorldBuilder.GroundName);
            Assert.That(ground, Is.Not.Null, "the world has a ground plane");

            var renderer = ground.GetComponent<Renderer>();
            Assert.That(renderer, Is.Not.Null);

            // The green space is the map's north-east-most tile; the grass plane
            // (sized from the whole Map) must extend to cover its centre.
            var center = TileGeometry.CenterOf(GreenTwo);
            var bounds = renderer.bounds;
            Assert.That(center.X, Is.InRange(bounds.min.x, bounds.max.x),
                "the ground covers the green space's X");
            Assert.That(center.Z, Is.InRange(bounds.min.z, bounds.max.z),
                "the ground covers the green space's Z");
        }
    }
}
