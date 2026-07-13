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
        /// Setback between the road edge and the sidewalk. 0.75m since
        /// Derek's midpoint request (2026-07-13, in conversation, Editor
        /// review): at 0m (his earlier same-day decision, superseding the
        /// original #106 1.5m verge) the dogs' walk line sat at 4m —
        /// "a little too close to the road"; at 1.5m it sat at 5.5m. The
        /// midpoint puts the sidewalk centerline at
        /// RoadWidth/2 + 0.75 + SidewalkWidth/2 = 4.75m — still within the
        /// City Kit road tiles' modeled raised curb+sidewalk band (3-5m
        /// from the centerline at tile scale 10), near its outer edge
        /// (#121/#122). Note this is now a LOGICAL setback for dog
        /// placement: the kit tiles cover 3-5m with pavement, so no visual
        /// grass strip appears in the kit path — Derek's "no grass verge"
        /// decision was about visuals and stands. Only WorldBuilder's
        /// primitive fallback renders it as an actual grass strip.
        /// </summary>
        public const float GrassVergeWidth = 0.75f;

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
