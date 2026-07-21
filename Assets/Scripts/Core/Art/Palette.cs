namespace Doggiehood.Core.Art
{
    /// <summary>
    /// Shared world palette (#64, docs/specs/world/art-style.md): bright,
    /// saturated, playful. Real per-house color now lives in the Kenney
    /// City Kit Suburban kit's own textures, applied per house via
    /// HouseStyleTable.TintVariant — these are the environment colors.
    /// Streets stay neutral so the houses pop.
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

        /// <summary>
        /// Graybox-fallback house wall color (#64) — used only when a
        /// house's kit model itself fails to load (WorldBuilder.BuildHouse
        /// falls back to a single plain box). Real per-house color comes
        /// from the kit's own textures via HouseStyleTable.TintVariant, so
        /// this single flat color replaces what used to be 4 distinct
        /// per-house WallColorHex values on HouseStyle.
        /// </summary>
        public const string HouseFallbackHex = "#D9A066";

        /// <summary>
        /// Flat desaturated tint for a vacant house's mesh (#58): while a
        /// house has no dog living in it yet (House.IsVacant), WorldBuilder
        /// paints its model with this grey instead of its normal
        /// HouseStyleTable coloring — a material color multiply over the
        /// existing mesh, no new art asset (docs/specs/expansion.md
        /// superseded the earlier "for sale sign" plan with this). Rendering
        /// is a pure function of House.IsVacant at build time (no live
        /// re-tint of an already-built house) — the next time the world
        /// (re)builds after #54's move-in system occupies a house, it
        /// renders that house's normal tint again.
        /// </summary>
        public const string VacantHouseTintHex = "#9A9A9A";

        /// <summary>
        /// Graybox marker color for an empty, buildable lot in an unlocked
        /// zone (#57) — a distinct flat pad color so the "build here" tap
        /// target reads clearly against the grass/street palette. Purely a
        /// graybox stand-in; no dedicated art is planned until a real
        /// lot-selection affordance is designed.
        /// </summary>
        public const string EmptyLotMarkerHex = "#F2A65A";
    }
}
