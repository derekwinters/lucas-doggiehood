using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Doggiehood.Core.Debugging;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.Diagnostics;
using Doggiehood.Core.Tuning;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Diagnostics
{
    /// <summary>
    /// #692: what each section of the bug-report snapshot actually says. The
    /// single highest-value line in the whole report is the verbatim
    /// <see cref="SaveCodec.Save"/> blob — paste it into a dev build and the
    /// neighborhood is exactly as it was — so that is asserted first.
    /// </summary>
    public class DiagnosticReportContentTests
    {
        [Test]
        public void TheSaveSection_IsTheVerbatimSaveBlob()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(137);

            var body = DiagnosticReportSectionTests.BodyOf(
                DiagnosticReportSectionTests.Render(state), "SAVE");

            Assert.That(body.Trim('\n'), Is.EqualTo(SaveCodec.Save(state).Trim('\n')),
                "the report carries the save blob byte for byte — this is the line that " +
                "reproduces the bug");
        }

        [Test]
        public void TheSaveSection_LoadsBackToAnEquivalentState()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(137);
            state.MarkOnboardingComplete();

            var body = DiagnosticReportSectionTests.BodyOf(
                DiagnosticReportSectionTests.Render(state), "SAVE").Trim('\n');

            var reloaded = SaveCodec.Load(body);

            Assert.That(reloaded.Wallet.Coins, Is.EqualTo(state.Wallet.Coins));
            Assert.That(reloaded.OnboardingComplete, Is.EqualTo(state.OnboardingComplete));
            Assert.That(SaveCodec.Save(reloaded).Trim('\n'), Is.EqualTo(body),
                "the blob pasted out of a report round-trips through the real codec");
        }

        // -------------------------------------------------------------
        // TUNING — current value, shipping default, and the override marker
        // -------------------------------------------------------------

        /// <summary>Renders with a caller-supplied config, so an override can be
        /// asserted without touching the global <see cref="TuningConfig.Active"/>.</summary>
        private static string TuningBodyWith(TuningConfig tuning)
        {
            var report = DiagnosticReport.Render(
                GameState.CreateNew(),
                tuning,
                new DebugToggleRegistry(),
                DiagnosticReportSectionTests.Environment(),
                new List<DiagnosticLogEntry>());

            return DiagnosticReportSectionTests.BodyOf(report, "TUNING");
        }

        private static string TuningLineFor(string body, string fieldName)
        {
            var line = body.Split('\n').FirstOrDefault(candidate => candidate.Contains(fieldName));
            Assert.That(line, Is.Not.Null, fieldName + " has a line in the TUNING section");
            return line;
        }

        [Test]
        public void TheTuningSection_MarksAnOverriddenFieldAndLeavesAnUntouchedOneUnmarked()
        {
            var tuning = new TuningConfig();
            var shippingPayout = new TuningConfig().QuestPayout;
            tuning.QuestPayout = shippingPayout + 5;

            var body = TuningBodyWith(tuning);
            var overridden = TuningLineFor(body, nameof(TuningConfig.QuestPayout));
            var untouched = TuningLineFor(body, nameof(TuningConfig.MoveInReward));

            Assert.That(overridden.StartsWith(DiagnosticReport.OverriddenMarker), Is.True,
                "a field dragged off its shipping default is marked: " + overridden);
            Assert.That(untouched.StartsWith(DiagnosticReport.OverriddenMarker), Is.False,
                "a field still at its shipping default is not marked: " + untouched);
        }

        [Test]
        public void TheTuningSection_PrintsBothTheLiveValueAndTheShippingDefault()
        {
            var tuning = new TuningConfig();
            var shippingPayout = new TuningConfig().QuestPayout;
            tuning.QuestPayout = shippingPayout + 5;

            var line = TuningLineFor(TuningBodyWith(tuning), nameof(TuningConfig.QuestPayout));

            Assert.That(line, Does.Contain((shippingPayout + 5).ToString()),
                "the value the game is actually running");
            Assert.That(line, Does.Contain("default=" + shippingPayout),
                "and the shipping default it was dragged away from");
        }

        [Test]
        public void EveryTuningConfigField_AppearsInTheTuningSection()
        {
            // The same guard TuningCatalog carries: add a tunable without report
            // coverage and this fails, so the snapshot cannot quietly drift as
            // the balance grows.
            var body = TuningBodyWith(new TuningConfig());

            var fields = typeof(TuningConfig)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(field => field.Name)
                .ToArray();

            Assert.That(fields, Is.Not.Empty, "TuningConfig has tunable instance fields");

            foreach (var field in fields)
            {
                Assert.That(body, Does.Contain(field),
                    field + " is missing from the report's TUNING section");
            }
        }

        // -------------------------------------------------------------
        // DEBUG + ECONOMY
        // -------------------------------------------------------------

        [Test]
        public void TheDebugSection_ListsEveryRegisteredToggleAndItsState()
        {
            var toggles = new DebugToggleRegistry();
            toggles.Register("show-backyard-fences", true);
            toggles.Register("show-debug-element-colors");

            var report = DiagnosticReport.Render(
                GameState.CreateNew(),
                new TuningConfig(),
                toggles,
                DiagnosticReportSectionTests.Environment(),
                new List<DiagnosticLogEntry>());

            var body = DiagnosticReportSectionTests.BodyOf(report, "DEBUG");

            Assert.That(body, Does.Contain("show-backyard-fences=on"));
            Assert.That(body, Does.Contain("show-debug-element-colors=off"));
        }

        [Test]
        public void TheEconomySection_QuotesTheWalletAndTheNextTwoPrices()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(321);

            var body = DiagnosticReportSectionTests.BodyOf(
                DiagnosticReportSectionTests.Render(state), "ECONOMY");

            Assert.That(body, Does.Contain("coins=321"));
            Assert.That(body, Does.Contain("nextTileUnlockCost=" + TileUnlock.Cost(state.RoadTileCount)),
                "the price quoted is the one the unlock seam would actually charge");
            Assert.That(body, Does.Contain("nextHouseBuildCost=" + HouseBuildNumbers.Cost(state.PlayerBuiltHouseCount)));
        }

        [Test]
        public void TheEconomySection_QuotesEveryPerLevelUpgradeCost()
        {
            var body = DiagnosticReportSectionTests.BodyOf(
                DiagnosticReportSectionTests.Render(GameState.CreateNew()), "ECONOMY");

            for (var level = House.InitialLevel + 1; level <= HouseUpgradeNumbers.MaxLevel; level++)
            {
                Assert.That(body, Does.Contain("upgradeCostToLevel" + level + "=" + HouseUpgradeNumbers.CostToReach(level)));
            }
        }

        // -------------------------------------------------------------
        // MAP / HOUSES / DOGS — the context a save file does not carry
        // -------------------------------------------------------------

        [Test]
        public void TheMapSection_ListsUnlockedTilesInUnlockOrder_WithTheirTileTypes()
        {
            var state = Tests.World.FrontierTestWorld.WithFirstTileUnlocked();
            var first = Tests.World.FrontierTestWorld.FirstTile;

            var body = DiagnosticReportSectionTests.BodyOf(
                DiagnosticReportSectionTests.Render(state), "MAP");

            Assert.That(body, Does.Contain("roadTiles=" + state.RoadTileCount));
            Assert.That(body, Does.Contain("(" + first.Col + "," + first.Row + ") " + state.Map.GetTileAt(first)),
                "an unlocked tile is listed with the type that was placed there");

            var lines = body.Split('\n').Where(line => line.Contains("(" + first.Col + "," + first.Row + ")")).ToArray();
            Assert.That(lines, Is.Not.Empty);
            Assert.That(lines[0], Does.StartWith("  1. "),
                "unlock ORDER is the point — a bug about 'the tile I opened last' needs the sequence");
        }

        [Test]
        public void TheHousesSection_CoversBuiltHousesAndAssignedButUnbuiltLotVariants()
        {
            var state = Tests.World.FrontierTestWorld.WithFirstTileUnlocked(
                extraCoins: HouseBuildNumbers.Cost(0));
            var builtLotId = Tests.World.FrontierTestWorld.FirstLotId;
            var unbuiltLotId = Tests.World.FrontierTestWorld.SecondLotId;
            Assert.That(state.TryBuildHouse(builtLotId), Is.True, "precondition: a house is built");

            var body = DiagnosticReportSectionTests.BodyOf(
                DiagnosticReportSectionTests.Render(state), "HOUSES");

            var builtLine = body.Split('\n').FirstOrDefault(line => line.Contains("#" + builtLotId + " "));
            Assert.That(builtLine, Is.Not.Null, "the built house has a line");
            Assert.That(builtLine, Does.Contain("level=" + state.GetHouseLevel(builtLotId)));
            Assert.That(builtLine, Does.Contain("vacant="));
            Assert.That(builtLine, Does.Contain("variant="));

            Assert.That(body, Does.Contain(DiagnosticReport.UnbuiltLotVariantsLabel));
            Assert.That(body.Split('\n').Any(line => line.Contains("#" + unbuiltLotId + " ")), Is.True,
                "an empty plot already knows the house that will stand on it — report it");
        }

        [Test]
        public void TheDogsSection_ReportsEachDogsHouseStatePositionAndActiveQuest()
        {
            var state = GameState.CreateNew();
            var dog = state.Dogs[0];
            var quest = state.Quests.GiveQuestTo(dog, Doggiehood.Core.Quests.QuestType.LostItem, new System.Random(1));
            var positions = new Dictionary<string, GridPoint> { { dog.Name, new GridPoint(3.5f, -7.25f) } };

            var report = DiagnosticReport.Render(
                state,
                new TuningConfig(),
                new DebugToggleRegistry(),
                DiagnosticReportSectionTests.Environment(),
                new List<DiagnosticLogEntry>(),
                positions);

            var line = DiagnosticReportSectionTests.BodyOf(report, "DOGS")
                .Split('\n').FirstOrDefault(candidate => candidate.Contains(dog.Name));

            Assert.That(line, Is.Not.Null, "every dog has a line");
            Assert.That(line, Does.Contain("house=" + dog.HouseId));
            Assert.That(line, Does.Contain("state=" + dog.State));
            Assert.That(line, Does.Contain("position=(3.50,-7.25)"),
                "where the dog was standing at that instant — only the scene knows it, " +
                "so the Unity layer hands it in");
            Assert.That(line, Does.Contain("#" + quest.Id), "and the quest it is holding");
        }

        // -------------------------------------------------------------
        // QUESTS / ONBOARDING / ITEMS
        // -------------------------------------------------------------

        [Test]
        public void TheQuestsSection_DescribesEachActiveQuestAndThePacingClock()
        {
            var state = GameState.CreateNew();
            var dog = state.Dogs[0];
            var quest = state.Quests.GiveQuestTo(dog, Doggiehood.Core.Quests.QuestType.BuyGift, new System.Random(2));
            var startedUtc = new System.DateTime(2026, 8, 26, 12, 0, 0, System.DateTimeKind.Utc);
            state.RecordQuestRefreshTimerStart(startedUtc);

            var tuning = new TuningConfig();
            var report = DiagnosticReport.Render(
                state, tuning, new DebugToggleRegistry(),
                DiagnosticReportSectionTests.Environment(), new List<DiagnosticLogEntry>());
            var body = DiagnosticReportSectionTests.BodyOf(report, "QUESTS");

            var line = body.Split('\n').FirstOrDefault(candidate => candidate.Contains("#" + quest.Id));
            Assert.That(line, Is.Not.Null, "each active quest has a line");
            Assert.That(line, Does.Contain(quest.Type.ToString()));
            Assert.That(line, Does.Contain("dog=" + quest.DogName));
            Assert.That(line, Does.Contain("status=" + quest.Status));

            Assert.That(body, Does.Contain("refreshTimerStartedUtc="),
                "the refresh timer, so 'no new quests appeared' is diagnosable");
            Assert.That(body, Does.Contain("pacingWindowHours=" + tuning.PacingWindowHours));
            Assert.That(body, Does.Contain("refreshIntervalMinutes=" + tuning.RefreshIntervalMinutes));
        }

        [Test]
        public void TheOnboardingSection_ReportsTheChainAndItsUpgradeTarget()
        {
            var state = GameState.CreateNew();
            state.GrantOnboardingCompletionReward(state.Dogs[0].HouseId);

            var body = DiagnosticReportSectionTests.BodyOf(
                DiagnosticReportSectionTests.Render(state), "ONBOARDING");

            Assert.That(body, Does.Contain("onboardingComplete=" + (state.OnboardingComplete ? "yes" : "no")));
            Assert.That(body, Does.Contain("rewardChainStep=" + state.RewardChain.CurrentStep));
            Assert.That(body, Does.Contain("onboardingUpgradeTargetHouseId=" + state.OnboardingUpgradeTargetHouseId));
        }

        [Test]
        public void TheItemsSection_ListsPlacedItemsAndDecorations()
        {
            var state = GameState.CreateNew();
            var houseId = state.Houses[0].Id;
            state.AddPlacedItem(houseId, "squeaky toy");
            state.AddDecoration(new Doggiehood.Core.Decorations.Decoration(
                "flower bed", houseId, new GridPoint(1.5f, 2.5f)));

            var body = DiagnosticReportSectionTests.BodyOf(
                DiagnosticReportSectionTests.Render(state), "ITEMS");

            Assert.That(body, Does.Contain(DiagnosticReport.PlacedItemsLabel));
            Assert.That(body, Does.Contain("squeaky toy"));
            Assert.That(body, Does.Contain(DiagnosticReport.DecorationsLabel));
            Assert.That(body, Does.Contain("flower bed"));
            Assert.That(body, Does.Contain("(1.50,2.50)"));
        }
    }
}