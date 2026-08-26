using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Doggiehood.Core.Debugging;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.Onboarding;
using Doggiehood.Core.Quests;
using Doggiehood.Core.Tuning;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Diagnostics
{
    /// <summary>
    /// #692: the engine-free bug-report renderer — everything the game knows
    /// about itself, as one deterministic plain-text document. It touches no
    /// <c>UnityEngine</c> type (the device facts arrive as a
    /// <see cref="DiagnosticEnvironment"/>, the log tail as plain
    /// <see cref="DiagnosticLogEntry"/> data), so the whole payload is
    /// unit-testable against a hand-built <see cref="GameState"/>.
    ///
    /// <para><b>Invariant — a diagnostic report never silently omits a
    /// system.</b> Every section header is emitted on every report, an empty
    /// body printing <see cref="EmptyMarker"/>, so a missing system reads as
    /// "there were none" rather than as "this build forgot to capture it".</para>
    /// </summary>
    public static class DiagnosticReport
    {
        // --- section names (#161: named, never inline literals) ---

        public const string ReportSection = "REPORT";
        public const string SaveSection = "SAVE";
        public const string TuningSection = "TUNING";
        public const string DebugSection = "DEBUG";
        public const string EconomySection = "ECONOMY";
        public const string MapSection = "MAP";
        public const string HousesSection = "HOUSES";
        public const string DogsSection = "DOGS";
        public const string QuestsSection = "QUESTS";
        public const string OnboardingSection = "ONBOARDING";
        public const string ItemsSection = "ITEMS";
        public const string LogSection = "LOG";

        /// <summary>The delimiter every section header opens with, so a reader
        /// can find section boundaries by prefix alone.</summary>
        public const string HeaderFence = "==";

        /// <summary>What an empty section or sub-list prints, so "there were
        /// none" is never confused with "this build forgot to capture it".</summary>
        public const string EmptyMarker = "(none)";

        /// <summary>Marks a tuning field whose live value differs from its
        /// shipping default — the first thing to look at when a bug only
        /// reproduces on one device.</summary>
        public const string OverriddenMarker = "*";

        /// <summary>Prefix of a tuning field sitting at its shipping default —
        /// the same width as <see cref="OverriddenMarker"/> so the column
        /// stays aligned.</summary>
        public const string DefaultMarker = " ";

        /// <summary>How a switched-on toggle reads in the <c>DEBUG</c> section.</summary>
        public const string OnMarker = "on";

        /// <summary>How a switched-off toggle reads.</summary>
        public const string OffMarker = "off";

        // --- named sub-list labels, so a reader (and a test) can find one ---

        public const string UnlockedTilesLabel = "unlockedTiles";
        public const string GreenSpacesLabel = "greenSpaces";
        public const string FrontierLabel = "frontier";
        public const string BuiltHousesLabel = "builtHouses";
        public const string UnbuiltLotVariantsLabel = "unbuiltLotVariants";
        public const string ActiveQuestsLabel = "activeQuests";
        public const string PlacedItemsLabel = "placedItems";
        public const string DecorationsLabel = "decorations";

        /// <summary>Indent for a list entry under its label.</summary>
        private const string Indent = "  ";

        /// <summary>Indent for a stack-trace line under its log entry.</summary>
        private const string StackTraceIndent = "      ";

        /// <summary>What an unresolvable value reads as. A report must never
        /// throw while capturing a bug.</summary>
        private const string Unknown = "(unknown)";

        private static readonly string[] Sections =
        {
            ReportSection,
            SaveSection,
            TuningSection,
            DebugSection,
            EconomySection,
            MapSection,
            HousesSection,
            DogsSection,
            QuestsSection,
            OnboardingSection,
            ItemsSection,
            LogSection,
        };

        /// <summary>Every section, in the order a report emits them. <c>LOG</c>
        /// is deliberately last: it makes the end of the report identifiable, so
        /// a truncated report is recognizable as truncated.</summary>
        public static IReadOnlyList<string> SectionNames => Sections;

        /// <summary>The exact header line a section is introduced by.</summary>
        public static string HeaderFor(string section)
        {
            return HeaderFence + " " + section + " " + HeaderFence;
        }

        /// <summary>Renders the whole snapshot.</summary>
        public static string Render(
            GameState state,
            TuningConfig tuning,
            DebugToggleRegistry toggles,
            DiagnosticEnvironment environment,
            IReadOnlyList<DiagnosticLogEntry> log,
            IReadOnlyDictionary<string, GridPoint> dogWorldPositions = null)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var builder = new StringBuilder();
            AppendReport(builder, environment);
            AppendSave(builder, state);
            AppendTuning(builder, tuning);
            AppendDebug(builder, toggles);
            AppendEconomy(builder, state);
            AppendMap(builder, state);
            AppendHouses(builder, state);
            AppendDogs(builder, state, dogWorldPositions);
            AppendQuests(builder, state, tuning ?? new TuningConfig());
            AppendOnboarding(builder, state);
            AppendItems(builder, state);
            AppendLog(builder, log);
            return builder.ToString();
        }

        private static void AppendReport(StringBuilder builder, DiagnosticEnvironment environment)
        {
            Header(builder, ReportSection);
            Pair(builder, "schemaVersion", Integer(DiagnosticNumbers.ReportSchemaVersion));
            Pair(builder, "timestamp", environment.Timestamp);
            Pair(builder, "appVersion", environment.AppVersion);
            Pair(builder, "buildFlavor", environment.BuildFlavor);
            Pair(builder, "platform", environment.Platform);
            Pair(builder, "deviceModel", environment.DeviceModel);
            Pair(builder, "operatingSystem", environment.OperatingSystem);
            Pair(builder, "screen",
                Integer(environment.ScreenWidth) + "x" + Integer(environment.ScreenHeight)
                + " @" + Number(environment.ScreenDpi, DiagnosticNumbers.CoordinateDecimals) + "dpi");
            Pair(builder, "sessionUptimeSeconds",
                Number(environment.SessionUptimeSeconds, DiagnosticNumbers.UptimeDecimals));
        }

        private static void AppendSave(StringBuilder builder, GameState state)
        {
            Header(builder, SaveSection);

            // Verbatim, byte for byte: this is the line that reproduces the bug.
            builder.Append(SaveCodec.Save(state));
        }

        private static void AppendTuning(StringBuilder builder, TuningConfig tuning)
        {
            Header(builder, TuningSection);

            // Both halves of every tunable: what the game is running, and what it
            // shipped with. A fresh config IS the shipping defaults.
            var live = tuning ?? new TuningConfig();
            var shipping = new TuningConfig();

            foreach (var field in TuningCatalog.Fields)
            {
                var current = field.Read(live);
                var original = field.Read(shipping);
                builder.Append(current.Equals(original) ? DefaultMarker : OverriddenMarker)
                    .Append(' ')
                    .Append(field.FieldName)
                    .Append(" (")
                    .Append(field.Label)
                    .Append(") = ")
                    .Append(TuningValue(field, current))
                    .Append(" default=")
                    .Append(TuningValue(field, original))
                    .Append('\n');
            }
        }

        private static void AppendDebug(StringBuilder builder, DebugToggleRegistry toggles)
        {
            Header(builder, DebugSection);
            var names = toggles == null ? (IReadOnlyList<string>)new string[0] : toggles.Names;
            if (names.Count == 0)
            {
                Empty(builder);
                return;
            }

            foreach (var name in names)
            {
                Pair(builder, name, toggles.IsOn(name) ? OnMarker : OffMarker);
            }
        }

        private static void AppendEconomy(StringBuilder builder, GameState state)
        {
            Header(builder, EconomySection);
            Pair(builder, "coins", Integer(state.Wallet.Coins));

            // Quoted through the same seams that charge, so the report can never
            // disagree with what a tap would actually cost.
            Pair(builder, "nextTileUnlockCost", Integer(TileUnlock.Cost(state.RoadTileCount)));
            Pair(builder, "nextHouseBuildCost", Integer(HouseBuildNumbers.Cost(state.PlayerBuiltHouseCount)));
            Pair(builder, "playerBuiltHouses", Integer(state.PlayerBuiltHouseCount));
            Pair(builder, "houseMaxLevel", Integer(HouseUpgradeNumbers.MaxLevel));
            for (var level = House.InitialLevel + 1; level <= HouseUpgradeNumbers.MaxLevel; level++)
            {
                Pair(builder, "upgradeCostToLevel" + Integer(level),
                    Integer(HouseUpgradeNumbers.CostToReach(level)));
            }
        }

        private static void AppendMap(StringBuilder builder, GameState state)
        {
            Header(builder, MapSection);
            var extent = MapExtent.Covering(state.Map);
            Pair(builder, "extent",
                "x[" + Coordinate(extent.MinX) + ".." + Coordinate(extent.MaxX) + "] "
                + "z[" + Coordinate(extent.MinZ) + ".." + Coordinate(extent.MaxZ) + "]");
            Pair(builder, "placedTiles", Integer(state.Map.Tiles.Count));
            Pair(builder, "roadTiles", Integer(state.RoadTileCount));

            // Unlock ORDER is the point: a bug about "the tile I opened last"
            // needs the sequence, not a set.
            var unlocked = new List<string>();
            for (var i = 0; i < state.UnlockedTiles.Count; i++)
            {
                var coordinate = state.UnlockedTiles[i];
                unlocked.Add(Integer(i + 1) + ". " + Coordinate(coordinate)
                    + " " + state.Map.GetTileAt(coordinate));
            }

            List(builder, UnlockedTilesLabel, unlocked);

            var greenSpaces = new List<TileCoordinate>();
            foreach (var pair in state.Map.Tiles)
            {
                if (pair.Value == TileType.GreenSpace)
                {
                    greenSpaces.Add(pair.Key);
                }
            }

            List(builder, GreenSpacesLabel, SortedCoordinates(greenSpaces));
            List(builder, FrontierLabel, SortedCoordinates(state.UnlockableFrontier()));
        }

        private static void AppendHouses(StringBuilder builder, GameState state)
        {
            Header(builder, HousesSection);

            var built = new List<string>();
            foreach (var house in state.Houses.OrderBy(house => house.Id))
            {
                built.Add(DescribeHouse(state, house));
            }

            List(builder, BuiltHousesLabel, built);

            // A lot whose art variant is rolled but which carries no house yet:
            // the empty plot already knows the house that will stand on it.
            var unbuilt = new List<string>();
            foreach (var pair in state.AssignedLotVariants.OrderBy(pair => pair.Key))
            {
                if (state.IsLotBuildable(pair.Key))
                {
                    unbuilt.Add("#" + Integer(pair.Key) + " " + DescribeVariant(pair.Value));
                }
            }

            List(builder, UnbuiltLotVariantsLabel, unbuilt);
        }

        private static string DescribeHouse(GameState state, House house)
        {
            var lot = TryGetLot(state, house.Id);
            var occupants = new List<string>();
            foreach (var dog in state.Dogs)
            {
                if (dog.HouseId == house.Id)
                {
                    occupants.Add(dog.Name);
                }
            }

            return "#" + Integer(house.Id)
                + " lot=" + (lot == null ? Unknown : Coordinate(lot.Position))
                + " quadrant=" + house.Quadrant
                + " level=" + Integer(house.Level)
                + " vacant=" + YesNo(house.IsVacant)
                + " variant=" + (house.Variant.HasValue ? DescribeVariant(house.Variant.Value) : EmptyMarker)
                + " upgradeEligible=" + YesNo(state.IsHouseUpgradeEligible(house.Id))
                + " occupants=" + (occupants.Count == 0 ? EmptyMarker : string.Join(",", occupants));
        }

        private static void AppendDogs(
            StringBuilder builder, GameState state, IReadOnlyDictionary<string, GridPoint> dogWorldPositions)
        {
            Header(builder, DogsSection);
            if (state.Dogs.Count == 0)
            {
                Empty(builder);
                return;
            }

            foreach (var dog in state.Dogs)
            {
                builder.Append(Indent).Append(DescribeDog(state, dog, dogWorldPositions)).Append('\n');
            }
        }

        private static string DescribeDog(
            GameState state, Dog dog, IReadOnlyDictionary<string, GridPoint> dogWorldPositions)
        {
            // Where the dog is standing right now is a scene fact, so the Unity
            // layer hands it in; a dog with no entry still reports its Core
            // location rather than dropping out of the report.
            var position = Unknown;
            GridPoint worldPosition;
            if (dogWorldPositions != null
                && dog.Name != null
                && dogWorldPositions.TryGetValue(dog.Name, out worldPosition))
            {
                position = Coordinate(worldPosition);
            }

            var quest = state.Quests.ActiveQuests.FirstOrDefault(candidate => candidate.DogName == dog.Name);
            return dog.Name
                + " breed=" + dog.Breed
                + " coat=" + dog.Coat
                + " personality=" + dog.Personality
                + " puppy=" + YesNo(dog.IsPuppy)
                + " house=" + Integer(dog.HouseId)
                + " position=" + position
                + " location=" + dog.Location
                + " state=" + dog.State
                + " happiness=" + Integer(dog.Happiness)
                + " hasActiveQuest=" + YesNo(dog.HasActiveQuest)
                + " quest=" + (quest == null ? EmptyMarker : "#" + Integer(quest.Id) + " " + quest.Type);
        }

        private static void AppendQuests(StringBuilder builder, GameState state, TuningConfig tuning)
        {
            Header(builder, QuestsSection);
            List(builder, ActiveQuestsLabel, state.Quests.ActiveQuests
                .OrderBy(quest => quest.Id)
                .Select(DescribeQuest));

            // The clock, not just the board: "no new quests appeared" is a
            // pacing question, and the pacing numbers are tunable.
            Pair(builder, "lastRotationUtc", Timestamp(state.LastRotationUtc));
            Pair(builder, "refreshTimerStartedUtc", Timestamp(state.QuestRefreshTimerStartedUtc));
            Pair(builder, "refreshIntervalHours", Integer(tuning.RefreshIntervalHours));
            Pair(builder, "pacingWindowHours", Integer(tuning.PacingWindowHours));
            Pair(builder, "questPacingAccumulator",
                state.QuestPacingAccumulator.ToString("R", CultureInfo.InvariantCulture));
        }

        private static string DescribeQuest(Quest quest)
        {
            return "#" + Integer(quest.Id)
                + " " + quest.Type
                + " dog=" + quest.DogName
                + " item=" + (string.IsNullOrEmpty(quest.ItemName) ? EmptyMarker : quest.ItemName)
                + " cost=" + (quest.Cost.HasValue ? Integer(quest.Cost.Value) : EmptyMarker)
                + " targetHouse=" + (quest.TargetHouseId.HasValue
                    ? Integer(quest.TargetHouseId.Value)
                    : EmptyMarker)
                + " status=" + quest.Status
                + " delivery=" + quest.DeliveryPhase
                + " options=" + (quest.Options.Count == 0 ? EmptyMarker : string.Join(",", quest.Options));
        }

        private static void AppendOnboarding(StringBuilder builder, GameState state)
        {
            Header(builder, OnboardingSection);
            Pair(builder, "onboardingComplete", YesNo(state.OnboardingComplete));

            // The tutorial sequence's in-session step is not persisted anywhere
            // Core can read without constructing a sequence (which would roll a
            // target dog and break the read-only invariant), so the report says
            // whether it would run at all.
            Pair(builder, "onboardingSequenceShouldRun", YesNo(OnboardingSequence.ShouldRun(state)));
            Pair(builder, "rewardChainStep", state.RewardChain.CurrentStep.ToString());
            Pair(builder, "rewardChainComplete", YesNo(state.RewardChain.IsComplete));
            Pair(builder, "onboardingUpgradeTargetHouseId", state.OnboardingUpgradeTargetHouseId.HasValue
                ? Integer(state.OnboardingUpgradeTargetHouseId.Value)
                : EmptyMarker);
        }

        private static void AppendItems(StringBuilder builder, GameState state)
        {
            Header(builder, ItemsSection);
            List(builder, PlacedItemsLabel, state.PlacedItems
                .Select(item => "house=" + Integer(item.HouseId) + " item=" + item.ItemName));
            List(builder, DecorationsLabel, state.Decorations
                .Select(decoration => "house=" + Integer(decoration.HouseId)
                    + " item=" + decoration.ItemName
                    + " at=" + Coordinate(decoration.YardPosition)));
        }

        private static void AppendLog(StringBuilder builder, IReadOnlyList<DiagnosticLogEntry> log)
        {
            Header(builder, LogSection);
            if (log == null || log.Count == 0)
            {
                Empty(builder);
                return;
            }

            // Newest LAST, capped at the tail size: the interesting lines are the
            // ones nearest the end of the report, which is also what makes a
            // truncated report recognizable as truncated.
            var first = Math.Max(0, log.Count - DiagnosticNumbers.LogTailSize);
            for (var i = first; i < log.Count; i++)
            {
                var entry = log[i];
                builder.Append('[').Append(entry.Severity).Append("] ").Append(entry.Message).Append('\n');
                if (string.IsNullOrEmpty(entry.StackTrace))
                {
                    continue;
                }

                foreach (var line in entry.StackTrace.Split('\n'))
                {
                    builder.Append(StackTraceIndent).Append(line.TrimEnd('\r')).Append('\n');
                }
            }
        }

        // -------------------------------------------------------------
        // Formatting helpers
        // -------------------------------------------------------------

        private static void Header(StringBuilder builder, string section)
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(HeaderFor(section)).Append('\n');
        }

        private static void Pair(StringBuilder builder, string key, string value)
        {
            builder.Append(key).Append('=').Append(value).Append('\n');
        }

        private static void Empty(StringBuilder builder)
        {
            builder.Append(EmptyMarker).Append('\n');
        }

        private static string Timestamp(DateTime? instant)
        {
            return instant.HasValue
                ? instant.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
                : EmptyMarker;
        }

        private static string Integer(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>A named sub-list: its label, then one indented line per
        /// entry — or a single indented <see cref="EmptyMarker"/> when there are
        /// none, so a sub-list is never silently absent either.</summary>
        private static void List(StringBuilder builder, string label, IEnumerable<string> entries)
        {
            builder.Append(label).Append(":\n");
            var any = false;
            foreach (var entry in entries)
            {
                builder.Append(Indent).Append(entry).Append('\n');
                any = true;
            }

            if (!any)
            {
                builder.Append(Indent).Append(EmptyMarker).Append('\n');
            }
        }

        private static IEnumerable<string> SortedCoordinates(IEnumerable<TileCoordinate> coordinates)
        {
            return coordinates
                .OrderBy(coordinate => coordinate.Row)
                .ThenBy(coordinate => coordinate.Col)
                .Select(Coordinate);
        }

        private static string Coordinate(TileCoordinate coordinate)
        {
            return "(" + Integer(coordinate.Col) + "," + Integer(coordinate.Row) + ")";
        }

        private static string Coordinate(GridPoint point)
        {
            return "(" + Coordinate(point.X) + "," + Coordinate(point.Z) + ")";
        }

        private static string Coordinate(float value)
        {
            return Number(value, DiagnosticNumbers.CoordinateDecimals);
        }

        private static string DescribeVariant(Art.HouseVariant variant)
        {
            return "ladder" + Integer(variant.LadderId) + "/tint" + Integer(variant.TintIndex);
        }

        private static string YesNo(bool value)
        {
            return value ? "yes" : "no";
        }

        /// <summary>A report must never throw while capturing a bug, so a house
        /// id with no resolvable lot degrades to <see cref="Unknown"/> rather
        /// than taking the whole snapshot down with it.</summary>
        private static HouseLot TryGetLot(GameState state, int houseId)
        {
            try
            {
                return state.GetHouseLot(houseId);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static string TuningValue(TuningField field, double value)
        {
            var text = field.IsInteger
                ? ((long)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
                : value.ToString("R", CultureInfo.InvariantCulture);
            return string.IsNullOrEmpty(field.Unit) ? text : text + field.Unit;
        }

        private static string Number(double value, int decimals)
        {
            return value.ToString(
                "F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }
    }
}
