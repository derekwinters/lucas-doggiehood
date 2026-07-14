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
        public const string SunName = "Sun";
        public const float GroundExtent = 30f;

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

        /// <summary>Target maximum horizontal footprint for a scaled house
        /// model. 8m (up from the original 4.2m graybox-sized target,
        /// which Derek's Editor check showed reading far too small against
        /// the kit roads): lots sit at +-14 and the logical sidewalk's
        /// outer edge is at 5.75m (0.75m verge), so an 8m-wide house spans
        /// 10-18 on its lot, leaving a sensible front yard.</summary>
        private const float HouseTargetFootprint = 8f;

        /// <summary>
        /// Yaw correction applied after pointing a house model at its
        /// street-front facing. 180: Derek's Editor screenshot showed the
        /// doors pointing opposite the look direction at 0, so the City
        /// Kit Suburban models face local -Z. Kept a single public
        /// constant (read by WorldKitArtTests) so one flip fixes all four
        /// houses if it's ever still wrong.
        /// </summary>
        public const float HouseModelYawOffsetDegrees = 180f;

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
                BuildHouse(root.transform, house);
            }

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

        private static void BuildHouse(Transform parent, House house)
        {
            var lot = NeighborhoodLayout.GetHouseLot(house.Id);

            var houseRoot = new GameObject(HouseNamePrefix + house.Id);
            houseRoot.transform.SetParent(parent);
            houseRoot.transform.position = new Vector3(lot.Position.X, 0f, lot.Position.Z);
            var view = houseRoot.AddComponent<HouseView>();
            view.Init(house.Id);

            // Window anchor on the intersection-facing side (#9). The
            // anchor's local pose is identical in both art paths — dogs'
            // window-watching depends on it — and it intentionally keeps
            // this diagonal facing even though the kit model itself now
            // faces its driveway's road squarely (HouseFrontFacing);
            // fine-tuning the anchor to each kit model's actual wall is a
            // follow-up.
            var anchor = new GameObject("WindowAnchor").transform;
            anchor.SetParent(houseRoot.transform);
            var facing = new Vector3(-Mathf.Sign(lot.Position.X), 0f, -Mathf.Sign(lot.Position.Z)).normalized;
            anchor.localPosition = new Vector3(facing.x * 2.1f, 1.5f, facing.z * 2.1f);
            anchor.localRotation = Quaternion.LookRotation(facing, Vector3.up);
            view.WindowAnchor = anchor;

            var model = ForcePrimitiveFallback
                ? null
                : Resources.Load<GameObject>(HouseModelResourcePath(house.Id));
            if (model != null)
            {
                BuildHouseModel(houseRoot, house.Id, model, HouseFrontFacing(lot));
                return;
            }

            var style = HouseStyleTable.ForHouse(house.Id);
            var walls = GameObject.CreatePrimitive(PrimitiveType.Cube);
            walls.name = "Walls";
            walls.transform.SetParent(houseRoot.transform);
            walls.transform.localScale = new Vector3(4f, 2.5f, 4f);
            walls.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            Paint(walls, style.WallColorHex);

            BuildRoof(houseRoot.transform, style);

            if (style.HasPorch)
            {
                BuildPorch(houseRoot.transform, lot, style);
            }
        }

        /// <summary>
        /// The direction a house model's front should face (Derek's Editor
        /// feedback on the first kit pass: diagonal toward-origin yaws
        /// looked scattered): squarely toward the road the lot's driveway
        /// connects to. The WalkNetwork's DrivewayStub edge for the lot IS
        /// that association — its far endpoint is the sidewalk attach
        /// point, and for this map's axis-aligned roads the stub runs
        /// exactly cardinal (the lot projects perpendicularly onto the
        /// sidewalk); snapping to the dominant axis is just defensive. If
        /// a lot ever had no stub, fall back to squarely facing the
        /// east-west road (north-side houses face south and vice versa —
        /// the classic street-front look).
        /// </summary>
        private static Vector3 HouseFrontFacing(HouseLot lot)
        {
            foreach (var edge in NeighborhoodLayout.WalkNetwork.Edges)
            {
                if (edge.Kind != WalkEdgeKind.DrivewayStub
                    || (!edge.A.Equals(lot.Position) && !edge.B.Equals(lot.Position)))
                {
                    continue;
                }

                var attach = edge.Other(lot.Position);
                var dx = attach.X - lot.Position.X;
                var dz = attach.Z - lot.Position.Z;
                return Mathf.Abs(dx) >= Mathf.Abs(dz)
                    ? new Vector3(Mathf.Sign(dx), 0f, 0f)
                    : new Vector3(0f, 0f, Mathf.Sign(dz));
            }

            return new Vector3(0f, 0f, -Mathf.Sign(lot.Position.Z));
        }

        /// <summary>
        /// The house as its mapped City Kit Suburban model (#122): placed
        /// directly on the lot (the models have ground-level pivots),
        /// uniformly scaled so the model's max horizontal footprint lands
        /// on HouseTargetFootprint, and yawed squarely toward the road its
        /// driveway connects to (see HouseFrontFacing) plus the art-side
        /// HouseModelYawOffsetDegrees correction. The imported FBX carries
        /// no collider, so a BoxCollider fitted to the combined renderer
        /// bounds goes on the HouseView object to keep tap interaction
        /// (TapRouter raycasts, then GetComponentInParent) working. None of
        /// the primitive walls/roof/porch are built in this path.
        /// </summary>
        private static void BuildHouseModel(GameObject houseRoot, int houseId, GameObject model, Vector3 facing)
        {
            var maxFootprint = HouseModelCatalog.ForHouse(houseId).MaxFootprint;

            var visual = Object.Instantiate(model, houseRoot.transform);
            visual.name = "Model";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.LookRotation(facing, Vector3.up)
                * Quaternion.Euler(0f, HouseModelYawOffsetDegrees, 0f);
            visual.transform.localScale = Vector3.one * (HouseTargetFootprint / maxFootprint);

            AddFittedTapCollider(houseRoot, visual);
        }

        private static void AddFittedTapCollider(GameObject houseRoot, GameObject visual)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            // houseRoot has identity rotation and unit scale, so the
            // world-space AABB converts to local space by translation only.
            var collider = houseRoot.AddComponent<BoxCollider>();
            collider.center = houseRoot.transform.InverseTransformPoint(bounds.center);
            collider.size = bounds.size;
        }

        private static void BuildRoof(Transform houseRoot, HouseStyle style)
        {
            switch (style.RoofShape)
            {
                case RoofShape.Gable:
                    // Diamond prism: a cube rotated 45° reads as a peaked roof.
                    AddRoofBlock(houseRoot, new Vector3(2.9f, 2.9f, 4.4f), new Vector3(0f, 2.5f, 0f),
                        Quaternion.Euler(0f, 0f, 45f), style.RoofColorHex);
                    break;
                case RoofShape.Hip:
                    AddRoofBlock(houseRoot, new Vector3(3f, 1f, 3f), new Vector3(0f, 3f, 0f),
                        Quaternion.identity, style.RoofColorHex);
                    break;
                case RoofShape.Gambrel:
                    AddRoofBlock(houseRoot, new Vector3(4.2f, 0.8f, 4.2f), new Vector3(0f, 2.9f, 0f),
                        Quaternion.identity, style.RoofColorHex);
                    AddRoofBlock(houseRoot, new Vector3(2.6f, 0.8f, 2.6f), new Vector3(0f, 3.7f, 0f),
                        Quaternion.identity, style.RoofColorHex);
                    break;
                case RoofShape.Shed:
                    AddRoofBlock(houseRoot, new Vector3(4.4f, 0.4f, 4.6f), new Vector3(0f, 3f, 0f),
                        Quaternion.Euler(12f, 0f, 0f), style.RoofColorHex);
                    break;
            }
        }

        private static void AddRoofBlock(Transform houseRoot, Vector3 scale, Vector3 localPosition,
            Quaternion localRotation, string colorHex)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "Roof";
            block.transform.SetParent(houseRoot);
            block.transform.localScale = scale;
            block.transform.localPosition = localPosition;
            block.transform.localRotation = localRotation;
            Paint(block, colorHex);
        }

        private static void BuildPorch(Transform houseRoot, HouseLot lot, HouseStyle style)
        {
            // The porch faces the intersection at the world origin.
            var toCenter = new Vector3(-Mathf.Sign(lot.Position.X), 0f, -Mathf.Sign(lot.Position.Z)) * 0.5f;

            var deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deck.name = "Porch";
            deck.transform.SetParent(houseRoot);
            deck.transform.localScale = new Vector3(2.4f, 0.25f, 1.6f);
            deck.transform.localPosition = new Vector3(toCenter.x * 5.6f, 0.125f, toCenter.z * 5.6f);
            Paint(deck, Palette.SidewalkHex);
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
