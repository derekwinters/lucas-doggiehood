using Doggiehood.Core.Art;
using Doggiehood.Core.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Builds the graybox starting neighborhood from Core data (#7, #38,
    /// #39, #64): ground, two streets, four styled houses, and the fixed
    /// daytime sun. Geometry is Unity primitives colored from the palette —
    /// placeholder art until real low-poly models land (#6) — but all
    /// positions, counts, styles, and lighting values come from Core, so
    /// swapping in real models later doesn't change any logic.
    /// </summary>
    public static class WorldBuilder
    {
        public const string RootName = "Neighborhood";
        public const string HouseNamePrefix = "House ";
        public const string StreetNamePrefix = "Street - ";
        public const string SunName = "Sun";
        public const float GroundExtent = 30f;

        public static GameObject Build(GameState state)
        {
            var root = new GameObject(RootName);

            BuildGround(root.transform);
            foreach (var street in NeighborhoodLayout.Streets)
            {
                BuildStreet(root.transform, street);
            }

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

        private static void BuildStreet(Transform parent, Street street)
        {
            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = StreetNamePrefix + street.Name;
            strip.transform.SetParent(parent);

            var length = GroundExtent * 2f;
            strip.transform.localScale = street.Orientation == StreetOrientation.NorthSouth
                ? new Vector3(NeighborhoodLayout.StreetWidth, 0.1f, length)
                : new Vector3(length, 0.1f, NeighborhoodLayout.StreetWidth);
            strip.transform.position = new Vector3(0f, 0.05f, 0f);
            Paint(strip, Palette.StreetHex);
        }

        private static void BuildHouse(Transform parent, House house)
        {
            var lot = NeighborhoodLayout.GetHouseLot(house.Id);
            var style = HouseStyleTable.ForHouse(house.Id);

            var houseRoot = new GameObject(HouseNamePrefix + house.Id);
            houseRoot.transform.SetParent(parent);
            houseRoot.transform.position = new Vector3(lot.Position.X, 0f, lot.Position.Z);
            houseRoot.AddComponent<HouseView>().Init(house.Id);

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
