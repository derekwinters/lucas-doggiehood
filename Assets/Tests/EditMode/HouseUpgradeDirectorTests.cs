using System.Linq;
using Doggiehood.Core.Art;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #407: upgrading a house must swap its model in the WORLD, not only in
    /// the profile panel. <see cref="HouseUpgradeDirector"/> re-renders the
    /// already-built <see cref="HouseView"/> after a successful
    /// <see cref="GameState.TryUpgradeHouse"/> — destroying the stale view and
    /// rebuilding it through <see cref="WorldBuilder.BuildHouse(Transform, House, HouseLot)"/>
    /// at the house's new level (mirroring how <see cref="ExpansionDirector"/>
    /// swaps an empty-lot marker for the real house on build) — and re-wires
    /// the fresh view's tap handlers so tap-to-open-profile (#208) and quest
    /// spray routing (#53) keep reaching the rebuilt object. Passing the same
    /// Core <see cref="House"/>/<see cref="HouseLot"/> back into BuildHouse
    /// preserves a zone house's rolled ladder + palette tint (#299) and a
    /// vacant house's greyscale (#58) for free.
    /// </summary>
    public class HouseUpgradeDirectorTests
    {
        private const string BundledFontPath = "Assets/UI/Fonts/Resources/DejaVuSans.ttf";

        private GameState state;
        private GameObject worldRoot;
        private GameObject directorHost;
        private GameObject canvasHost;
        private HouseUpgradeDirector director;

        [SetUp]
        public void SetUp()
        {
            WorldBuilder.ForcePrimitiveFallback = false;
            state = GameState.CreateNew();
        }

        [TearDown]
        public void Cleanup()
        {
            WorldBuilder.ForcePrimitiveFallback = false;
            if (worldRoot != null)
            {
                Object.DestroyImmediate(worldRoot);
            }

            if (canvasHost != null)
            {
                Object.DestroyImmediate(canvasHost);
            }
        }

        private void BuildWorldAndDirector(System.Action<HouseView> onRebuilt = null)
        {
            worldRoot = WorldBuilder.Build(state);
            directorHost = new GameObject("house-upgrade-director-host");
            directorHost.transform.SetParent(worldRoot.transform);
            director = directorHost.AddComponent<HouseUpgradeDirector>();
            director.Init(state, worldRoot.transform, onRebuilt ?? (_ => { }));
        }

        private HouseView HouseViewFor(int houseId)
        {
            return worldRoot.GetComponentsInChildren<HouseView>().SingleOrDefault(h => h.HouseId == houseId);
        }

        private static void OpenProfileFor(HouseProfileOverlay overlay, GameState state, int houseId)
        {
            var house = state.Houses.FirstOrDefault(h => h.Id == houseId);
            if (house == null)
            {
                return;
            }

            var residents = state.Dogs.Where(d => d.HouseId == houseId).ToList();
            overlay.Open(house, residents);
        }

        [Test]
        public void SuccessfulUpgrade_DestroysTheStaleHouseView_AndRebuildsItThroughBuildHouse()
        {
            BuildWorldAndDirector();
            var houseId = state.Houses[0].Id;
            var original = HouseViewFor(houseId);
            Assert.That(original, Is.Not.Null, "the starter house has a HouseView at build time");

            state.Wallet.Deposit(HouseUpgradeNumbers.CostToReach(2));
            Assert.That(state.TryUpgradeHouse(houseId), Is.True, "the house upgrades one level in Core");

            var rebuilt = director.RefreshHouse(houseId);

            Assert.That(original == null, Is.True, "the stale HouseView is destroyed on rebuild");
            Assert.That(rebuilt, Is.Not.Null, "a fresh HouseView is built for the upgraded house");
            Assert.That(rebuilt.HouseId, Is.EqualTo(houseId));
            Assert.That(HouseViewFor(houseId), Is.SameAs(rebuilt),
                "exactly one fresh HouseView for that id remains, under the world root");
        }

        [Test]
        public void UpgradedStarterHouse_RebuildsWithTheNextRungsMesh()
        {
            BuildWorldAndDirector();
            var houseId = state.Houses[0].Id;

            Assert.That(HouseLevelModelTable.ForHouseLevel(houseId, 2),
                Is.Not.EqualTo(HouseLevelModelTable.ForHouseLevel(houseId, 1)),
                "sanity: level 2 uses a different ladder mesh than level 1");

            state.Wallet.Deposit(HouseUpgradeNumbers.CostToReach(2));
            state.TryUpgradeHouse(houseId); // level 1 -> 2

            var rebuilt = director.RefreshHouse(houseId);

            var expectedMesh = HouseLevelModelTable.ForHouseLevel(houseId, 2);
            var sourceMeshes = Resources.Load<GameObject>(expectedMesh)
                .GetComponentsInChildren<MeshFilter>().Select(mf => mf.sharedMesh)
                .Where(m => m != null).ToList();

            var model = rebuilt.transform.Find("Model");
            Assert.That(model, Is.Not.Null, "the rebuilt starter house renders a kit mesh");
            var houseMeshes = model.GetComponentsInChildren<MeshFilter>()
                .Select(mf => mf.sharedMesh).Where(m => m != null).ToList();
            Assert.That(houseMeshes, Is.Not.Empty, "the rebuilt house renders a mesh");
            Assert.That(houseMeshes, Is.SubsetOf(sourceMeshes),
                "the rebuilt starter house renders level 2's ladder mesh, not the stale level 1 mesh");
        }

        [Test]
        public void AfterUpgrade_TheRebuiltHousePosition_AndItsWalkway_MatchTheUpgradedLevel_NotStaleLevelOne()
        {
            // #454: the bug was that an upgraded mesh sat at the stale
            // level-1-calibrated pivot and its walkway ran to the stale level-1
            // door. After RefreshHouse, the rebuilt HouseView's world position and
            // its lot's front walkway must both match the just-upgraded level's
            // model geometry, not level 1.
            BuildWorldAndDirector();
            var house = state.Houses.Single(h => h.Id == 1); // r -> c: footprint depth grows
            var lot = state.GetHouseLot(house.Id);

            var levelOnePivot = HousePlacement.Position(
                lot, WorldBuilder.HouseKitScale, state.WalkNetwork, 1);

            state.Wallet.Deposit(HouseUpgradeNumbers.CostToReach(2));
            Assert.That(state.TryUpgradeHouse(house.Id), Is.True);
            Assert.That(house.Level, Is.EqualTo(2));

            var rebuilt = director.RefreshHouse(house.Id);

            var expectedPivot = HousePlacement.Position(
                lot, WorldBuilder.HouseKitScale, state.WalkNetwork, house.Level);
            Assert.That(expectedPivot, Is.Not.EqualTo(levelOnePivot),
                "sanity: house 1's level-2 pivot differs from the stale level-1 pivot");
            Assert.That(rebuilt.transform.position.x, Is.EqualTo(expectedPivot.X).Within(0.001f),
                "the rebuilt house sits at the level-2 front-setback pivot (X)");
            Assert.That(rebuilt.transform.position.z, Is.EqualTo(expectedPivot.Z).Within(0.001f),
                "the rebuilt house sits at the level-2 front-setback pivot (Z)");

            // The lot's front walkway now runs to the level-2 door.
            Assert.That(state.WalkNetwork.TryGetFrontWalkway(house.Id, out var walkway), Is.True);
            var expectedDoor = HouseModelCatalog.ForHouse(house.Id, house.Level).FrontDoorWorldPosition(
                expectedPivot,
                HousePlacement.ModelYawDegrees(HousePlacement.FrontFacing(lot, state.WalkNetwork)),
                WorldBuilder.HouseKitScale);
            var levelOneDoor = HouseModelCatalog.ForHouse(house.Id, 1).FrontDoorWorldPosition(
                levelOnePivot,
                HousePlacement.ModelYawDegrees(HousePlacement.FrontFacing(lot, state.WalkNetwork)),
                WorldBuilder.HouseKitScale);
            Assert.That(expectedDoor, Is.Not.EqualTo(levelOneDoor),
                "sanity: house 1's level-2 door differs from level 1");
            Assert.That(walkway.A.X, Is.EqualTo(expectedDoor.X).Within(0.001f),
                "the walkway door node re-aligns to the level-2 door (X)");
            Assert.That(walkway.A.Z, Is.EqualTo(expectedDoor.Z).Within(0.001f),
                "the walkway door node re-aligns to the level-2 door (Z)");

            // The rebuilt walkway visual container exists for the house.
            Assert.That(worldRoot.transform.Find(WorldBuilder.WalkwayNamePrefix + house.Id), Is.Not.Null,
                "the walkway visual is re-rendered for the upgraded house");
        }

        [Test]
        public void AfterUpgrade_TheRebuiltHouseFootprint_StaysInsideQuadrantBounds_AtTheUpgradedLevel_NotStaleLevelOne()
        {
            // #462 acceptance bar, verified end-to-end through the rebuild: after
            // RefreshHouse the rebuilt HouseView's footprint — derived from its
            // ACTUAL rebuilt transform and the upgraded level's model — must stay
            // inside its lot's QuadrantBounds ("leveling never resizes or moves the
            // lot") AND be sized to the just-upgraded level's mesh, not stale
            // level-1 geometry. House 1's ladder (r -> c) genuinely grows, so the
            // stale-vs-upgraded distinction is observable.
            BuildWorldAndDirector();
            var house = state.Houses.Single(h => h.Id == 1);
            var lot = state.GetHouseLot(house.Id);

            var staleLevelOneFootprint = FootprintOf(
                lot, HousePlacement.Position(lot, WorldBuilder.HouseKitScale, state.WalkNetwork, 1), 1);

            state.Wallet.Deposit(HouseUpgradeNumbers.CostToReach(2));
            Assert.That(state.TryUpgradeHouse(house.Id), Is.True);
            Assert.That(house.Level, Is.EqualTo(2));

            var rebuilt = director.RefreshHouse(house.Id);

            var rebuiltCenter = new GridPoint(rebuilt.transform.position.x, rebuilt.transform.position.z);
            var rebuiltFootprint = FootprintOf(lot, rebuiltCenter, house.Level);
            var bounds = LotBounds.QuadrantBounds(lot);

            Assert.That(bounds.Contains(rebuiltFootprint), Is.True,
                $"the rebuilt level-{house.Level} footprint " +
                $"[{rebuiltFootprint.MinX}..{rebuiltFootprint.MaxX}]x[{rebuiltFootprint.MinZ}..{rebuiltFootprint.MaxZ}] " +
                $"must stay inside its lot quadrant bounds " +
                $"[{bounds.MinX}..{bounds.MaxX}]x[{bounds.MinZ}..{bounds.MaxZ}]");

            // It is the upgraded level's geometry, not the stale level-1 footprint.
            Assert.That(
                rebuiltFootprint.Width != staleLevelOneFootprint.Width
                    || rebuiltFootprint.Depth != staleLevelOneFootprint.Depth,
                Is.True,
                "the rebuilt footprint must be sized to level 2's mesh, not stale level-1 geometry");

            // And it matches Core's level-aware footprint for the rebuilt level.
            var coreFootprint = HousePlacement.HouseFootprint(lot, state.WalkNetwork, house.Level);
            Assert.That(rebuiltFootprint.MinX, Is.EqualTo(coreFootprint.MinX).Within(0.001f));
            Assert.That(rebuiltFootprint.MaxX, Is.EqualTo(coreFootprint.MaxX).Within(0.001f));
            Assert.That(rebuiltFootprint.MinZ, Is.EqualTo(coreFootprint.MinZ).Within(0.001f));
            Assert.That(rebuiltFootprint.MaxZ, Is.EqualTo(coreFootprint.MaxZ).Within(0.001f));
        }

        /// <summary>The axis-aligned footprint rect for a house centred at
        /// <paramref name="center"/> at <paramref name="level"/>, built exactly as
        /// <see cref="HousePlacement.HouseFootprint(HouseLot, int)"/> does (same
        /// width/depth axis-swap on the network-resolved facing) but from an
        /// explicit centre so it can be derived from the REBUILT view's real
        /// transform.</summary>
        private LotRect FootprintOf(HouseLot lot, GridPoint center, int level)
        {
            var facing = HousePlacement.FrontFacing(lot, state.WalkNetwork);
            var model = HouseModelCatalog.ForHouse(lot.HouseId, level);
            var halfWidth = WorldBuilder.HouseKitScale * model.FootprintX / 2f;
            var halfDepth = WorldBuilder.HouseKitScale * model.FootprintZ / 2f;

            return facing.X != 0f
                ? new LotRect(center.X - halfDepth, center.X + halfDepth, center.Z - halfWidth, center.Z + halfWidth)
                : new LotRect(center.X - halfWidth, center.X + halfWidth, center.Z - halfDepth, center.Z + halfDepth);
        }

        [Test]
        public void UpgradedZoneHouse_RebuildsWithItsRolledLaddersNextMesh_AndKeepsItsPaletteTint()
        {
            // An OCCUPIED zone-built house (RestoreBuiltHouse lets a test create
            // it occupied) so the rolled palette tint — not the vacancy grey —
            // is the thing that must survive the rebuild.
            state.Wallet.Deposit(1000);
            state.SetTargetMap(FrontierEditModeWorld.LoadTargetMap());
            Assert.That(state.TryUnlockTile(FrontierEditModeWorld.FirstTile), Is.True);
            BuildWorldAndDirector();

            var zoneHouseId = worldRoot.GetComponentsInChildren<EmptyLotView>().First().HouseId;
            var variant = HouseVariantAssignment.ForHouse(zoneHouseId);
            state.RestoreBuiltHouse(zoneHouseId, HouseLevelModelTable.MinLevel, isVacant: false, variant);
            var house = state.Houses.Single(h => h.Id == zoneHouseId);
            WorldBuilder.BuildHouse(worldRoot.transform, house, state.GetHouseLot(zoneHouseId));

            state.Wallet.Deposit(HouseUpgradeNumbers.CostToReach(2));
            Assert.That(state.TryUpgradeHouse(zoneHouseId), Is.True);

            var rebuilt = director.RefreshHouse(zoneHouseId);

            var expectedMesh = HouseLevelModelTable.ForHouseLevel(variant.LadderId, 2);
            var sourceMeshes = Resources.Load<GameObject>(expectedMesh)
                .GetComponentsInChildren<MeshFilter>().Select(mf => mf.sharedMesh)
                .Where(m => m != null).ToList();

            var model = rebuilt.transform.Find("Model");
            Assert.That(model, Is.Not.Null, "the rebuilt zone house renders its rolled kit mesh");
            var houseMeshes = model.GetComponentsInChildren<MeshFilter>()
                .Select(mf => mf.sharedMesh).Where(m => m != null).ToList();
            Assert.That(houseMeshes, Is.SubsetOf(sourceMeshes),
                $"the rebuild keeps rolled ladder {variant.LadderId} and renders its level 2 mesh");

            var expectedTint = CoreColors.FromHex(Palette.HouseTintHex(variant.TintIndex));
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var color = renderer.sharedMaterial.color;
                Assert.That(color.r, Is.EqualTo(expectedTint.r).Within(0.01f), $"{renderer.name} R keeps the rolled tint");
                Assert.That(color.g, Is.EqualTo(expectedTint.g).Within(0.01f), $"{renderer.name} G keeps the rolled tint");
                Assert.That(color.b, Is.EqualTo(expectedTint.b).Within(0.01f), $"{renderer.name} B keeps the rolled tint");
            }
        }

        [Test]
        public void UpgradedVacantZoneHouse_RebuildKeepsTheVacancyGreyscale()
        {
            state.Wallet.Deposit(1000);
            state.SetTargetMap(FrontierEditModeWorld.LoadTargetMap());
            Assert.That(state.TryUnlockTile(FrontierEditModeWorld.FirstTile), Is.True);
            BuildWorldAndDirector();

            var zoneHouseId = worldRoot.GetComponentsInChildren<EmptyLotView>().First().HouseId;
            var variant = HouseVariantAssignment.ForHouse(zoneHouseId);
            state.RestoreBuiltHouse(zoneHouseId, HouseLevelModelTable.MinLevel, isVacant: true, variant);
            var house = state.Houses.Single(h => h.Id == zoneHouseId);
            WorldBuilder.BuildHouse(worldRoot.transform, house, state.GetHouseLot(zoneHouseId));

            state.Wallet.Deposit(HouseUpgradeNumbers.CostToReach(2));
            Assert.That(state.TryUpgradeHouse(zoneHouseId), Is.True);

            var rebuilt = director.RefreshHouse(zoneHouseId);

            var expected = CoreColors.FromHex(Palette.VacantHouseTintHex);
            var model = rebuilt.transform.Find("Model");
            Assert.That(model, Is.Not.Null);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var color = renderer.sharedMaterial.color;
                Assert.That(color.r, Is.EqualTo(expected.r).Within(0.01f), $"{renderer.name} R stays vacancy grey");
                Assert.That(color.g, Is.EqualTo(expected.g).Within(0.01f), $"{renderer.name} G stays vacancy grey");
                Assert.That(color.b, Is.EqualTo(expected.b).Within(0.01f), $"{renderer.name} B stays vacancy grey");
            }
        }

        [Test]
        public void SuccessfulUpgrade_RebuildsTheFenceContainer_SoTerminationPointsRenderImmediately()
        {
            // #460: the backyard fence connectors are sized to the house's
            // CURRENT level's footprint, so after an upgrade the fence must be
            // rebuilt for the corrected termination points to render immediately
            // rather than only at the next full world build. Force fences visible
            // (they are hidden by default) so a container exists to observe, and
            // upgrade house 1 whose ladder (r -> c) genuinely widens.
            var original = WorldBuilder.ForceFencesVisible;
            try
            {
                WorldBuilder.ForceFencesVisible = true;
                BuildWorldAndDirector();

                const int houseId = 1;
                var staleFence = worldRoot.transform.Find(WorldBuilder.FenceNamePrefix + houseId);
                Assert.That(staleFence, Is.Not.Null,
                    "the forced-visible fence container renders at world-build time");

                state.Wallet.Deposit(HouseUpgradeNumbers.CostToReach(2));
                Assert.That(state.TryUpgradeHouse(houseId), Is.True, "house 1 upgrades one level");

                director.RefreshHouse(houseId);

                Assert.That(staleFence == null, Is.True,
                    "the stale fence container is destroyed as part of the upgrade rebuild");
                var rebuiltFence = worldRoot.transform.Find(WorldBuilder.FenceNamePrefix + houseId);
                Assert.That(rebuiltFence, Is.Not.Null,
                    "a fresh fence container is rebuilt after the upgrade so termination points move");
            }
            finally
            {
                WorldBuilder.ForceFencesVisible = original;
            }
        }

        [Test]
        public void AfterUpgrade_TheRebuiltHouseTap_StillOpensTheProfile_AndStillReachesQuestSpray()
        {
            // The re-wiring guarantee (triage's core wrinkle): HouseView.Tapped
            // is subscribed by two independent one-time loops — WorldBootstrap's
            // profile-open loop and QuestDirector's spray loop. A naive
            // destroy-and-rebuild would produce a fresh view neither loop has
            // seen, silently breaking both on that house after an upgrade. The
            // fix re-subscribes both; a single tap on the rebuilt view must
            // reach the profile overlay AND the quest spray path.
            AssetDatabase.ImportAsset(BundledFontPath, ImportAssetOptions.ForceSynchronousImport);

            worldRoot = WorldBuilder.Build(state);
            DogSpawner.SpawnDogs(state, worldRoot.transform);

            var questHost = new GameObject("quest-director");
            questHost.transform.SetParent(worldRoot.transform);
            var questDirector = questHost.AddComponent<QuestDirector>();
            questDirector.Init(state, worldRoot.transform);

            canvasHost = new GameObject("ui-canvas", typeof(Canvas));
            canvasHost.AddComponent<UiCanvas>().Configure();
            var overlayHost = new GameObject("house-profile-overlay");
            overlayHost.transform.SetParent(canvasHost.transform, false);
            var overlay = overlayHost.AddComponent<HouseProfileOverlay>();
            overlay.Init();

            directorHost = new GameObject("house-upgrade-director");
            directorHost.transform.SetParent(worldRoot.transform);
            director = directorHost.AddComponent<HouseUpgradeDirector>();
            director.Init(state, worldRoot.transform, view =>
            {
                var rewiredId = view.HouseId;
                view.Tapped += () => OpenProfileFor(overlay, state, rewiredId);
                questDirector.WireHouses();
            });

            // Initial profile-tap subscription — mirrors WorldBootstrap's loop.
            foreach (var view in worldRoot.GetComponentsInChildren<HouseView>())
            {
                var initialId = view.HouseId;
                view.Tapped += () => OpenProfileFor(overlay, state, initialId);
            }

            overlay.ConfigureUpgrade(
                () => state.Wallet.Coins,
                houseId =>
                {
                    if (!state.TryUpgradeHouse(houseId))
                    {
                        return false;
                    }

                    director.RefreshHouse(houseId);
                    return true;
                });

            // A bug quest on the house makes the spray observable.
            var dog = state.Dogs.First();
            var targetHouseId = dog.HouseId;
            var house = state.Houses.Single(h => h.Id == targetHouseId);
            var quest = state.Quests.GiveQuestTo(dog, QuestType.PestControl, new System.Random(5));
            Assert.That(state.Quests.Accept(quest), Is.True);
            questDirector.OnQuestAccepted(quest);
            Assert.That(Object.FindObjectsByType<BugSwarmView>(FindObjectsSortMode.None).Any(s => s.HouseId == targetHouseId),
                Is.True, "the bugged house shows a swarm");

            // Upgrade through the overlay: direct spend + world re-render + re-wire.
            state.Wallet.Deposit(HouseUpgradeNumbers.CostToReach(2));
            overlay.Open(house, state.Dogs.Where(d => d.HouseId == targetHouseId).ToList());
            overlay.UpgradeButton.onClick.Invoke();
            Assert.That(house.Level, Is.EqualTo(2), "the house upgraded one level via the overlay");

            overlay.Close();
            Assert.That(overlay.IsOpen, Is.False);

            // A single tap on the REBUILT house must reach BOTH subscribers.
            var rebuilt = worldRoot.GetComponentsInChildren<HouseView>().Single(h => h.HouseId == targetHouseId);
            var payoutBefore = state.Wallet.Coins;
            rebuilt.OnTapped();

            Assert.That(overlay.IsOpen, Is.True,
                "tap-to-open-profile still reaches the rebuilt HouseView after an upgrade (#208)");
            Assert.That(overlay.CurrentHouse, Is.SameAs(house), "and it opens the right house");
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Completed),
                "quest spray still reaches the rebuilt HouseView after an upgrade (#53)");
            Assert.That(state.Wallet.Coins, Is.EqualTo(payoutBefore + Doggiehood.Core.Economy.EconomyNumbers.QuestPayout),
                "the spray completion pays the flat quest payout");
            Assert.That(Object.FindObjectsByType<BugSwarmView>(FindObjectsSortMode.None).Any(s => s.HouseId == targetHouseId),
                Is.False, "the swarm is cleared once the rebuilt house is sprayed");
        }
    }
}
