using Doggiehood.Core.World;

namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// The actionable "unlock the next zone" offer (#343): the next authored
    /// <see cref="Zone"/> in sequence, its <see cref="ZoneUnlock.CostForZoneNumber"/>
    /// cost, and whether <see cref="GameState.Wallet"/> can afford it right
    /// now. This is the single Core source the tap-to-unlock confirmation
    /// dialog reads — the cost it shows on Yes, and the affordability that
    /// gates whether a lock tap does anything (a grey/unaffordable lock's
    /// tap is a no-op, docs/specs/expansion.md "Expansion indicator").
    /// Always computed fresh from live state — nothing here is cached.
    /// </summary>
    public readonly struct ZoneUnlockOffer
    {
        /// <summary>1-based number of the zone this offer would unlock
        /// (the first zone ever unlocked is zone 1).</summary>
        public int ZoneNumber { get; }

        /// <summary>Coin cost of unlocking <see cref="ZoneNumber"/>.</summary>
        public int Cost { get; }

        /// <summary>Whether the live wallet balance covers <see cref="Cost"/>.</summary>
        public bool IsAffordable { get; }

        public ZoneUnlockOffer(int zoneNumber, int cost, bool isAffordable)
        {
            ZoneNumber = zoneNumber;
            Cost = cost;
            IsAffordable = isAffordable;
        }

        /// <summary>
        /// Resolves the offer for <paramref name="state"/>, or null when
        /// every authored <see cref="ZoneCatalog.Zones"/> entry is already
        /// unlocked — there is nothing left to offer.
        /// </summary>
        public static ZoneUnlockOffer? Resolve(GameState state)
        {
            var zoneNumber = state.UnlockedZones.Count + 1;
            if (zoneNumber > ZoneCatalog.Zones.Count)
            {
                return null;
            }

            var cost = ZoneUnlock.CostForZoneNumber(zoneNumber);
            var affordable = ZoneUnlock.IsAffordable(state.Wallet.Coins, zoneNumber);
            return new ZoneUnlockOffer(zoneNumber, cost, affordable);
        }
    }
}
