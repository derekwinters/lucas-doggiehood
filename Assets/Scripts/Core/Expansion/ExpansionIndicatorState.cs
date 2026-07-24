using Doggiehood.Core.World;

namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// The map-expansion lock indicator's live state (#178): where it
    /// hovers and whether it should tint affordable (gold) or not
    /// (grey/black). See <see cref="ExpansionIndicator.Resolve"/>.
    /// </summary>
    public readonly struct ExpansionIndicatorState
    {
        public GridPoint Position { get; }
        public bool IsAffordable { get; }

        public ExpansionIndicatorState(GridPoint position, bool isAffordable)
        {
            Position = position;
            IsAffordable = isAffordable;
        }
    }
}
