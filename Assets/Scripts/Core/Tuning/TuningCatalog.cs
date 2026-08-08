using System;
using System.Collections.Generic;

namespace Doggiehood.Core.Tuning
{
    /// <summary>
    /// #622: the engine-free descriptor table the debug tuning menu renders
    /// from — one <see cref="TuningField"/> per <see cref="TuningConfig"/>
    /// instance field, carrying its label, unit, group and declared slider
    /// range.
    ///
    /// <para><b>Why it exists.</b> The overlay's slider set must be built from
    /// Core, never from a list hand-maintained in the Unity layer that could
    /// drift from <see cref="TuningConfig"/> (#622's first checklist item).
    /// The entries are written out explicitly — no runtime reflection, so
    /// nothing here depends on IL2CPP keeping metadata alive — and a Core
    /// reflection <em>test</em> asserts the table covers the config exactly,
    /// field for field. Add a field to <see cref="TuningConfig"/> without a row
    /// here and the suite fails.</para>
    ///
    /// <para><b>Where the ranges come from.</b> <see cref="TuningConfig"/>
    /// declares the shipping defaults but no bounds, so each range is derived
    /// by a stated convention rather than invented per field: a proportion is
    /// <c>0..1</c> by step <see cref="ChanceStep"/>; a multiplier runs from its
    /// neutral floor by step <see cref="MultiplierStep"/>; and a whole-number
    /// count/cost runs from its natural floor (0, or 1 where zero would be
    /// degenerate) up to a round ceiling comfortably above the shipping default
    /// so there is headroom to explore in both directions. They are dev-tool
    /// slider bounds, not balance values — the balance numbers stay in
    /// <see cref="TuningConfig"/>.</para>
    /// </summary>
    public static class TuningCatalog
    {
        // --- Step grids (#161: named, not inline literals) ---

        /// <summary>Step for a whole-number count or coin cost.</summary>
        private const double WholeStep = 1d;

        /// <summary>Step for a 0..1 probability slider.</summary>
        private const double ChanceStep = 0.01d;

        /// <summary>Step for a multiplier slider.</summary>
        private const double MultiplierStep = 0.1d;

        /// <summary>Step for the breed-weight smoothing term.</summary>
        private const double SmoothingStep = 0.1d;

        // --- Shared range bounds ---

        private const double ChanceMin = 0d;
        private const double ChanceMax = 1d;
        private const double MarkupMin = 1d;
        private const double MarkupMax = 5d;
        private const double SmoothingMin = 0d;
        private const double SmoothingMax = 10d;
        private const double WeightMin = 0d;
        private const double WeightMax = 100d;
        private const double PopulationMin = 1d;
        private const double PopulationMaxSmall = 50d;
        private const double PopulationMaxLarge = 100d;
        private const double HoursMin = 1d;
        private const double HoursMax = 24d;
        private const double CoinsMin = 0d;
        private const double CoinsMaxSmall = 100d;
        private const double CoinsMaxMedium = 200d;
        private const double CoinsMaxLarge = 400d;
        private const double CoinsMaxPremium = 600d;
        private const double CoinsMaxBuild = 500d;
        private const double CoinsMaxUpgrade = 1000d;
        private const double QuestTargetMin = 1d;
        private const double QuestTargetFloorMax = 30d;
        private const double QuestTargetCeilingMax = 60d;
        private const double DivisorMin = 1d;
        private const double DivisorMax = 10d;
        private const double TileCountMin = 0d;
        private const double TileCountMax = 10d;
        private const double BatchMin = 1d;
        private const double BatchMax = 20d;
        private const double LevelMin = 1d;
        private const double LevelMax = 10d;

        // --- Unit suffixes ---

        private const string NoUnit = "";
        private const string CoinsUnit = "coins";
        private const string HoursUnit = "h";
        private const string DogsUnit = "dogs";
        private const string QuestsUnit = "quests";
        private const string TilesUnit = "tiles";
        private const string HousesUnit = "houses";
        private const string LevelUnit = "lv";
        private const string MarkupUnit = "×";
        private const string WeightUnit = "/100";

        // --- Group display names (the wireframe's exact headings) ---

        private const string PacingGroupName = "Pacing";
        private const string EconomyGroupName = "Economy";
        private const string ExpansionGroupName = "Expansion";
        private const string MoveInGroupName = "Move-in";

        private static readonly TuningGroup[] GroupOrder =
        {
            TuningGroup.Pacing,
            TuningGroup.Economy,
            TuningGroup.Expansion,
            TuningGroup.MoveIn,
        };

