using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Art;
using Doggiehood.Core.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Builds the starting neighborhood from Core data (#7, #38, #39, #64,
    /// #106): ground, roads, crosswalks, four houses, and the fixed daytime
    /// sun. Road surfaces and houses render as Kenney City Kit Roads /
    /// City Kit Suburban models when importable (#121, #122, toward #6),
    /// falling back to the original graybox primitives otherwise — the same
    /// pattern DogView uses for the Cube Pets model (#119). All positions,
    /// counts, styles, and lighting values come from Core either way, so
    /// the art swap changes no logic: WalkNetwork, Sidewalk geometry, and
    /// dog spawn positions are untouched.
    /// </summary>
    public static class WorldBuilder
    {
        public const string RootName = "Neighborhood";
        public const string HouseNamePrefix = "House ";
        public const string RoadNamePrefix = "Road - ";
        public const string VergeNamePrefix = "Verge - ";
        public const string SidewalkNamePrefix = "Sidewalk - ";
        public const string CrosswalkNamePrefix = "Crosswalk - ";
        public const string RoadTileNamePrefix = "RoadTile - ";
        public const string IntersectionTileName = RoadTileNamePrefix + "Intersection";
        public const string WalkwayNamePrefix = "Walkway - ";
        public const string FenceNamePrefix = "Fence - ";
        public const string EmptyLotNamePrefix = "EmptyLot - ";
        public const string ExpansionIndicatorName = "ExpansionIndicator";
        public const string SunName = "Sun";

        /// <summary>Container name prefix for the road surfaces of an unlocked
        /// zone tile (#373): one per placed zone tile, holding the graybox/kit
        /// road segments derived from the tile catalog geometry
        /// (<see cref="TileRoadGeometry"/>). Suffixed with the tile's grid
        /// coordinate so each tile's roads stay a distinct scene object.</summary>
        public const string ZoneRoadNamePrefix = "ZoneRoad - ";

        public const string GroundName = "Ground";

        /// <summary>Half-extent (meters) of the fixed starting grass pad this
        /// once was — kept because <see cref="DeliveryTruckView"/> still enters
        /// the scene from this distance. The ground plane itself is no longer
        /// this fixed size: since #373 it grows to cover the live map extent
        /// (<see cref="MapExtent"/>) so an unlocked zone has grass under it.</summary>
        public const float GroundExtent = 30f;

        /// <summary>Edge length (meters) of Unity's primitive Plane mesh at
        /// scale 1 — the divisor that turns a target ground span into the
        /// plane's local scale (#373).</summary>
        private const float GroundPlaneMeshSize = 10f;

        /// <summary>Vertical thickness (meters) of a graybox zone road surface
        /// slab (#373), matching the primitive starting-road fallback so an
        /// unlocked tile's road reads at the same height.</summary>
        private const float ZoneRoadThickness = 0.1f;

        /// <summary>Graybox marker footprint (local X/Z) for an empty,
        /// buildable lot (#57) — sized just to read as a "house goes here"
        /// slab within the lot's own space. Public since #300 so the
        /// EditMode slab test can pin that the raised slab keeps THIS fixed
        /// footprint rather than being sized to
        /// <c>HousePlacement.HouseFootprint</c> — that per-house footprint
        /// throws for zone lots (id >= 5, no assigned style), which is
        /// exactly the only kind of lot BuildEmptyLot ever runs on (Derek's
        /// #300 option-3 decision).</summary>
        public const float EmptyLotMarkerFootprint = 3f;

        /// <summary>Thickness of the empty-lot "foundation" slab (#300 (B),
        /// Derek): the marker is a low RAISED graybox slab that reads as "a
        /// house goes here", not the old thin flat tap-pad (0.2m). Low
        /// enough to still read as a foundation footing rather than a solid
        /// block. Public so the EditMode slab test can assert the reshaped
        /// height against a named constant.</summary>
        public const float EmptyLotFoundationSlabHeight = 0.6f;

        /// <summary>Resources key for the #183 lock icon, staged at
        /// Assets/Art/UI/ExpansionIndicator/Resources/locked.png (bare
        /// filename, same convention as every other Resources-loaded art
        /// key here).</summary>
        private const string LockIconResource = "locked";

        /// <summary>World footprint (meters) of the expansion indicator
        /// marker (#178): the staged icon is 100x100px at
        /// TintedIcon.SpritePixelsPerUnit (100 px/unit), so a default,
        /// unscaled sprite is 1m wide — this scales it up to a size that
        /// reads clearly next to the EmptyLotMarkerFootprint-sized lot
        /// markers.</summary>
        private const float ExpansionIndicatorWorldSize = 4f;

        /// <summary>Ground footprint (local X/Z) of the graybox fallback
        /// house's single "Walls" box (#64) — only ever built when the
        /// kit model itself fails to load.</summary>
        private const float HouseFallbackWallsFootprint = 4f;

        /// <summary>Height of the graybox fallback house's "Walls" box.</summary>
        private const float HouseFallbackWallsHeight = 2.5f;

        /// <summary>Resources key for the front-walkway paver piece (#128)
        /// — the clean square-paver look from the same City Kit Suburban
        /// kit as the houses, staged alongside them.</summary>
        public const string WalkwayPieceResource = "path-short";

        /// <summary>Resources key for the lot-fence piece (#129) — the
        /// straight City Kit Suburban fence segment, staged alongside the
        /// houses. (The kit's fence-low.fbx is an L-shaped low-wall corner
        /// piece, not a straight run — plain fence segments tile every run
        /// and meet at the corners on their own.)</summary>
        public const string FencePieceResource = "fence";

        /// <summary>Resources key for the large yard tree kit piece (#170),
        /// staged alongside the houses.</summary>
        public const string TreeLargeResource = "tree-large";

        /// <summary>Resources key for the small yard tree kit piece (#170).</summary>
        public const string TreeSmallResource = "tree-small";

        /// <summary>Container name prefix for a lot's procedural yard
        /// landscaping (#170) — one per lot, holding its selected front and
        /// back yard trees.</summary>
        public const string YardLandscapingNamePrefix = "Yard - ";

        /// <summary>Graybox-fallback yard prop height (#170) — only ever
        /// built when neither the tree-large nor tree-small kit piece can
        /// load. Sized off Core's own collision radius (four
        /// radii tall) so it reads as a small rounded tree/bush rather
        /// than a flat disc, without inventing an unrelated tuning
        /// number.</summary>
        private const float YardLandscapingFallbackHeight = YardLandscaping.TreeFootprintRadius * 4f;

        /// <summary>
        /// Seam (#146) that builds every lot's backyard fence even though
        /// HouseLot.HasFence defaults false (fences are hidden until a future
        /// quest purchases them, #147). Since #219 this is driven at runtime
        /// by the Settings ▸ Debug tab's "Show backyard fences" toggle: the
        /// toggle sets this flag and calls <see cref="RebuildFences"/> so the
        /// enclosures show/hide on a live build — used to check #152 on-device
        /// without the Unity Editor. Not part of normal gameplay state.
        /// </summary>
        public static bool ForceFencesVisible { get; set; }

        /// <summary>
        /// Uniform scale for the 1x1-unit City Kit Roads tiles: at x10 a
        /// tile covers 10x10 m and its 0.6-unit road band becomes 6 m —
        /// exactly WorldDimensions.RoadWidth. With GrassVergeWidth at
        /// 0.75m (Derek's 2026-07-13 midpoint request) Core's logical
        /// sidewalk band (3.75-5.75 m from the centerline) overlaps the
        /// tile's modeled raised curb+sidewalk band (3-5 m after scaling),
        /// so dogs walk at 4.75 m — on the kit's pavement, near its outer
        /// edge (#121).
        /// </summary>
        public const float RoadTileScale = 10f;

        /// <summary>Resources keys for the City Kit Roads tiles (#121),
        /// staged under Assets/Art/Roads/CityKitRoads/Resources/ — load
        /// keys are relative to the Resources folder, so they are the bare
        /// file names (see 505278e).</summary>
        private const string RoadStraightResource = "road-straight";
        // road-crossroad-path is the crosswalk-striped 4-way variant —
        // Derek's 2026-07-13 Editor review asked for painted crosswalks at
        // the intersection. Same 1x1 ground-pivot tile as the plain
        // road-crossroad it replaced, with zebra-stripe geometry across all
        // four arms at ~3-5m from center (tile scale 10) — right on the
        // WalkNetwork's crosswalk edges.
        private const string RoadCrossroadResource = "road-crossroad-path";
        private const string RoadCrossingResource = "road-crossing";

        /// <summary>The ONE fixed uniform scale applied to every City Kit
        /// house model (#145, replacing the 8m max-footprint normalization
        /// that gave each model a different scale factor). Public since
        /// #126: the editor-only catalog gallery must scale models by the
        /// exact number the game uses so it can never drift. The canonical
        /// value lives in Core (the walk network's front walkways need
        /// each door's world position engine-free); this is the Unity-side
        /// alias existing callers and tests use.</summary>
        public const float HouseKitScale = HousePlacement.KitScale;

        /// <summary>
        /// Yaw correction applied after pointing a house model at its
        /// street-front facing. 180: Derek's Editor screenshot showed the
        /// doors pointing opposite the look direction at 0, so the City
        /// Kit Suburban models face local -Z. Kept a single public
        /// constant (read by WorldKitArtTests) so one flip fixes all four
        /// houses if it's ever still wrong — canonical in Core since #128
        /// (HousePlacement.ModelYawOffsetDegrees, needed for the door
        /// math); this is the Unity-side alias.
        /// </summary>
        public const float HouseModelYawOffsetDegrees = HousePlacement.ModelYawOffsetDegrees;

        /// <summary>
        /// EditMode test seam: forces the graybox primitive path even when
        /// the Kenney kit assets are importable, by routing through the
        /// same branch a null Resources.Load takes. A project that has the
        /// assets staged can't otherwise exercise the fallback. Never set
        /// in production code.
        /// </summary>
        public static bool ForcePrimitiveFallback { get; set; }

        /// <summary>
        /// Resources load key for a house's kit model. The houseId ->
        /// model assignment and each model's authored footprint/door data
        /// moved into Core as HouseModelCatalog (#125) — this stays as the
        /// Unity-side accessor existing callers and EditMode tests use.
        /// </summary>
        public static string HouseModelResourcePath(int houseId)
        {
            return HouseModelCatalog.ForHouse(houseId).ModelName;
        }

        /// <summary>
        /// Resources load key for a house's kit model at a given level (#59):
        /// the level-resolved mesh from
        /// <see cref="HouseLevelModelTable"/> (level 1 is the anchored
        /// as-built mesh, matching the single-arg overload; upgrading swaps
        /// in the next rung). Used by BuildHouse to render the mesh for a
        /// house's current level.
        /// </summary>
        public static string HouseModelResourcePath(int houseId, int level)
        {
            return HouseLevelModelTable.ForHouseLevel(houseId, level);
        }

        public static GameObject Build(GameState state)
        {
            var root = new GameObject(RootName);

            BuildGround(root.transform, state.Map);

            if (TryLoadRoadTiles(out var straight, out var crossroad, out var crossing))
            {
                BuildKitRoads(root.transform, straight, crossroad, crossing);
            }
            else
            {
                foreach (var road in NeighborhoodLayout.Roads)
                {
                    BuildRoad(root.transform, road);
                }

                BuildCrosswalks(root.transform);
            }

            foreach (var house in state.Houses)
            {
                BuildHouse(root.transform, house, state.GetHouseLot(house.Id));
            }

            BuildWalkways(root.transform);
            BuildFences(root.transform, state);
            BuildYardLandscaping(root.transform);
            BuildEmptyLots(root.transform, state);
            BuildUnlockedZoneRoads(root.transform, state);
            BuildExpansionIndicator(root.transform, state);

            BuildSun(root.transform);
            ApplyAmbientLighting();

            return root;
        }

        /// <summary>All-or-nothing load of the three City Kit Roads tiles —
        /// a partial kit would render a broken corridor, so any missing
        /// tile falls back to the full primitive road path.</summary>
        private static bool TryLoadRoadTiles(out GameObject straight, out GameObject crossroad, out GameObject crossing)
        {
            straight = null;
            crossroad = null;
            crossing = null;
            if (ForcePrimitiveFallback)
            {
                return false;
            }

            straight = Resources.Load<GameObject>(RoadStraightResource);
            crossroad = Resources.Load<GameObject>(RoadCrossroadResource);
            crossing = Resources.Load<GameObject>(RoadCrossingResource);
            return straight != null && crossroad != null && crossing != null;
        }

        /// <summary>
        /// The visual road corridor as City Kit Roads tiles (#121, #392):
        /// one crossroad tile on the intersection, then a whole number of
        /// straight tiles compressed evenly to span each street arm exactly
        /// from the intersection tile's edge to the tile edge (so adjacent
        /// tiles' roads connect on expansion, #392) — except where the
        /// WalkNetwork defines a Crosswalk edge inside a tile's span, which
        /// gets the road-crossing tile instead (replacing the primitive
        /// crosswalk quads). Tiles have ground-level pivots (y = 0) and
        /// their road runs along local X, so north-south streets rotate 90°.
        /// The kit tiles model their own sidewalks and curbs, so none of
        /// the primitive verge/sidewalk/crosswalk strips are built in this
        /// path. Each street keeps a "Road - Orientation" container object
        /// — the logical scene contract other systems and tests rely on.
        /// </summary>
        private static void BuildKitRoads(Transform parent, GameObject straight, GameObject crossroad,
            GameObject crossing)
        {
            var intersection = Object.Instantiate(crossroad, parent);
            intersection.name = IntersectionTileName;
            intersection.transform.position = new Vector3(
                NeighborhoodLayout.Intersection.X, 0f, NeighborhoodLayout.Intersection.Z);
            intersection.transform.rotation = Quaternion.identity;
            intersection.transform.localScale = Vector3.one * RoadTileScale;

            foreach (var road in NeighborhoodLayout.Roads)
            {
                var isNorthSouth = road.Orientation == StreetOrientation.NorthSouth;
                var roadParent = new GameObject(RoadNamePrefix + road.Orientation);
                roadParent.transform.SetParent(parent);
                roadParent.transform.position = Vector3.zero;

                // The tile's road runs along local X; a north-south street
                // runs along world Z.
                var rotation = isNorthSouth ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
                var crosswalkAlongs = CrosswalkAlongPositions(road);
                var nearEdge = RoadTileScale / 2f; // the crossroad tile's own edge

                // #392: compress a whole number of tiles to span exactly
                // from the intersection tile's edge (nearEdge = 5m) out to
                // the tile edge (road.HalfLength = half a tile = 30m), so
                // adjacent tiles' roads connect edge-to-edge on expansion
                // with no green gap and no overshoot. Same compress-to-fit
                // technique as WalkwayTiling.PiecesAlong: only the along-road
                // axis (local X) is scaled to the piece length; the
                // perpendicular road-band width and the height keep the
                // uniform RoadTileScale so the road texture doesn't distort.
                var armSpan = road.HalfLength - nearEdge;
                var armTileCount = Mathf.Max(1, Mathf.CeilToInt(armSpan / RoadTileScale - 0.0001f));
                var pieceLength = armSpan / armTileCount;

                foreach (var sign in new[] { 1f, -1f })
                {
                    for (var i = 0; i < armTileCount; i++)
                    {
                        var along = sign * (nearEdge + (i + 0.5f) * pieceLength);
                        var isCrossing = crosswalkAlongs.Any(a => Mathf.Abs(a - along) <= pieceLength / 2f);
                        var tile = Object.Instantiate(isCrossing ? crossing : straight, roadParent.transform);
                        tile.name = RoadTileNamePrefix + road.Orientation
                            + (isCrossing ? " Crossing " : " Straight ") + (sign * (i + 1));
                        var point = road.PointAt(along, 0f);
                        tile.transform.position = new Vector3(point.X, 0f, point.Z);
                        tile.transform.rotation = rotation;
                        tile.transform.localScale = new Vector3(pieceLength, RoadTileScale, RoadTileScale);
                    }
                }
            }
        }

        /// <summary>Signed along-axis positions (relative to the road's
        /// center) of the WalkNetwork Crosswalk edges that cross this road
        /// — an edge crossing a north-south road spans X at constant Z,
        /// and vice versa.</summary>
        private static List<float> CrosswalkAlongPositions(Road road)
        {
            var isNorthSouth = road.Orientation == StreetOrientation.NorthSouth;
            return NeighborhoodLayout.WalkNetwork.Edges
                .Where(e => e.Kind == WalkEdgeKind.Crosswalk)
                .Where(e => isNorthSouth
                    ? Mathf.Abs(e.A.Z - e.B.Z) < 0.01f
                    : Mathf.Abs(e.A.X - e.B.X) < 0.01f)
                .Select(e => isNorthSouth ? e.A.Z - road.Center.Z : e.A.X - road.Center.X)
                .ToList();
        }

        /// <summary>The base grass plane (#373): a single flat
        /// <see cref="Palette.GrassHex"/> primitive Plane (decision #300 (A))
        /// sized and centred to cover the whole live map extent
        /// (<see cref="MapExtent"/>) rather than a fixed pad — so every placed
        /// tile, including an unlocked zone's, has grass under it.</summary>
        private static void BuildGround(Transform parent, TileMap map)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = GroundName;
            ground.transform.SetParent(parent);
            ApplyGroundExtent(ground.transform, map);
            Paint(ground, Palette.GrassHex);
        }

        /// <summary>
        /// Regrows the already-built base grass plane to cover the current map
        /// extent (#373) — called after a zone unlock adds tiles, so the new
        /// zone sits on grass rather than floating over void. A defensive no-op
        /// if no <see cref="GroundName"/> plane is present.
        /// </summary>
        public static void ResizeGroundToMap(Transform root, TileMap map)
        {
            var ground = root.Find(GroundName);
            if (ground == null)
            {
                return;
            }

            ApplyGroundExtent(ground, map);
        }

        private static void ApplyGroundExtent(Transform ground, TileMap map)
        {
            var extent = MapExtent.Covering(map);
            // A default Unity Plane is GroundPlaneMeshSize x GroundPlaneMeshSize
            // meters at scale 1, so the span-to-scale divisor is that mesh size.
            ground.localScale = new Vector3(
                extent.Width / GroundPlaneMeshSize, 1f, extent.Depth / GroundPlaneMeshSize);
            ground.position = new Vector3(extent.CenterX, WorldDimensions.RoadSurfaceHeight, extent.CenterZ);
        }

        /// <summary>Road surface plus a sidewalk on both sides (#106), all
        /// sized from Road/Sidewalk — which are in turn built purely from
        /// the locked #105 WorldDimensions constants. Verge strips are only
        /// built when GrassVergeWidth is non-zero (a 0-width cube would be
        /// degenerate geometry) — at today's 0.75m (Derek's 2026-07-13
        /// midpoint request) the grass strip legitimately renders in this
        /// fallback path, even though the kit path shows no grass there
        /// (the kit tiles pave 3-5m; the verge is a logical setback for
        /// dog placement in that path).</summary>
        private static void BuildRoad(Transform parent, Road road)
        {
            var isNorthSouth = road.Orientation == StreetOrientation.NorthSouth;
            var length = road.HalfLength * 2f;

            var surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = RoadNamePrefix + road.Orientation;
            surface.transform.SetParent(parent);
            surface.transform.localScale = isNorthSouth
                ? new Vector3(road.Width, 0.1f, length)
                : new Vector3(length, 0.1f, road.Width);
            surface.transform.position = new Vector3(road.Center.X, 0.05f, road.Center.Z);
            Paint(surface, Palette.StreetHex);

            foreach (var sidewalk in road.Sidewalks)
            {
                if (sidewalk.VergeWidth > 0.001f)
                {
                    var vergeOffset = Mathf.Sign(sidewalk.CenterOffset) * (road.Width / 2f + sidewalk.VergeWidth / 2f);
                    BuildStripArms(parent, road, vergeOffset, sidewalk.VergeWidth, isNorthSouth,
                        VergeNamePrefix + road.Orientation + " " + sidewalk.Side, Palette.GrassVergeHex, 0.06f);
                }

                BuildStripArms(parent, road, sidewalk.CenterOffset, sidewalk.Width, isNorthSouth,
                    SidewalkNamePrefix + road.Orientation + " " + sidewalk.Side, Palette.SidewalkHex, 0.07f);
            }
        }

        /// <summary>
        /// A verge/sidewalk strip on one side of a road, split into its two
        /// arm segments so it stops at the crossing road's own half-width
        /// from the intersection center instead of running through it as
        /// one continuous piece. Without this, the strip painted over the
        /// crossing road's own pavement (visible in-game as a stray grass
        /// ring around the crosswalk box). NeighborhoodLayout only ever
        /// has today's one origin-centered crossing (#109's multi-tile
        /// grid stays deferred), so the gap is computed directly from
        /// WorldDimensions.RoadWidth rather than via general multi-crossing
        /// detection (that generality already lives in WalkNetwork).
        /// </summary>
        private static void BuildStripArms(Transform parent, Road road, float perpendicularOffset, float stripWidth,
            bool isNorthSouth, string namePrefix, string colorHex, float height)
        {
            var gapHalfWidth = WorldDimensions.RoadWidth / 2f;
            var armLength = road.HalfLength - gapHalfWidth;
            if (armLength <= 0f)
            {
                return;
            }

            BuildStripArm(parent, road, perpendicularOffset, stripWidth, isNorthSouth,
                namePrefix, colorHex, height, -road.HalfLength, -gapHalfWidth, armLength, positiveAlong: false);
            BuildStripArm(parent, road, perpendicularOffset, stripWidth, isNorthSouth,
                namePrefix, colorHex, height, gapHalfWidth, road.HalfLength, armLength, positiveAlong: true);
        }

        private static void BuildStripArm(Transform parent, Road road, float perpendicularOffset, float stripWidth,
            bool isNorthSouth, string namePrefix, string colorHex, float height, float from, float to,
            float armLength, bool positiveAlong)
        {
            var armLabel = isNorthSouth
                ? (positiveAlong ? "North" : "South")
                : (positiveAlong ? "East" : "West");
            var center = road.PointAt((from + to) / 2f, perpendicularOffset);

            var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = namePrefix + " " + armLabel;
            arm.transform.SetParent(parent);
            arm.transform.localScale = isNorthSouth
                ? new Vector3(stripWidth, 0.1f, armLength)
                : new Vector3(armLength, 0.1f, stripWidth);
            arm.transform.position = new Vector3(center.X, height, center.Z);
            Paint(arm, colorHex);
        }

        /// <summary>
        /// The standard 4-crosswalk box at the intersection (#106), one
        /// per road arm — positioned from the walk network's Crosswalk
        /// edges, but visually clipped to just the road's own span
        /// (RoadWidth + 2 * GrassVergeWidth = 7.5m at the 0.75m verge)
        /// rather than the edge's full
        /// sidewalk-center-to-sidewalk-center length. The WalkNetwork
        /// edge itself stays sidewalk-center to sidewalk-center — that's
        /// the real distance a dog covers crossing the road, and moving it
        /// would break graph connectivity — this is purely a rendering
        /// clip so the crosswalk never paints over sidewalk pavement.
        /// </summary>
        private static void BuildCrosswalks(Transform parent)
        {
            var crosswalks = NeighborhoodLayout.WalkNetwork.Edges
                .Where(e => e.Kind == WalkEdgeKind.Crosswalk)
                .ToList();

            var crossRoadSpan = WorldDimensions.RoadWidth + 2f * WorldDimensions.GrassVergeWidth;

            for (var i = 0; i < crosswalks.Count; i++)
            {
                var edge = crosswalks[i];
                var alongX = Mathf.Abs(edge.A.Z - edge.B.Z) < 0.01f;

                var crosswalk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crosswalk.name = CrosswalkNamePrefix + i;
                crosswalk.transform.SetParent(parent);
                crosswalk.transform.position = new Vector3(
                    (edge.A.X + edge.B.X) / 2f, 0.08f, (edge.A.Z + edge.B.Z) / 2f);
                crosswalk.transform.localScale = alongX
                    ? new Vector3(crossRoadSpan, 0.1f, edge.Width)
                    : new Vector3(edge.Width, 0.1f, crossRoadSpan);
                Paint(crosswalk, Palette.CrosswalkHex);
            }
        }

        /// <summary>
        /// The front walkways (#128): one "Walkway - N" container per
        /// house, rendering the Core WalkNetwork's FrontWalkway edge (door
        /// -> sidewalk). In the kit path it's tiled City Kit Suburban
        /// path-short pavers at the exact positions/scales Core's
        /// WalkwayTiling computes; when the piece can't be loaded it falls
        /// back to one flat graybox strip (same pattern as the roads), so
        /// the walkway always exists visually. All geometry comes from
        /// Core either way — nothing here decides where a walkway goes.
        /// </summary>
        private static void BuildWalkways(Transform parent)
        {
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                BuildWalkway(parent, lot);
            }
        }

        /// <summary>
        /// Renders just one lot's front walkway (#405): the single-lot form of
        /// <see cref="BuildWalkways"/>, so a house built mid-game on a zone lot
        /// (<see cref="ExpansionDirector"/>) gets the same "Walkway - N"
        /// container a starting house gets at world-build time, instead of only
        /// its bare mesh. Renders identical geometry to the full-build loop for
        /// any given lot. A lot with no front-walkway edge in
        /// <see cref="NeighborhoodLayout.WalkNetwork"/> renders nothing — same
        /// skip the loop makes.
        /// </summary>
        public static void BuildWalkway(Transform parent, HouseLot lot)
        {
            if (!NeighborhoodLayout.WalkNetwork.TryGetFrontWalkway(lot.HouseId, out var walkway))
            {
                return;
            }

            var piece = ForcePrimitiveFallback
                ? null
                : Resources.Load<GameObject>(WalkwayPieceResource);

            var container = new GameObject(WalkwayNamePrefix + lot.HouseId);
            container.transform.SetParent(parent);
            container.transform.position = Vector3.zero;

            if (piece != null)
            {
                BuildKitWalkway(container.transform, walkway, piece);
            }
            else
            {
                BuildPrimitiveWalkway(container.transform, walkway);
            }
        }

        private static void BuildKitWalkway(Transform container, WalkEdge walkway, GameObject piece)
        {
            var pieces = WalkwayTiling.PiecesAlong(walkway);
            for (var i = 0; i < pieces.Count; i++)
            {
                var tile = Object.Instantiate(piece, container);
                tile.name = "Path " + i;
                tile.transform.position = new Vector3(pieces[i].Position.X, 0f, pieces[i].Position.Z);
                tile.transform.rotation = Quaternion.Euler(0f, pieces[i].YawDegrees, 0f);
                // Width (x) and height (y) at the uniform kit scale; the
                // length axis (local z) compressed so the pieces cover the
                // walkway exactly.
                tile.transform.localScale = new Vector3(
                    WalkwayTiling.WidthScale, WalkwayTiling.WidthScale, pieces[i].LengthScale);
            }
        }

        private static void BuildPrimitiveWalkway(Transform container, WalkEdge walkway)
        {
            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = "Strip";
            strip.transform.SetParent(container);
            strip.transform.position = new Vector3(
                (walkway.A.X + walkway.B.X) / 2f, 0.07f, (walkway.A.Z + walkway.B.Z) / 2f);

            // The walkway is axis-aligned (perpendicular to its street).
            var alongX = Mathf.Abs(walkway.A.Z - walkway.B.Z) < 0.01f;
            strip.transform.localScale = alongX
                ? new Vector3(walkway.Length, 0.1f, walkway.Width)
                : new Vector3(walkway.Width, 0.1f, walkway.Length);
            Paint(strip, Palette.SidewalkHex);
        }

        /// <summary>
        /// The backyard fences (#129, reshaped by #146): one "Fence - N"
        /// container per fenced lot, rendering Core's LotFence runs —
        /// anchored at the house's side-wall midpoints and wrapping the
        /// back yard only, no gate gap (the front stays open for the #128
        /// walkway). Lots are UNFENCED by default (HouseLot.HasFence off
        /// until a future quest purchases fences, #147), so the default
        /// world renders no fences; ForceFencesVisible is the
        /// Editor-check/test seam that builds them anyway. In the kit path
        /// it's tiled City Kit Suburban fence pieces at the exact
        /// positions/yaws/scales Core's FenceTiling computes; when the
        /// piece can't be loaded it falls back to one thin graybox rail
        /// per run (same pattern as the walkways). All geometry comes from
        /// Core either way — nothing here decides where a fence goes.
        /// </summary>
        /// <summary>
        /// Rebuilds only the backyard fences on an already-built world
        /// (#219): destroys the existing "Fence - N" containers and rebuilds
        /// them from the current <see cref="ForceFencesVisible"/> state. The
        /// Settings ▸ Debug fence toggle calls this so fences show/hide live
        /// without a full scene reload, leaving houses, dogs, and quests
        /// untouched. All geometry still comes from Core (LotFence/FenceTiling).
        /// </summary>
        public static void RebuildFences(Transform root, GameState state)
        {
            var existing = root.Cast<Transform>()
                .Where(child => child.name.StartsWith(FenceNamePrefix))
                .ToList();
            foreach (var container in existing)
            {
                Object.DestroyImmediate(container.gameObject);
            }

            BuildFences(root, state);
        }

        /// <summary>
        /// Builds a backyard fence for EVERY built house (#424), not just the
        /// starting four: iterates <see cref="GameState.Houses"/> and resolves
        /// each lot via <see cref="GameState.GetHouseLot"/> — the same
        /// resolution the build/upgrade paths use — so a house on an unlocked
        /// zone gets its fence too. Each lot is still unfenced by default
        /// (<see cref="LotFence.RunsFor"/> empty until <see cref="HouseLot.HasFence"/>
        /// or <see cref="ForceFencesVisible"/>), so this changes only WHICH
        /// houses are covered, not the hidden-by-default state.
        /// </summary>
        private static void BuildFences(Transform parent, GameState state)
        {
            foreach (var house in state.Houses)
            {
                BuildFence(parent, state.GetHouseLot(house.Id));
            }
        }

        /// <summary>
        /// Renders just one lot's backyard fence (#405): the single-lot form of
        /// <see cref="BuildFences"/>, so a mid-game zone-lot build
        /// (<see cref="ExpansionDirector"/>) gets the same "Fence - N"
        /// treatment. Fixes the confirmed <see cref="LotFence"/> gap — its run
        /// geometry resolves the lot's model via
        /// <see cref="Doggiehood.Core.World.HouseModelCatalog.ForHouse"/>, which
        /// is now zone-safe (#414). Unfenced lots (the default) render nothing,
        /// same as the loop; <see cref="ForceFencesVisible"/> forces them on.
        /// </summary>
        public static void BuildFence(Transform parent, HouseLot lot)
        {
            var runs = ForceFencesVisible ? LotFence.GeometryFor(lot) : LotFence.RunsFor(lot);
            if (runs.Count == 0)
            {
                return;
            }

            var piece = ForcePrimitiveFallback
                ? null
                : Resources.Load<GameObject>(FencePieceResource);

            var container = new GameObject(FenceNamePrefix + lot.HouseId);
            container.transform.SetParent(parent);
            container.transform.position = Vector3.zero;

            if (piece != null)
            {
                BuildKitFence(container.transform, runs, piece);
            }
            else
            {
                BuildPrimitiveFence(container.transform, runs);
            }
        }

        private static void BuildKitFence(Transform container, IReadOnlyList<FenceRun> runs, GameObject piece)
        {
            var index = 0;
            foreach (var run in runs)
            {
                foreach (var placement in FenceTiling.PiecesAlong(run))
                {
                    var segment = Object.Instantiate(piece, container);
                    segment.name = "Fence " + index++;
                    segment.transform.position = new Vector3(placement.Position.X, 0f, placement.Position.Z);
                    segment.transform.rotation = Quaternion.Euler(0f, placement.YawDegrees, 0f);
                    // Height (y) and thickness (z) at the uniform fence
                    // scale; the length axis (local x) compressed so the
                    // pieces cover the run exactly.
                    segment.transform.localScale = new Vector3(
                        placement.LengthScale, FenceTiling.Scale, FenceTiling.Scale);
                }
            }
        }

        private static void BuildPrimitiveFence(Transform container, IReadOnlyList<FenceRun> runs)
        {
            var height = FenceTiling.Scale * FenceTiling.PieceModelHeight;
            const float thickness = 0.3f;

            for (var i = 0; i < runs.Count; i++)
            {
                var run = runs[i];
                var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = "Rail " + i;
                rail.transform.SetParent(container);
                rail.transform.position = new Vector3(
                    (run.A.X + run.B.X) / 2f, height / 2f, (run.A.Z + run.B.Z) / 2f);

                // Fence runs are axis-aligned (houses face a cardinal
                // street direction, and the runs follow the house axes).
                var alongX = Mathf.Abs(run.A.Z - run.B.Z) < 0.01f;
                rail.transform.localScale = alongX
                    ? new Vector3(run.Length, height, thickness)
                    : new Vector3(thickness, height, run.Length);
                Paint(rail, Palette.SidewalkHex);
            }
        }

        /// <summary>
        /// Procedural yard landscaping (#170): one "Yard - N" container per
        /// lot, holding its selected front and back yard trees — Core's
        /// YardLandscaping.FrontTreesFor/BackTreesFor decides which
        /// positions and kit models, seeded deterministically per lot;
        /// nothing here decides where a tree goes. In the kit path each
        /// pick instantiates its matching tree-large/tree-small model; when
        /// a piece can't be loaded it falls back to one simple primitive
        /// marker per pick (same pattern as the walkways/fences).
        /// </summary>
        private static void BuildYardLandscaping(Transform parent)
        {
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                BuildYardLandscaping(parent, lot);
            }
        }

        /// <summary>
        /// Renders just one lot's front/back yard trees (#405): the single-lot
        /// form of <see cref="BuildYardLandscaping(Transform)"/>, so a mid-game
        /// zone-lot build (<see cref="ExpansionDirector"/>) gets its "Yard - N"
        /// trees like a starting house. The tree positions come from Core's
        /// <see cref="YardLandscaping"/>, which rejection-samples against
        /// <see cref="Doggiehood.Core.World.HousePlacement.HouseFootprint"/> —
        /// now zone-safe (#414) — so a zone lot no longer throws here. A lot
        /// whose yard selects no trees renders nothing, same as the loop.
        /// </summary>
        public static void BuildYardLandscaping(Transform parent, HouseLot lot)
        {
            var picks = YardLandscaping.FrontTreesFor(lot).Concat(YardLandscaping.BackTreesFor(lot)).ToList();
            if (picks.Count == 0)
            {
                return;
            }

            var container = new GameObject(YardLandscapingNamePrefix + lot.HouseId);
            container.transform.SetParent(parent);
            container.transform.position = Vector3.zero;

            for (var i = 0; i < picks.Count; i++)
            {
                BuildYardTree(container.transform, picks[i], i);
            }
        }

        /// <summary>Resources load key for a YardTreeKind's kit model.</summary>
        public static string YardTreeResourceName(YardTreeKind kind)
        {
            switch (kind)
            {
                case YardTreeKind.TreeLarge:
                    return TreeLargeResource;
                case YardTreeKind.TreeSmall:
                    return TreeSmallResource;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private static void BuildYardTree(Transform container, YardTreePlacement placement, int index)
        {
            var piece = ForcePrimitiveFallback
                ? null
                : Resources.Load<GameObject>(YardTreeResourceName(placement.Kind));

            if (piece != null)
            {
                var tree = Object.Instantiate(piece, container);
                tree.name = placement.Kind + " " + index;
                tree.transform.position = new Vector3(placement.Position.X, 0f, placement.Position.Z);
                tree.transform.rotation = Quaternion.identity;
                tree.transform.localScale = Vector3.one * YardLandscaping.UniformScale;
            }
            else
            {
                BuildPrimitiveYardTree(container, placement, index);
            }
        }

        private static void BuildPrimitiveYardTree(Transform container, YardTreePlacement placement, int index)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = placement.Kind + " " + index;
            marker.transform.SetParent(container);

            // Unity's primitive Cylinder is 1 unit in diameter and 2 units
            // tall at scale 1 — scale.x/z land the diameter on Core's
            // TreeFootprintRadius, scale.y halves the target height to
            // compensate for the model's own 2-unit height.
            var diameter = YardLandscaping.TreeFootprintRadius * 2f;
            marker.transform.localScale = new Vector3(diameter, YardLandscapingFallbackHeight / 2f, diameter);
            marker.transform.position = new Vector3(
                placement.Position.X, YardLandscapingFallbackHeight / 2f, placement.Position.Z);
            Paint(marker, Palette.YardLandscapingFallbackHex);
        }

        /// <summary>
        /// One graybox marker (#57) per lot in every unlocked zone that has
        /// no house on it yet (GameState.IsLotBuildable) — the "empty lot"
        /// tap targets ExpansionDirector wires up to GameState.TryBuildHouse.
        /// Locked zones and lots that already have a house get nothing.
        /// </summary>
        private static void BuildEmptyLots(Transform parent, GameState state)
        {
            foreach (var zone in state.UnlockedZones)
            {
                foreach (var lot in zone.Lots)
                {
                    if (state.IsLotBuildable(lot.HouseId))
                    {
                        BuildEmptyLot(parent, lot);
                    }
                }
            }
        }

        /// <summary>
        /// Builds one graybox marker for an empty, buildable lot: a low
        /// raised "foundation" slab (#300 (B)) at the lot's Core position
        /// with an EmptyLotView tap target, its base flush on the ground
        /// plane. Public so ExpansionDirector's EditMode tests can build a
        /// single marker directly, same pattern as BuildHouse. The slab
        /// keeps the fixed <see cref="EmptyLotMarkerFootprint"/> — it is NOT
        /// sized to HousePlacement.HouseFootprint, which throws for the zone
        /// lots this only ever runs on (Derek's #300 option-3 decision).
        /// </summary>
        public static GameObject BuildEmptyLot(Transform parent, HouseLot lot)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = EmptyLotNamePrefix + lot.HouseId;
            marker.transform.SetParent(parent);
            marker.transform.localScale = new Vector3(EmptyLotMarkerFootprint, EmptyLotFoundationSlabHeight, EmptyLotMarkerFootprint);
            marker.transform.position = new Vector3(lot.Position.X, EmptyLotFoundationSlabHeight / 2f, lot.Position.Z);
            Paint(marker, Palette.EmptyLotMarkerHex);

            var view = marker.AddComponent<EmptyLotView>();
            view.Init(lot.HouseId);
            return marker;
        }

        /// <summary>
        /// Renders road surfaces for every currently unlocked zone's tiles
        /// (#373), derived from <see cref="TileRoadGeometry"/> — the tile
        /// catalog geometry, not the hardcoded <see cref="NeighborhoodLayout"/>.
        /// Runs at initial build so a loaded game whose save already unlocked a
        /// zone (<see cref="GameState.RestoreUnlockedZoneCount"/>) renders that
        /// zone's roads, not just its empty-lot markers.
        /// </summary>
        private static void BuildUnlockedZoneRoads(Transform parent, GameState state)
        {
            var straightKit = LoadStraightRoadKit();
            foreach (var zone in state.UnlockedZones)
            {
                foreach (var placement in zone.TilePlacements)
                {
                    BuildTileRoads(parent, placement.Coordinate, placement.Type, straightKit);
                }
            }
        }

        /// <summary>
        /// Renders a freshly unlocked zone into the live world (#373): regrows
        /// the base grass plane to cover the newly extended map extent, renders
        /// each of the zone's tiles' road surfaces from the tile catalog
        /// geometry, and drops the zone's buildable empty-lot markers. The
        /// single entry point <see cref="ExpansionUnlockDirector"/> calls on a
        /// confirmed unlock, so the new zone appears as real neighborhood —
        /// grass under it and roads along its tiles — instead of markers
        /// floating over void.
        /// </summary>
        public static void RenderUnlockedZone(Transform root, GameState state, Zone zone)
        {
            ResizeGroundToMap(root, state.Map);

            var straightKit = LoadStraightRoadKit();
            foreach (var placement in zone.TilePlacements)
            {
                BuildTileRoads(root, placement.Coordinate, placement.Type, straightKit);
            }

            foreach (var lot in zone.Lots)
            {
                if (state.IsLotBuildable(lot.HouseId))
                {
                    BuildEmptyLot(root, lot);
                }
            }
        }

        /// <summary>The City Kit straight road tile, or null to take the
        /// graybox primitive road path (the kit assets aren't staged, or the
        /// #171 fallback is forced) — the same all-or-nothing seam the starting
        /// corridor uses.</summary>
        private static GameObject LoadStraightRoadKit()
        {
            return ForcePrimitiveFallback ? null : Resources.Load<GameObject>(RoadStraightResource);
        }

        /// <summary>
        /// Road surfaces for one placed tile (#373): a "ZoneRoad - col,row"
        /// container holding one straight road run per road-carrying edge, from
        /// the tile centre out to that edge — the arms
        /// <see cref="TileRoadGeometry.SegmentsFor"/> derives from the catalog.
        /// A cul-de-sac renders a single south arm meeting the origin tile's
        /// road; a straight/turn/tee tile renders its own arms the same way.
        /// Kit straight tiles when loadable, else graybox road slabs — the same
        /// two-path split the starting intersection uses.
        /// </summary>
        private static void BuildTileRoads(Transform parent, TileCoordinate coordinate, TileType type,
            GameObject straightKit)
        {
            var container = new GameObject(ZoneRoadNamePrefix + coordinate.Col + "," + coordinate.Row);
            container.transform.SetParent(parent);
            container.transform.position = Vector3.zero;

            var segments = TileRoadGeometry.SegmentsFor(coordinate, type);
            for (var i = 0; i < segments.Count; i++)
            {
                if (straightKit != null)
                {
                    BuildKitTileRoadArm(container.transform, segments[i], straightKit, i);
                }
                else
                {
                    BuildPrimitiveTileRoadArm(container.transform, segments[i], i);
                }
            }
        }

        /// <summary>One road arm as tiled City Kit straight tiles (#373): the
        /// tile's road runs along local X, so a north-south arm rotates 90°;
        /// tiles are laid every <see cref="RoadTileScale"/> meters along the
        /// arm to cover its full <see cref="TileRoadSegment.Length"/>.</summary>
        private static void BuildKitTileRoadArm(Transform container, TileRoadSegment segment, GameObject straightKit,
            int armIndex)
        {
            var isNorthSouth = segment.Orientation == StreetOrientation.NorthSouth;
            var rotation = isNorthSouth ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            var tileCount = Mathf.Max(1, Mathf.RoundToInt(segment.Length / RoadTileScale));
            var step = segment.Length / tileCount;

            for (var i = 0; i < tileCount; i++)
            {
                // Centres of the tileCount tiles evenly spanning the arm length,
                // centred on the segment's own centre.
                var along = -segment.Length / 2f + (i + 0.5f) * step;
                var position = isNorthSouth
                    ? new Vector3(segment.Center.X, WorldDimensions.RoadSurfaceHeight, segment.Center.Z + along)
                    : new Vector3(segment.Center.X + along, WorldDimensions.RoadSurfaceHeight, segment.Center.Z);

                var tile = Object.Instantiate(straightKit, container);
                tile.name = RoadTileNamePrefix + armIndex + " " + i;
                tile.transform.position = position;
                tile.transform.rotation = rotation;
                tile.transform.localScale = Vector3.one * RoadTileScale;
            }
        }

        /// <summary>One road arm as a graybox slab (#373), the primitive
        /// fallback matching <see cref="BuildRoad"/>'s surface.</summary>
        private static void BuildPrimitiveTileRoadArm(Transform container, TileRoadSegment segment, int armIndex)
        {
            var isNorthSouth = segment.Orientation == StreetOrientation.NorthSouth;
            var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = RoadNamePrefix + armIndex;
            arm.transform.SetParent(container);
            arm.transform.localScale = isNorthSouth
                ? new Vector3(segment.Width, ZoneRoadThickness, segment.Length)
                : new Vector3(segment.Length, ZoneRoadThickness, segment.Width);
            arm.transform.position = new Vector3(
                segment.Center.X, WorldDimensions.RoadSurfaceHeight + ZoneRoadThickness / 2f, segment.Center.Z);
            Paint(arm, Palette.StreetHex);
        }

        /// <summary>
        /// The map-expansion lock indicator marker (#178): a SpriteRenderer
        /// showing the #183 lock icon, positioned and tinted by
        /// ExpansionIndicatorView from Core's live ExpansionIndicator
        /// state. Skipped entirely if the icon can't load — there's no
        /// designed graybox stand-in for it (unlike the CityKit models,
        /// this repo-native icon is always expected to be present, so this
        /// is a defensive no-op rather than a real fallback path).
        /// </summary>
        private static void BuildExpansionIndicator(Transform parent, GameState state)
        {
            var baseTexture = Resources.Load<Texture2D>(LockIconResource);
            if (baseTexture == null)
            {
                return;
            }

            var marker = new GameObject(ExpansionIndicatorName);
            marker.transform.SetParent(parent);
            marker.transform.localScale = Vector3.one * ExpansionIndicatorWorldSize;

            marker.AddComponent<SpriteRenderer>();

            var affordableSprite = TintedIcon.Recolor(baseTexture, CoreColors.FromHex(Palette.ExpansionIndicatorAffordableHex));
            var lockedSprite = TintedIcon.Recolor(baseTexture, CoreColors.FromHex(Palette.ExpansionIndicatorLockedHex));

            var view = marker.AddComponent<ExpansionIndicatorView>();
            view.Init(state, affordableSprite, lockedSprite);
        }

        /// <summary>
        /// Builds one house's full visual (view + model, #38): public so
        /// it can be called for a single house directly — both the #58
        /// vacancy EditMode tests (a house not part of a full GameState)
        /// and #57's "build one new house mid-game" action (ExpansionDirector),
        /// which needs exactly this without rebuilding the whole scene.
        /// Resolves the lot from NeighborhoodLayout (the starting layout) —
        /// use the <see cref="BuildHouse(Transform, House, HouseLot)"/>
        /// overload for a house built on a zone lot, which
        /// NeighborhoodLayout doesn't know about. Returns the house's root
        /// GameObject.
        /// </summary>
        public static GameObject BuildHouse(Transform parent, House house)
        {
            return BuildHouse(parent, house, NeighborhoodLayout.GetHouseLot(house.Id));
        }

        /// <summary>
        /// Same as <see cref="BuildHouse(Transform, House)"/> but takes the
        /// lot directly (#57) — needed for a house built on a zone lot,
        /// which NeighborhoodLayout (the starting layout only) doesn't
        /// know about. ExpansionDirector resolves the lot via
        /// GameState.GetHouseLot and calls this overload when swapping a
        /// tapped empty-lot marker for the real house.
        /// </summary>
        public static GameObject BuildHouse(Transform parent, House house, HouseLot lot)
        {
            // #127: the house stands at Core's front-setback position —
            // pulled from the lot center toward its facing street so the
            // scaled front facade sits HousePlacement.FrontSetback from
            // the sidewalk's outer edge. The lot center itself is not
            // moved (it still anchors the deferred expansion geometry);
            // since #128 the walk network connects at the front DOOR.
            var position = HousePlacement.Position(lot, HouseKitScale);

            var houseRoot = new GameObject(HouseNamePrefix + house.Id);
            houseRoot.transform.SetParent(parent);
            houseRoot.transform.position = new Vector3(position.X, 0f, position.Z);
            var view = houseRoot.AddComponent<HouseView>();
            view.Init(house.Id);

            // Window anchor on the intersection-facing side (#9). The
            // anchor's local pose is identical in both art paths — dogs'
            // window-watching depends on it — and it intentionally keeps
            // this diagonal facing even though the kit model itself now
            // faces its walkway's road squarely (HouseFrontFacing);
            // fine-tuning the anchor to each kit model's actual wall is a
            // follow-up.
            var anchor = new GameObject("WindowAnchor").transform;
            anchor.SetParent(houseRoot.transform);
            var facing = new Vector3(-Mathf.Sign(lot.Position.X), 0f, -Mathf.Sign(lot.Position.Z)).normalized;
            anchor.localPosition = new Vector3(facing.x * 2.1f, 1.5f, facing.z * 2.1f);
            anchor.localRotation = Quaternion.LookRotation(facing, Vector3.up);
            view.WindowAnchor = anchor;

            // Render the mesh for the house's CURRENT level, then steer to the
            // graybox fallback below only when no kit mesh can be resolved.
            // Two kinds of house resolve a mesh:
            //   * a starter house (ids 1-4, #59): its fixed per-house ladder
            //     from HouseLevelModelTable, tinted by its HouseStyleTable kit
            //     texture variant.
            //   * a zone-built house (id >= 5, #299): its rolled HouseVariant's
            //     ladder mesh, tinted by the generated palette color-multiply.
            // Leveling swaps the mesh within the same ladder and never resizes
            // the lot. A zone house with no rolled variant (constructed
            // directly, or an unknown expansion id) resolves no mesh and falls
            // back to graybox, as does any mesh missing a catalog entry.
            var isZoneHouse = HouseVariantAssignment.IsZoneHouse(house.Id);
            string levelModelName;
            if (isZoneHouse)
            {
                levelModelName = house.Variant.HasValue
                    ? HouseLevelModelTable.ForHouseLevel(house.Variant.Value.LadderId, house.Level)
                    : null;
            }
            else
            {
                levelModelName = HouseLevelModelTable.HasHouse(house.Id)
                    ? HouseModelResourcePath(house.Id, house.Level)
                    : null;
            }

            var model = (ForcePrimitiveFallback || levelModelName == null || !HouseModelCatalog.HasModel(levelModelName))
                ? null
                : Resources.Load<GameObject>(levelModelName);
            if (model != null)
            {
                if (isZoneHouse)
                {
                    // #299: no HouseStyleTable style for zone houses — the
                    // per-house look is the generated palette tint (Colormap
                    // texture, no variant swap), applied as a color-multiply.
                    var paletteTintHex = Palette.HouseTintHex(house.Variant.Value.TintIndex);
                    BuildHouseModel(houseRoot, model, HouseFrontFacing(lot),
                        HouseTintVariant.Colormap, paletteTintHex, house.IsVacant);
                }
                else
                {
                    var tintVariant = HouseStyleTable.ForHouse(house.Id).TintVariant;
                    BuildHouseModel(houseRoot, model, HouseFrontFacing(lot), tintVariant, null, house.IsVacant);
                }

                return houseRoot;
            }

            // Graybox fallback (only reached when the kit model itself
            // can't load): a single plain box. Pre-#64 this branch also
            // built a procedural roof shape and an optional porch keyed on
            // HouseStyle.RoofShape/HasPorch — both removed (#64) along with
            // their per-house hex colors, since real per-house visual
            // identity now comes from the kit model + BuildHouseModel's
            // tint-variant texture swap, which this fallback never reaches.
            // #58: a vacant house gets the flat vacancy tint instead of the
            // fallback's normal wall color.
            var walls = GameObject.CreatePrimitive(PrimitiveType.Cube);
            walls.name = "Walls";
            walls.transform.SetParent(houseRoot.transform);
            walls.transform.localScale = new Vector3(HouseFallbackWallsFootprint, HouseFallbackWallsHeight, HouseFallbackWallsFootprint);
            walls.transform.localPosition = new Vector3(0f, HouseFallbackWallsHeight / 2f, 0f);
            Paint(walls, house.IsVacant ? Palette.VacantHouseTintHex : Palette.HouseFallbackHex);
            return houseRoot;
        }

        /// <summary>
        /// The direction a house model's front should face (Derek's Editor
        /// feedback on the first kit pass: diagonal toward-origin yaws
        /// looked scattered): squarely toward the road the lot's front
        /// walkway attaches to. The rule itself lives in Core since #127
        /// (HousePlacement.FrontFacing — the front-setback math needs it
        /// engine-free); this is just the Vector3 conversion at the Unity
        /// boundary.
        /// </summary>
        private static Vector3 HouseFrontFacing(HouseLot lot)
        {
            var facing = HousePlacement.FrontFacing(lot);
            return new Vector3(facing.X, 0f, facing.Z);
        }

        /// <summary>
        /// The house as its mapped City Kit Suburban model (#122): placed
        /// directly at the house root's front-setback position (#127; the
        /// models have ground-level pivots),
        /// uniformly scaled by the one fixed kit-wide HouseKitScale
        /// (#145), and yawed squarely toward the road its
        /// walkway attaches to (see HouseFrontFacing) plus the art-side
        /// HouseModelYawOffsetDegrees correction, then painted with its
        /// HouseStyle.TintVariant kit texture (#64, see ApplyTintVariant).
        /// The imported FBX carries no collider, so a BoxCollider fitted
        /// to the combined renderer bounds goes on the HouseView object to
        /// keep tap interaction (TapRouter raycasts, then
        /// GetComponentInParent) working. None of the primitive
        /// walls/roof/porch are built in this path.
        /// </summary>
        private static void BuildHouseModel(GameObject houseRoot, GameObject model, Vector3 facing,
            HouseTintVariant tintVariant, string paletteTintHex, bool isVacant)
        {
            var visual = Object.Instantiate(model, houseRoot.transform);
            visual.name = "Model";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.LookRotation(facing, Vector3.up)
                * Quaternion.Euler(0f, HouseModelYawOffsetDegrees, 0f);
            visual.transform.localScale = Vector3.one * HouseKitScale;

            ApplyTintVariant(visual, tintVariant);
            ApplyPaletteTint(visual, paletteTintHex);
            ApplyVacancyTint(visual, isVacant);

            // houseRoot has identity rotation and unit scale at this point,
            // matching TapColliders.AddFitted's requirement.
            TapColliders.AddFitted(houseRoot, visual);
        }

        /// <summary>
        /// Resources load key for a HouseTintVariant's texture, staged
        /// alongside the house models (Assets/Art/Houses/CityKitSuburban/
        /// Resources/, same bare-filename convention
        /// HouseModelResourcePath and WalkwayPieceResource use — load keys
        /// are relative to the Resources folder itself). Colormap is the
        /// kit's own default texture (already on the imported model's
        /// material), so it has no separate file to load here; callers
        /// only need this for the three variation textures.
        /// </summary>
        public static string TintTextureResourceName(HouseTintVariant tintVariant)
        {
            switch (tintVariant)
            {
                case HouseTintVariant.VariationA:
                    return "variation-a";
                case HouseTintVariant.VariationB:
                    return "variation-b";
                case HouseTintVariant.VariationC:
                    return "variation-c";
                default:
                    return "colormap";
            }
        }

        /// <summary>
        /// Paints a house model with its HouseStyle.TintVariant kit
        /// texture (#64): a real texture swap, not a color multiply — the
        /// kit's variation-a/b/c textures are hand-painted alternates for
        /// the same meshes, so swapping .mainTexture is correct where
        /// DogView.PaintModel's .color multiply (for the white-base Cube
        /// Pets coat) is not. Colormap houses keep whatever material the
        /// FBX import gave them — nothing to swap to.
        /// </summary>
        private static void ApplyTintVariant(GameObject visual, HouseTintVariant tintVariant)
        {
            if (tintVariant == HouseTintVariant.Colormap)
            {
                return;
            }

            var texture = Resources.Load<Texture2D>(TintTextureResourceName(tintVariant));
            if (texture == null)
            {
                return;
            }

            foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
            {
                var material = renderer.sharedMaterial != null
                    ? new Material(renderer.sharedMaterial)
                    : new Material(Shader.Find("Standard"));
                material.mainTexture = texture;
                renderer.sharedMaterial = material;
            }
        }

        /// <summary>
        /// Multiplies a zone-built house's rolled palette tint over its mesh
        /// (#299): the generated <see cref="Palette.HouseTintHex"/> color for
        /// the house's <see cref="HouseVariant.TintIndex"/>, applied with the
        /// same material color-multiply technique as
        /// <see cref="ApplyVacancyTint"/> and DogView.PaintModel — NOT a
        /// kit-texture-variant swap. No-op when <paramref name="tintHex"/> is
        /// null (a starter house, which is tinted by ApplyTintVariant instead).
        /// Runs before ApplyVacancyTint so the vacancy grey still wins while a
        /// house is vacant; the palette color shows once a dog moves in.
        /// </summary>
        private static void ApplyPaletteTint(GameObject visual, string tintHex)
        {
            if (tintHex == null)
            {
                return;
            }

            foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
            {
                var material = renderer.sharedMaterial != null
                    ? new Material(renderer.sharedMaterial)
                    : new Material(Shader.Find("Standard"));
                material.color = CoreColors.FromHex(tintHex);
                renderer.sharedMaterial = material;
            }
        }

        /// <summary>
        /// Greyscales a vacant house's mesh (#58, superseding the earlier
        /// "for sale sign" plan): while House.IsVacant, every renderer on
        /// the model gets a flat desaturated color multiply instead of its
        /// normal ApplyTintVariant coloring — the same color-multiply
        /// technique DogView.PaintModel uses for its white-base coat, no
        /// new art asset needed. Pure function of House.IsVacant at build
        /// time, no logic of its own: occupied houses are left exactly as
        /// ApplyTintVariant already rendered them (this simply never
        /// touches them), so the next time the world rebuilds after a
        /// house's dog moves in, it renders the normal tint again.
        /// </summary>
        private static void ApplyVacancyTint(GameObject visual, bool isVacant)
        {
            if (!isVacant)
            {
                return;
            }

            foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
            {
                var material = renderer.sharedMaterial != null
                    ? new Material(renderer.sharedMaterial)
                    : new Material(Shader.Find("Standard"));
                material.color = CoreColors.FromHex(Palette.VacantHouseTintHex);
                renderer.sharedMaterial = material;
            }
        }

        private static void BuildSun(Transform parent)
        {
            var sun = new GameObject(SunName);
            sun.transform.SetParent(parent);
            sun.transform.rotation = Quaternion.Euler(LightingPreset.SunPitchDegrees, LightingPreset.SunYawDegrees, 0f);

            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = LightingPreset.SunIntensity;
            light.color = CoreColors.FromHex(LightingPreset.SunColorHex);
            light.shadows = LightShadows.Hard;
        }

        private static void ApplyAmbientLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = CoreColors.FromHex(LightingPreset.AmbientColorHex);
        }

        private static void Paint(GameObject target, string colorHex)
        {
            var renderer = target.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Standard"));
            material.color = CoreColors.FromHex(colorHex);
            renderer.sharedMaterial = material;
        }
    }
}
