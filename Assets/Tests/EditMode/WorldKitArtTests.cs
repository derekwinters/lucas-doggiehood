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
            // road-crossroad-path is the crosswalk-striped 4-way variant
            // (Derek's 2026-07-13 Editor review: the intersection should
            // have painted crosswalks; the plain road-crossroad is gone).
            foreach (var key in new[] { "road-straight", "road-crossroad-path", "road-crossing" })
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

            // #128: the front-walkway paver piece, staged from the same
            // City Kit Suburban kit as the houses.
            Assert.That(Resources.Load<GameObject>(WorldBuilder.WalkwayPieceResource), Is.Not.Null,
                $"City Kit Suburban piece '{WorldBuilder.WalkwayPieceResource}' must be loadable from Resources");
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
        public void EveryCrosswalkEdge_IsCoveredByTheCrossroadTileOrACrossingTile()
        {
            // #121: every WalkNetwork Crosswalk edge must fall inside the
            // span of a road tile that models a pedestrian crossing — the
            // intersection's own crossroad tile (whose corner sidewalks are
            // where crossings meet) or a dedicated road-crossing tile on an
            // arm. At GrassVergeWidth 0.75m (Derek's 2026-07-13 midpoint
            // request) the crosswalk edges sit at ±4.75m — inside the
            // crossroad tile's own ±5m span, on its modeled pavement band
            // (3-5m) where the road-crossroad-path variant paints its
            // zebra stripes, so no arm tile needs to be a road-crossing
            // tile and none should be built.
            var halfTile = WorldBuilder.RoadTileScale / 2f;
            var intersection = root.transform.Find(WorldBuilder.IntersectionTileName);
            Assert.That(intersection, Is.Not.Null, "sanity: intersection crossroad tile exists");

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

                var expectedCrossingTiles = 0;
                foreach (var edge in edges)
                {
                    var edgeAlong = isNorthSouth ? edge.A.Z : edge.A.X;
                    var intersectionAlong = isNorthSouth ? intersection.position.z : intersection.position.x;

                    if (Mathf.Abs(edgeAlong - intersectionAlong) <= halfTile + 0.001f)
                    {
                        continue; // covered by the crossroad tile itself
                    }

                    expectedCrossingTiles++;
                    var covered = crossingTiles.Any(t =>
                    {
                        var tileAlong = isNorthSouth ? t.position.z : t.position.x;
                        return Mathf.Abs(edgeAlong - tileAlong) <= halfTile + 0.001f;
                    });
                    Assert.That(covered, Is.True,
                        $"crosswalk edge at along={edgeAlong} on {road.Orientation} has no crossing tile");
                }

                Assert.That(crossingTiles.Count, Is.EqualTo(expectedCrossingTiles),
                    $"{parent.name} should only have crossing tiles where a crosswalk edge is outside the crossroad tile");
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
        public void HouseModels_ScaleToAnEightMeterFootprint()
        {
            // Derek's Editor feedback on the first kit-house pass: at the
            // old 4.2m target the models read far too small against the
            // kit roads. New target is 8m: lots sit at +-14 and the
            // logical sidewalk's outer edge is at 5.75m (0.75m verge), so
            // an 8m-wide house spans 10-18 on its lot — a sensible front
            // yard.
            foreach (var view in root.GetComponentsInChildren<HouseView>())
            {
                var model = view.transform.Find("Model");
                Assert.That(model, Is.Not.Null, $"house {view.HouseId} has no kit model child");

                var renderers = model.GetComponentsInChildren<Renderer>();
                Assert.That(renderers, Is.Not.Empty, $"house {view.HouseId} model renders nothing");

                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                }

                // Rotation is cardinal (see the facing test), so the AABB's
                // max horizontal extent is the model's true footprint.
                var footprint = Mathf.Max(bounds.size.x, bounds.size.z);
                Assert.That(footprint, Is.EqualTo(8f).Within(0.25f),
                    $"house {view.HouseId} footprint should land on the 8m target");
            }
        }

        [Test]
        public void HouseModels_FaceTheirWalkwayRoad_SquarelyAtACardinalYaw()
        {
            // Derek's Editor feedback on the first kit-house pass: houses
            // yawed diagonally toward the world origin looked scattered,
            // and the assumed model-forward was wrong (the SW house's door
            // faced away from the neighborhood). Each house must now face
            // SQUARELY toward the road its front walkway (#128 — it
            // replaced the driveway stub) attaches to — the WalkNetwork's
            // FrontWalkway edge for the lot is that association, and its B
            // endpoint is the sidewalk attach point.
            // HouseModelYawOffsetDegrees (180, per the screenshot evidence
            // that doors pointed opposite the look direction at 0) is the
            // single art-side correction on top of that logical facing —
            // one flip fixes all four houses if it's still wrong.
            foreach (var view in root.GetComponentsInChildren<HouseView>())
            {
                var lot = NeighborhoodLayout.GetHouseLot(view.HouseId);
                var model = view.transform.Find("Model");
                Assert.That(model, Is.Not.Null, $"house {view.HouseId} has no kit model child");

                // Undo the art-side yaw correction to recover the logical
                // look direction the builder aimed at.
                var look = model.localRotation
                    * Quaternion.Euler(0f, -WorldBuilder.HouseModelYawOffsetDegrees, 0f)
                    * Vector3.forward;

                // Square to the street grid: exactly one horizontal axis.
                Assert.That(Mathf.Abs(look.x) < 0.001f || Mathf.Abs(look.z) < 0.001f, Is.True,
                    $"house {view.HouseId} look direction {look} is not cardinal");
                Assert.That(Mathf.Abs(look.y), Is.LessThan(0.001f),
                    $"house {view.HouseId} look direction {look} is not horizontal");

                Assert.That(NeighborhoodLayout.WalkNetwork.TryGetFrontWalkway(view.HouseId, out var walkway),
                    Is.True, $"house {view.HouseId} has no front walkway");
                var attach = walkway.B;
                var toAttach = new Vector3(attach.X - lot.Position.X, 0f, attach.Z - lot.Position.Z).normalized;

                Assert.That(Vector3.Dot(look, toAttach), Is.GreaterThan(0.99f),
                    $"house {view.HouseId} must face its walkway attach point {attach} (look {look})");
            }
        }

        [Test]
        public void FrontWalkways_AreTiledFromKitPathPieces_AlongTheCoreSegment()
        {
            // #128: each house gets a "Walkway - N" container whose
            // children are instantiated City Kit Suburban path pieces,
            // placed exactly where Core's WalkwayTiling says — flat on the
            // ground, tiling the door -> sidewalk walkway edge.
            var source = Resources.Load<GameObject>(WorldBuilder.WalkwayPieceResource);
            Assert.That(source, Is.Not.Null, "sanity: walkway piece staged");
            var sourceMeshes = source.GetComponentsInChildren<MeshFilter>()
                .Select(f => f.sharedMesh)
                .Where(m => m != null)
                .ToList();
            Assert.That(sourceMeshes, Is.Not.Empty, "sanity: walkway piece has meshes");

            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                Assert.That(NeighborhoodLayout.WalkNetwork.TryGetFrontWalkway(lot.HouseId, out var walkway),
                    Is.True, $"house {lot.HouseId} has no front walkway");
                var expected = WalkwayTiling.PiecesAlong(walkway);

                var container = root.transform.Find(WorldBuilder.WalkwayNamePrefix + lot.HouseId);
                Assert.That(container, Is.Not.Null, $"missing walkway container for house {lot.HouseId}");

                var pieces = container.Cast<Transform>().ToList();
                Assert.That(pieces.Count, Is.EqualTo(expected.Count),
                    $"house {lot.HouseId} walkway piece count");

                for (var i = 0; i < pieces.Count; i++)
                {
                    var piece = pieces[i];
                    Assert.That(piece.position.x, Is.EqualTo(expected[i].Position.X).Within(0.001f),
                        $"house {lot.HouseId} piece {i} X");
                    Assert.That(piece.position.z, Is.EqualTo(expected[i].Position.Z).Within(0.001f),
                        $"house {lot.HouseId} piece {i} Z");
                    Assert.That(piece.position.y, Is.EqualTo(0f).Within(0.001f),
                        $"house {lot.HouseId} piece {i} must sit on the ground");

                    var pieceMeshes = piece.GetComponentsInChildren<MeshFilter>()
                        .Select(f => f.sharedMesh)
                        .Where(m => m != null)
                        .ToList();
                    Assert.That(pieceMeshes, Is.Not.Empty, $"house {lot.HouseId} piece {i} renders no mesh");
                    Assert.That(pieceMeshes, Is.SubsetOf(sourceMeshes),
                        $"house {lot.HouseId} piece {i} must render the kit path piece");
                }
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

            // #128: the front walkways stay in the graybox world too — as
            // one flat primitive strip per house instead of kit pieces.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var container = root.transform.Find(WorldBuilder.WalkwayNamePrefix + lot.HouseId);
                Assert.That(container, Is.Not.Null,
                    $"missing fallback walkway for house {lot.HouseId}");
                Assert.That(container.GetComponentsInChildren<MeshRenderer>(), Is.Not.Empty,
                    $"house {lot.HouseId} fallback walkway renders nothing");
            }
        }
    }
}
