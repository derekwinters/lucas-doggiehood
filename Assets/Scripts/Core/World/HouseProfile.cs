using Doggiehood.Core.Expansion;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// Engine-free presentation model for the house profile view (#208,
    /// docs/specs/ui/house-profile.md): the header level badge (`Lv N` plus a
    /// row of N-of-4 pips), the vacant empty-state line, and the footer
    /// Upgrade entry point (#59) — its next cost, or a disabled Max-level
    /// state at the cap. All read from a <see cref="House"/>'s Core data; the
    /// Unity overlay is thin wiring that renders these values. Resident rows
    /// reuse the dog's own <see cref="Dogs.DogProfile"/> and are handled by
    /// the overlay, so this model stays house-level only.
    ///
    /// This is the entry-point affordance's state only; the upgrade flow's own
    /// confirmation UI is out of scope here (#294).
    /// </summary>
    public readonly struct HouseProfile
    {
        private const string LevelBadgePrefix = "Lv ";
        private const string UpgradeLabel = "Upgrade";
        private const string UpgradeCostSeparator = " · ";
        private const string MaxLevelLabel = "Max level";

        // Reflects the greyscaled vacant house (#58): a house with no dogs yet
        // shows this line instead of resident rows, and offers no Upgrade.
        private const string VacantEmptyStateText = "No dogs live here yet.";

        public int Level { get; }
        public bool IsVacant { get; }

        private HouseProfile(int level, bool isVacant)
        {
            Level = level;
            IsVacant = isVacant;
        }

        public static HouseProfile For(House house)
        {
            return new HouseProfile(house.Level, house.IsVacant);
        }

        /// <summary>The `Lv N` badge label shown in the header.</summary>
        public string LevelText => LevelBadgePrefix + Level;

        /// <summary>Total level pips — the level cap
        /// (<see cref="HouseUpgradeNumbers.MaxLevel"/>).</summary>
        public int PipCount => HouseUpgradeNumbers.MaxLevel;

        /// <summary>How many pips are filled — the current level, so the
        /// remaining headroom to upgrade reads at a glance.</summary>
        public int FilledPipCount => Level;

        /// <summary>The empty-state line for a vacant house.</summary>
        public string EmptyStateText => VacantEmptyStateText;

        /// <summary>Whether the footer Upgrade action is shown at all. A
        /// vacant house offers none (house-profile.md); an occupied house
        /// always shows it, disabled at the cap.</summary>
        public bool ShowsUpgradeAction => !IsVacant;

        /// <summary>True once the house is at the level cap
        /// (<see cref="HouseUpgradeNumbers.MaxLevel"/>) — the Upgrade button
        /// disables into its Max-level state.</summary>
        public bool IsMaxLevel => Level >= HouseUpgradeNumbers.MaxLevel;

        /// <summary>Coin cost of the next upgrade step (100 / 200 / 400), or 0
        /// when there is no upgrade to offer (vacant or at the cap).</summary>
        public int UpgradeCost =>
            ShowsUpgradeAction && !IsMaxLevel ? HouseUpgradeNumbers.CostToReach(Level + 1) : 0;

        /// <summary>The Upgrade button label: the next cost while upgradable,
        /// or the disabled Max-level label at the cap.</summary>
        public string UpgradeButtonText =>
            IsMaxLevel ? MaxLevelLabel : UpgradeLabel + UpgradeCostSeparator + UpgradeCost;
    }
}
