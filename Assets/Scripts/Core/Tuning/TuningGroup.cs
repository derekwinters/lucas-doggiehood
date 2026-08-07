namespace Doggiehood.Core.Tuning
{
    /// <summary>
    /// #622: the four systems the debug tuning menu groups its sliders under,
    /// in the order the approved wireframe lists them
    /// (docs/specs/ui/debug-tuning-menu.md — "the four groups in order:
    /// Pacing, Economy, Expansion, Move-in").
    ///
    /// <para>Engine-free so the whole descriptive model of the panel — which
    /// tunable belongs where, and its per-group reset scope — stays
    /// NUnit-testable with no Unity install. The Unity overlay renders these;
    /// it does not decide them.</para>
    /// </summary>
    public enum TuningGroup
    {
        /// <summary>Quest-rotation cadence and the concurrent-quest target.</summary>
        Pacing = 0,

        /// <summary>Coin payouts, quest cost tiers and their population gates.</summary>
        Economy = 1,

        /// <summary>Tile-unlock, house-build and house-upgrade pricing.</summary>
        Expansion = 2,

        /// <summary>Move-in rates, the population-scaled curve, and household mix.</summary>
        MoveIn = 3,
    }
}