        private static readonly TuningField[] AllFields =
        {
            // ---- Pacing ----
            Whole(nameof(TuningConfig.TargetActiveFloor), "Quest min target (floor)", QuestsUnit, TuningGroup.Pacing,
                QuestTargetMin, QuestTargetFloorMax,
                c => c.TargetActiveFloor, (c, v) => c.TargetActiveFloor = v),
            Whole(nameof(TuningConfig.TargetActiveCeiling), "Quest max target (ceiling)", QuestsUnit, TuningGroup.Pacing,
                QuestTargetMin, QuestTargetCeilingMax,
                c => c.TargetActiveCeiling, (c, v) => c.TargetActiveCeiling = v),
            Whole(nameof(TuningConfig.TargetActiveDivisor), "Dogs per active quest", DogsUnit, TuningGroup.Pacing,
                DivisorMin, DivisorMax,
                c => c.TargetActiveDivisor, (c, v) => c.TargetActiveDivisor = v),
            Whole(nameof(TuningConfig.PacingWindowHours), "Pacing window", HoursUnit, TuningGroup.Pacing,
                HoursMin, HoursMax,
                c => c.PacingWindowHours, (c, v) => c.PacingWindowHours = v),
            Whole(nameof(TuningConfig.RefreshIntervalHours), "Refresh interval", HoursUnit, TuningGroup.Pacing,
                HoursMin, HoursMax,
                c => c.RefreshIntervalHours, (c, v) => c.RefreshIntervalHours = v),

            // ---- Economy ----
            Whole(nameof(TuningConfig.QuestPayout), "Quest reward", CoinsUnit, TuningGroup.Economy,
                CoinsMin, CoinsMaxMedium,
                c => c.QuestPayout, (c, v) => c.QuestPayout = v),
            Whole(nameof(TuningConfig.MoveInReward), "Move-in reward", CoinsUnit, TuningGroup.Economy,
                CoinsMin, CoinsMaxBuild,
                c => c.MoveInReward, (c, v) => c.MoveInReward = v),
            Real(nameof(TuningConfig.PaidQuestMarkup), "Paid-quest markup", MarkupUnit, TuningGroup.Economy,
                MarkupMin, MarkupMax, MultiplierStep,
                c => c.PaidQuestMarkup, (c, v) => c.PaidQuestMarkup = v),
            Whole(nameof(TuningConfig.StarterMinCost), "Starter cost — min", CoinsUnit, TuningGroup.Economy,
                CoinsMin, CoinsMaxMedium,
                c => c.StarterMinCost, (c, v) => c.StarterMinCost = v),
            Whole(nameof(TuningConfig.StarterMaxCost), "Starter cost — max", CoinsUnit, TuningGroup.Economy,
                CoinsMin, CoinsMaxMedium,
                c => c.StarterMaxCost, (c, v) => c.StarterMaxCost = v),
            Whole(nameof(TuningConfig.MidMinCost), "Mid cost — min", CoinsUnit, TuningGroup.Economy,
                CoinsMin, CoinsMaxLarge,
                c => c.MidMinCost, (c, v) => c.MidMinCost = v),
            Whole(nameof(TuningConfig.MidMaxCost), "Mid cost — max", CoinsUnit, TuningGroup.Economy,
                CoinsMin, CoinsMaxLarge,
                c => c.MidMaxCost, (c, v) => c.MidMaxCost = v),
            Whole(nameof(TuningConfig.PremiumMinCost), "Premium cost — min", CoinsUnit, TuningGroup.Economy,
                CoinsMin, CoinsMaxPremium,
                c => c.PremiumMinCost, (c, v) => c.PremiumMinCost = v),
            Whole(nameof(TuningConfig.StarterPopulationGate), "Starter tier gate", DogsUnit, TuningGroup.Economy,
                PopulationMin, PopulationMaxSmall,
                c => c.StarterPopulationGate, (c, v) => c.StarterPopulationGate = v),
            Whole(nameof(TuningConfig.MidPopulationGate), "Mid tier gate", DogsUnit, TuningGroup.Economy,
                PopulationMin, PopulationMaxSmall,
                c => c.MidPopulationGate, (c, v) => c.MidPopulationGate = v),
            Whole(nameof(TuningConfig.PremiumPopulationGate), "Premium tier gate", DogsUnit, TuningGroup.Economy,
                PopulationMin, PopulationMaxSmall,
                c => c.PremiumPopulationGate, (c, v) => c.PremiumPopulationGate = v),
            Whole(nameof(TuningConfig.OnboardingRewardPerStep), "Onboarding reward per step", CoinsUnit, TuningGroup.Economy,
                CoinsMin, CoinsMaxBuild,
                c => c.OnboardingRewardPerStep, (c, v) => c.OnboardingRewardPerStep = v),

            // ---- Expansion ----
            Whole(nameof(TuningConfig.TileUnlockBaseCost), "Tile unlock — base", CoinsUnit, TuningGroup.Expansion,
                CoinsMin, CoinsMaxBuild,
                c => c.TileUnlockBaseCost, (c, v) => c.TileUnlockBaseCost = v),
            Whole(nameof(TuningConfig.TileUnlockPerExistingTileStep), "Tile unlock — step", CoinsUnit, TuningGroup.Expansion,
                CoinsMin, CoinsMaxSmall,
                c => c.TileUnlockPerExistingTileStep, (c, v) => c.TileUnlockPerExistingTileStep = v),
            Whole(nameof(TuningConfig.TileUnlockOriginTileCount), "Tile unlock — origin tiles", TilesUnit, TuningGroup.Expansion,
                TileCountMin, TileCountMax,
                c => c.TileUnlockOriginTileCount, (c, v) => c.TileUnlockOriginTileCount = v),
            Whole(nameof(TuningConfig.HouseBuildBaseCost), "House build — base", CoinsUnit, TuningGroup.Expansion,
                CoinsMin, CoinsMaxBuild,
                c => c.HouseBuildBaseCost, (c, v) => c.HouseBuildBaseCost = v),
            Whole(nameof(TuningConfig.HouseBuildPerBatchStep), "House build — step", CoinsUnit, TuningGroup.Expansion,
                CoinsMin, CoinsMaxSmall,
                c => c.HouseBuildPerBatchStep, (c, v) => c.HouseBuildPerBatchStep = v),
            Whole(nameof(TuningConfig.HouseBuildHousesPerStep), "House build — houses per step", HousesUnit, TuningGroup.Expansion,
                BatchMin, BatchMax,
                c => c.HouseBuildHousesPerStep, (c, v) => c.HouseBuildHousesPerStep = v),
            Whole(nameof(TuningConfig.HouseMaxLevel), "House max level", LevelUnit, TuningGroup.Expansion,
                LevelMin, LevelMax,
                c => c.HouseMaxLevel, (c, v) => c.HouseMaxLevel = v),
            Whole(nameof(TuningConfig.HouseUpgradeCostToLevel2), "Upgrade cost — to level 2", CoinsUnit, TuningGroup.Expansion,
                CoinsMin, CoinsMaxUpgrade,
                c => c.HouseUpgradeCostToLevel2, (c, v) => c.HouseUpgradeCostToLevel2 = v),
            Whole(nameof(TuningConfig.HouseUpgradeCostToLevel3), "Upgrade cost — to level 3", CoinsUnit, TuningGroup.Expansion,
                CoinsMin, CoinsMaxUpgrade,
                c => c.HouseUpgradeCostToLevel3, (c, v) => c.HouseUpgradeCostToLevel3 = v),
            Whole(nameof(TuningConfig.HouseUpgradeCostToLevel4), "Upgrade cost — to level 4", CoinsUnit, TuningGroup.Expansion,
                CoinsMin, CoinsMaxUpgrade,
                c => c.HouseUpgradeCostToLevel4, (c, v) => c.HouseUpgradeCostToLevel4 = v),

            // ---- Move-in ----
            Real(nameof(TuningConfig.EarlyMoveInChance), "Early move-in chance", NoUnit, TuningGroup.MoveIn,
                ChanceMin, ChanceMax, ChanceStep,
                c => c.EarlyMoveInChance, (c, v) => c.EarlyMoveInChance = v),
            Real(nameof(TuningConfig.EarlyMoveInChanceIncrementPerQuest), "Early increment per quest", NoUnit, TuningGroup.MoveIn,
                ChanceMin, ChanceMax, ChanceStep,
                c => c.EarlyMoveInChanceIncrementPerQuest, (c, v) => c.EarlyMoveInChanceIncrementPerQuest = v),
            Real(nameof(TuningConfig.BaseMoveInChance), "Late move-in chance", NoUnit, TuningGroup.MoveIn,
                ChanceMin, ChanceMax, ChanceStep,
                c => c.BaseMoveInChance, (c, v) => c.BaseMoveInChance = v),
            Real(nameof(TuningConfig.MoveInChanceIncrementPerQuest), "Late increment per quest", NoUnit, TuningGroup.MoveIn,
                ChanceMin, ChanceMax, ChanceStep,
                c => c.MoveInChanceIncrementPerQuest, (c, v) => c.MoveInChanceIncrementPerQuest = v),
            Whole(nameof(TuningConfig.MoveInEarlyPopulation), "Early population", DogsUnit, TuningGroup.MoveIn,
                PopulationMin, PopulationMaxSmall,
                c => c.MoveInEarlyPopulation, (c, v) => c.MoveInEarlyPopulation = v),
            Whole(nameof(TuningConfig.MoveInLatePopulation), "Late population", DogsUnit, TuningGroup.MoveIn,
                PopulationMin, PopulationMaxLarge,
                c => c.MoveInLatePopulation, (c, v) => c.MoveInLatePopulation = v),
            Whole(nameof(TuningConfig.MoveInSingleWeight), "Household — single", WeightUnit, TuningGroup.MoveIn,
                WeightMin, WeightMax,
                c => c.MoveInSingleWeight, (c, v) => c.MoveInSingleWeight = v),
            Whole(nameof(TuningConfig.MoveInParentAndPuppyWeight), "Household — parent+puppy", WeightUnit, TuningGroup.MoveIn,
                WeightMin, WeightMax,
                c => c.MoveInParentAndPuppyWeight, (c, v) => c.MoveInParentAndPuppyWeight = v),
            Whole(nameof(TuningConfig.MoveInThreeDogWeight), "Household — three dogs", WeightUnit, TuningGroup.MoveIn,
                WeightMin, WeightMax,
                c => c.MoveInThreeDogWeight, (c, v) => c.MoveInThreeDogWeight = v),
            Real(nameof(TuningConfig.EasterEggChance), "Easter-egg chance", NoUnit, TuningGroup.MoveIn,
                ChanceMin, ChanceMax, ChanceStep,
                c => c.EasterEggChance, (c, v) => c.EasterEggChance = v),
            Real(nameof(TuningConfig.BreedWeightSmoothing), "Breed weight smoothing", NoUnit, TuningGroup.MoveIn,
                SmoothingMin, SmoothingMax, SmoothingStep,
                c => c.BreedWeightSmoothing, (c, v) => c.BreedWeightSmoothing = v),
        };

