using Doggiehood.Core.World;

namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// The actionable "build a house on this lot" offer (#406): the flat
    /// <see cref="HouseBuildNumbers.Cost"/> and whether <see cref="GameState.Wallet"/>
    /// can afford it right now. This is the single Core source the tap-to-build
    /// confirmation dialog reads — the cost it shows on Yes, and the buildability
    /// that gates whether a lot tap does anything (a non-buildable lot's tap is a
    /// no-op that never opens the dialog). The build-side twin of the frontier
    /// tile-unlock offer; Core stays the sole authority on the spend
    /// (<see cref="GameState.TryBuildHouse"/>) either way. Always computed fresh
    /// from live state — nothing here is cached.
    /// </summary>
    public readonly struct HouseBuildOffer
    {
        /// <summary>Coin cost of building a house on the offered lot.</summary>
        public int Cost { get; }

        /// <summary>Whether the live wallet balance covers <see cref="Cost"/>.</summary>
        public bool IsAffordable { get; }

        public HouseBuildOffer(int cost, bool isAffordable)
        {
            Cost = cost;
            IsAffordable = isAffordable;
        }

        /// <summary>
        /// Resolves the offer for <paramref name="houseId"/>'s lot, or null when
        /// the lot isn't buildable (<see cref="GameState.IsLotBuildable"/> false —
        /// it already carries a house). The build cost is the flat
        /// <see cref="HouseBuildNumbers.Cost"/>.
        /// </summary>
        public static HouseBuildOffer? Resolve(GameState state, int houseId)
        {
            if (!state.IsLotBuildable(houseId))
            {
                return null;
            }

            var cost = HouseBuildNumbers.Cost;
            return new HouseBuildOffer(cost, state.Wallet.CanAfford(cost));
        }
    }
}
