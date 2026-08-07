using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Art;
using Doggiehood.Core.Debugging;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.Onboarding;
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
        private GameState state;

        [SetUp]
        public void BuildWorld()
        {
            WorldBuilder.ForcePrimitiveFallback = false;
            state = GameState.CreateNew();
            root = WorldBuilder.Build(state);
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

        /// <summary>
        /// Rebuilds the world through the graybox primitive path. The tests
        /// that assert primitive-specific geometry (verge/sidewalk strips,
        /// crosswalk quads, roof blocks, palette colors) now pin the
        /// fallback contract for when the Kenney kit assets can't be loaded
        /// — the kit path's equivalent contract lives in WorldKitArtTests.
        /// </summary>
        private void RebuildWithPrimitiveFallback()
        {
            Object.DestroyImmediate(root);
            WorldBuilder.ForcePrimitiveFallback = true;
            state = GameState.CreateNew();
            root = WorldBuilder.Build(state);
        }

        private IEnumerable<Transform> Children()
        {
            return root.transform.Cast<Transform>();
        }

        private int FenceContainerCount()
        {
            return Children().Count(t => t.name.StartsWith(WorldBuilder.FenceNamePrefix));
        }

        [Test]
        public void RebuildFences_ShowsAndHidesFencesOnALiveBuild()
        {
            // #219: the Debug tab's fence toggle sets ForceFencesVisible then
            // rebuilds just the fences on the live world (no full reload), so
            // #152 can be eyeballed on-device — fences appear when forced on
            // and disappear when forced off.
            var original = WorldBuilder.ForceFencesVisible;
            try
            {
                WorldBuilder.ForceFencesVisible = false;
                WorldBuilder.RebuildFences(root.transform, state);
                Assert.That(FenceContainerCount(), Is.EqualTo(0), "default lots are unfenced");

                WorldBuilder.ForceFencesVisible = true;
                WorldBuilder.RebuildFences(root.transform, state);
                Assert.That(FenceContainerCount(), Is.GreaterThan(0), "fences appear on a live rebuild");

                WorldBuilder.ForceFencesVisible = false;
                WorldBuilder.RebuildFences(root.transform, state);
                Assert.That(FenceContainerCount(), Is.EqualTo(0), "fences disappear on a live rebuild");
            }
            finally
            {
                WorldBuilder.ForceFencesVisible = original;
            }
        }

        [Test]
        public void BuildGround_UsesTheMatchedGrassColor_WhenDebugElementColorsOff()
        {
            // #611 regression: with the diagnostic toggle off (default), the
            // ground stays the exact matched Palette.GrassHex — byte-identical to
            // today, so normal play is untouched.
            var original = WorldBuilder.ShowDebugElementColors;
            try
            {
                WorldBuilder.ShowDebugElementColors = false;
                WorldBuilder.RepaintGround(root.transform);

                var ground = root.transform.Find(WorldBuilder.GroundName);
                Assert.That(ground.GetComponent<Renderer>().sharedMaterial.color,
                    Is.EqualTo(CoreColors.FromHex(Palette.GrassHex)),
                    "debug off keeps the matched grass green");
            }
            finally
            {
                WorldBuilder.ShowDebugElementColors = original;
            }
        }

        [Test]
        public void BuildGround_UsesTheDebugGroundColor_WhenDebugElementColorsOn()
        {
            // #611: with the diagnostic toggle on, the ground plane is painted the
            // loud, obviously-fake debug ground colour (not grass green), so a
            // playtester can tell it apart from the camera backstop.
            var original = WorldBuilder.ShowDebugElementColors;
            try
            {
                WorldBuilder.ShowDebugElementColors = true;
                Object.DestroyImmediate(root);
                root = WorldBuilder.Build(state);

                var ground = root.transform.Find(WorldBuilder.GroundName);
                Assert.That(ground.GetComponent<Renderer>().sharedMaterial.color,
                    Is.EqualTo(CoreColors.FromHex(DebugElementColors.GroundDebugHex)),
                    "debug on paints the ground the debug ground colour");
                Assert.That(ground.GetComponent<Renderer>().sharedMaterial.color,
                    Is.Not.EqualTo(CoreColors.FromHex(Palette.GrassHex)),
                    "and it is no longer the matched grass green");
            }
            finally
            {
                WorldBuilder.ShowDebugElementColors = original;
            }
        }

        [Test]
        public void RepaintGround_SwapsTheGroundColorLive_WithoutAFullRebuild()
        {
            // #611: flipping the toggle on-device must repaint the existing ground
            // plane live (no restart), mirroring RebuildFences.
            var original = WorldBuilder.ShowDebugElementColors;
            try
            {
                var ground = root.transform.Find(WorldBuilder.GroundName);

                WorldBuilder.ShowDebugElementColors = true;
                WorldBuilder.RepaintGround(root.transform);
                Assert.That(ground.GetComponent<Renderer>().sharedMaterial.color,
                    Is.EqualTo(CoreColors.FromHex(DebugElementColors.GroundDebugHex)),
                    "the live repaint swaps to the debug ground colour");

                WorldBuilder.ShowDebugElementColors = false;
                WorldBuilder.RepaintGround(root.transform);
                Assert.That(ground.GetComponent<Renderer>().sharedMaterial.color,
                    Is.EqualTo(CoreColors.FromHex(Palette.GrassHex)),
                    "and back to the matched grass green when toggled off");
            }
            finally
            {
                WorldBuilder.ShowDebugElementColors = original;
            }
        }

        [Test]
        public void PurchasedFence_RendersFromPlacedItems_OnRebuild()
        {
            // #318: a completed fence-purchase quest records a
            // PlacedItem(houseId, "fence"); WorldBuilder derives fence
            // visibility from that persisted state (LotFence.IsFenced), so a
            // rebuild shows exactly that house's fence with no static flag and
            // without ForceFencesVisible.
            var original = WorldBuilder.ForceFencesVisible;
            try
            {
                WorldBuilder.ForceFencesVisible = false;
                WorldBuilder.RebuildFences(root.transform, state);
                Assert.That(FenceContainerCount(), Is.EqualTo(0), "no fences bought yet");

                var houseId = state.Houses.First().Id;
                state.AddPlacedItem(houseId, ItemCatalog.FenceItemName);
                WorldBuilder.RebuildFences(root.transform, state);

                Assert.That(FenceContainerCount(), Is.EqualTo(1),
                    "exactly the house with a purchased fence renders one");
                Assert.That(Children().Any(t => t.name == WorldBuilder.FenceNamePrefix + houseId),
                    Is.True, "the purchased fence is that house's container");
            }
            finally
            {
                WorldBuilder.ForceFencesVisible = original;
            }
        }

        [Test]
        public void BuildsExactlyFourHouses_AtTheirCoreFrontSetbackPositions()
        {
            // #38: the scene contains exactly 4 houses on the #7 lots —
            // and since #127 each stands at Core's front-setback position
            // (pulled from the lot center toward its facing street so the
            // facade sits FrontSetback from the sidewalk's outer edge),
            // not on the raw lot center. The setback math itself is pinned
            // by HousePlacementTests in the Core suite; this pins that
            // WorldBuilder consumes it rather than lot.Position.
            var houses = Children().Where(t => t.name.StartsWith(WorldBuilder.HouseNamePrefix)).ToList();

            Assert.That(houses.Count, Is.EqualTo(4));

            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var house = houses.SingleOrDefault(h => h.name == WorldBuilder.HouseNamePrefix + lot.HouseId);
                Assert.That(house, Is.Not.Null, $"missing house {lot.HouseId}");

                var expected = HousePlacement.Position(lot, WorldBuilder.HouseKitScale);
                Assert.That(house.position.x, Is.EqualTo(expected.X).Within(0.001f));
                Assert.That(house.position.z, Is.EqualTo(expected.Z).Within(0.001f));

                // Sanity that the contract is really the moved-toward-the-
                // street position, not the lot center it used to be.
                Assert.That(new Vector2(expected.X - lot.Position.X, expected.Z - lot.Position.Z).magnitude,
                    Is.GreaterThan(0.5f), $"house {lot.HouseId} should not sit on its lot center anymore");
            }
        }

        [Test]
        public void EveryHouse_HasAViewWithItsId_AndASinglePlainWallsBlock()
        {
            // #64: RoofShape/HasPorch (and their per-house hex colors) are
            // gone — the real per-house visual identity now comes from the
            // kit model + HouseStyleTable.TintVariant texture swap
            // (WorldKitArtTests), never built in this primitive-fallback
            // path. The fallback (only reached when the kit model itself
            // can't load) is simplified to one plain box — no procedural
            // roof/porch geometry keyed on removed fields.
            RebuildWithPrimitiveFallback();
            var views = root.GetComponentsInChildren<HouseView>();

            Assert.That(views.Length, Is.EqualTo(4));
            Assert.That(views.Select(v => v.HouseId), Is.EquivalentTo(new[] { 1, 2, 3, 4 }));

            foreach (var view in views)
            {
                var childNames = view.GetComponentsInChildren<Transform>().Select(t => t.name).ToList();
                Assert.That(childNames, Does.Contain("Walls"), $"house {view.HouseId} missing its fallback Walls block");
                Assert.That(childNames, Has.No.Member("Roof"), $"house {view.HouseId} still builds a removed Roof block");
                Assert.That(childNames, Has.No.Member("Porch"), $"house {view.HouseId} still builds a removed Porch block");
            }
        }

        [Test]
        public void VacantHouse_RendersGreyscaled_AndRestoresNormalTintOnceOccupied()
        {
            // #58: a vacant house's mesh renders with a flat desaturated
            // tint instead of its normal HouseStyleTable coloring; once a
            // dog moves in (House.MarkOccupied), rebuilding the same
            // logical house must render its normal tint again. Kit path —
            // this SetUp already staged the real City Kit Suburban models.
            var lot = NeighborhoodLayout.HouseLots.First();
            var house = new House(lot.HouseId, lot.Quadrant, isVacant: true);
            var container = new GameObject("VacancyTestContainer");
            var vacantTint = CoreColors.FromHex(Palette.VacantHouseTintHex);

            var vacantVisual = WorldBuilder.BuildHouse(container.transform, house);
            var vacantColors = vacantVisual.GetComponentsInChildren<Renderer>()
                .Select(r => r.sharedMaterial.color).ToList();

            Assert.That(vacantColors, Is.Not.Empty);
            Assert.That(vacantColors, Has.All.EqualTo(vacantTint),
                "every renderer on a vacant house should carry the flat vacancy tint");

            Object.DestroyImmediate(vacantVisual);
            house.MarkOccupied();
            var occupiedVisual = WorldBuilder.BuildHouse(container.transform, house);
            var occupiedColors = occupiedVisual.GetComponentsInChildren<Renderer>()
                .Select(r => r.sharedMaterial.color).ToList();

            Assert.That(occupiedColors, Has.None.EqualTo(vacantTint),
                "an occupied house must never keep the vacancy tint");

            Object.DestroyImmediate(container);
        }

        [Test]
        public void VacantHouse_RendersGreyscaled_InThePrimitiveFallbackPathToo()
        {
            // Same contract as above, but for the graybox fallback (only
            // reached when a house's kit model itself can't load).
            WorldBuilder.ForcePrimitiveFallback = true;
            var lot = NeighborhoodLayout.HouseLots.First();
            var house = new House(lot.HouseId, lot.Quadrant, isVacant: true);
            var container = new GameObject("VacancyTestContainer");
            var vacantTint = CoreColors.FromHex(Palette.VacantHouseTintHex);

            var vacantVisual = WorldBuilder.BuildHouse(container.transform, house);
            var walls = vacantVisual.GetComponentsInChildren<Transform>().Single(t => t.name == "Walls");

            Assert.That(walls.GetComponent<Renderer>().sharedMaterial.color, Is.EqualTo(vacantTint));

            Object.DestroyImmediate(vacantVisual);
            house.MarkOccupied();
            var occupiedVisual = WorldBuilder.BuildHouse(container.transform, house);
            var occupiedWalls = occupiedVisual.GetComponentsInChildren<Transform>().Single(t => t.name == "Walls");

            Assert.That(occupiedWalls.GetComponent<Renderer>().sharedMaterial.color, Is.Not.EqualTo(vacantTint));

            Object.DestroyImmediate(container);
        }

        [Test]
        public void BuildsNoEmptyLotMarkers_WhenNoZoneIsUnlocked()
        {
            // #57: a fresh GameState has no unlocked zones, so there is
            // nothing yet to build on.
            var markers = root.GetComponentsInChildren<EmptyLotView>();

            Assert.That(markers, Is.Empty);
        }

        [Test]
        public void BuildsAnEmptyLotMarker_ForEveryBuildableLot_InAnUnlockedZone()
        {
            // #57: the whole first zone is unlocked and empty — every one
            // of its lots gets a tappable "build here" marker.
            var state = GameState.CreateNew();
            state.Wallet.Deposit(100);
            state.SetTargetMap(FrontierEditModeWorld.LoadTargetMap());
            state.TryUnlockTile(FrontierEditModeWorld.FirstTile);

            Object.DestroyImmediate(root);
            root = WorldBuilder.Build(state);

            var markers = root.GetComponentsInChildren<EmptyLotView>();
            var zoneLots = state.LotsForUnlockedTile(FrontierEditModeWorld.FirstTile);
            Assert.That(markers.Select(m => m.HouseId), Is.EquivalentTo(zoneLots.Select(lot => lot.HouseId)));
        }

        [Test]
        public void BuildsNoEmptyLotMarker_ForALotThatAlreadyHasAHouse()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(150);
            state.SetTargetMap(FrontierEditModeWorld.LoadTargetMap());
            state.TryUnlockTile(FrontierEditModeWorld.FirstTile);
            var zoneLots = state.LotsForUnlockedTile(FrontierEditModeWorld.FirstTile);
            var builtLot = zoneLots[0];
            state.TryBuildHouse(builtLot.HouseId);

            Object.DestroyImmediate(root);
            root = WorldBuilder.Build(state);

            var markers = root.GetComponentsInChildren<EmptyLotView>();
            Assert.That(markers.Select(m => m.HouseId).ToList(), Has.No.Member(builtLot.HouseId));
            Assert.That(markers.Length, Is.EqualTo(zoneLots.Count - 1));
        }

        [Test]
        public void EmptyLotMarker_IsARaisedFoundationSlab_SittingOnTheGround()
        {
            // #300 (B): the empty-lot marker is restyled from the old flat
            // 0.2m tap-pad into a low raised graybox "foundation" slab that
            // reads as "a house goes here" — still a single primitive box
            // painted the marker color, its base flush on the ground plane.
            //
            // #569: build the marker through the network-aware overload for a zone
            // lot whose REAL (network-resolved) facing is along X, and assert the
            // slab is sized/centred on the NETWORK footprint — NOT the single-arg
            // Z-fallback footprint the old singleton path produced. This is the
            // assertion that would have caught the marker rendering the right size
            // but transposed against the house that will be built on it.
            var zoneLot = new HouseLot(
                HouseVariantAssignment.FirstZoneHouseId, Quadrant.NorthEast,
                new GridPoint(NeighborhoodLayout.LotDistanceFromCenter, 20f));
            var network = WalkNetwork.BuildFrom(NeighborhoodLayout.Roads, new[] { zoneLot });
            Assert.That(HousePlacement.FrontFacing(zoneLot, network).X, Is.Not.EqualTo(0f),
                "precondition: the network faces this zone lot along X, so its footprint swaps axes");

            var container = new GameObject("EmptyLotSlabTestContainer");

            var marker = WorldBuilder.BuildEmptyLot(container.transform, zoneLot, network);

            // A raised slab, thicker than the old flat 0.2m pad.
            Assert.That(marker.transform.localScale.y,
                Is.EqualTo(WorldBuilder.EmptyLotFoundationSlabHeight).Within(0.001f));
            Assert.That(WorldBuilder.EmptyLotFoundationSlabHeight, Is.GreaterThan(0.2f),
                "a foundation slab reads as raised, thicker than the old flat pad");

            // #434/#569: sized + centred on the NETWORK-aware footprint (the
            // house's real orientation), zone-safe since #414.
            var footprint = HousePlacement.HouseFootprint(zoneLot, network);
            Assert.That(marker.transform.localScale.x,
                Is.EqualTo(footprint.Width).Within(0.001f));
            Assert.That(marker.transform.localScale.z,
                Is.EqualTo(footprint.Depth).Within(0.001f));
            Assert.That(marker.transform.position.x,
                Is.EqualTo(footprint.Center.X).Within(0.001f), "the slab centres on the house footprint");
            Assert.That(marker.transform.position.z,
                Is.EqualTo(footprint.Center.Z).Within(0.001f));

            // #569 regression guard: the network footprint is transposed from the
            // single-arg Z-fallback one, so this assertion fails if the marker ever
            // regresses to the singleton path (the bug this issue fixes).
            var singletonFootprint = HousePlacement.HouseFootprint(zoneLot);
            Assert.That(footprint.Width, Is.Not.EqualTo(singletonFootprint.Width).Within(0.001f),
                "the network footprint differs from the singleton one — the marker must use the network one");

            // Base sits on the ground plane (bottom at y = 0).
            var bottom = marker.transform.position.y - marker.transform.localScale.y / 2f;
            Assert.That(bottom, Is.EqualTo(0f).Within(0.001f),
                "the slab's base must sit on the ground plane");

            // Single primitive graybox box painted the marker color — no
            // new kit asset.
            Assert.That(marker.GetComponent<MeshFilter>().sharedMesh.name, Does.Contain("Cube"));
            Assert.That(marker.GetComponent<Renderer>().sharedMaterial.color,
                Is.EqualTo(CoreColors.FromHex(Palette.EmptyLotMarkerHex)));

            UnityEngine.Object.DestroyImmediate(container);
        }

        [Test]
        public void EmptyLotMarker_KeepsItsEmptyLotViewTapTarget_AfterTheReshape()
        {
            // #300 (B): the tap wiring must survive the reshape — the raised
            // slab still carries an EmptyLotView initialized with the lot's
            // HouseId, so ExpansionDirector's tap -> GameState.TryBuildHouse
            // routing (pinned end-to-end by ExpansionDirectorTests) is
            // unchanged.
            var lot = FrontierEditModeWorld.FirstTileLots()[0];
            var container = new GameObject("EmptyLotTapTestContainer");

            var marker = WorldBuilder.BuildEmptyLot(container.transform, lot);
            var view = marker.GetComponent<EmptyLotView>();

            Assert.That(view, Is.Not.Null, "the reshaped slab still carries an EmptyLotView tap target");
            Assert.That(view.HouseId, Is.EqualTo(lot.HouseId));

            UnityEngine.Object.DestroyImmediate(container);
        }

        [Test]
        public void UnlockedButUnbuiltLot_RendersItsYardTrees_AlongsideTheFoundation()
        {
            // #434: an empty lot is no longer a bare slab on grass — it renders
            // the predetermined house's yard trees at unlock, so the plot reads
            // as a real home-to-be. The trees come from Core's deterministic
            // YardLandscaping (keyed on the lot alone), rendered at the empty-lot
            // render sites via the existing BuildYardLandscaping(lot) helper.
            var state = GameState.CreateNew();
            state.Wallet.Deposit(100);
            state.SetTargetMap(FrontierEditModeWorld.LoadTargetMap());
            state.TryUnlockTile(FrontierEditModeWorld.FirstTile);

            Object.DestroyImmediate(root);
            root = WorldBuilder.Build(state);

            var zoneLots = state.LotsForUnlockedTile(FrontierEditModeWorld.FirstTile);
            var treedLot = zoneLots.First(lot =>
                YardLandscaping.FrontTreesFor(lot).Concat(YardLandscaping.BackTreesFor(lot)).Any());

            Assert.That(root.transform.Find(WorldBuilder.YardLandscapingNamePrefix + treedLot.HouseId),
                Is.Not.Null, "an unlocked-but-unbuilt lot renders its yard trees");
            // The empty-lot foundation is still there too (marker + trees).
            Assert.That(root.GetComponentsInChildren<EmptyLotView>().Select(v => v.HouseId),
                Does.Contain(treedLot.HouseId), "the foundation slab renders alongside the trees");
        }

        [Test]
        public void EmptyLotFoundations_AreSizedToTheirHouseFootprint_OnAFullWorldBuild()
        {
            // #434: the foundation-slab sizing (HousePlacement.HouseFootprint)
            // holds through the full BuildEmptyLots loop, not only the single-lot
            // BuildEmptyLot helper.
            // #569: BuildEmptyLots now threads the live map-spanning network, so the
            // slab is sized to the NETWORK-aware footprint (the house's real
            // street-ward facing) rather than the single-arg Z-fallback. This zone
            // lot's real facing is along X, so the two diverge — asserting the
            // network footprint here is what the full-build path must now match.
            var state = GameState.CreateNew();
            state.Wallet.Deposit(100);
            state.SetTargetMap(FrontierEditModeWorld.LoadTargetMap());
            state.TryUnlockTile(FrontierEditModeWorld.FirstTile);

            Object.DestroyImmediate(root);
            root = WorldBuilder.Build(state);

            var lot = state.LotsForUnlockedTile(FrontierEditModeWorld.FirstTile)[0];
            var footprint = HousePlacement.HouseFootprint(lot, state.WalkNetwork);
            Assert.That(HousePlacement.PredeterminedFrontFacing(lot, state.WalkNetwork).X, Is.Not.EqualTo(0f),
                "precondition: this frontier lot's real facing is along X, so the network footprint swaps axes");
            Assert.That(footprint.Width, Is.Not.EqualTo(HousePlacement.HouseFootprint(lot).Width).Within(0.001f),
                "the network footprint diverges from the single-arg one — the full-build slab must use the network one");
            var slab = root.transform.Find(WorldBuilder.EmptyLotNamePrefix + lot.HouseId);

            Assert.That(slab, Is.Not.Null);
            Assert.That(slab.localScale.x, Is.EqualTo(footprint.Width).Within(0.001f));
            Assert.That(slab.localScale.z, Is.EqualTo(footprint.Depth).Within(0.001f));
        }

        [Test]
        public void BuildGround_StaysAFlatGrassPlane_WithNoTextureOrTiledMesh()
        {
            // #300 (A): guard test locking Derek's decision to KEEP the base
            // ground a flat Palette.GrassHex plane — a single flat primitive
            // Plane, no grass texture, no tiled grid of grass mesh children.
            var ground = root.transform.Find("Ground");
            Assert.That(ground, Is.Not.Null, "the world must build a Ground plane");

            var mesh = ground.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.name, Does.Contain("Plane"),
                "the ground stays Unity's single flat primitive Plane (no tiled grass mesh)");
            Assert.That(ground.childCount, Is.EqualTo(0),
                "a flat plane, not a tiled grid of grass mesh children");

            var renderer = ground.GetComponent<Renderer>();
            Assert.That(renderer.sharedMaterial.color, Is.EqualTo(CoreColors.FromHex(Palette.GrassHex)),
                "flat GrassHex fill");
            Assert.That(renderer.sharedMaterial.mainTexture, Is.Null,
                "no grass texture applied (decision A keeps the flat colored plane)");
        }

        private static GameState WithFirstZoneUnlocked()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(1000);
            state.SetTargetMap(FrontierEditModeWorld.LoadTargetMap());
            Assert.That(state.TryUnlockTile(FrontierEditModeWorld.FirstTile), Is.True, "the test needs the first zone unlocked");
            return state;
        }

        /// <summary>The scripted onboarding expansion coordinate (0,1) — the one
        /// coordinate a fresh game may unlock before the onboarding gate lifts.
        /// #508's Tee tests place a Tee here so the unlock succeeds.</summary>
        private static readonly TileCoordinate TeeTileCoordinate = new TileCoordinate(0, 1);

        /// <summary>A game with a three-way (Tee) tile unlocked at
        /// <see cref="TeeTileCoordinate"/> (#508). Uses <c>TeeSouth</c> — its
        /// south road connects to the origin FourWay's north edge, so placement
        /// passes #109 adjacency; its three arms are N-absent, so it exercises
        /// the "no phantom crosswalk over the closed edge" fix.</summary>
        private static GameState WithTeeTileUnlocked()
        {
            var target = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            target.Place(TeeTileCoordinate, TileType.TeeSouth);

            var state = GameState.CreateNew();
            state.SetTargetMap(target);
            state.Wallet.Deposit(Doggiehood.Core.Expansion.TileUnlock.Cost(state.Map.Tiles.Count));
            Assert.That(state.TryUnlockTile(TeeTileCoordinate), Is.True, "the test needs the Tee tile unlocked");
            return state;
        }

        private System.Collections.Generic.List<Transform> TeeCrosswalks()
        {
            var key = WorldBuilder.CrosswalkNamePrefix + TeeTileCoordinate.Col + "," + TeeTileCoordinate.Row;
            return Children().Where(t => t.name.StartsWith(key)).ToList();
        }

        [Test]
        public void UnlockedTee_PaintsCrosswalks_OnePerRoadArm_InThePrimitiveFallback()
        {
            // #508: the reported bug — an unlocked Tee rendered NO crosswalks,
            // because the fallback derived them from the hardcoded origin walk
            // network. Now each intersection's crosswalks derive from the tile
            // catalog geometry (TileCrosswalkGeometry), so TeeSouth's three arms
            // each get a patch — and its closed north edge gets none.
            Object.DestroyImmediate(root);
            WorldBuilder.ForcePrimitiveFallback = true;
            var state = WithTeeTileUnlocked();
            root = WorldBuilder.Build(state);

            var teeCrosswalks = TeeCrosswalks();
            Assert.That(teeCrosswalks.Count, Is.EqualTo(3), "TeeSouth has three road arms -> three crosswalk patches");

            var teeCenter = TileGeometry.CenterOf(TeeTileCoordinate);
            Assert.That(teeCrosswalks.Any(t => t.position.z > teeCenter.Z + 0.001f), Is.False,
                "no phantom crosswalk over the closed (roadless) north edge");

            // The origin FourWay still paints its own four, keyed to its own
            // coordinate — the fallback now covers every intersection.
            var originCrosswalks = Children()
                .Where(t => t.name.StartsWith(WorldBuilder.CrosswalkNamePrefix + "0,0")).ToList();
            Assert.That(originCrosswalks.Count, Is.EqualTo(4), "the origin 4-way keeps its four patches");
        }

        [Test]
        public void UnlockedTee_Crosswalks_StayClippedToTheRoad_NeverOverSidewalkPavement()
        {
            // #508: each patch's across-the-road extent is clipped to
            // RoadWidth + 2 * GrassVergeWidth (the road+verge span), so it stops
            // at the sidewalk boundary and never paints over sidewalk pavement —
            // the same clip the origin crosswalks use.
            Object.DestroyImmediate(root);
            WorldBuilder.ForcePrimitiveFallback = true;
            var state = WithTeeTileUnlocked();
            root = WorldBuilder.Build(state);

            var acrossSpan = WorldDimensions.RoadWidth + 2f * WorldDimensions.GrassVergeWidth;
            foreach (var crosswalk in TeeCrosswalks())
            {
                var across = Mathf.Max(crosswalk.localScale.x, crosswalk.localScale.z);
                var along = Mathf.Min(crosswalk.localScale.x, crosswalk.localScale.z);
                Assert.That(across, Is.EqualTo(acrossSpan).Within(0.001f),
                    "across-road extent clipped to the road+verge span, off the sidewalk");
                Assert.That(along, Is.EqualTo(WorldDimensions.CrosswalkWidth).Within(0.001f),
                    "along-road stripe depth is the crosswalk width");
            }
        }

        [Test]
        public void UnlockedTee_Crosswalks_RenderInTheDistinctCrosswalkColor()
        {
            // #508: the Tee's patches read as crosswalks (Palette.CrosswalkHex),
            // distinct from road/verge/sidewalk — same contract the origin has.
            Object.DestroyImmediate(root);
            WorldBuilder.ForcePrimitiveFallback = true;
            var state = WithTeeTileUnlocked();
            root = WorldBuilder.Build(state);

            var teeCrosswalks = TeeCrosswalks();
            Assert.That(teeCrosswalks, Is.Not.Empty);
            foreach (var crosswalk in teeCrosswalks)
            {
                Assert.That(crosswalk.GetComponent<Renderer>().sharedMaterial.color,
                    Is.EqualTo(CoreColors.FromHex(Palette.CrosswalkHex)));
            }
        }

        [Test]
        public void BuildGround_GrowsToCoverAnUnlockedZone_StayingASingleFlatPlane()
        {
            // #373 (Gap 1): the base grass plane must cover the whole extended
            // map (the north cul-de-sac zone), not the fixed starting pad, so
            // the new zone has grass under it — while staying the single flat
            // plane #300 (A) locked in.
            Object.DestroyImmediate(root);
            var state = WithFirstZoneUnlocked();
            root = WorldBuilder.Build(state);

            var ground = root.transform.Find(WorldBuilder.GroundName);
            Assert.That(ground, Is.Not.Null);
            Assert.That(ground.childCount, Is.EqualTo(0), "still a flat plane, not a tiled grid");
            Assert.That(ground.GetComponent<MeshFilter>().sharedMesh.name, Does.Contain("Plane"));

            // #558: the plane covers the map footprint plus a constant margin
            // (GroundExtentForMap), which still reaches under the northern zone's
            // lots — the margin comfortably clears them.
            var groundExtent = Doggiehood.Core.Cameras.CameraController.GroundExtentForMap(state.Map);
            var halfDepth = ground.localScale.z * 10f / 2f; // a Unity Plane is 10m at scale 1
            var northReach = ground.position.z + halfDepth;
            var northLotZ = FrontierEditModeWorld.FirstTileLots().Max(lot => lot.Position.Z);
            Assert.That(northReach, Is.GreaterThanOrEqualTo(northLotZ),
                "grass now reaches under the northern zone's lots");
            Assert.That(northReach, Is.EqualTo(groundExtent.MaxZ).Within(0.001f),
                "the plane covers exactly the margin-padded ground extent");
        }

        [Test]
        public void BuildGround_TracksTheMapFootprintPlusMargin_NotTheCameraReach()
        {
            // #558: the plane is sized to the map's own tile footprint plus a
            // modest constant margin (GroundExtentForMap = MapExtent.Covering +
            // BoundsMargin), NOT the camera's max-zoom-out reach as #536 did —
            // that reach ballooned with the map and dwarfed the neighborhood.
            // The "never show void" guarantee now lives in the grass-coloured
            // camera clear colour, so the mesh can stay proportionate. Assert the
            // built plane matches the margin-only extent exactly on every axis.
            Object.DestroyImmediate(root);
            state = GameState.CreateNew();
            root = WorldBuilder.Build(state);

            var ground = root.transform.Find(WorldBuilder.GroundName);
            Assert.That(ground, Is.Not.Null);

            AssertGroundMatchesMarginExtent(ground, state.Map);
        }

        [Test]
        public void ResizeGroundToMap_TracksTheGrownFootprintPlusMargin_AfterAnUnlock()
        {
            // #558: the zone-unlock path (ExpansionUnlockDirector -> ResizeGroundToMap)
            // resizes the plane to the grown map's footprint plus the same
            // constant margin — a genuinely multi-tile map — decoupled from the
            // camera's grown MaxZoom. Exercises the resize path (not just Build).
            Object.DestroyImmediate(root);
            var unlocked = WithFirstZoneUnlocked();
            root = WorldBuilder.Build(GameState.CreateNew());

            WorldBuilder.ResizeGroundToMap(root.transform, unlocked.Map);

            var ground = root.transform.Find(WorldBuilder.GroundName);
            Assert.That(ground, Is.Not.Null);
            Assert.That(unlocked.Map.Tiles.Count(), Is.GreaterThan(1), "sanity: a multi-tile map");

            AssertGroundMatchesMarginExtent(ground, unlocked.Map);
        }

        /// <summary>Asserts the built ground plane's transform (scale + position)
        /// reproduces the margin-only <see cref="Doggiehood.Core.Cameras.CameraController.GroundExtentForMap"/>
        /// for <paramref name="map"/> on every axis. A Unity Plane is 10m across
        /// at scale 1, so half-span = localScale * 10 / 2.</summary>
        private static void AssertGroundMatchesMarginExtent(Transform ground, TileMap map)
        {
            var extent = Doggiehood.Core.Cameras.CameraController.GroundExtentForMap(map);
            var halfWidth = ground.localScale.x * 10f / 2f;
            var halfDepth = ground.localScale.z * 10f / 2f;

            Assert.That(ground.position.x - halfWidth, Is.EqualTo(extent.MinX).Within(0.001f), "west edge");
            Assert.That(ground.position.x + halfWidth, Is.EqualTo(extent.MaxX).Within(0.001f), "east edge");
            Assert.That(ground.position.z - halfDepth, Is.EqualTo(extent.MinZ).Within(0.001f), "south edge");
            Assert.That(ground.position.z + halfDepth, Is.EqualTo(extent.MaxZ).Within(0.001f), "north edge");
        }

        [Test]
        public void Build_RendersRoadSurfacesForAnUnlockedZonesTiles()
        {
            // #373 (Gap 1): the unlocked cul-de-sac tile gets a road surface
            // derived from GameState.Map (TileRoadGeometry) — its south arm,
            // sitting in the tile's south half so it meets the origin tile's
            // road — not just floating lot markers. Pinned on the graybox
            // primitive path (one slab per road edge) so the per-edge count is
            // deterministic; the kit path tiles the same arm with several kit
            // tiles instead (its contract lives in WorldKitArtTests).
            Object.DestroyImmediate(root);
            WorldBuilder.ForcePrimitiveFallback = true;
            var state = WithFirstZoneUnlocked();
            root = WorldBuilder.Build(state);

            var zoneRoad = root.transform.Cast<Transform>()
                .FirstOrDefault(t => t.name == WorldBuilder.ZoneRoadNamePrefix + "0,1");
            Assert.That(zoneRoad, Is.Not.Null, "the north tile (0,1) gets a ZoneRoad container");

            var arms = zoneRoad.Cast<Transform>().ToList();
            Assert.That(arms.Count, Is.EqualTo(1), "a cul-de-sac carries a road on exactly one edge");

            // The single arm lies in the tile's south half (world z between the
            // tile centre at 60 and its south edge at 30), meeting the origin.
            var arm = arms[0];
            Assert.That(arm.position.z, Is.InRange(30f, 60f));
            Assert.That(arm.position.x, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Build_RendersOpenSpaceTreesOnACulDeSacsDroppedBulbSideQuadrants()
        {
            // #385: the cul-de-sac keeps only the 2 lots adjacent to its roaded
            // edge; the 2 bulb-side quadrants become open space with trees. The
            // live first zone (CulDeSacSouth at 0,1) drops its two north
            // quadrants, so the world renders a tree at each. Pinned on the
            // graybox primitive path so the per-tree object count is
            // deterministic regardless of whether the kit art is staged.
            Object.DestroyImmediate(root);
            WorldBuilder.ForcePrimitiveFallback = true;
            var state = WithFirstZoneUnlocked();
            root = WorldBuilder.Build(state);

            var container = root.transform.Cast<Transform>()
                .FirstOrDefault(t => t.name == WorldBuilder.OpenSpaceTreeNamePrefix + "0,1");
            Assert.That(container, Is.Not.Null, "the cul-de-sac tile (0,1) gets an open-space-trees container");

            var expected = TileGeometry.TreeWorldPositionsFor(TileType.CulDeSacSouth, new TileCoordinate(0, 1));
            Assert.That(expected.Count, Is.EqualTo(2), "precondition: two bulb-side tree quadrants");
            Assert.That(container.childCount, Is.EqualTo(expected.Count), "one rendered tree per dropped bulb-side quadrant");

            var treeXZ = container.Cast<Transform>()
                .Select(t => new Vector2(t.position.x, t.position.z))
                .ToList();
            foreach (var position in expected)
            {
                Assert.That(treeXZ.Any(p =>
                        Mathf.Abs(p.x - position.X) < 0.001f && Mathf.Abs(p.y - position.Z) < 0.001f),
                    Is.True, $"a tree renders at bulb-side quadrant ({position.X}, {position.Z})");
            }

            // The trees never overlap a buildable lot marker (the kept lots).
            var emptyLots = root.transform.Cast<Transform>()
                .Where(t => t.name.StartsWith(WorldBuilder.EmptyLotNamePrefix))
                .Select(t => new Vector2(t.position.x, t.position.z))
                .ToList();
            foreach (var tree in treeXZ)
            {
                Assert.That(emptyLots.Any(l => Mathf.Abs(l.x - tree.x) < 0.001f && Mathf.Abs(l.y - tree.y) < 0.001f),
                    Is.False, "an open-space tree never sits on a buildable lot");
            }
        }

        /// <summary>A game with a single road tile of <paramref name="type"/>
        /// unlocked at <see cref="TeeTileCoordinate"/> (0,1) — the one
        /// onboarding-permitted expansion coordinate. The type must carry a
        /// south road so it connects to the origin FourWay's north edge (#109).
        /// #614 uses this to render open-space trees on bend/twin-bend tiles.</summary>
        private static GameState WithRoadTileUnlockedAtFirstZone(TileType type)
        {
            var target = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            target.Place(TeeTileCoordinate, type);

            var state = GameState.CreateNew();
            state.SetTargetMap(target);
            state.Wallet.Deposit(Doggiehood.Core.Expansion.TileUnlock.Cost(state.Map.Tiles.Count));
            Assert.That(state.TryUnlockTile(TeeTileCoordinate), Is.True, "the test needs the tile unlocked");
            return state;
        }

        // #614: every quadrant with no kept house lot renders open-space trees,
        // not just cul-de-sacs. A bend (TurnSE) drops its cupped corner and the
        // diagonal opposite (2 trees). One rendered tree per Core tree-quadrant
        // world position, in the same per-tile "OpenSpaceTree - col,row"
        // container the cul-de-sac path uses. Pinned on the primitive fallback so
        // the per-tile object count is deterministic. (#583 removed the twin-bend
        // OpposingTurns types, which used to cover the all-four-quadrants case.)
        [TestCase(TileType.TurnSE, 2)]
        public void Build_RendersOpenSpaceTreesOnEveryDroppedQuadrant_ForBends(
            TileType type, int expectedTreeCount)
        {
            Object.DestroyImmediate(root);
            WorldBuilder.ForcePrimitiveFallback = true;
            var state = WithRoadTileUnlockedAtFirstZone(type);
            root = WorldBuilder.Build(state);

            var container = root.transform.Cast<Transform>()
                .FirstOrDefault(t => t.name == WorldBuilder.OpenSpaceTreeNamePrefix + "0,1");
            Assert.That(container, Is.Not.Null, "the tile (0,1) gets an open-space-trees container");

            var expected = TileGeometry.TreeWorldPositionsFor(type, TeeTileCoordinate);
            Assert.That(expected.Count, Is.EqualTo(expectedTreeCount), "precondition: one tree per dropped quadrant");
            Assert.That(container.childCount, Is.EqualTo(expected.Count), "one rendered tree per dropped quadrant");

            var treeXZ = container.Cast<Transform>()
                .Select(t => new Vector2(t.position.x, t.position.z))
                .ToList();
            foreach (var position in expected)
            {
                Assert.That(treeXZ.Any(p =>
                        Mathf.Abs(p.x - position.X) < 0.001f && Mathf.Abs(p.y - position.Z) < 0.001f),
                    Is.True, $"a tree renders at dropped quadrant ({position.X}, {position.Z})");
            }
        }

        [Test]
        public void BuildYardTree_ScalesEachYardTree_ByItsPlacementScale_NotTheFlatUniformScale()
        {
            // #458: yard trees vary in size — each rendered tree's localScale is
            // YardLandscaping.UniformScale * placement.Scale (Scale drawn per
            // tree in [1.0, 1.25]), not a single flat UniformScale for every
            // tree. Kit path (SetUp staged the real City Kit tree models).
            var host = new GameObject("yard-scale-host");
            var sawVariation = false;
            try
            {
                foreach (var lot in NeighborhoodLayout.HouseLots)
                {
                    var picks = YardLandscaping.FrontTreesFor(lot)
                        .Concat(YardLandscaping.BackTreesFor(lot)).ToList();
                    Assert.That(picks, Is.Not.Empty, $"lot {lot.HouseId}: sanity — selects yard trees");

                    WorldBuilder.BuildYardLandscaping(host.transform, lot);
                    var container = host.transform.Find(WorldBuilder.YardLandscapingNamePrefix + lot.HouseId);
                    Assert.That(container, Is.Not.Null, $"lot {lot.HouseId}: gets a yard container");

                    var trees = container.Cast<Transform>().ToList();
                    Assert.That(trees.Count, Is.EqualTo(picks.Count), $"lot {lot.HouseId}: one tree per pick");

                    for (var i = 0; i < trees.Count; i++)
                    {
                        var expected = YardLandscaping.UniformScale * picks[i].Scale;
                        Assert.That(trees[i].localScale.x, Is.EqualTo(expected).Within(0.001f),
                            $"lot {lot.HouseId} tree {i}: X = UniformScale * placement.Scale");
                        Assert.That(trees[i].localScale.y, Is.EqualTo(expected).Within(0.001f),
                            $"lot {lot.HouseId} tree {i}: Y = UniformScale * placement.Scale");
                        Assert.That(trees[i].localScale.z, Is.EqualTo(expected).Within(0.001f),
                            $"lot {lot.HouseId} tree {i}: Z = UniformScale * placement.Scale");

                        if (Mathf.Abs(trees[i].localScale.x - YardLandscaping.UniformScale) > 0.001f)
                        {
                            sawVariation = true;
                        }
                    }
                }

                Assert.That(sawVariation, Is.True,
                    "at least one yard tree renders larger than the flat uniform scale — sizes really vary");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void BuildTileOpenSpaceTrees_StayAtTheFixedBaseScale_VariabilityScopedToYardTrees()
        {
            // #458 regression guard: cul-de-sac open-space trees construct a
            // YardTreePlacement with no lot/seed context, so Scale defaults to
            // YardTreePlacement.BaselineScale (1.0) and they render at the flat
            // UniformScale — the size variability is scoped to yard trees only.
            Object.DestroyImmediate(root);
            var state = WithFirstZoneUnlocked();
            root = WorldBuilder.Build(state);

            var container = root.transform.Cast<Transform>()
                .FirstOrDefault(t => t.name == WorldBuilder.OpenSpaceTreeNamePrefix + "0,1");
            Assert.That(container, Is.Not.Null, "the cul-de-sac tile (0,1) gets an open-space-trees container");
            Assert.That(container.childCount, Is.GreaterThan(0), "it renders open-space trees");

            foreach (Transform tree in container)
            {
                Assert.That(tree.localScale.x, Is.EqualTo(YardLandscaping.UniformScale).Within(0.001f),
                    "open-space tree X stays at the fixed base UniformScale");
                Assert.That(tree.localScale.y, Is.EqualTo(YardLandscaping.UniformScale).Within(0.001f),
                    "open-space tree Y stays at the fixed base UniformScale");
                Assert.That(tree.localScale.z, Is.EqualTo(YardLandscaping.UniformScale).Within(0.001f),
                    "open-space tree Z stays at the fixed base UniformScale");
            }
        }

        [Test]
        public void BuildHouse_OnAZoneLotWithNoAuthoredStyle_FallsBackToTheGrayboxRender_WithoutThrowing()
        {
            // #57: houses built beyond the starting 4 have no
            // HouseStyleTable entry yet (per-zone-house model/tint
            // assignment is undesigned) — BuildHouse must render the
            // existing graybox fallback rather than crashing on
            // HouseStyleTable.ForHouse's ArgumentException.
            var lot = FrontierEditModeWorld.FirstTileLots()[0];
            var house = new House(lot.HouseId, lot.Quadrant);
            var container = new GameObject("ZoneHouseTestContainer");

            GameObject houseRoot = null;
            Assert.DoesNotThrow(() => houseRoot = WorldBuilder.BuildHouse(container.transform, house, lot));

            var walls = houseRoot.GetComponentsInChildren<Transform>().Single(t => t.name == "Walls");
            Assert.That(walls, Is.Not.Null);

            Object.DestroyImmediate(container);
        }

        [Test]
        public void BuildsBothRoads()
        {
            var roads = Children().Where(t => t.name.StartsWith(WorldBuilder.RoadNamePrefix)).ToList();

            Assert.That(roads.Count, Is.EqualTo(2));
        }

        [Test]
        public void BuildsSidewalkAndVergeStripsOnBothSidesOfEveryRoad()
        {
            // #106: symmetric placement — every road gets a sidewalk on
            // both sides, each split into two arm segments (one per
            // direction from the intersection) so the strip can stop at
            // the crossing road's own footprint instead of running through
            // it as one continuous piece. Primitive-fallback contract —
            // the kit tiles model their own sidewalks.
            //
            // Verge strips are back: GrassVergeWidth is 0.75m (Derek's
            // 2026-07-13 midpoint request — dogs at 4m sat "a little too
            // close to the road", so the walk line moved to 4.75m, between
            // the original 5.5m and the abutting 4m). In the kit path the
            // verge is a purely logical setback (the tiles render their
            // own pavement, no grass strip), but in THIS primitive
            // fallback the 0.75m grass strip legitimately renders again —
            // WorldBuilder's degenerate-geometry skip only applies at 0.
            RebuildWithPrimitiveFallback();
            var verges = Children().Where(t => t.name.StartsWith(WorldBuilder.VergeNamePrefix)).ToList();
            var sidewalks = Children().Where(t => t.name.StartsWith(WorldBuilder.SidewalkNamePrefix)).ToList();

            Assert.That(verges.Count, Is.EqualTo(NeighborhoodLayout.Roads.Count * 2 * 2),
                "verge strips render again in the fallback now that GrassVergeWidth is nonzero");
            Assert.That(sidewalks.Count, Is.EqualTo(NeighborhoodLayout.Roads.Count * 2 * 2));
        }

        [Test]
        public void SidewalkArms_NeverPaintOverTheCrossingRoadsOwnPavement()
        {
            // Regression: sidewalk strips used to run as one continuous
            // piece straight through the intersection, painting over the
            // crossing road's own pavement (Derek's playtest). Every
            // road's sidewalk line, sampled at the crossing road's own
            // centerline (squarely inside the crossing road's pavement
            // footprint), must now be covered by nothing.
            // Primitive-fallback contract (no strips exist in the kit
            // path). Verge strips are back (GrassVergeWidth 0.75m, Derek's
            // 2026-07-13 midpoint request) and are covered here too.
            RebuildWithPrimitiveFallback();
            var strips = Children()
                .Where(t => t.name.StartsWith(WorldBuilder.VergeNamePrefix) || t.name.StartsWith(WorldBuilder.SidewalkNamePrefix))
                .ToList();

            foreach (var road in NeighborhoodLayout.Roads)
            {
                foreach (var sidewalk in road.Sidewalks)
                {
                    AssertNothingCovers(strips, road.PointAt(0f, sidewalk.CenterOffset), $"sidewalk of {road.Orientation} {sidewalk.Side}");
                }
            }
        }

        private static void AssertNothingCovers(IEnumerable<Transform> strips, GridPoint point, string description)
        {
            var worldPoint = new Vector3(point.X, 0f, point.Z);

            foreach (var strip in strips)
            {
                var halfX = strip.localScale.x / 2f;
                var halfZ = strip.localScale.z / 2f;
                var covers = Mathf.Abs(worldPoint.x - strip.position.x) < halfX - 0.001f
                    && Mathf.Abs(worldPoint.z - strip.position.z) < halfZ - 0.001f;

                Assert.That(covers, Is.False,
                    $"{strip.name} paints over the crossing road's pavement at {worldPoint} ({description})");
            }
        }

        [Test]
        public void BuildsTheFourCrosswalks_OnePerRoadArm()
        {
            // Primitive-fallback contract — in the kit path the crosswalks
            // are road-crossing tiles (WorldKitArtTests).
            RebuildWithPrimitiveFallback();
            var crosswalks = Children().Where(t => t.name.StartsWith(WorldBuilder.CrosswalkNamePrefix)).ToList();

            Assert.That(crosswalks.Count, Is.EqualTo(4));
        }

        [Test]
        public void Crosswalks_NeverPaintOverSidewalkPavement()
        {
            // Regression (Derek's playtest, follow-up to the sidewalk
            // intersection fix): each Crosswalk edge in the walk network
            // runs sidewalk-center to sidewalk-center (+-4.75m at the
            // 0.75m verge) — that's the real distance a dog covers
            // crossing the road, and moving it would break graph
            // connectivity. But visually, the rendered crosswalk quad must
            // stop at the verge/sidewalk boundary
            // (RoadWidth/2 + GrassVergeWidth = 3.75m) and never cover the
            // sidewalk pavement itself (3.75m-5.75m band). Sample a point
            // in the inner half of that band (between the verge's outer
            // edge and the sidewalk's own centerline) at each crosswalk's
            // position. Primitive-fallback contract.
            RebuildWithPrimitiveFallback();
            var crosswalkObjects = Children().Where(t => t.name.StartsWith(WorldBuilder.CrosswalkNamePrefix)).ToList();
            var roadEdge = WorldDimensions.RoadWidth / 2f + WorldDimensions.GrassVergeWidth; // 3.75

            foreach (var edge in NeighborhoodLayout.WalkNetwork.Edges.Where(e => e.Kind == WalkEdgeKind.Crosswalk))
            {
                var alongX = Mathf.Abs(edge.A.Z - edge.B.Z) < 0.01f;
                var sidewalkCenterMagnitude = Mathf.Abs(alongX ? edge.A.X : edge.A.Z);
                var sampleMagnitude = (roadEdge + sidewalkCenterMagnitude) / 2f;
                var alongPosition = alongX ? (edge.A.Z + edge.B.Z) / 2f : (edge.A.X + edge.B.X) / 2f;

                foreach (var sign in new[] { 1f, -1f })
                {
                    var point = alongX
                        ? new GridPoint(sign * sampleMagnitude, alongPosition)
                        : new GridPoint(alongPosition, sign * sampleMagnitude);

                    AssertNothingCovers(crosswalkObjects, point, $"crosswalk sidewalk-band sample at {point}");
                }
            }
        }

        [Test]
        public void Road_Verge_Sidewalk_AndCrosswalk_AreVisuallyDistinctColors()
        {
            // #106: placeholder flat-colored surfaces, no literal striping,
            // but road/verge/sidewalk/crosswalk must each read as its own
            // distinct surface. Primitive-fallback contract — kit tiles
            // bring their own colormap texture instead. (Verge strips are
            // back: GrassVergeWidth is 0.75m, Derek's 2026-07-13 midpoint
            // request.)
            RebuildWithPrimitiveFallback();
            Color ColorOf(string prefix) => Children().First(t => t.name.StartsWith(prefix))
                .GetComponent<Renderer>().sharedMaterial.color;

            var road = ColorOf(WorldBuilder.RoadNamePrefix);
            var verge = ColorOf(WorldBuilder.VergeNamePrefix);
            var sidewalk = ColorOf(WorldBuilder.SidewalkNamePrefix);
            var crosswalk = ColorOf(WorldBuilder.CrosswalkNamePrefix);

            Assert.That(road, Is.EqualTo(CoreColors.FromHex(Palette.StreetHex)));
            Assert.That(verge, Is.EqualTo(CoreColors.FromHex(Palette.GrassVergeHex)));
            Assert.That(sidewalk, Is.EqualTo(CoreColors.FromHex(Palette.SidewalkHex)));
            Assert.That(crosswalk, Is.EqualTo(CoreColors.FromHex(Palette.CrosswalkHex)));

            var colors = new[] { road, verge, sidewalk, crosswalk };
            Assert.That(colors, Is.Unique);
        }

        [Test]
        public void SpawnedDogs_StandOnSidewalks_NeverOnARoadOrItsVerge()
        {
            // #106: dogs spawn outside both roads' pavement + grass verge
            // band — i.e. on a sidewalk, never on the road itself.
            DogSpawner.SpawnDogs(GameState.CreateNew(), root.transform);
            var roadAndVergeHalfWidth = NeighborhoodLayout.StreetWidth / 2f + WorldDimensions.GrassVergeWidth;

            foreach (var view in root.GetComponentsInChildren<DogView>())
            {
                var p = view.transform.position;
                Assert.That(Mathf.Abs(p.x) > roadAndVergeHalfWidth && Mathf.Abs(p.z) > roadAndVergeHalfWidth, Is.True,
                    $"{view.Dog.Name} spawned on the road or its verge at {p}");
            }
        }

        [Test]
        public void SpawnedDogs_RestAtTheSidewalkSurfaceHeight_NotTheRoadsHeight()
        {
            // #151: street dogs spawn on their house's walkway attach point
            // on the sidewalk, which the Kenney kit models raised above the
            // road surface — the spawn point must snap up to that height,
            // or the dogs' legs render clipped into the raised mesh.
            DogSpawner.SpawnDogs(GameState.CreateNew(), root.transform);

            foreach (var view in root.GetComponentsInChildren<DogView>())
            {
                Assert.That(view.transform.position.y, Is.EqualTo(WorldDimensions.SidewalkSurfaceHeight).Within(0.001f),
                    $"{view.Dog.Name} did not spawn at the sidewalk surface height");
            }
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
        public void SunHasNoVisibleLightObject_AndSitsWellAboveGround()
        {
            // #560: the Sun's transform was left at the parent's default
            // origin (ground level, map centre), where a halo/flare/gizmo can
            // render a visible bright hotspot on the grass. It must be lifted
            // to LightingPreset.SunHeight, and the halo/flare must be pinned
            // off so "no visible light object" is a checked contract, not a
            // reliance on component defaults.
            var sun = root.GetComponentsInChildren<Light>().Single();

            Assert.That(sun.flare, Is.Null, "Sun must not carry a lens flare (#560).");
            Assert.That(sun.transform.position.y,
                Is.EqualTo(LightingPreset.SunHeight).Within(0.001f),
                "Sun must sit at LightingPreset.SunHeight, not ground-level origin (#560).");
            Assert.That(sun.transform.position.y, Is.Not.EqualTo(0f));
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
        public void YardLandscaping_FallsBackToPrimitiveMarkers_WhenKitPiecesCannotLoad()
        {
            // #170: with the kit tree pieces unavailable, each lot's
            // "Yard - N" container still gets one primitive marker per
            // Core-selected pick — same fallback contract as the
            // walkways/fences (#128/#129) — painted the yard-landscaping
            // fallback color, positioned exactly where Core says.
            RebuildWithPrimitiveFallback();

            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var expected = YardLandscaping.FrontTreesFor(lot).Concat(YardLandscaping.BackTreesFor(lot)).ToList();
                Assert.That(expected, Is.Not.Empty, $"sanity: lot {lot.HouseId} has yard landscaping picks");

                var container = root.transform.Find(WorldBuilder.YardLandscapingNamePrefix + lot.HouseId);
                Assert.That(container, Is.Not.Null, $"missing yard container for lot {lot.HouseId}");

                var markers = container.Cast<Transform>().ToList();
                Assert.That(markers.Count, Is.EqualTo(expected.Count), $"lot {lot.HouseId} yard marker count");

                var expectedColor = CoreColors.FromHex(Palette.YardLandscapingFallbackHex);
                for (var i = 0; i < markers.Count; i++)
                {
                    var marker = markers[i];
                    Assert.That(marker.position.x, Is.EqualTo(expected[i].Position.X).Within(0.001f),
                        $"lot {lot.HouseId} yard marker {i} X");
                    Assert.That(marker.position.z, Is.EqualTo(expected[i].Position.Z).Within(0.001f),
                        $"lot {lot.HouseId} yard marker {i} Z");

                    var renderer = marker.GetComponent<Renderer>();
                    Assert.That(renderer, Is.Not.Null, $"lot {lot.HouseId} yard marker {i} must render a primitive");
                    Assert.That(renderer.sharedMaterial.color.r, Is.EqualTo(expectedColor.r).Within(0.001f));
                    Assert.That(renderer.sharedMaterial.color.g, Is.EqualTo(expectedColor.g).Within(0.001f));
                    Assert.That(renderer.sharedMaterial.color.b, Is.EqualTo(expectedColor.b).Within(0.001f));
                }
            }
        }

        [Test]
        public void SyncExpansionIndicators_BuildsOneViewPerUnlockableFrontierCoordinate_AndStaysInSync()
        {
            // #453: the multi-lock rework — WorldBuilder builds ONE
            // ExpansionIndicatorView per currently-unlockable frontier
            // coordinate (rendering the #183 lock icon via a SpriteRenderer),
            // and the set stays in sync as coordinates unlock (spawn for new
            // frontier entries, destroy for placed ones), superseding the single
            // fixed marker.
            var state = FrontierEditModeWorld.WithTargetMap();
            var container = new GameObject("indicator-container");
            var views = new Dictionary<TileCoordinate, ExpansionIndicatorView>();
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
            try
            {
                // During onboarding only the single scripted (0,1) tile is
                // unlockable — exactly one lock.
                WorldBuilder.SyncExpansionIndicators(container.transform, state, views, sprite, sprite, null);
                Assert.That(views.Keys, Is.EquivalentTo(new[] { FrontierEditModeWorld.FirstTile }),
                    "onboarding-gated: one lock on the scripted first tile");
                var firstView = views[FrontierEditModeWorld.FirstTile];
                Assert.That(firstView.GetComponent<SpriteRenderer>(), Is.Not.Null);

                // After onboarding the whole frontier opens — multiple locks.
                state.RestoreRewardChainStep(OnboardingRewardStep.Done);
                WorldBuilder.SyncExpansionIndicators(container.transform, state, views, sprite, sprite, null);
                Assert.That(views.Count, Is.GreaterThanOrEqualTo(2),
                    "post-onboarding the origin borders multiple open frontier tiles at once");
                Assert.That(views.ContainsKey(FrontierEditModeWorld.FirstTile), Is.True,
                    "the existing scripted-tile view is kept, not rebuilt");
                Assert.That(views[FrontierEditModeWorld.FirstTile], Is.SameAs(firstView));

                // Unlocking a coordinate destroys its lock and drops it from the set.
                state.Wallet.Deposit(TileUnlock.Cost(state.Map.Tiles.Count));
                Assert.That(state.TryUnlockTile(FrontierEditModeWorld.FirstTile), Is.True);
                WorldBuilder.SyncExpansionIndicators(container.transform, state, views, sprite, sprite, null);
                Assert.That(views.ContainsKey(FrontierEditModeWorld.FirstTile), Is.False,
                    "a placed coordinate's lock is destroyed and drops out of the set");
            }
            finally
            {
                Object.DestroyImmediate(container);
            }
        }

        private static HouseLot ZoneLot(bool hasFence = false)
        {
            // The REAL first unlocked-zone lot (id >= 5), which sits on its own
            // tile at world Z ~= 60 — not a hand-placed lot on the starting
            // tile. This exercises the actual zone geometry whose lot-quadrant
            // bounds regressed in #405 (a starting-tile lot would not reproduce
            // it). Its model resolves through HouseVariantAssignment (#414).
            var lot = FrontierEditModeWorld.FirstTileLots().First();
            return hasFence ? new HouseLot(lot.HouseId, lot.Quadrant, lot.Position, true) : lot;
        }

        [Test]
        public void BuildYardLandscaping_ForAZoneLot_RendersYardTrees_WithoutThrowing()
        {
            // #405: the single-lot yard helper lets a mid-game zone build get
            // its trees. YardLandscaping rejection-samples against
            // HousePlacement.HouseFootprint, which resolves the zone model via
            // the now-zone-safe HouseModelCatalog.ForHouse (#414) — so a zone
            // lot renders its yard instead of throwing.
            var zoneLot = ZoneLot();
            var expected = YardLandscaping.FrontTreesFor(zoneLot)
                .Concat(YardLandscaping.BackTreesFor(zoneLot)).ToList();
            Assert.That(expected, Is.Not.Empty, "a zone lot selects yard trees");

            var host = new GameObject("zone-yard-host");
            try
            {
                Assert.That(() => WorldBuilder.BuildYardLandscaping(host.transform, zoneLot), Throws.Nothing);

                var container = host.transform.Find(WorldBuilder.YardLandscapingNamePrefix + zoneLot.HouseId);
                Assert.That(container, Is.Not.Null, "the zone lot gets a yard container");
                Assert.That(container.childCount, Is.EqualTo(expected.Count),
                    "one rendered object per Core-selected pick");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void BuildYardLandscaping_TileAware_KeepsAZoneLotsTreesOutOfItsOwnTilesRoad()
        {
            // #455: the tile-aware overload clips a zone lot's yard trees against
            // its OWN cul-de-sac road (a TileRoadSegment, invisible to the
            // origin-roads-only clip), so no rendered tree sits in the paved
            // strip. The first zone is a CulDeSacSouth at (0,1).
            var roadStrips = TileRoadGeometry.SegmentsFor(
                    FrontierEditModeWorld.FirstTile, FrontierEditModeWorld.FirstTileType)
                .Select(RoadStrip).ToList();

            var host = new GameObject("tile-aware-yard-host");
            try
            {
                foreach (var lot in FrontierEditModeWorld.FirstTileLots())
                {
                    WorldBuilder.BuildYardLandscaping(host.transform, lot, FrontierEditModeWorld.FirstTileType);

                    var container = host.transform.Find(WorldBuilder.YardLandscapingNamePrefix + lot.HouseId);
                    Assert.That(container, Is.Not.Null, $"zone lot {lot.HouseId} gets a yard container");

                    foreach (Transform tree in container)
                    {
                        var p = tree.position;
                        foreach (var strip in roadStrips)
                        {
                            var inStrip = p.x > strip.MinX && p.x < strip.MaxX
                                && p.z > strip.MinZ && p.z < strip.MaxZ;
                            Assert.That(inStrip, Is.False,
                                $"zone lot {lot.HouseId}: rendered tree at ({p.x}, {p.z}) must not sit in the road strip");
                        }
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static LotRect RoadStrip(TileRoadSegment segment)
        {
            var halfWidth = segment.Width / 2f;
            var halfLength = segment.Length / 2f;
            return segment.Orientation == StreetOrientation.NorthSouth
                ? new LotRect(
                    segment.Center.X - halfWidth, segment.Center.X + halfWidth,
                    segment.Center.Z - halfLength, segment.Center.Z + halfLength)
                : new LotRect(
                    segment.Center.X - halfLength, segment.Center.X + halfLength,
                    segment.Center.Z - halfWidth, segment.Center.Z + halfWidth);
        }

        [Test]
        public void BuildFence_ForAZoneLotWithHasFence_ResolvesRuns_WithoutThrowing()
        {
            // #405: the confirmed LotFence gap — its run geometry resolves the
            // lot model through HouseModelCatalog.ForHouse, which used to throw
            // for a zone id. The single-lot fence helper renders a fenced zone
            // lot's runs without throwing now that ForHouse is zone-safe (#414).
            // #461: the state-aware fence path resolves the lot's facing/position
            // from state.WalkNetwork, so the realistic scenario a fence renders in
            // is an UNLOCKED zone with a BUILT house — that live network carries
            // the lot's own tile sidewalks (and, once built, its front walkway).
            var state = GameState.CreateNew();
            state.Wallet.Deposit(1_000_000);
            state.SetTargetMap(FrontierEditModeWorld.LoadTargetMap());
            Assert.That(state.TryUnlockTile(FrontierEditModeWorld.FirstTile), Is.True, "the first zone unlocks");
            var built = FrontierEditModeWorld.FirstTileLots().First();
            Assert.That(state.TryBuildHouse(built.HouseId), Is.True, "the zone house builds");
            var zoneLot = new HouseLot(built.HouseId, built.Quadrant, built.Position, hasFence: true);
            var expected = LotFence.GeometryFor(zoneLot, state);
            Assert.That(expected, Is.Not.Empty, "a fenced zone lot resolves fence runs");

            var host = new GameObject("zone-fence-host");
            try
            {
                Assert.That(() => WorldBuilder.BuildFence(host.transform, zoneLot, state), Throws.Nothing);

                var container = host.transform.Find(WorldBuilder.FenceNamePrefix + zoneLot.HouseId);
                Assert.That(container, Is.Not.Null, "the fenced zone lot gets a fence container");
                Assert.That(container.childCount, Is.GreaterThan(0), "the fence renders segments/rails");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void BuildFences_FenceEveryBuiltHouse_IncludingZoneHouses_NotJustTheStartingFour()
        {
            // #424: the fence loop iterated NeighborhoodLayout.HouseLots (the
            // starting four only), so a house built on an unlocked zone never
            // got a "Fence - N" container even with fences forced on. It must
            // now iterate every built house via GameState.GetHouseLot — the
            // same resolution the build/upgrade paths use — so the zone house
            // is fenced alongside the four starters.
            var withZoneHouse = GameState.CreateNew();
            withZoneHouse.Wallet.Deposit(150);
            withZoneHouse.SetTargetMap(FrontierEditModeWorld.LoadTargetMap());
            Assert.That(withZoneHouse.TryUnlockTile(FrontierEditModeWorld.FirstTile), Is.True, "the test needs the first zone unlocked");
            var zoneLot = withZoneHouse.LotsForUnlockedTile(FrontierEditModeWorld.FirstTile)[0];
            Assert.That(withZoneHouse.TryBuildHouse(zoneLot.HouseId), Is.True, "the test needs a zone house built");
            Assert.That(zoneLot.HouseId, Is.GreaterThanOrEqualTo(HouseVariantAssignment.FirstZoneHouseId),
                "the built lot is a real zone lot (id >= 5)");

            var original = WorldBuilder.ForceFencesVisible;
            Object.DestroyImmediate(root);
            try
            {
                WorldBuilder.ForceFencesVisible = true;
                root = WorldBuilder.Build(withZoneHouse);

                // Every built house — the four starters AND the zone house —
                // gets its own fence container.
                foreach (var house in withZoneHouse.Houses)
                {
                    Assert.That(
                        root.transform.Find(WorldBuilder.FenceNamePrefix + house.Id), Is.Not.Null,
                        $"house {house.Id} must get a fence container");
                }

                Assert.That(
                    root.transform.Find(WorldBuilder.FenceNamePrefix + zoneLot.HouseId), Is.Not.Null,
                    "the zone house must be fenced alongside the starting four");
                Assert.That(FenceContainerCount(), Is.EqualTo(withZoneHouse.Houses.Count),
                    "one fence container per built house");
            }
            finally
            {
                WorldBuilder.ForceFencesVisible = original;
            }
        }

        [Test]
        public void RebuildFences_FencesTheZoneHouse_OnTheLiveDebugTogglePath_ViaGameState()
        {
            // #424: the Settings ▸ Debug fence toggle drives RebuildFences,
            // which now takes GameState and iterates every built house. On a
            // live world with a zone house, toggling fences on must fence the
            // zone house too — not only the starting four.
            var withZoneHouse = GameState.CreateNew();
            withZoneHouse.Wallet.Deposit(150);
            withZoneHouse.SetTargetMap(FrontierEditModeWorld.LoadTargetMap());
            withZoneHouse.TryUnlockTile(FrontierEditModeWorld.FirstTile);
            var zoneLot = withZoneHouse.LotsForUnlockedTile(FrontierEditModeWorld.FirstTile)[0];
            withZoneHouse.TryBuildHouse(zoneLot.HouseId);

            var original = WorldBuilder.ForceFencesVisible;
            Object.DestroyImmediate(root);
            try
            {
                WorldBuilder.ForceFencesVisible = false;
                root = WorldBuilder.Build(withZoneHouse);
                Assert.That(FenceContainerCount(), Is.EqualTo(0), "fences hidden by default");

                WorldBuilder.ForceFencesVisible = true;
                WorldBuilder.RebuildFences(root.transform, withZoneHouse);
                Assert.That(
                    root.transform.Find(WorldBuilder.FenceNamePrefix + zoneLot.HouseId), Is.Not.Null,
                    "the live rebuild fences the zone house");
                Assert.That(FenceContainerCount(), Is.EqualTo(withZoneHouse.Houses.Count),
                    "the rebuild fences every built house");
            }
            finally
            {
                WorldBuilder.ForceFencesVisible = original;
            }
        }

        [Test]
        public void RebuildFences_WithAStartingOnlyState_StillFencesExactlyTheFourStarters()
        {
            // #424 regression guard: threading GameState through RebuildFences
            // (and its one WorldBootstrap.BuildSettingsPanel call site) must not
            // change behavior for a world with no zone houses — the starting
            // four are still fenced, no more, no fewer.
            var original = WorldBuilder.ForceFencesVisible;
            try
            {
                WorldBuilder.ForceFencesVisible = true;
                WorldBuilder.RebuildFences(root.transform, state);
                Assert.That(FenceContainerCount(), Is.EqualTo(NeighborhoodLayout.HouseLots.Count),
                    "a starting-only world fences exactly the four starters");
            }
            finally
            {
                WorldBuilder.ForceFencesVisible = original;
            }
        }

        [Test]
        public void BuildWalkway_RendersTheSameGeometryForOneLot_AsTheFullBuild()
        {
            // #405: the single-lot walkway helper must render exactly what the
            // full-build loop renders for that lot. Pin it on the primitive path
            // so the geometry is deterministic regardless of kit-asset
            // availability.
            RebuildWithPrimitiveFallback();
            var lot = NeighborhoodLayout.HouseLots.First();

            var fromFullBuild = root.transform.Find(WorldBuilder.WalkwayNamePrefix + lot.HouseId);
            Assert.That(fromFullBuild, Is.Not.Null, "the full build renders the lot's walkway");

            var host = new GameObject("single-lot-walkway-host");
            try
            {
                WorldBuilder.BuildWalkway(host.transform, lot);

                var single = host.transform.Find(WorldBuilder.WalkwayNamePrefix + lot.HouseId);
                Assert.That(single, Is.Not.Null, "the single-lot helper renders the same container");

                var fullChildren = fromFullBuild.Cast<Transform>().ToList();
                var singleChildren = single.Cast<Transform>().ToList();
                Assert.That(singleChildren.Count, Is.EqualTo(fullChildren.Count), "same piece count");

                for (var i = 0; i < fullChildren.Count; i++)
                {
                    Assert.That(singleChildren[i].position.x, Is.EqualTo(fullChildren[i].position.x).Within(0.001f),
                        $"piece {i} X");
                    Assert.That(singleChildren[i].position.z, Is.EqualTo(fullChildren[i].position.z).Within(0.001f),
                        $"piece {i} Z");
                    Assert.That(Quaternion.Angle(singleChildren[i].rotation, fullChildren[i].rotation),
                        Is.LessThan(0.01f), $"piece {i} rotation");
                    Assert.That(singleChildren[i].localScale.x, Is.EqualTo(fullChildren[i].localScale.x).Within(0.001f),
                        $"piece {i} scale X");
                    Assert.That(singleChildren[i].localScale.z, Is.EqualTo(fullChildren[i].localScale.z).Within(0.001f),
                        $"piece {i} scale Z");
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
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
