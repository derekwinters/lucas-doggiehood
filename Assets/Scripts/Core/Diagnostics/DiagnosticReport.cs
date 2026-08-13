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
    /// <see cref="DiagnosticEnvironment"/> and the log tail as plain
    /// <see cref="DiagnosticLogEntry"/> data), so the whole payload is
    /// unit-testable against a hand-built <see cref="GameState"/>.
    ///
    /// <para>The single highest-value line in the report is the verbatim
    /// <see cref="SaveCodec.Save"/> blob: paste it into a dev build and the
    /// neighborhood is exactly as it was. Everything else is the context a save
    /// file does not carry — which tuning sliders had been dragged off their
    /// shipping defaults, which debug toggles were on, where each dog was
    /// standing, and what the log said just before things went sideways.</para>
    ///
    /// <para><b>Invariant — a diagnostic report never silently omits a
    /// system.</b> Every section header, and every named sub-list inside a
    /// section, is emitted on every report — an empty one printing
    /// <see cref="EmptyMarker"/> — so a missing system reads as "there were
    /// none" rather than as "this build forgot to capture it".</para>
    ///
    /// <para><b>Invariant — a diagnostic report is read-only.</b> Rendering
    /// never mutates <see cref="GameState"/>, <see cref="TuningConfig"/>, the
    /// save file, or any toggle. Snapshotting a bug must not change the
    /// bug.</para>
    ///
    /// <para><b>Invariant — a diagnostic report never leaves the device.</b> It
    /// is rendered to a string here and delivered only to destinations the
    /// player already controls (the clipboard, a local file, an explicit share
    /// they initiate). Nothing in this class or its callers opens a socket
    /// (docs/specs/product-scope.md).</para>
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
        /// (and #695) can find section boundaries by prefix alone.</summary>
        public const string HeaderFence = "==";

        /// <summary>What an empty section or sub-list prints, so "there were
        /// none" is never confused with "this build forgot to capture it".</summary>
        public const string EmptyMarker = "(none)";

        /// <summary>Marks a tuning field whose live value differs from its
        /// shipping default.</summary>
        public const string OverriddenMarker = "*";

        /// <summary>Prefix of a tuning field sitting at its shipping default —
        /// the same width as <see cref="OverriddenMarker"/> so the column
        /// aligns.</summary>
        public const string DefaultMarker = " ";

        // --- named sub-list labels ---

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

        /// <summary>
        /// Renders the whole snapshot. <paramref name="tuning"/> is expected to
        /// be <see cref="TuningConfig.Active"/> — the config the game's balance
        /// seams actually read — so the <c>TUNING</c> and <c>ECONOMY</c>
        /// sections describe the same world.
        /// <paramref name="dogWorldPositions"/> maps a dog's name to where its
        /// view is standing right now; it is optional because only the Unity
        /// layer knows it, and a dog with no entry simply reports its Core
        /// location.
        /// </summary>
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
            AppendTuning(builder, tuning ?? new TuningConfig());
            AppendDebug(builder, toggles);
            AppendEconomy(builder, state);
            AppendMap(builder, state);
            AppendHouses(builder, state);
            AppendDogs(builder, state, dogWorldPositions);
            AppendQuests(builder, state);
            AppendOnboarding(builder, state);
            AppendItems(builder, state);
            AppendLog(builder, log);
            return builder.ToString();
        }

        // -------------------------------------------------------------
        // Sections
        // -------------------------------------------------------------

        private static void AppendReport(StringBuilder builder, DiagnosticEnvironment environment)
        {
            Header(builder, ReportSection);
            Pair(builder, "schemaVersion", DiagnosticNumbers.ReportSchemaVersion.ToString(CultureInfo.InvariantCulture));
            Pair(builder, "timestamp", environment.Timestamp);
            Pair(builder, "appVersion", environment.AppVersion);
            Pair(builder, "buildFlavor", environment.BuildFlavor);
            Pair(builder, "platform", environment.Platform);
            Pair(builder, "deviceModel", environment.DeviceModel);
            Pair(builder, "operatingSystem", environment.OperatingSystem);
            Pair(builder, "screen", environment.ScreenWidth.ToString(CultureInfo.InvariantCulture)
                + "x" + environment.ScreenHeight.ToString(CultureInfo.InvariantCulture)
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
            var defaults = new TuningConfig();
            foreach (var field in TuningCatalog.Fields)
            {
                var current = field.Read(tuning);
                var shipping = field.Read(defaults);
                var marker = current.Equals(shipping) ? DefaultMarker : OverriddenMarker;
                builder.Append(marker)
                    .Append(' ')
                    .Append(field.FieldName)
                    .Append(" (")
                    .Append(field.Label)
                    .Append(") = ")
                    .Append(FormatTuningValue(field, current))
                    .Append(" default=")
                    .Append(FormatTuningValue(field, shipping))
                    .Append('\n');
            }
        }

        private static void AppendDebug(StringBuilder builder, DebugToggleRegistry toggles)
        {
            Header(builder, DebugSection);
            var names = toggles == null ? Array.Empty<string>() : toggles.Names.ToArray();
            if (names.Length == 0)
            {
                Empty(builder);
                return;
            }

            foreach (var name in names)
            {
                Pair(builder, name, toggles.IsOn(name) ? "on" : "off");
            }
        }

        private static void AppendEconomy(StringBuilder builder, GameState state)
        {
            Header(builder, EconomySection);
            Pair(builder, "coins", state.Wallet.Coins.ToString(CultureInfo.InvariantCulture));
            Pair(builder, "nextTileUnlockCost",
                TileUnlock.Cost(state.RoadTileCount).ToString(CultureInfo.InvariantCulture));
            Pair(builder, "nextHouseBuildCost",
                HouseBuildNumbers.Cost(state.PlayerBuiltHouseCount).ToString(CultureInfo.InvariantCulture));
            Pair(builder, "playerBuiltHouses", state.PlayerBuiltHouseCount.ToString(CultureInfo.InvariantCulture));
            Pair(builder, "houseMaxLevel", HouseUpgradeNumbers.MaxLevel.ToString(CultureInfo.InvariantCulture));
            for (var level = House.InitialLevel + 1; level <= HouseUpgradeNumbers.MaxLevel; level++)
            {
                Pair(builder, "upgradeCostToLevel" + level.ToString(CultureInfo.InvariantCulture),
                    HouseUpgradeNumbers.CostToReach(level).ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void AppendMap(StringBuilder builder, GameState state)
        {
            Header(builder, MapSection);
            var extent = MapExtent.Covering(state.Map);
            Pair(builder, "extent",
                "x[" + Coordinate(extent.MinX) + ".." + Coordinate(extent.MaxX) + "] " +
                "z[" + Coordinate(extent.MinZ) + ".." + Coordinate(extent.MaxZ) + "]");
            Pair(builder, "placedTiles", state.Map.Tiles.Count.ToString(CultureInfo.InvariantCulture));
            Pair(builder, "roadTiles", state.RoadTileCount.ToString(CultureInfo.InvariantCulture));

            // Unlock ORDER is the point: a bug about "the tile I opened last"
            // needs the sequence, not a set.
            List(builder, UnlockedTilesLabel, state.UnlockedTiles.Select((coordinate, index) =>
                (index + 1).ToString(CultureInfo.InvariantCulture) + ". "
                + Coordinate(coordinate) + " " + state.Map.GetTileAt(coordinate)));

            List(builder, GreenSpacesLabel, SortedCoordinates(
                state.Map.Tiles.Where(pair => pair.Value == TileType.GreenSpace).Select(pair => pair.Key)));

            List(builder, FrontierLabel, SortedCoordinates(state.UnlockableFrontier()));
        }

        private static void AppendHouses(StringBuilder builder, GameState state)
        {
            Header(builder, HousesSection);
            List(builder, BuiltHousesLabel, state.Houses
                .OrderBy(house => house.Id)
                .Select(house => DescribeHouse(state, house)));

            // A lot whose art variant is rolled but which carries no house yet:
            // the empty plot already knows the house that will stand on it.
            List(builder, UnbuiltLotVariantsLabel, state.AssignedLotVariants
                .Where(pair => state.IsLotBuildable(pair.Key))
                .OrderBy(pair => pair.Key)
                .Select(pair => "#" + pair.Key.ToString(CultureInfo.InvariantCulture)
                    + " " + DescribeVariant(pair.Value)));
        }

        private static string DescribeHouse(GameState state, House house)
        {
            var lot = TryGetLot(state, house.Id);
            var occupants = state.Dogs
                .Where(dog => dog.HouseId == house.Id)
                .Select(dog => dog.Name)
                .ToArray();

            return "#" + house.Id.ToString(CultureInfo.InvariantCulture)
                + " lot=" + (lot == null ? Unknown : Coordinate(lot.Position))
                + " quadrant=" + house.Quadrant
                + " level=" + house.Level.ToString(CultureInfo.InvariantCulture)
                + " vacant=" + YesNo(house.IsVacant)
                + " variant=" + (house.Variant.HasValue ? DescribeVariant(house.Variant.Value) : EmptyMarker)
                + " upgradeEligible=" + YesNo(state.IsHouseUpgradeEligible(house.Id))
                + " occupants=" + (occupants.Length == 0 ? EmptyMarker : string.Join(",", occupants));
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
            var position = Unknown;
            if (dogWorldPositions != null
                && dog.Name != null
                && dogWorldPositions.TryGetValue(dog.Name, out var worldPosition))
            {
                position = Coordinate(worldPosition);
            }

            var quest = state.Quests.ActiveQuests.FirstOrDefault(q => q.DogName == dog.Name);
            return dog.Name
                + " breed=" + dog.Breed
                + " coat=" + dog.Coat
                + " personality=" + dog.Personality
                + " puppy=" + YesNo(dog.IsPuppy)
                + " house=" + dog.HouseId.ToString(CultureInfo.InvariantCulture)
                + " position=" + position
                + " location=" + dog.Location
                + " state=" + dog.State
                + " happiness=" + dog.Happiness.ToString(CultureInfo.InvariantCulture)
                + " hasActiveQuest=" + YesNo(dog.HasActiveQuest)
                + " quest=" + (quest == null
                    ? EmptyMarker
                    : "#" + quest.Id.ToString(CultureInfo.InvariantCulture) + " " + quest.Type);
        }

        private static void AppendQuests(StringBuilder builder, GameState state)
        {
            Header(builder, QuestsSection);
            List(builder, ActiveQuestsLabel, state.Quests.ActiveQuests
                .OrderBy(quest => quest.Id)
                .Select(DescribeQuest));

            Pair(builder, "lastRotationUtc", state.LastRotationUtc.HasValue
                ? state.LastRotationUtc.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
                : EmptyMarker);
            Pair(builder, "refreshIntervalHours",
                TuningConfig.Active.RefreshIntervalHours.ToString(CultureInfo.InvariantCulture));
            Pair(builder, "pacingWindowHours",
                TuningConfig.Active.PacingWindowHours.ToString(CultureInfo.InvariantCulture));
            Pair(builder, "questPacingAccumulator",
                state.QuestPacingAccumulator.ToString("R", CultureInfo.InvariantCulture));
        }

        private static string DescribeQuest(Quest quest)
        {
            return "#" + quest.Id.ToString(CultureInfo.InvariantCulture)
                + " " + quest.Type
                + " dog=" + quest.DogName
                + " item=" + (string.IsNullOrEmpty(quest.ItemName) ? EmptyMarker : quest.ItemName)
                + " cost=" + (quest.Cost.HasValue
                    ? quest.Cost.Value.ToString(CultureInfo.InvariantCulture)
                    : EmptyMarker)
                + " targetHouse=" + (quest.TargetHouseId.HasValue
                    ? quest.TargetHouseId.Value.ToString(CultureInfo.InvariantCulture)
                    : EmptyMarker)
                + " status=" + quest.Status
                + " delivery=" + quest.DeliveryPhase
                + " options=" + (quest.Options.Count == 0 ? EmptyMarker : string.Join(",", quest.Options));
        }

        private static void AppendOnboarding(StringBuilder builder, GameState state)
        {
            Header(builder, OnboardingSection);
            Pair(builder, "onboardingComplete", YesNo(state.OnboardingComplete));
            Pair(builder, "onboardingSequenceShouldRun", YesNo(OnboardingSequence.ShouldRun(state)));
            Pair(builder, "rewardChainStep", state.RewardChain.CurrentStep.ToString());
            Pair(builder, "rewardChainComplete", YesNo(state.RewardChain.IsComplete));
            Pair(builder, "onboardingUpgradeTargetHouseId", state.OnboardingUpgradeTargetHouseId.HasValue
                ? state.OnboardingUpgradeTargetHouseId.Value.ToString(CultureInfo.InvariantCulture)
                : EmptyMarker);
        }

        private static void AppendItems(StringBuilder builder, GameState state)
        {
            Header(builder, ItemsSection);
            List(builder, PlacedItemsLabel, state.PlacedItems
                .Select(item => "house=" + item.HouseId.ToString(CultureInfo.InvariantCulture)
                    + " item=" + item.ItemName));
            List(builder, DecorationsLabel, state.Decorations
                .Select(decoration => "house=" + decoration.HouseId.ToString(CultureInfo.InvariantCulture)
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
            // ones nearest the end of the file, where a truncated report is
            // recognizably truncated.
            var first = Math.Max(0, log.Count - DiagnosticNumbers.LogTailSize);
            for (var i = first; i < log.Count; i++)
            {
                var entry = log[i];
                builder.Append('[').Append(entry.Severity).Append("] ")
                    .Append(entry.Message).Append('\n');
                if (!string.IsNullOrEmpty(entry.StackTrace))
                {
                    foreach (var line in entry.StackTrace.Split('\n'))
                    {
                        builder.Append(StackTraceIndent).Append(line.TrimEnd('\r')).Append('\n');
                    }
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

        /// <summary>A named sub-list: its label, then one indented line per
        /// entry — or the single indented <see cref="EmptyMarker"/> when there
        /// are none, so the sub-list is never silently absent.</summary>
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
            return "(" + coordinate.Col.ToString(CultureInfo.InvariantCulture)
                + "," + coordinate.Row.ToString(CultureInfo.InvariantCulture) + ")";
        }

        private static string Coordinate(GridPoint point)
        {
            return "(" + Coordinate(point.X) + "," + Coordinate(point.Z) + ")";
        }

        private static string Coordinate(float value)
        {
            return Number(value, DiagnosticNumbers.CoordinateDecimals);
        }

        private static string Number(double value, int decimals)
        {
            return value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        private static string FormatTuningValue(TuningField field, double value)
        {
            var text = field.IsInteger
                ? ((long)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
                : value.ToString("R", CultureInfo.InvariantCulture);
            return string.IsNullOrEmpty(field.Unit) ? text : text + field.Unit;
        }

        private static string DescribeVariant(Art.HouseVariant variant)
        {
            return "ladder" + variant.LadderId.ToString(CultureInfo.InvariantCulture)
                + "/tint" + variant.TintIndex.ToString(CultureInfo.InvariantCulture);
        }

        private static string YesNo(bool value)
        {
            return value ? "yes" : "no";
        }

        /// <summary>A report must never throw while capturing a bug, so an id
        /// with no resolvable lot degrades to <see cref="Unknown"/> rather than
        /// taking the whole snapshot down with it.</summary>
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
    }
}
