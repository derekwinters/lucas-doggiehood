using System;
using System.Collections.Generic;
using Doggiehood.Core.Expansion;

namespace Doggiehood.Core.Onboarding
{
    /// <summary>
    /// #674: the arithmetic of the onboarding reward-chain's <b>self-funding
    /// ladder</b> — the promise (docs/specs/onboarding.md) that the four
    /// scripted steps "seed enough coins that they never stall".
    ///
    /// <para>Each step asks the player to perform one real action, and every
    /// action but the first costs coins. The step's own reward is paid
    /// <i>after</i> its spend (the Core entry points charge the wallet, then
    /// advance <see cref="OnboardingRewardChain"/>), so step <c>i</c> has to be
    /// covered by the <c>i</c> rewards banked before it. That makes the ladder a
    /// relationship between the reward and the live sink prices, not a
    /// standalone number — and this type is where the relationship lives, so a
    /// balance pass that moves one side can be caught by a test rather than by a
    /// player stuck in the tutorial.</para>
    ///
    /// <para>Every cost is read from the live pricing seams
    /// (<see cref="HouseUpgradeNumbers"/>, <see cref="TileUnlock"/>,
    /// <see cref="HouseBuildNumbers"/> — all of which draw
    /// <c>TuningConfig.Active</c>), so an override from the debug tuning menu
    /// (#622) is reflected here too and nothing is inlined as a literal
    /// (#161). Engine-free plain C# per the Core/Unity split.</para>
    /// </summary>
    public static class OnboardingLadder
    {
        /// <summary>Step 1 (complete the first quest) asks the player to spend
        /// nothing — it is the rung that primes the ladder, and the only one a
        /// player with an empty wallet could possibly reach.</summary>
        public const int FirstQuestStepCost = 0;

        /// <summary>Player-unlocked tiles standing when step 3 (expand) fires:
        /// none — only the seeded origin is placed, so the unlock is priced at
        /// the base. <see cref="TileUnlock.Cost"/> takes the total placed count
        /// (origin included), which is exactly
        /// <see cref="TileUnlockNumbers.OriginTileCount"/> at this point.</summary>
        private static int PlacedTilesAtExpandStep => TileUnlockNumbers.OriginTileCount;

        /// <summary>Player-built houses standing when step 4 (build) fires:
        /// none — the four starting houses are excluded from the build curve, so
        /// the build is priced at the base.</summary>
        private const int PlayerBuiltHousesAtBuildStep = 0;

        /// <summary>How many scripted steps the chain runs.</summary>
        public static int StepCount => StepCosts.Count;

        /// <summary>The coin cost of the action each scripted step asks for, in
        /// <see cref="OnboardingRewardStep"/> order, read live from the pricing
        /// seams.</summary>
        public static IReadOnlyList<int> StepCosts
        {
            get
            {
                return new[]
                {
                    FirstQuestStepCost,
                    HouseUpgradeNumbers.CostToLevel2,
                    TileUnlock.Cost(PlacedTilesAtExpandStep),
                    HouseBuildNumbers.Cost(PlayerBuiltHousesAtBuildStep),
                };
            }
        }

        /// <summary>Everything the four steps cost the player end to end.</summary>
        public static int TotalCost
        {
            get
            {
                var total = 0;
                var costs = StepCosts;
                for (var step = 0; step < costs.Count; step++)
                {
                    total += costs[step];
                }

                return total;
            }
        }

        /// <summary>The smallest flat per-step reward that funds the whole
        /// ladder at today's costs — <b>derived</b>, never typed in. Step
        /// <c>i</c> (0-based) is paid for out of the <c>i</c> rewards banked
        /// before it, so the reward must satisfy
        /// <c>i × R ≥ cost₀ + … + costᵢ</c> at every rung; the binding rung is
        /// whichever needs the most.</summary>
        public static int MinimumSelfFundingRewardPerStep
        {
            get
            {
                var costs = StepCosts;
                var cumulative = 0;
                var minimum = 0;
                for (var step = 0; step < costs.Count; step++)
                {
                    cumulative += costs[step];

                    // `step` is also the number of rewards already banked when
                    // this rung's action is paid for. At the first rung that is
                    // zero — nothing has paid out yet — so no reward can fund a
                    // cost there; the rung is free by construction
                    // (FirstQuestStepCost).
                    if (step == 0)
                    {
                        continue;
                    }

                    minimum = Math.Max(minimum, CeilingDivide(cumulative, step));
                }

                return minimum;
            }
        }

        /// <summary>Whether the shipping per-step reward funds the whole ladder
        /// at today's costs — the invariant the tutorial rests on.</summary>
        public static bool IsSelfFunding => IsSelfFundingAt(OnboardingRewardChainNumbers.RewardPerStep);

        /// <summary>Walks the ladder at a hypothetical flat
        /// <paramref name="rewardPerStep"/>: true when the player, starting from
        /// nothing, can afford every step's action at the moment the chain
        /// reaches it and never goes into the red.</summary>
        public static bool IsSelfFundingAt(int rewardPerStep)
        {
            var balance = 0;
            var costs = StepCosts;
            for (var step = 0; step < costs.Count; step++)
            {
                if (balance < costs[step])
                {
                    return false;
                }

                // The step's action is paid for first, then its reward lands.
                balance = balance - costs[step] + rewardPerStep;
            }

            return true;
        }

        private static int CeilingDivide(int dividend, int divisor)
        {
            return (dividend + divisor - 1) / divisor;
        }
    }
}
