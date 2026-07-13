using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// Kit-art path of the world (#121 roads, #122 houses, toward #6):
    /// with the Kenney City Kit Roads / City Kit Suburban assets staged
    /// under Resources, WorldBuilder must build the road corridor from
    /// imported tiles instead of the primitive road cube, and each house
    /// from its mapped suburban model instead of primitive walls/roof —
    /// while the graybox primitive path stays intact as the fallback
    /// (pinned via WorldBuilder.ForcePrimitiveFallback, the EditMode seam
    /// that simulates the assets being absent).
    /// </summary>
    public class WorldKitArtTests
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

        private IEnumerable<Transform> Children()
        {
            return root.transform.Cast<Transform>();
        }

        [Test]
        public void KitAssets_AreStagedAndLoadable()
        {
            // Guard: if staging under .../Resources/ ever breaks, the other
            // tests here must not silently pass through the primitive
            // fallback. Load keys are relative to each Resources folder
            // (the exact mistake fixed in 505278e), so they are bare names.
            foreach (var key in new[] { "road-straight", "road-crossroad", "road-crossing" })
            {
                Assert.That(Resources.Load<GameObject>(key), Is.Not.Null,
                    $"City Kit Roads tile '{key}' must be loadable from Resources");
            }

            foreach (var houseId in new[] { 1, 2, 3, 4 })
            {
                var key = WorldBuilder.HouseModelResourcePath(houseId);
                Assert.That(Resources.Load<GameObject>(key), Is.Not.Null,
                    $"City Kit Suburban model '{key}' (house {houseId}) must be loadable from Resources");
            }
        }

        [Test]
        public void RoadCorridor_IsBuiltFromKitTiles_NotThePrimitiveRoadCube()
        {
            // #121: both "Road - X" objects survive as logical containers
            // (their name is the scene contract other tests rely on), but
            // they no longer render a primitive cube themselves — they hold
            // imported tile children instead.
            var roads = Children().Where(t => t.name.StartsWith(WorldBuilder.RoadNamePrefix)).ToList();
            Assert.That(roads.Count, Is.EqualTo(2));

            foreach (var road in roads)
            {
                Assert.That(road.GetComponent<Renderer>(), Is.Null,
                    $"{road.name} must not be a primitive road cube in the kit path");

                var tiles = road.Cast<Transform>()
                    .Where(t => t.name.StartsWith(WorldBuilder.RoadTileNamePrefix))
                    .ToList();
                Assert.That(tiles, Is.Not.Empty, $"{road.name} should contain kit road tiles");

                foreach (var tile in tiles)
                {
                    Assert.That(tile.GetComponentsInChildren<MeshFilter>().Any(f => f.sharedMesh != null),
                        Is.True, $"{tile.name} should carry an imported mesh");
                }
            }

            // One crossroad tile sits at the intersection itself.
            var intersection = root.transform.Find(WorldBuilder.IntersectionTileName);
            Assert.That(intersection, Is.Not.Null, "intersection crossroad tile missing");
            Assert.That(intersection.position.x, Is.EqualTo(NeighborhoodLayout.Intersection.X).Within(0.001f));
            Assert.That(intersection.position.z, Is.EqualTo(NeighborhoodLayout.Intersection.Z).Within(0.001f));
        }

        [Test]
        public void KitPath_BuildsNoPrimitiveStripsOnTheRoadCorridor()
        {
            // The kit tiles model their own sidewalks and curbs, so the
            // primitive verge/sidewalk strips and crosswalk cubes must not
            // also be built on the corridor. (Core's WalkNetwork — where
            // dogs actually walk — is untouched; this is visual only.)
            var strips = Children().Where(t =>
                    t.name.StartsWith(WorldBuilder.VergeNamePrefix)
                    || t.name.StartsWith(WorldBuilder.SidewalkNamePrefix)
                    || t.name.StartsWith(WorldBuilder.CrosswalkNamePrefix))
                .Select(t => t.name)
                .ToList();

            Assert.That(strips, Is.Empty);
        }

        [Test]
        public void StraightTiles_CoverBothArmsOfEachStreet_OnItsCenterline()
        {
            // Uniform tile scale 10 -> each tile is 10x10 with a 6m road
            // band matching WorldDimensions.RoadWidth. Arms are tiled every
            // 10m outward from the intersection tile.
            foreach (var road in NeighborhoodLayout.Roads)
            {
                var parent = root.transform.Find(WorldBuilder.RoadNamePrefix + road.Orientation);
                var tiles = parent.Cast<Transform>()
                    .Where(t => t.name.StartsWith(WorldBuilder.RoadTileNamePrefix))
                    .ToList();

                // HalfLength 26 with the crossroad tile covering ±5 leaves
                // room for 2 whole tiles per arm (centers ±10 and ±20).
                Assert.That(tiles.Count, Is.EqualTo(4), $"{parent.name} tile count");

                var isNorthSouth = road.Orientation == StreetOrientation.NorthSouth;
                var expectedAlong = new[] { -20f, -10f, 10f, 20f };
                var actualAlong = tiles
                    .Select(t => isNorthSouth ? t.position.z : t.position.x)
                    .OrderBy(v => v)
                    .ToArray();
                Assert.That(actualAlong, Is.EqualTo(expectedAlong).Within(0.001f));

                foreach (var tile in tiles)
                {
                    var perpendicular = isNorthSouth ? tile.position.x : tile.position.z;
                    Assert.That(perpendicular, Is.EqualTo(0f).Within(0.001f),
                        $"{tile.name} must sit on the road centerline");
                    Assert.That(tile.position.y, Is.EqualTo(0f).Within(0.001f),
                        $"{tile.name} has a ground-level pivot and must sit at y=0");
                    Assert.That(tile.localScale.x, Is.EqualTo(10f).Within(0.001f),
                        $"{tile.name} must use the uniform x10 tile scale");
                }
            }
        }

        [Test]
        public void CrossingTiles_SitWhereTheWalkNetworkDefinesCrosswalkEdges()
        {
            // #121: every WalkNetwork Crosswalk edge adjacent to the
            // intersection must fall inside the span of a road-crossing
            // tile of the road it crosses (the tile replacing the plain
            // straight there). The edge sits at ±5.5m; the first tile of
            // each arm spans 5..15, so it is the crossing tile.
            foreach (var road in NeighborhoodLayout.Roads)
            {
                var isNorthSouth = road.Orientation == StreetOrientation.NorthSouth;
                var parent = root.transform.Find(WorldBuilder.RoadNamePrefix + road.Orientation);
                var crossingTiles = parent.Cast<Transform>()
                    .Where(t => t.name.Contains("Crossing"))
                    .ToList();

                // A crosswalk edge spanning X crosses the north-south road;
                // one spanning Z crosses the east-west road.
                var edges = NeighborhoodLayout.WalkNetwork.Edges
                    .Where(e => e.Kind == WalkEdgeKind.Crosswalk)
                    .Where(e => isNorthSouth
                        ? Mathf.Abs(e.A.Z - e.B.Z) < 0.01f
                        : Mathf.Abs(e.A.X - e.B.X) < 0.01f)
                    .ToList();
                Assert.That(edges, Is.Not.Empty, "sanity: each road has crosswalk edges");
                Assert.That(crossingTiles.Count, Is.EqualTo(edges.Count),
                    $"{parent.name} should have one crossing tile per crosswalk edge");

                foreach (var edge in edges)
                {
                    var edgeAlong = isNorthSouth ? edge.A.Z : edge.A.X;
                    var covered = crossingTiles.Any(t =>
                    {
                        var tileAlong = isNorthSouth ? t.position.z : t.position.x;
                        return Mathf.Abs(edgeAlong - tileAlong) <= 5f + 0.001f;
                    });
                    Assert.That(covered, Is.True,
                        $"crosswalk edge at along={edgeAlong} on {road.Orientation} has no crossing tile");
                }
            }
        }

        [Test]
        public void Houses_UseTheirMappedSuburbanModel_InsteadOfPrimitives()
        {
            // #122: each house id renders its mapped City Kit Suburban
            // model; the primitive walls/roof/porch are not built.
            var views = root.GetComponentsInChildren<HouseView>();
            Assert.That(views.Length, Is.EqualTo(4));

            foreach (var view in views)
            {
                var sourceModel = Resources.Load<GameObject>(WorldBuilder.HouseModelResourcePath(view.HouseId));
                var sourceMeshes = sourceModel.GetComponentsInChildren<MeshFilter>()
                    .Select(f => f.sharedMesh)
                    .Where(m => m != null)
                    .ToList();
                Assert.That(sourceMeshes, Is.Not.Empty, "sanity: mapped model has meshes");

                var houseMeshes = view.GetComponentsInChildren<MeshFilter>()
                    .Select(f => f.sharedMesh)
                    .Where(m => m != null)
                    .ToList();
                Assert.That(houseMeshes, Is.Not.Empty, $"house {view.HouseId} renders no mesh");
                Assert.That(houseMeshes, Is.SubsetOf(sourceMeshes),
                    $"house {view.HouseId} must render meshes from {WorldBuilder.HouseModelResourcePath(view.HouseId)}");

                var childNames = view.GetComponentsInChildren<Transform>().Select(t => t.name).ToList();
                Assert.That(childNames, Has.No.Member("Walls"));
                Assert.That(childNames, Has.No.Member("Roof"));
                Assert.That(childNames, Has.No.Member("Porch"));
            }
        }

        [Test]
        public void Houses_KeepATapCollider_CoveringTheCameraRigTapPoint()
        {
            // #122: the imported FBX carries no collider, so HouseView's
            // object must get a BoxCollider fitted to the model's renderer
            // bounds — covering position + 1m up, the exact sample point
            // CameraRigTests.TapOnAHouse raycasts through TapRouter.
            Physics.SyncTransforms();

            foreach (var view in root.GetComponentsInChildren<HouseView>())
            {
                var collider = view.GetComponent<BoxCollider>();
                Assert.That(collider, Is.Not.Null, $"house {view.HouseId} lost its tap collider");
                Assert.That(collider.bounds.Contains(view.transform.position + Vector3.up * 1f),
                    Is.True, $"house {view.HouseId} collider misses the tap sample point");
            }
        }

        [Test]
        public void Houses_KeepTheirWindowAnchor_AtTheSameLocalPose()
        {
            // #9 depends on this exact anchor: dogs' window-watching pose
            // renders at (facing*2.1, 1.5, facing*2.1) toward the
            // intersection. The model swap must not move it (fine-tuning to
            // each model's wall is a follow-up).
            foreach (var view in root.GetComponentsInChildren<HouseView>())
            {
                var lot = NeighborhoodLayout.GetHouseLot(view.HouseId);
                var facing = new Vector3(-Mathf.Sign(lot.Position.X), 0f, -Mathf.Sign(lot.Position.Z)).normalized;

                Assert.That(view.WindowAnchor, Is.Not.Null);
                var local = view.WindowAnchor.localPosition;
                Assert.That(local.x, Is.EqualTo(facing.x * 2.1f).Within(0.001f));
                Assert.That(local.y, Is.EqualTo(1.5f).Within(0.001f));
                Assert.That(local.z, Is.EqualTo(facing.z * 2.1f).Within(0.001f));
            }
        }

        [Test]
        public void ForcedPrimitiveFallback_StillBuildsTheFullGrayboxWorld()
        {
            // The fallback when Resources are absent must stay intact. The
            // real absent-asset condition can't be simulated in a project
            // that has the assets imported, so WorldBuilder exposes
            // ForcePrimitiveFallback as the test seam that routes through
            // the same null-model branch. The detailed graybox contract
            // (verges, crosswalk quads, colors, roof shapes) stays pinned
            // in WorldBuilderTests; this covers the switch itself.
            Object.DestroyImmediate(root);
            WorldBuilder.ForcePrimitiveFallback = true;
            root = WorldBuilder.Build(GameState.CreateNew());

            var roadCubes = Children()
                .Where(t => t.name.StartsWith(WorldBuilder.RoadNamePrefix))
                .ToList();
            Assert.That(roadCubes.Count, Is.EqualTo(2));
            Assert.That(roadCubes.All(r => r.GetComponent<MeshRenderer>() != null), Is.True,
                "fallback roads are primitive cubes again");

            Assert.That(root.GetComponentsInChildren<Transform>().Any(
                    t => t.name.StartsWith(WorldBuilder.RoadTileNamePrefix)),
                Is.False, "no kit tiles in the forced fallback");

            foreach (var view in root.GetComponentsInChildren<HouseView>())
            {
                Assert.That(view.transform.Find("Walls"), Is.Not.Null,
                    $"house {view.HouseId} should be primitive walls in the fallback");
            }
        }
    }
}
