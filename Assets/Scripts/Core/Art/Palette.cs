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

        /// <summary>
        /// Graybox-fallback yard landscaping color (#170) — used only when
        /// neither the tree-large nor tree-small kit model can load
        /// (WorldBuilder.BuildYardLandscaping falls back to a simple
        /// primitive per pick, same pattern as HouseFallbackHex). A darker,
        /// more saturated green than the base GrassHex ground so a
        /// fallback tree still reads as a distinct object standing on the
        /// lawn rather than blending into it.
        /// </summary>
        public const string YardLandscapingFallbackHex = "#2F8F3F";

        /// <summary>
        /// Map-expansion lock indicator tint when the wallet covers the
        /// next zone's unlock cost (#178, docs/specs/expansion.md
        /// "Expansion indicator" — Derek, 2026-07-18: "Gold icon if
        /// affordable.").
        /// </summary>
        public const string ExpansionIndicatorAffordableHex = "#FFD700";

        /// <summary>
        /// Map-expansion lock indicator tint when the wallet does not yet
        /// cover the next zone's unlock cost (#178 — Derek, 2026-07-18:
        /// "Grey or black lock icon if can't purchase.").
        /// </summary>
        public const string ExpansionIndicatorLockedHex = "#4A4A4A";

        /// <summary>
        /// Lost-item "finder glow" colour (#521, Derek 2026-08-02: "Option 1 —
        /// a world-space glow on the item itself, colored RED"). A bright,
        /// saturated red so the soft pulsing halo / ground ring / sparkle
        /// (<see cref="Doggiehood.Core.Quests.LostItemGlow"/>) reads on the
        /// sidewalk (<see cref="SidewalkHex"/>), grass (<see cref="GrassHex"/>)
        /// and road (<see cref="StreetHex"/>) alike — it's the glow the eye
        /// catches, regardless of the item's own colour. The Unity
        /// <c>LostItemView</c> applies it via CoreColors.FromHex.
        /// </summary>
        public const string LostItemGlowHex = "#FF2A2A";

        /// <summary>
        /// The #299 zone-house tint palette — a CURATED, explicit 20-entry
        /// ordered table (Derek &amp; Lucas, 2026-08-02, #519), replacing the
        /// earlier generated even-18-deg-hue / fixed S=0.70,V=0.90 rule that
        /// produced an unpleasant electric violet/blue. 10 slots are kept from
        /// the old generated values; the 10 flagged ones are softened — mostly
        /// desaturated, with the cool blues/violets nudged lighter. These are
        /// the palette definition (named palette data, born-and-approved here),
        /// the single source of truth for what colour each tint index paints.
        /// Ordering is index-stable: entry <c>i</c> is the colour for tint
        /// index <c>i</c>, and the count stays 20, so a house persists its tint
        /// INDEX (SaveCodec) with no save migration.
        /// </summary>
        private static readonly string[] HouseTints =
        {
            "#E64545", "#E67545", "#E6A545", "#E6D545", "#C5E645",
            "#95E645", "#88D15E", "#6ACC9E", "#45E685", "#45E6B5",
            "#45E6E6", "#45B5E6", "#6AB5EB", "#809DED", "#9E8EED",
            "#C18AE6", "#D87EE6", "#E879D9", "#ED72AF", "#ED6B85",
        };

        /// <summary>
        /// The #299 zone-house tint at <paramref name="index"/> (0-based, in
        /// 0..<see cref="HouseVariantAssignment.TintCount"/>-1), looked up from
        /// the curated <see cref="HouseTints"/> table (Derek &amp; Lucas #519).
        /// A house persists its tint INDEX (SaveCodec), not the colour, so the
        /// curated retune re-colours existing houses onto the approved palette
        /// with no save migration. Applied as a material color-multiply over
        /// the mesh (the ApplyVacancyTint technique), not a kit-texture-variant
        /// swap.
        /// </summary>
        public static string HouseTintHex(int index)
        {
            if (index < 0 || index >= HouseVariantAssignment.TintCount)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(index), index, $"House tint index must be 0..{HouseVariantAssignment.TintCount - 1}.");
            }

            return HouseTints[index];
        }

        /// <summary>
        /// The #601 delivery-truck car-color spread (Derek, 2026-08-05): a
        /// small CURATED table of real-world standard car colors — white,
        /// black, silver, gray, red, dark blue, dark green — deliberately NOT
        /// the broad decorative 20-tint <see cref="HouseTints"/> table houses
        /// use. A truck picks one at spawn via <see cref="CarColorAssignment"/>
        /// and it is applied as a material color-multiply over the kit model
        /// (the same <c>WorldBuilder.ApplyPaletteTint</c> technique the houses
        /// use), so the values are chosen to read as their named colours over
        /// the model's light base. Ordering is index-stable: entry <c>i</c> is
        /// the colour for car-color index <c>i</c>.
        /// </summary>
        private static readonly string[] CarColors =
        {
            "#EDEDED", // white
            "#2B2B2B", // black
            "#C4C8CC", // silver
            "#83878C", // gray
            "#B32424", // red
            "#23366B", // dark blue
            "#235939", // dark green
        };

        /// <summary>
        /// The #601 standard car color at <paramref name="index"/> (0-based, in
        /// 0..<see cref="CarColorAssignment.CarColorCount"/>-1), looked up from
        /// the curated <see cref="CarColors"/> table. Trucks are transient, so
        /// this is applied per-spawn (no persisted index the way houses store
        /// their tint), as a material color-multiply over the truck model.
        /// </summary>
        public static string CarColorHex(int index)
        {
            if (index < 0 || index >= CarColorAssignment.CarColorCount)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(index), index, $"Car color index must be 0..{CarColorAssignment.CarColorCount - 1}.");
            }

            return CarColors[index];
        }
    }
}
