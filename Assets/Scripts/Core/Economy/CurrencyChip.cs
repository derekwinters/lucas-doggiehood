using System.Globalization;

namespace Doggiehood.Core.Economy
{
    /// <summary>
    /// Formats the HUD currency chip's balance (#159, restyled #296). The chip's
    /// coin token region carries the "coins" meaning (shared-components.md), so
    /// this returns the bare balance — no "Coins: " prefix — with invariant
    /// culture digit grouping so it reads the same on every device locale.
    /// </summary>
    public static class CurrencyChip
    {
        public static string Label(int coins)
        {
            return coins.ToString("N0", CultureInfo.InvariantCulture);
        }
    }
}