        /// <summary>Every tunable, in the panel's top-to-bottom order (grouped
        /// by <see cref="Groups"/>).</summary>
        public static IReadOnlyList<TuningField> Fields => AllFields;

        /// <summary>The four groups, in the wireframe's display order.</summary>
        public static IReadOnlyList<TuningGroup> Groups => GroupOrder;

        /// <summary>The tunables in <paramref name="group"/>, in catalog order.</summary>
        public static IReadOnlyList<TuningField> FieldsIn(TuningGroup group)
        {
            var matches = new List<TuningField>();
            for (var i = 0; i < AllFields.Length; i++)
            {
                if (AllFields[i].Group == group)
                {
                    matches.Add(AllFields[i]);
                }
            }

            return matches;
        }

        /// <summary>The group's heading text, exactly as the wireframe writes
        /// it (note the hyphen in "Move-in").</summary>
        public static string DisplayName(TuningGroup group)
        {
            switch (group)
            {
                case TuningGroup.Pacing:
                    return PacingGroupName;
                case TuningGroup.Economy:
                    return EconomyGroupName;
                case TuningGroup.Expansion:
                    return ExpansionGroupName;
                case TuningGroup.MoveIn:
                    return MoveInGroupName;
                default:
                    return group.ToString();
            }
        }

        private static TuningField Whole(
            string fieldName, string label, string unit, TuningGroup group,
            double min, double max,
            Func<TuningConfig, int> read, Action<TuningConfig, int> write)
        {
            return new TuningField(
                fieldName, label, unit, group, min, max, WholeStep, isInteger: true,
                config => read(config),
                (config, value) => write(config, (int)value));
        }

        private static TuningField Real(
            string fieldName, string label, string unit, TuningGroup group,
            double min, double max, double step,
            Func<TuningConfig, double> read, Action<TuningConfig, double> write)
        {
            return new TuningField(
                fieldName, label, unit, group, min, max, step, isInteger: false, read, write);
        }
    }
}
