namespace Doggiehood.Core.Art
{
    /// <summary>
    /// The rolled visual identity of a zone-built house (#299): which of the
    /// five <see cref="HouseLevelModelTable"/> ladders it renders through
    /// (<see cref="LadderId"/>, 1-5) and which of the 20 generated palette
    /// tints multiplies over its mesh (<see cref="TintIndex"/>, 0-19). Both
    /// are assigned once at build time by <see cref="HouseVariantAssignment"/>
    /// and persist across the house's L1->L4 upgrades — leveling swaps the
    /// mesh within the same ladder but never re-rolls the variant. Starting
    /// houses (ids 1-4) have no variant (they keep their fixed
    /// <see cref="HouseStyleTable"/> ladder/tint).
    /// </summary>
    public readonly struct HouseVariant
    {
        public int LadderId { get; }
        public int TintIndex { get; }

        public HouseVariant(int ladderId, int tintIndex)
        {
            LadderId = ladderId;
            TintIndex = tintIndex;
        }
    }
}
