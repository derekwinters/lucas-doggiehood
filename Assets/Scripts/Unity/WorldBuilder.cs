using System.Linq;
using Doggiehood.Core.Art;
using Doggiehood.Core.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Builds the graybox starting neighborhood from Core data (#7, #38,
    /// #39, #64, #106): ground, roads with a symmetric grass verge and
    /// sidewalk on both sides, the intersection's crosswalk box, four
    /// styled houses, and the fixed daytime sun. Geometry is Unity
    /// primitives colored from the palette — placeholder art until real
    /// low-poly models land (#6) — but all positions, counts, styles, and
    /// lighting values come from Core, so swapping in real models later
    /// doesn't change any logic.
    /// </summary>
    public static class WorldBuilder
    {
        public const string RootName = "Neighborhood";
        public const string HouseNamePrefix = "House ";
        public const string RoadNamePrefix = "Road - ";
        public const string VergeNamePrefix = "Verge - ";
        public const string SidewalkNamePrefix = "Sidewalk - ";
        public const string CrosswalkNamePrefix = "Crosswalk - ";
        public const string SunName = "Sun";
        public const float GroundExtent = 30f;

        public static GameObject Build(GameState state)
        {
            var root = new GameObject(RootName);

            BuildGround(root.transform);
            foreach (var road in NeighborhoodLayout.Roads)
            {
                BuildRoad(root.transform, road);
            }

            BuildCrosswalks(root.transform);

            foreach (var house in state.Houses)
            {
                BuildHouse(root.transform, house);
            }

            BuildSun(root.transform);
            ApplyAmbientLighting();

            return root;
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

        /// <summary>Road surface plus a grass verge and sidewalk on both
        /// sides (#106), all sized from Road/Sidewalk — which are in turn
        /// built purely from the locked #105 WorldDimensions constants.</summary>
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
                var vergeOffset = Mathf.Sign(sidewalk.CenterOffset) * (road.Width / 2f + sidewalk.VergeWidth / 2f);
                BuildStripArms(parent, road, vergeOffset, sidewalk.VergeWidth, isNorthSouth,
                    VergeNamePrefix + road.Orientation + " " + sidewalk.Side, Palette.GrassVergeHex, 0.06f);
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
        /// edges, but visually clipped to just the road and its two grass
        /// verges (RoadWidth + 2 * GrassVergeWidth) rather than the edge's
        /// full sidewalk-center-to-sidewalk-center length. The WalkNetwork
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
            var style = HouseStyleTable.ForHouse(house.Id);

            var houseRoot = new GameObject(HouseNamePrefix + house.Id);
            houseRoot.transform.SetParent(parent);
            houseRoot.transform.position = new Vector3(lot.Position.X, 0f, lot.Position.Z);
            var view = houseRoot.AddComponent<HouseView>();
            view.Init(house.Id);

            // Window anchor on the wall facing the intersection (#9).
            var anchor = new GameObject("WindowAnchor").transform;
            anchor.SetParent(houseRoot.transform);
            var facing = new Vector3(-Mathf.Sign(lot.Position.X), 0f, -Mathf.Sign(lot.Position.Z)).normalized;
            anchor.localPosition = new Vector3(facing.x * 2.1f, 1.5f, facing.z * 2.1f);
            anchor.localRotation = Quaternion.LookRotation(facing, Vector3.up);
            view.WindowAnchor = anchor;

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
