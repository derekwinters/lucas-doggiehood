using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #622: the one place that answers "is this a development build?" for
    /// dev-only affordances.
    ///
    /// <para>The approved wireframe (docs/specs/ui/debug-tuning-menu.md) makes
    /// this a hard rule for the balance tuning menu: its Settings Debug-tab
    /// entry row, and therefore the whole overlay, is shown <b>only in
    /// development builds</b> and "must never appear in a release build — not
    /// merely hidden behind the existing 10-tap Debug unlock, but absent from
    /// release entirely". That is a stricter gate than the rest of the Debug
    /// tab, which the unlock gesture alone guards.</para>
    ///
    /// <para>Two layers, deliberately: the <c>DEVELOPMENT_BUILD</c> /
    /// <c>UNITY_EDITOR</c> <b>build-symbol guard</b> compiles the whole check
    /// away to a constant <c>false</c> in a release player, so no runtime path
    /// can reach the dev UI there; inside a dev build or the Editor it defers
    /// to <see cref="Debug.isDebugBuild"/>. Callers take the answer as an
    /// injected <c>bool</c> rather than reading this directly, so EditMode
    /// tests can exercise <em>both</em> sides of the gate — the Editor can only
    /// ever observe the dev-build side of the symbol itself.</para>
    /// </summary>
    public static class DevBuildGate
    {
        /// <summary>True when dev-only affordances (today: the #622 balance
        /// tuning menu) may be built. Constant-folded to <c>false</c> in a
        /// release player build.</summary>
        public static bool IsDevBuild
        {
            get
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                return Debug.isDebugBuild;
#else
                return false;
#endif
            }
        }
    }
}
