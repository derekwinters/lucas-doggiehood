namespace Doggiehood.Core.Tuning
{
    /// <summary>
    /// #620: the single, central, runtime-overridable home for Core balance
    /// values. Every balance number the game reads at runtime lives here as a
    /// named, mutable instance field, seeded to today's shipping defaults —
    /// so a fresh <see cref="TuningConfig"/> reproduces current behavior
    /// exactly, and a live override (from the debug tuning menu, #622) flows
    /// through to the dependent Core logic without any call-site change.
    ///
    /// <para><b>How the seams reach it.</b> The domain "numbers" classes
    /// (<c>EconomyNumbers</c>, <c>TileUnlockNumbers</c>,
    /// <c>HouseBuildNumbers</c>, <c>MoveInNumbers</c>,
    /// <c>HouseUpgradeNumbers</c>, <c>QuestCostTiers</c>,
    /// <c>OnboardingRewardChainNumbers</c>) read their values from
    /// <see cref="Active"/>, so every consumer that goes through those classes
    /// — the pricing/pacing seams AND the gameplay paths that call them — is
    /// live-tunable from one place. The <c>…Numbers</c> classes remain the
    /// documented, discoverable named-constant homes (#161); the literal
    /// defaults now live on this config's field initializers.</para>
    ///
    /// <para><b>Behavior-preserving (#620 scope: plumbing only).</b> No default
    /// value changes here — the new pacing numbers land in #623–#626 on top of
    /// this seam. Overriding is opt-in: nothing changes on screen until a
    /// slider moves.</para>
    ///
    /// <para>Unity-independent plain C# (no <c>UnityEngine</c> dependency), per
    /// the Core/Unity split, so it stays NUnit-testable with no engine.</para>
    ///
    /// <para>Most values are a <b>scalar</b> or a linear <c>base + step×n</c>
    /// function, so each is exposed as one (scalar) or two (base + slope)
    /// tunable fields — the shape the slider UI (#622) consumes.</para>
    /// </summary>
    public sealed class TuningConfig
    {
        // ---- Economy (EconomyNumbers) ----

        /// <summary>Flat payout per completed <b>free</b>-type quest
        /// (LostItem / PestControl), which carry no item cost to reimburse.
        /// #623: raised 10 -> 20 to roughly double the early-game earn rate,
        /// which comes only from the free quest types. #626: paid types
        /// (BuyGift / DecorationRequest / fence) no longer use this flat value —
        /// they pay <see cref="PaidQuestMarkup"/> × their item cost instead.</summary>
        public int QuestPayout = 20;

        /// <summary>#675: flat coin payout when a household moves into a vacant
        /// house — <b>per household, not per dog</b>, so one move-in event pays
        /// this once whatever the household's size. 50 is exactly what a house
        /// costs to build (<see cref="HouseBuildBaseCost"/>), so a new neighbour
        /// pays for the next house. Income from it is rate-limited by quest
        /// completions (a move-in is rolled on quest completion, never on
        /// construction), so raising it cannot open a build-to-earn loop — see
        /// docs/specs/expansion.md#move-in-system.</summary>
        public int MoveInReward = 50;

        /// <summary>#626: the payout markup on a <b>paid</b>-type quest — the
        /// "getting hired to do it" margin. Completing a paid quest (BuyGift /
        /// DecorationRequest / fence) reimburses the fronted item cost times
        /// this factor (<c>round(cost × markup)</c>), so every paid job is net
        /// positive and a bigger stake pays a bigger fee. Default 1.5×
        /// (toy 30 -> 45, pool 50 -> 75, fence 100 -> 150).</summary>
        public double PaidQuestMarkup = 1.5;

        /// <summary>#543/#743: <b>minutes</b> between quest-rotation refresh
        /// boundary checks. #743 moved this off whole hours so the trickle can
        /// arrive in sub-hour chunks — the interval is granularity only, and
        /// the amount each refresh adds scales with it
        /// (<c>target × RefreshIntervalMinutes / (PacingWindowHours × 60)</c>),
        /// so the board still fills in exactly one pacing window whatever this
        /// is set to. Must stay above zero: it sits in a divisor and in the
        /// refresh <c>TimeSpan</c>, so
        /// <see cref="Doggiehood.Core.Economy.EconomyNumbers.RefreshIntervalMinutes"/>
        /// clamps a degenerate override at the config edge.</summary>
        public int RefreshIntervalMinutes = 15;

        /// <summary>#543: window (hours) the population-scaled active-quest
        /// target is spread over — the per-refresh trickle amount is
        /// <c>target × RefreshIntervalMinutes / (PacingWindowHours × 60)</c>.
        /// #624: shortened 6 -> 4 to lift the early quest rate off its floor
        /// (target/4 instead of target/6). Must stay above zero for the same
        /// divisor reason as the interval above.</summary>
        public int PacingWindowHours = 4;

        /// <summary>#310: divisor of the population-scaled concurrent-quest cap
        /// — roughly one active quest per this many dogs.</summary>
        public int TargetActiveDivisor = 3;

        /// <summary>#310: minimum aggregate active-quest target. #624: raised
        /// 3 -> 5 so more of the early neighborhood holds a quest at once.</summary>
        public int TargetActiveFloor = 5;

        /// <summary>#310: maximum aggregate active-quest target (the flood
        /// control dial).</summary>
        public int TargetActiveCeiling = 12;

        // ---- Tile unlock pricing (TileUnlockNumbers) ----

        /// <summary>Coin cost of unlocking the FIRST frontier tile (#540,
        /// raised 50 -> 200 by #674 so expanding costs about what it costs to
        /// FILL the tile it opens — 4 lots x a 50-coin house — which flips the
        /// incentive from spreading out to building houses first).</summary>
        public int TileUnlockBaseCost = 200;

        /// <summary>How much each already-unlocked tile adds to the next
        /// unlock's cost (#540).</summary>
        public int TileUnlockPerExistingTileStep = 10;

        /// <summary>Pre-seeded non-player tiles on a fresh map (just the origin
        /// FourWay), excluded from the per-tile scaling.</summary>
        public int TileUnlockOriginTileCount = 1;

        // ---- House-build pricing (HouseBuildNumbers) ----

        /// <summary>Coin cost of the FIRST house the player builds.</summary>
        public int HouseBuildBaseCost = 50;

        /// <summary>How much the build cost rises per completed batch of
        /// <see cref="HouseBuildHousesPerStep"/> houses (#540).</summary>
        public int HouseBuildPerBatchStep = 5;

        /// <summary>How many houses make up one build-cost step.</summary>
        public int HouseBuildHousesPerStep = 4;

        // ---- House-upgrade pricing (HouseUpgradeNumbers) ----

        /// <summary>The highest level a house can reach (#59).</summary>
        public int HouseMaxLevel = 4;

        /// <summary>Coin cost of the level 1 -> 2 upgrade.</summary>
        public int HouseUpgradeCostToLevel2 = 100;

        /// <summary>Coin cost of the level 2 -> 3 upgrade.</summary>
        public int HouseUpgradeCostToLevel3 = 200;

        /// <summary>Coin cost of the level 3 -> 4 upgrade.</summary>
        public int HouseUpgradeCostToLevel4 = 400;

        // ---- Move-in system (MoveInNumbers) ----

        /// <summary>Move-in chance rolled on the next completed quest after a
        /// success (or at neighborhood start), at/above the <b>late</b>
        /// population. #625: this is now the settled (large-neighborhood) end of
        /// the population-scaled curve; below <see cref="MoveInLatePopulation"/>
        /// the effective base rises toward <see cref="EarlyMoveInChance"/>.</summary>
        public double BaseMoveInChance = 0.05;

        /// <summary>Added to the chance for every completed quest without a
        /// move-in; reset to base on a success. #625: the settled (late)
        /// increment — the effective increment scales up toward
        /// <see cref="EarlyMoveInChanceIncrementPerQuest"/> for small
        /// neighborhoods.</summary>
        public double MoveInChanceIncrementPerQuest = 0.05;

        /// <summary>#625: the <b>early</b> (small-neighborhood) base move-in
        /// chance — the high end of the population-scaled curve, so a tiny
        /// starting neighborhood grows faster.</summary>
        public double EarlyMoveInChance = 0.15;

        /// <summary>#625: the <b>early</b> (small-neighborhood) per-quest
        /// increment — the high end of the population-scaled curve.</summary>
        public double EarlyMoveInChanceIncrementPerQuest = 0.15;

        /// <summary>#625: at/below this dog count the effective move-in rate is
        /// the early rate; the scaling span's small end.</summary>
        public int MoveInEarlyPopulation = 6;

        /// <summary>#625: at/above this dog count the effective move-in rate
        /// settles to the late rate (today's values); the scaling span's large
        /// end. Between the two populations the rate interpolates linearly.</summary>
        public int MoveInLatePopulation = 20;

        /// <summary>Relative weight out of 100 for a single-dog household.</summary>
        public int MoveInSingleWeight = 70;

        /// <summary>Relative weight out of 100 for a parent+puppy household.</summary>
        public int MoveInParentAndPuppyWeight = 25;

        /// <summary>Relative weight out of 100 for a three-dog household.</summary>
        public int MoveInThreeDogWeight = 5;

        /// <summary>Chance a household head is drawn from the easter-egg
        /// reserve instead of the general pools.</summary>
        public double EasterEggChance = 0.05;

        /// <summary>Smoothing term in the inverse-count breed weight so a breed
        /// with zero current dogs still gets a finite, positive weight.</summary>
        public double BreedWeightSmoothing = 1.0;

        // ---- Quest cost tiers (QuestCostTiers) ----

        /// <summary>Cheapest starter-band cost — today's catalog floor.</summary>
        public int StarterMinCost = 30;

        /// <summary>Starter-band ceiling; the gated cost cap at minimum
        /// population.</summary>
        public int StarterMaxCost = 50;

        /// <summary>Mid-band floor.</summary>
        public int MidMinCost = 60;

        /// <summary>Mid-band ceiling.</summary>
        public int MidMaxCost = 90;

        /// <summary>Premium-band floor (premium carries no ceiling).</summary>
        public int PremiumMinCost = 100;

        /// <summary>Population at which the starter tier becomes eligible
        /// (i.e. always, so today's behavior is preserved).</summary>
        public int StarterPopulationGate = 1;

        /// <summary>Population at which the mid tier becomes eligible.</summary>
        public int MidPopulationGate = 5;

        /// <summary>Population at which the premium tier becomes eligible.</summary>
        public int PremiumPopulationGate = 10;

        // ---- Onboarding reward chain (OnboardingRewardChainNumbers) ----

        /// <summary>Flat coin reward granted at each of the four scripted
        /// onboarding steps. #674 raised this 100 -> 200 in lockstep with
        /// <see cref="TileUnlockBaseCost"/>: the guided chain is self-funding by
        /// design, and the binding rung is the expand step — the player reaches
        /// it holding <c>2R − upgrade</c>, which has to cover the unlock. See
        /// <c>Doggiehood.Core.Onboarding.OnboardingLadder</c>, which derives the
        /// minimum viable reward from the live costs.</summary>
        public int OnboardingRewardPerStep = 200;

        private static TuningConfig active = new TuningConfig();

        /// <summary>The active config that all Core balance seams read from.
        /// Defaults to a fresh (shipping-defaults) instance; the debug tuning
        /// menu (#622) mutates this instance's fields or replaces it live.</summary>
        public static TuningConfig Active
        {
            get { return active; }
            set { active = value ?? new TuningConfig(); }
        }

        /// <summary>Restores <see cref="Active"/> to a fresh, shipping-defaults
        /// config — the debug menu's reset-to-defaults hook (#620). A fresh
        /// <see cref="TuningConfig"/> is, by construction, bit-identical to
        /// shipping defaults.</summary>
        public static void ResetToDefaults()
        {
            active = new TuningConfig();
        }

        /// <summary>#622: restores only <paramref name="group"/>'s fields on
        /// <see cref="Active"/> to their shipping defaults, leaving every other
        /// group's live override in place — the debug tuning menu's per-group
        /// "Reset" button (docs/specs/ui/debug-tuning-menu.md: "each group also
        /// carries its own Reset restoring just that group's fields").
        ///
        /// <para>Unlike <see cref="ResetToDefaults"/>, which re-seeds by
        /// swapping in a fresh instance, this mutates the live instance in
        /// place: a partial reset must not discard the other groups' overrides
        /// that instance is carrying. Which fields belong to which group is the
        /// engine-free <see cref="TuningCatalog"/>'s answer, so the scope can
        /// never drift from the panel's own grouping.</para></summary>
        public static void ResetGroupToDefaults(TuningGroup group)
        {
            var defaults = new TuningConfig();
            var fields = TuningCatalog.FieldsIn(group);
            for (var i = 0; i < fields.Count; i++)
            {
                fields[i].Write(active, fields[i].Read(defaults));
            }
        }
    }
}
