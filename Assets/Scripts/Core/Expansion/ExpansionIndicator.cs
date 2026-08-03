using System.Collections.Generic;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// One resolved map-expansion lock indicator (#453): the frontier
    /// <see cref="Coordinate"/> it marks and its live
    /// <see cref="ExpansionIndicatorState"/> (where it hovers + whether the
    /// wallet can afford to unlock it). The Unity layer builds one
    /// <c>ExpansionIndicatorView</c> per entry.
    /// </summary>
    public readonly struct ExpansionIndicatorEntry
    {
        public TileCoordinate Coordinate { get; }
        public ExpansionIndicatorState State { get; }

        public ExpansionIndicatorEntry(TileCoordinate coordinate, ExpansionIndicatorState state)
        {
            Coordinate = coordinate;
            State = state;
        }
    }

    /// <summary>
    /// Resolves the map-expansion lock indicators off <see cref="GameState"/>
    /// (#178/#453, docs/specs/expansion.md "Expansion indicator"): one indicator
    /// per coordinate the player may currently unlock
    /// (<see cref="GameState.UnlockableFrontier"/>), combining
    /// <see cref="ExpansionIndicatorPlacement"/> (where it hovers, from the #109
    /// tile layout) with the #295 pricing path
    /// (<see cref="TileUnlock.Cost"/> vs the live wallet) into an
    /// <see cref="ExpansionIndicatorState"/>. Always computed fresh from live
    /// state — nothing here is cached — so the Unity views re-read it, the same
    /// "never cache" contract the HUD currency chip uses for the wallet.
    /// </summary>
    public static class ExpansionIndicator
    {
        /// <summary>
        /// One indicator per currently-unlockable frontier coordinate. Empty when
        /// nothing is unlockable (no target map supplied, or every reachable tile
        /// already placed). During onboarding the frontier is gated to the single
        /// scripted tile, so exactly one entry is returned; afterwards every open
        /// connection point returns its own entry (the #453 multi-lock fix).
        /// </summary>
        public static IReadOnlyList<ExpansionIndicatorEntry> ResolveAll(GameState state)
        {
            var cost = TileUnlock.Cost(state.Map.Tiles.Count);
            var affordable = state.Wallet.Coins >= cost;

            var entries = new List<ExpansionIndicatorEntry>();
            foreach (var coordinate in state.UnlockableFrontier())
            {
                var position = ExpansionIndicatorPlacement.Resolve(
                    state.Map, coordinate, state.TargetMap.GetTileAt(coordinate));
                entries.Add(new ExpansionIndicatorEntry(
                    coordinate, new ExpansionIndicatorState(position, affordable)));
            }

            return entries;
        }
    }
}
