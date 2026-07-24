using Doggiehood.Core.World;

namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// Resolves the map-expansion lock indicator's full live state off
    /// <see cref="GameState"/> (#178, docs/specs/expansion.md "Expansion
    /// indicator"): combines <see cref="ExpansionIndicatorPlacement"/>
    /// (where it hovers, from the #109 tile layout) with
    /// <see cref="ZoneUnlock.IsAffordable"/> (whether the wallet covers the
    /// next zone's cost) into one <see cref="ExpansionIndicatorState"/>.
    /// Always computed fresh from live state — nothing here is cached —
    /// so the Unity view re-reads it every frame, the same pattern the HUD
    /// currency chip uses for the wallet.
    /// </summary>
    public static class ExpansionIndicator
    {
        /// <summary>
        /// Returns null when every authored <see cref="ZoneCatalog.Zones"/>
        /// entry is already unlocked — there is no next locked zone left
        /// to point an indicator at.
        /// </summary>
        public static ExpansionIndicatorState? Resolve(GameState state)
        {
            var zoneNumber = state.UnlockedZones.Count + 1;
            if (zoneNumber > ZoneCatalog.Zones.Count)
            {
                return null;
            }

            var nextZone = ZoneCatalog.Zones[zoneNumber - 1];
            var position = ExpansionIndicatorPlacement.Resolve(state.Map, nextZone);
            var affordable = ZoneUnlock.IsAffordable(state.Wallet.Coins, zoneNumber);

            return new ExpansionIndicatorState(position, affordable);
        }
    }
}
