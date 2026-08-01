namespace Doggiehood.Core.Art
{
    /// <summary>
    /// The single source of truth for a house's CURRENT kit-model resource
    /// name (#464): given a house's id, level, and (for zone houses) its rolled
    /// <see cref="HouseVariant"/>, returns the same model-resource string
    /// WorldBuilder loads to render the house in the world — or null when no
    /// kit mesh resolves (the graybox fallback). Both the world builder and the
    /// house-profile render-to-texture snapshot resolve through here, so a
    /// profile always shows the house's real current model (its variant + its
    /// upgrade level) without a second, drift-prone copy of the branch.
    ///
    /// Mirrors WorldBuilder.BuildHouse's resolution exactly:
    /// <list type="bullet">
    /// <item>Starter ids (1-4): the fixed per-house
    /// <see cref="HouseLevelModelTable"/> ladder mesh at the house's level.</item>
    /// <item>Zone ids (&gt;= <see cref="HouseVariantAssignment.FirstZoneHouseId"/>):
    /// the rolled variant's ladder mesh at the house's level — or null when the
    /// house carries no variant.</item>
    /// </list>
    /// Engine-free so it lives beside the tables it composes; the Unity layer
    /// turns the returned name into a loaded, tinted model.
    /// </summary>
    public static class HouseModelResolver
    {
        /// <summary>
        /// The kit-model resource name for the house identified by
        /// <paramref name="houseId"/> at <paramref name="level"/>, using
        /// <paramref name="variant"/> for zone houses, or null when no mesh
        /// resolves (graybox fallback).
        /// </summary>
        public static string ResolveModelName(int houseId, int level, HouseVariant? variant)
        {
            if (HouseVariantAssignment.IsZoneHouse(houseId))
            {
                return variant.HasValue
                    ? HouseLevelModelTable.ForHouseLevel(variant.Value.LadderId, level)
                    : null;
            }

            return HouseLevelModelTable.HasHouse(houseId)
                ? HouseLevelModelTable.ForHouseLevel(houseId, level)
                : null;
        }
    }
}
