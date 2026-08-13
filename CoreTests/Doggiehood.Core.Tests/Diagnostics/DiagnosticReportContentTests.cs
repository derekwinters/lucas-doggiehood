using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Doggiehood.Core.Debugging;
using Doggiehood.Core.Diagnostics;
using Doggiehood.Core.Onboarding;
using Doggiehood.Core.Tuning;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Diagnostics
{
    /// <summary>
    /// #692: what each section of the bug report actually carries. The value of
    /// the feature is that the snapshot is complete enough to reproduce the bug,
    /// so these assert the payload, not the button.
    /// </summary>
    public class DiagnosticReportContentTests
    {
        [TearDown]
        public void RestoreTuning()
        {
            TuningConfig.ResetToDefaults();
        }

        // ---------------------------------------------------------------
        // REPORT — Core describes the device with no engine call
        // ---------------------------------------------------------------

        [Test]
        public void ReportHeader_CarriesEveryDiagnosticEnvironmentValue()
        {
            var environment = DiagnosticReportSectionTests.SampleEnvironment();
            var body = DiagnosticReportSectionTests.SectionBody(
                DiagnosticReportSectionTests.Render(GameState.CreateNew()),
                DiagnosticReport.ReportSection);

            Assert.That(body, Does.Contain("appVersion=" + environment.AppVersion));
            Assert.That(body, Does.Contain("buildFlavor=" + environment.BuildFlavor));
            Assert.That(body, Does.Contain("platform=" + environment.Platform));
            Assert.That(body, Does.Contain("deviceModel=" + environment.DeviceModel));
            Assert.That(body, Does.Contain("operatingSystem=" + environment.OperatingSystem));
            Assert.That(body, Does.Contain("timestamp=" + environment.Timestamp));
            Assert.That(body, Does.Contain("2560x1600"));
            Assert.That(body, Does.Contain("276.00dpi"));
            Assert.That(body, Does.Contain("sessionUptimeSeconds=412.5"));
            Assert.That(body, Does.Contain(
                "schemaVersion=" + DiagnosticNumbers.ReportSchemaVersion));
        }

        // ---------------------------------------------------------------
        // SAVE — the reproducibility payload
        // ---------------------------------------------------------------

        [Test]
        public void SaveSection_IsTheVerbatimSaveBlobAndLoadsBackToAnEquivalentState()
        {
            var state = AfterOnboarding();
            state.Wallet.Deposit(137);
            state.AddPlacedItem(1, "Red ball");

            var body = DiagnosticReportSectionTests.SectionBody(
                DiagnosticReportSectionTests.Render(state), DiagnosticReport.SaveSection);

            var expected = SaveCodec.Save(state);
            Assert.That(body.Trim('\n'), Is.EqualTo(expected.Trim('\n')),
                "the SAVE section is the save blob, byte for byte");

            var reloaded = SaveCodec.Load(body);
            Assert.That(reloaded.Wallet.Coins, Is.EqualTo(state.Wallet.Coins));
            Assert.That(reloaded.PlacedItems.Count, Is.EqualTo(state.PlacedItems.Count));
            Assert.That(reloaded.UnlockedTiles.Count, Is.EqualTo(state.UnlockedTiles.Count));
        }

        // ---------------------------------------------------------------
        // TUNING — current value, shipping default, and the override marker
        // ---------------------------------------------------------------

        [Test]
        public void TuningSection_MarksAnOverriddenFieldAndLeavesAnUntouchedOneUnmarked()
        {
            var shippingPayout = new TuningConfig().QuestPayout;
            TuningConfig.Active.QuestPayout = shippingPayout + 7;

            var body = DiagnosticReportSectionTests.SectionBody(
                DiagnosticReportSectionTests.Render(GameState.CreateNew(), TuningConfig.Active),
                DiagnosticReport.TuningSection);

            var overridden = LineFor(body, nameof(TuningConfig.QuestPayout));
            Assert.That(overridden, Does.StartWith(DiagnosticReport.OverriddenMarker));
            Assert.That(overridden, Does.Contain("= " + (shippingPayout + 7)));
            Assert.That(overridden, Does.Contain("default=" + shippingPayout));

            var untouched = LineFor(body, nameof(TuningConfig.HouseMaxLevel));
            Assert.That(untouched, Does.Not.StartWith(DiagnosticReport.OverriddenMarker));
            Assert.That(untouched, Does.Contain("default=" + new TuningConfig().HouseMaxLevel));
        }

        [Test]
        public void EveryTuningConfigField_AppearsInTheTuningSection()
        {
            // The same guard TuningCatalog carries: a tunable added without
            // report coverage fails the suite rather than quietly vanishing from
            // the snapshot — a report that omits the slider you had dragged is
            // worse than useless.
            var body = DiagnosticReportSectionTests.SectionBody(
                DiagnosticReportSectionTests.Render(GameState.CreateNew()),
                DiagnosticReport.TuningSection);

            var missing = typeof(TuningConfig)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(field => field.Name)
                .Where(name => LineFor(body, name) == null)
                .ToList();

            Assert.That(missing, Is.Empty,
                "TuningConfig fields absent from the report's TUNING section: " + string.Join(", ", missing));
        }

        // ---------------------------------------------------------------
        // DEBUG
        // ---------------------------------------------------------------

        [Test]
        public void DebugSection_ListsEveryRegisteredToggleAndItsState()
        {
            var toggles = new DebugToggleRegistry();
            toggles.Register("show-backyard-fences");
            toggles.Register("show-debug-element-colors", true);

            var body = DiagnosticReportSectionTests.SectionBody(
                DiagnosticReportSectionTests.Render(GameState.CreateNew(), toggles: toggles),
                DiagnosticReport.DebugSection);

            Assert.That(body, Does.Contain("show-backyard-fences=off"));
            Assert.That(body, Does.Contain("show-debug-element-colors=on"));
        }

        // ---------------------------------------------------------------
        // ECONOMY
        // ---------------------------------------------------------------

        [Test]
        public void EconomySection_QuotesTheBalanceAndTheNextExpansionPrices()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(240);

            var body = DiagnosticReportSectionTests.SectionBody(
                DiagnosticReportSectionTests.Render(state), DiagnosticReport.EconomySection);

            Assert.That(body, Does.Contain("coins=240"));
            Assert.That(body, Does.Contain("nextTileUnlockCost="
                + Doggiehood.Core.Expansion.TileUnlock.Cost(state.RoadTileCount)));
            Assert.That(body, Does.Contain("nextHouseBuildCost="
                + Doggiehood.Core.Expansion.HouseBuildNumbers.Cost(state.PlayerBuiltHouseCount)));
            Assert.That(body, Does.Contain("upgradeCostToLevel2="
                + Doggiehood.Core.Expansion.HouseUpgradeNumbers.CostToLevel2));
            Assert.That(body, Does.Contain("upgradeCostToLevel4="
                + Doggiehood.Core.Expansion.HouseUpgradeNumbers.CostToLevel4));
        }

        // ---------------------------------------------------------------
        // MAP / HOUSES / DOGS
        // ---------------------------------------------------------------

        [Test]
        public void MapSection_ListsUnlockedTilesInUnlockOrderWithTheirTypes()
        {
            var state = AfterOnboarding();
            var unlocked = UnlockTwoTiles(state);

            var body = DiagnosticReportSectionTests.SectionBody(
                DiagnosticReportSectionTests.Render(state), DiagnosticReport.MapSection);

            var listed = body.Split('\n')
                .SkipWhile(line => line.Trim() != DiagnosticReport.UnlockedTilesLabel + ":")
                .Skip(1)
                .Take(unlocked.Count)
                .Select(line => line.Trim())
                .ToList();

            Assert.That(listed.Count, Is.EqualTo(unlocked.Count));
            for (var i = 0; i < unlocked.Count; i++)
            {
                Assert.That(listed[i], Does.StartWith((i + 1) + ". ("
                    + unlocked[i].Col + "," + unlocked[i].Row + ")"),
                    "unlock ORDER is the point — a bug about the tile opened last needs the sequence");
                Assert.That(listed[i], Does.EndWith(state.Map.GetTileAt(unlocked[i]).ToString()),
                    "each unlocked tile carries its tile type");
            }

            Assert.That(body, Does.Contain("roadTiles=" + state.RoadTileCount));
        }

        [Test]
        public void HousesSection_CoversBuiltHousesAndAssignedButUnbuiltLotVariants()
        {
            var state = AfterOnboarding();
            var unlocked = UnlockTwoTiles(state);
            var lots = state.LotsForUnlockedTile(unlocked[0]);
            Assert.That(lots, Is.Not.Empty, "precondition: an unlocked tile carries lots");

            state.Wallet.Deposit(Doggiehood.Core.Expansion.HouseBuildNumbers.Cost(state.PlayerBuiltHouseCount));
            Assert.That(state.TryBuildHouse(lots[0].HouseId), Is.True, "precondition");

            var body = DiagnosticReportSectionTests.SectionBody(
                DiagnosticReportSectionTests.Render(state), DiagnosticReport.HousesSection);

            // The freshly built frontier house, with everything about it.
            var built = body.Split('\n').First(line => line.Trim().StartsWith("#" + lots[0].HouseId + " "));
            Assert.That(built, Does.Contain("level=1"));
            Assert.That(built, Does.Contain("vacant=yes"));
            Assert.That(built, Does.Contain("variant=ladder"));
            Assert.That(built, Does.Contain("upgradeEligible="));
            Assert.That(built, Does.Contain("lot=("));

            // A starting house reports its occupants.
            var starter = body.Split('\n').First(line => line.Trim().StartsWith("#1 "));
            Assert.That(starter, Does.Not.Contain("occupants=" + DiagnosticReport.EmptyMarker));

            // …and the sibling lots on the same tile are assigned-but-unbuilt.
            var unbuilt = DiagnosticReportSectionTests.SectionBody(
                    DiagnosticReportSectionTests.Render(state), DiagnosticReport.HousesSection)
                .Split('\n')
                .SkipWhile(line => line.Trim() != DiagnosticReport.UnbuiltLotVariantsLabel + ":")
                .Skip(1)
                .Select(line => line.Trim())
                .ToList();
            Assert.That(unbuilt.Any(line => line.StartsWith("#" + lots[1].HouseId + " ladder")), Is.True,
                "an empty lot already knows the house that will stand on it");
            Assert.That(unbuilt.Any(line => line.StartsWith("#" + lots[0].HouseId + " ")), Is.False,
                "a built lot carries its variant on its house line instead");
        }

        [Test]
        public void DogsSection_ReportsPositionStateHouseAndActiveQuestForEachDog()
        {
            var state = GameState.CreateNew();
            var dog = state.Dogs[0];
            state.Quests.GiveQuestTo(dog, Doggiehood.Core.Quests.QuestType.LostItem, new Random(7));

            var positions = new Dictionary<string, GridPoint>
            {
                { dog.Name, new GridPoint(3.25f, -4.5f) },
            };

            var body = DiagnosticReportSectionTests.SectionBody(
                DiagnosticReportSectionTests.Render(state, dogPositions: positions),
                DiagnosticReport.DogsSection);

            var line = body.Split('\n').First(l => l.Trim().StartsWith(dog.Name + " "));
            Assert.That(line, Does.Contain("position=(3.25,-4.50)"),
                "where the dog was standing at that instant");
            Assert.That(line, Does.Contain("house=" + dog.HouseId));
            Assert.That(line, Does.Contain("state=" + dog.State));
            Assert.That(line, Does.Contain("location=" + dog.Location));
            Assert.That(line, Does.Contain("hasActiveQuest=yes"));
            Assert.That(line, Does.Contain("quest=#"));

            // Every dog is covered, not just the one with a position supplied.
            Assert.That(state.Dogs.Count, Is.GreaterThan(1), "precondition");
            foreach (var roster in state.Dogs)
            {
                Assert.That(body, Does.Contain(roster.Name + " breed="));
            }
        }

        // ---------------------------------------------------------------
        // ONBOARDING
        // ---------------------------------------------------------------

        [Test]
        public void OnboardingSection_ReportsTheFlagTheChainStepAndTheUpgradeTarget()
        {
            var state = GameState.CreateNew();
            state.GrantOnboardingCompletionReward(houseId: 3);

            var body = DiagnosticReportSectionTests.SectionBody(
                DiagnosticReportSectionTests.Render(state), DiagnosticReport.OnboardingSection);

            Assert.That(body, Does.Contain("onboardingComplete=no"));
            Assert.That(body, Does.Contain("rewardChainStep=" + state.RewardChain.CurrentStep));
            Assert.That(body, Does.Contain("onboardingUpgradeTargetHouseId=3"));
        }

        // ---------------------------------------------------------------
        // LOG — last section, bounded, newest last, stack traces preserved
        // ---------------------------------------------------------------

        [Test]
        public void LogSection_EmitsAtMostTheTailSizeNewestLastWithStackTraces()
        {
            var log = new List<DiagnosticLogEntry>();
            for (var i = 0; i < DiagnosticNumbers.LogTailSize + 25; i++)
            {
                log.Add(new DiagnosticLogEntry(DiagnosticLogSeverity.Log, "line-" + i));
            }

            log.Add(new DiagnosticLogEntry(
                DiagnosticLogSeverity.Exception, "boom", "at Foo()\nat Bar()"));

            var report = DiagnosticReportSectionTests.Render(GameState.CreateNew(), log: log);
            var body = DiagnosticReportSectionTests.SectionBody(report, DiagnosticReport.LogSection);
            var lines = body.Split('\n').Where(line => line.Length > 0).ToList();

            Assert.That(lines.Count(line => line.StartsWith("[")),
                Is.EqualTo(DiagnosticNumbers.LogTailSize),
                "the tail is bounded — an unbounded log would swamp the report");
            Assert.That(body, Does.Not.Contain("line-0 "), "the oldest lines fall off the front");
            Assert.That(lines.Last(line => line.StartsWith("[")), Does.Contain("boom"),
                "newest last");
            Assert.That(body, Does.Contain("at Foo()"));
            Assert.That(body, Does.Contain("at Bar()"),
                "an exception carries its stack trace");
            Assert.That(report.TrimEnd('\n'), Does.EndWith("at Bar()"),
                "LOG is the final section, so a truncated report is recognizable as truncated");
        }

        // ---------------------------------------------------------------
        // Invariant — a diagnostic report is read-only
        // ---------------------------------------------------------------

        [Test]
        public void RenderingTwice_IsByteIdenticalAndChangesNothing()
        {
            var state = AfterOnboarding();
            UnlockTwoTiles(state);
            var toggles = new DebugToggleRegistry();
            toggles.Register("show-backyard-fences", true);

            var saveBefore = SaveCodec.Save(state);
            var coinsBefore = state.Wallet.Coins;
            var tilesBefore = state.UnlockedTiles.Count;
            var dogsBefore = state.Dogs.Count;
            var payoutBefore = TuningConfig.Active.QuestPayout;

            var first = DiagnosticReportSectionTests.Render(state, TuningConfig.Active, toggles);
            var second = DiagnosticReportSectionTests.Render(state, TuningConfig.Active, toggles);

            Assert.That(second, Is.EqualTo(first), "the same state must render the same bytes");
            Assert.That(SaveCodec.Save(state), Is.EqualTo(saveBefore),
                "snapshotting a bug must not change the bug");
            Assert.That(state.Wallet.Coins, Is.EqualTo(coinsBefore));
            Assert.That(state.UnlockedTiles.Count, Is.EqualTo(tilesBefore));
            Assert.That(state.Dogs.Count, Is.EqualTo(dogsBefore));
            Assert.That(TuningConfig.Active.QuestPayout, Is.EqualTo(payoutBefore));
            Assert.That(toggles.IsOn("show-backyard-fences"), Is.True, "no toggle is flipped");
        }

        // ---------------------------------------------------------------
        // helpers
        // ---------------------------------------------------------------

        private static string LineFor(string tuningBody, string fieldName)
        {
            return tuningBody.Split('\n')
                .FirstOrDefault(line => line.Length > 2 && line.Substring(2).StartsWith(fieldName + " "));
        }

        private static GameState AfterOnboarding()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(LoadAuthoredTargetMap());
            state.RestoreRewardChainStep(OnboardingRewardStep.Done);
            return state;
        }

        /// <summary>Unlocks the first two affordable frontier tiles, returning
        /// them in unlock order.</summary>
        private static IReadOnlyList<TileCoordinate> UnlockTwoTiles(GameState state)
        {
            var unlocked = new List<TileCoordinate>();
            for (var i = 0; i < 2; i++)
            {
                var next = state.UnlockableFrontier().First();
                state.Wallet.Deposit(Doggiehood.Core.Expansion.TileUnlock.Cost(state.RoadTileCount));
                Assert.That(state.TryUnlockTile(next), Is.True, "precondition: unlock " + next);
                unlocked.Add(next);
            }

            return unlocked;
        }

        private static TileMap LoadAuthoredTargetMap()
        {
            var definition = MapDefinition.Parse(File.ReadAllText(AuthoredMapPath()));
            return MapLoader.Load(definition).Map;
        }

        private static string AuthoredMapPath([CallerFilePath] string thisFilePath = null)
        {
            var testFileDirectory = Path.GetDirectoryName(thisFilePath);
            var repoRoot = Path.GetFullPath(Path.Combine(testFileDirectory, "..", "..", ".."));
            return Path.Combine(repoRoot, "docs", "tools", "map-data.json");
        }
    }
}
