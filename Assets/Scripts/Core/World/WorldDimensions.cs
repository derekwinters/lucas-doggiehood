namespace Doggiehood.Core.World
{
    /// <summary>
    /// Locked standard world dimensions (#105), in meters. This is the
    /// single source of truth for these 7 measurements — every other
    /// standard/road/sidewalk/crosswalk-shaped constant elsewhere in Core
    /// should reference these rather than re-declaring the literal.
    ///
    /// These values are the geometric basis for the deferred tile catalog
    /// and multi-tile grid/placement system (#109) and future road/sidewalk
    /// rendering (#106) — see docs/specs/world/tile-catalog.md. Nothing in
    /// Core consumes GrassVergeWidth, SidewalkWidth, CrosswalkWidth,
    /// CulDeSacBulbRadius, or OpposingTurnArchRadius yet; only RoadWidth is
    /// wired up today, via NeighborhoodLayout.StreetWidth.
    /// </summary>
    public static class WorldDimensions
    {
        /// <summary>Square tile footprint: 60m x 60m.</summary>
        public const float TileSize = 60f;

        /// <summary>
        /// Road width. Same concept as the existing
        /// <see cref="NeighborhoodLayout.StreetWidth"/>.
        /// </summary>
        public const float RoadWidth = 6f;

        /// <summary>Grass verge between the road edge and the sidewalk.</summary>
        public const float GrassVergeWidth = 1.5f;

        /// <summary>Sidewalk width.</summary>
        public const float SidewalkWidth = 2f;

        /// <summary>Crosswalk width.</summary>
        public const float CrosswalkWidth = 3f;

        /// <summary>Cul-de-sac bulb radius.</summary>
        public const float CulDeSacBulbRadius = 9f;

        /// <summary>
        /// Opposing-turn arch radius (a quarter-circle radius for the two
        /// arches in an <c>OpposingTurnsNS</c>/<c>OpposingTurnsEW</c> tile).
        /// </summary>
        public const float OpposingTurnArchRadius = 15f;
    }
}
