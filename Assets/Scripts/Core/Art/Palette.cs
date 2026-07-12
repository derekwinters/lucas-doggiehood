namespace Doggiehood.Core.Art
{
    /// <summary>
    /// Shared world palette (#64, docs/specs/world/art-style.md): bright,
    /// saturated, playful. House colors live on HouseStyleTable; these are
    /// the environment colors. Streets stay neutral so the houses pop.
    /// </summary>
    public static class Palette
    {
        public const string GrassHex = "#7ED957";
        public const string StreetHex = "#8A8FA3";
        public const string SidewalkHex = "#EFE8D8";

        /// <summary>Grass verge between road edge and sidewalk (#106) — a
        /// distinct shade from the base GrassHex ground so it reads as its
        /// own declared surface rather than disappearing into the lawn.</summary>
        public const string GrassVergeHex = "#5FBF3F";

        /// <summary>Crosswalk surface (#106) — bright and distinct from
        /// the neutral sidewalk/street so crossings read clearly, without
        /// literal zebra-stripe geometry (deferred polish).</summary>
        public const string CrosswalkHex = "#FFE066";
    }
}
