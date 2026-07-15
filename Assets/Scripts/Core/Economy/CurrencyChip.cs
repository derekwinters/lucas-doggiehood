using System.Globalization;

namespace Doggiehood.Core.Economy
{
    /// <summary>
    /// Formats the HUD currency chip's label (#159). Invariant-culture digit
    /// grouping so the chip reads the same on every device locale; the chip's
    /// Candy Cottage restyle lands with #65.
    /// </summary>
    public static class CurrencyChip
    {
        public static string Label(int coins)
        {
            return string.Format(CultureInfo.InvariantCulture, "Coins: {0:N0}", coins);
        }
    }
}
