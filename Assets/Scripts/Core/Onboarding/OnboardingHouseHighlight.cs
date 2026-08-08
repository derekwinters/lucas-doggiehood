using System.Collections.Generic;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Onboarding
{
    /// <summary>
    /// #571/#668: the Unity-independent decision for the onboarding target
    /// highlight — the existing red ground-ring cue (#535) reused on the one
    /// thing the reward chain wants tapped next, so it's obvious which one to
    /// click. Mirrors <see cref="LostItemGlow.ShouldShow"/>'s shape: a small
    /// engine-free predicate the thin Unity view applies, keeping the lifecycle
    /// decision in Core.
    ///
    /// <para>It covers the two reward-chain steps that point at a specific thing
    /// in the world:</para>
    /// <list type="bullet">
    /// <item><see cref="OnboardingRewardStep.UpgradeHouse"/> ("fix up a home") —
    /// the target is the already-stored
    /// <see cref="GameState.OnboardingUpgradeTargetHouseId"/> (#469).</item>
    /// <item><see cref="OnboardingRewardStep.BuildHouse"/> ("build a new house",
    /// #668) — the target is the <b>easternmost buildable empty lot</b>
    /// (<see cref="BuildTargetLot"/>), derived live from the map.</item>
    /// </list>
    ///
    /// <para>No new Core state (#469): both targets read only
    /// <see cref="OnboardingRewardChain.CurrentStep"/> plus state that already
    /// exists — a stored id for the upgrade step, and for the build step a pure
    /// derivation from the unlocked tiles and their built houses, so no save
    /// field is added and a reload resolves the same lot. The highlight clears
    /// the moment the chain advances past its step, and stays cleared for a
    /// returning player already past it.</para>
    /// </summary>
    public static class OnboardingHouseHighlight
    {
        /// <summary>True when a target should carry the highlight: the reward
        /// chain is waiting on a step that points at something
        /// (<see cref="OnboardingRewardStep.UpgradeHouse"/> or
        /// <see cref="OnboardingRewardStep.BuildHouse"/>) AND a target id
        /// resolved. Any other step, or no target (a pre-#469 legacy save on the
        /// upgrade step; no buildable empty lot on the build step), gets no
        /// highlight.</summary>
        public static bool ShouldShow(OnboardingRewardStep currentStep, int? targetHouseId)
        {
            if (!targetHouseId.HasValue)
            {
                return false;
            }

            return currentStep == OnboardingRewardStep.UpgradeHouse
                || currentStep == OnboardingRewardStep.BuildHouse;
        }

        /// <summary>The house/lot id to highlight for <paramref name="state"/>,
        /// or null when no highlight should show. On
        /// <see cref="OnboardingRewardStep.UpgradeHouse"/> it is always exactly
        /// the stored <see cref="GameState.OnboardingUpgradeTargetHouseId"/>
        /// (never any other house) — so the target id persisting past the step
        /// (it does, #469) never re-shows the highlight. On
        /// <see cref="OnboardingRewardStep.BuildHouse"/> it is the
        /// <see cref="BuildTargetLot"/> of the map's buildable empty lots.
        /// Null-safe.</summary>
        public static int? TargetHouseId(GameState state)
        {
            if (state == null)
            {
                return null;
            }

            var step = state.RewardChain.CurrentStep;
            if (step == OnboardingRewardStep.UpgradeHouse)
            {
                var target = state.OnboardingUpgradeTargetHouseId;
                return ShouldShow(step, target) ? target : null;
            }

            if (step == OnboardingRewardStep.BuildHouse)
            {
                var lot = BuildTargetLot(BuildableLots(state));
                return lot == null ? (int?)null : lot.HouseId;
            }

            return null;
        }

        /// <summary>
        /// #668: the single lot the "build a new house" step points at — the
        /// <b>easternmost</b> of <paramref name="buildableLots"/> (Derek,
        /// 2026-08-07: "East lot"), taking the <b>northern</b> one when two are
        /// equally east. Lots sit on quadrants, and a tile that keeps all four
        /// (or a <c>CulDeSacEast</c>) has two equally-eastern lots, so the
        /// tie-break is what makes the rule total: NorthEast &gt; SouthEast &gt;
        /// NorthWest &gt; SouthWest within a tile, and the house id as a final
        /// tie-break so the answer is deterministic even for two lots at the
        /// exact same point. Returns null for an empty or null set (no buildable
        /// empty lot ⇒ no target, no throw).
        /// </summary>
        public static HouseLot BuildTargetLot(IEnumerable<HouseLot> buildableLots)
        {
            if (buildableLots == null)
            {
                return null;
            }

            HouseLot best = null;
            foreach (var lot in buildableLots)
            {
                if (lot != null && (best == null || IsFurtherEast(lot, best)))
                {
                    best = lot;
                }
            }

            return best;
        }

        /// <summary>Whether <paramref name="candidate"/> beats
        /// <paramref name="incumbent"/> under the east-then-north-then-id
        /// ordering. X is east (see <see cref="GridPoint"/>), Z is north.</summary>
        private static bool IsFurtherEast(HouseLot candidate, HouseLot incumbent)
        {
            if (candidate.Position.X != incumbent.Position.X)
            {
                return candidate.Position.X > incumbent.Position.X;
            }

            if (candidate.Position.Z != incumbent.Position.Z)
            {
                return candidate.Position.Z > incumbent.Position.Z;
            }

            return candidate.HouseId < incumbent.HouseId;
        }

        /// <summary>Every empty lot on the map that can still be built on: the
        /// lots of each unlocked frontier tile with no house standing on them —
        /// exactly the set the Unity layer renders a foundation marker for
        /// (<c>WorldBuilder.BuildEmptyLots</c>). A pure derivation from state
        /// that already exists, which is what keeps the build-step target free
        /// of any persisted field (#469).</summary>
        private static IEnumerable<HouseLot> BuildableLots(GameState state)
        {
            foreach (var coordinate in state.UnlockedTiles)
            {
                foreach (var lot in state.LotsForUnlockedTile(coordinate))
                {
                    if (state.IsLotBuildable(lot.HouseId))
                    {
                        yield return lot;
                    }
                }
            }
        }
    }
}
