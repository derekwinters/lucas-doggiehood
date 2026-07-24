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
        public const float GroundExtent = 30f;

        /// <summary>Graybox marker footprint (local X/Z) for an empty,
        /// buildable lot (#57) — a flat pad distinct from a house's
        /// fallback wall block, sized just to read as a tap target within
        /// the lot's own space.</summary>
        private const float EmptyLotMarkerFootprint = 3f;

        /// <summary>Graybox marker height/thickness — thin, so it reads as
        /// a ground-level pad rather than a solid block.</summary>
        private const float EmptyLotMarkerHeight = 0.2f;

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

        /// <summary>Resources key for the yard planter kit piece (#170).</summary>
        public const string PlanterResource = "planter";

        /// <summary>Container name prefix for a lot's procedural yard
        /// landscaping (#170) — one per lot, holding its selected front and
        /// back yard trees/planters.</summary>
        public const string YardLandscapingNamePrefix = "Yard - ";

        /// <summary>Graybox-fallback yard prop height (#170) — only ever
        /// built when none of the tree-large/tree-small/planter kit
        /// pieces can load. Sized off Core's own collision radius (four
        /// radii tall) so it reads as a small rounded tree/bush rather
        /// than a flat disc, without inventing an unrelated tuning
        /// number.</summary>
        private const float YardLandscapingFallbackHeight = YardLandscaping.TreeFootprintRadius * 4f;

        /// <summary>
        /// Editor-check/test seam (#146): builds every lot's backyard
        /// fence even though HouseLot.HasFence defaults false (fences are
        /// hidden until a future quest purchases them, #147). To eyeball
        /// the enclosures in the Editor, set this to true at the top of
        /// WorldBootstrap.Awake (one temporary line:
        /// <c>WorldBuilder.ForceFencesVisible = true;</c>), enter Play
        /// mode, then remove the line. Never set in production code.
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

        public static GameObject Build(GameState state)
        {
            var root = new GameObject(RootName);

            BuildGround(root.transform);

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
            BuildFences(root.transform);
            BuildYardLandscaping(root.transform);
            BuildEmptyLots(root.transform, state);
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
        /// The visual road corridor as City Kit Roads tiles (#121): one
        /// crossroad tile on the intersection, then straight tiles every
        /// RoadTileScale meters along each street arm — except where the
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
                var halfTile = RoadTileScale / 2f;

                // Whole tiles per arm outward from the intersection tile's
                // edge (StreetHalfLength 26 - 5 -> 2 tiles, centers 10, 20).
                // The last (26 - 25 = 1 m) sliver of each arm stays untiled
                // rather than overshooting the street end and ground plane.
                var armTileCount = (int)((road.HalfLength - halfTile) / RoadTileScale);

                foreach (var sign in new[] { 1f, -1f })
                {
                    for (var i = 1; i <= armTileCount; i++)
                    {
                        var along = sign * i * RoadTileScale;
                        var isCrossing = crosswalkAlongs.Any(a => Mathf.Abs(a - along) <= halfTile);
                        var tile = Object.Instantiate(isCrossing ? crossing : straight, roadParent.transform);
                        tile.name = RoadTileNamePrefix + road.Orientation
                            + (isCrossing ? " Crossing " : " Straight ") + (sign * i);
                        var point = road.PointAt(along, 0f);
                        tile.transform.position = new Vector3(point.X, 0f, point.Z);
                        tile.transform.rotation = rotation;
                        tile.transform.localScale = Vector3.one * RoadTileScale;
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

        private static void BuildGround(Transform parent)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(parent);
            // A default plane is 10x10m at scale 1.
            ground.transform.localScale = new Vector3(GroundExtent / 5f, 1f, GroundExtent / 5f);
            ground.transform.position = Vector3.zero;
            Paint(ground, Palette.GrassHex);
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
            var piece = ForcePrimitiveFallback
                ? null
                : Resources.Load<GameObject>(WalkwayPieceResource);

            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                if (!NeighborhoodLayout.WalkNetwork.TryGetFrontWalkway(lot.HouseId, out var walkway))
                {
                    continue;
                }

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
        private static void BuildFences(Transform parent)
        {
            var piece = ForcePrimitiveFallback
                ? null
                : Resources.Load<GameObject>(FencePieceResource);

            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var runs = ForceFencesVisible ? LotFence.GeometryFor(lot) : LotFence.RunsFor(lot);
                if (runs.Count == 0)
                {
                    continue;
                }

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
        /// lot, holding its selected front and back yard trees/planters —
        /// Core's YardLandscaping.FrontTreesFor/BackTreesFor decides which
        /// positions and kit models, seeded deterministically per lot;
        /// nothing here decides where a tree goes. In the kit path each
        /// pick instantiates its matching tree-large/tree-small/planter
        /// model; when a piece can't be loaded it falls back to one simple
        /// primitive marker per pick (same pattern as the walkways/fences).
        /// </summary>
        private static void BuildYardLandscaping(Transform parent)
        {
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var picks = YardLandscaping.FrontTreesFor(lot).Concat(YardLandscaping.BackTreesFor(lot)).ToList();
                if (picks.Count == 0)
                {
                    continue;
                }

                var container = new GameObject(YardLandscapingNamePrefix + lot.HouseId);
                container.transform.SetParent(parent);
                container.transform.position = Vector3.zero;

                for (var i = 0; i < picks.Count; i++)
                {
                    BuildYardTree(container.transform, picks[i], i);
                }
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
                case YardTreeKind.Planter:
                    return PlanterResource;
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
        /// Builds one graybox marker for an empty, buildable lot: a flat
        /// pad at the lot's Core position with an EmptyLotView tap target.
        /// Public so ExpansionDirector's EditMode tests can build a single
        /// marker directly, same pattern as BuildHouse.
        /// </summary>
        public static GameObject BuildEmptyLot(Transform parent, HouseLot lot)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = EmptyLotNamePrefix + lot.HouseId;
            marker.transform.SetParent(parent);
            marker.transform.localScale = new Vector3(EmptyLotMarkerFootprint, EmptyLotMarkerHeight, EmptyLotMarkerFootprint);
            marker.transform.position = new Vector3(lot.Position.X, EmptyLotMarkerHeight / 2f, lot.Position.Z);
            Paint(marker, Palette.EmptyLotMarkerHex);

            var view = marker.AddComponent<EmptyLotView>();
            view.Init(lot.HouseId);
            return marker;
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

            // #57: a house built on a zone lot beyond the starting 4 has no
            // authored HouseStyleTable entry yet (per-zone-house model/tint
            // assignment is undesigned) — HasStyle steers it straight to
            // the graybox fallback below instead of letting
            // HouseModelResourcePath's HouseStyleTable.ForHouse throw.
            var model = (ForcePrimitiveFallback || !HouseStyleTable.HasStyle(house.Id))
                ? null
                : Resources.Load<GameObject>(HouseModelResourcePath(house.Id));
            if (model != null)
            {
                var tintVariant = HouseStyleTable.ForHouse(house.Id).TintVariant;
                BuildHouseModel(houseRoot, model, HouseFrontFacing(lot), tintVariant, house.IsVacant);
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
            HouseTintVariant tintVariant, bool isVacant)
        {
            var visual = Object.Instantiate(model, houseRoot.transform);
            visual.name = "Model";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.LookRotation(facing, Vector3.up)
                * Quaternion.Euler(0f, HouseModelYawOffsetDegrees, 0f);
            visual.transform.localScale = Vector3.one * HouseKitScale;

            ApplyTintVariant(visual, tintVariant);
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
