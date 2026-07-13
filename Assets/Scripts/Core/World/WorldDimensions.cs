namespace Doggiehood.Core.World
{
    /// <summary>
    /// Locked standard world dimensions (#105), in meters. This is the
    /// single source of truth for these 7 measurements — every other
    /// standard/road/sidewalk/crosswalk-shaped constant elsewhere in Core
    /// should reference these rather than re-declaring the literal.
    ///
    /// These values are the geometric basis for the deferred tile catalog
    /// and multi-tile grid/placement system (#109) — see
    /// docs/specs/world/tile-catalog.md — and for today's Road/Sidewalk
    /// geometry and walk network (#106) — see
    /// docs/specs/world/sidewalks.md. RoadWidth, GrassVergeWidth, and
    /// SidewalkWidth are wired up via Road/Sidewalk/WalkNetwork;
    /// CrosswalkWidth is wired up via WalkNetwork's Crosswalk edges.
    /// CulDeSacBulbRadius and OpposingTurnArchRadius remain unconsumed —
    /// they're only meaningful once the multi-tile grid (#109) exists.
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

        /// <summary>
        /// Grass verge between the road edge and the sidewalk. 0m since
        /// Derek's decision (2026-07-13, in conversation, superseding the
        /// original #106 1.5m verge): the sidewalk abuts the road directly,
        /// with grass only outside the sidewalk. This puts the sidewalk
        /// centerline at RoadWidth/2 + SidewalkWidth/2 = 4m, exactly
        /// centered on the City Kit road tiles' modeled raised
        /// curb+sidewalk band (3-5m from the centerline at tile scale 10)
        /// — so dogs walk on the kit art's pavement (#121/#122).
        /// </summary>
        public const float GrassVergeWidth = 0f;

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
