namespace Doggiehood.Core.Art
{
    /// <summary>
    /// Which of the kit's built-in texture variants paints a house model
    /// (#64). "Colormap" is the kit's base/default texture; the
    /// variation-a/b/c textures are the kit's alternate paint jobs for the
    /// same meshes — swapping the material's main texture is how the
    /// palette is actually applied to the real rendered model.
    /// </summary>
    public enum HouseTintVariant
    {
        Colormap,
        VariationA,
        VariationB,
        VariationC,
    }

    /// <summary>
    /// A cottage variant (#64): which City Kit Suburban model represents
    /// this starting house, and which of the kit's built-in texture
    /// variants tints it. Roof/porch geometry and hex colors used to live
    /// here (pre-#64 closure); the game now renders the kit's real house
    /// models directly, so styling is "which model + which kit texture",
    /// not procedural geometry or Core-owned color data.
    /// </summary>
    public sealed class HouseStyle
    {
        public int StyleId { get; }
        public string ModelName { get; }
        public HouseTintVariant TintVariant { get; }

        public HouseStyle(int styleId, string modelName, HouseTintVariant tintVariant)
        {
            StyleId = styleId;
            ModelName = modelName;
            TintVariant = tintVariant;
        }
    }
}
