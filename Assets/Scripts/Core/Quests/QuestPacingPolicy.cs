using System;
using System.Collections.Generic;
using Doggiehood.Core.Economy;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Quests
{
    /// <summary>
    /// #310: the single dedicated seam that owns both quest-pacing decisions,
    /// so <see cref="QuestManager"/> asks the policy instead of reading raw
    /// constants inline — and future scaling rules (by dog count, by a
    /// progression/"hidden level", by quest-type mix) change here only, never
    /// in the quest engine or the Unity layer.
    ///
    /// <para><b>Cadence</b> (<see cref="ShouldRefresh"/>): every
    /// <see cref="EconomyNumbers.RefreshInterval"/> (8h), measured against a
    /// persisted UTC timestamp. This is a boundary <em>check</em>, not a
    /// timer/countdown/expiry — nothing is ever removed and no quest can fail,
    /// so it stays inside the "no timer or fail condition anywhere" constraint
    /// (economy.md #28). UTC-only, so a device-timezone change can neither
    /// double-fire nor stall it.</para>
    ///
    /// <para><b>Cap</b> (<see cref="TargetActiveCount"/>): a population-scaled
    /// target so a small neighborhood stays cozy and a large one never floods
    /// — <c>clamp(round(dogCount / divisor), floor, ceiling)</c>.</para>
    /// </summary>
    public sealed class QuestPacingPolicy
    {
        /// <summary>Whether it is time to add quests. True when no rotation has
        /// happened yet, or at least <see cref="EconomyNumbers.RefreshInterval"/>
        /// has elapsed since the last one. <paramref name="nowUtc"/> must be a
        /// UTC instant; the policy never reads <c>DateTime.Now</c> or
        /// <c>TimeZoneInfo.Local</c>.</summary>
        public bool ShouldRefresh(DateTime nowUtc, GameState state)
        {
            if (!state.LastRotationUtc.HasValue)
            {
                return true;
            }

            return nowUtc - state.LastRotationUtc.Value >= EconomyNumbers.RefreshInterval;
        }

        /// <summary>How many active quests the neighborhood should hold:
        /// <c>clamp(round(Dogs.Count / <see cref="EconomyNumbers.TargetActiveDivisor"/>),
        /// <see cref="EconomyNumbers.TargetActiveFloor"/>,
        /// <see cref="EconomyNumbers.TargetActiveCeiling"/>)</c>. Takes the whole
        /// <paramref name="state"/> so a future rule can scale on any signal
        /// (progression, quest-type mix) without changing callers.</summary>
        public int TargetActiveCount(GameState state)
        {
            var raw = (int)Math.Round(
                (double)state.Dogs.Count / EconomyNumbers.TargetActiveDivisor,
                MidpointRounding.AwayFromZero);

            if (raw < EconomyNumbers.TargetActiveFloor)
            {
                return EconomyNumbers.TargetActiveFloor;
            }

            if (raw > EconomyNumbers.TargetActiveCeiling)
            {
                return EconomyNumbers.TargetActiveCeiling;
            }

            return raw;
        }

        /// <summary>#317: the population-gated purchasable subject pool for a
        /// quest type — every <see cref="ItemCatalog"/> entry tagged
        /// <paramref name="tag"/> whose cost falls in a tier eligible at the
        /// neighborhood's current population (<c>state.Dogs.Count</c>). The
        /// pacing seam owns pool selection so difficulty scaling lives here,
        /// not in the quest engine; the pure classification is
        /// <see cref="QuestCostTiers.EligibleNames"/>. No new persisted state —
        /// population is already known. Find-only (no-cost) entries and the
        /// no-item PestControl type are unaffected, since this filters only
        /// purchasable subjects.</summary>
        public IReadOnlyList<string> EligibleSubjectPool(ItemEligibility tag, GameState state)
        {
            return QuestCostTiers.EligibleNames(ItemCatalog.Items, tag, state.Dogs.Count);
        }
    }
}
