using Doggiehood.Core.Art;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Prepares the tinted model a <see cref="PortraitCamera"/> snapshots for a
    /// profile box (#464): a house's ACTUAL current kit model (its variant +
    /// upgrade level + vacancy tint) or a dog's shared model tinted to its breed
    /// coat. Resolution and tinting reuse the SAME code the world render uses —
    /// the model name comes from the Core.Art <see cref="HouseModelResolver"/>
    /// and the house tint stack from <see cref="WorldBuilder.ApplyHouseTints"/> —
    /// so a portrait can never drift from the house standing in the world. When
    /// a kit model can't load (e.g. assets not staged), it falls back to a
    /// tinted primitive, mirroring WorldBuilder's own graybox fallback.
    /// </summary>
    internal static class PortraitSubjects
    {
        /// <summary>Shared Cube Pets model, the same Resources path
        /// <see cref="DogView"/> loads for every roster dog (#119).</summary>
        private const string DogModelResourcePath = "animal-dog";

        internal static GameObject ForHouse(House house)
        {
            var modelName = HouseModelResolver.ResolveModelName(house.Id, house.Level, house.Variant);
            var model = !WorldBuilder.ForcePrimitiveFallback && modelName != null
                ? Resources.Load<GameObject>(modelName)
                : null;

            if (model != null)
            {
                var visual = Object.Instantiate(model);
                visual.name = "PortraitHouse";
                WorldBuilder.ApplyHouseTints(visual, house);
                return visual;
            }

            // Graybox fallback, mirroring WorldBuilder.BuildHouse: a tinted cube
            // (vacancy grey while vacant, else the fallback wall color).
            var fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.name = "PortraitHouse";
            Paint(fallback, house.IsVacant ? Palette.VacantHouseTintHex : Palette.HouseFallbackHex);
            return fallback;
        }

        internal static GameObject ForDog(Dog dog)
        {
            var coat = BreedCoats.ForDog(dog);
            var model = WorldBuilder.ForcePrimitiveFallback
                ? null
                : Resources.Load<GameObject>(DogModelResourcePath);

            if (model != null)
            {
                var visual = Object.Instantiate(model);
                visual.name = "PortraitDog";
                PaintModel(visual, coat);
                return visual;
            }

            // Graybox fallback, mirroring DogView: a coat-tinted capsule.
            var fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            fallback.name = "PortraitDog";
            PaintModel(fallback, coat);
            return fallback;
        }

        private static void Paint(GameObject target, string colorHex)
        {
            var renderer = target.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Standard"));
            material.color = CoreColors.FromHex(colorHex);
            renderer.sharedMaterial = material;
        }

        private static void PaintModel(GameObject root, Color color)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                var material = renderer.sharedMaterial != null
                    ? new Material(renderer.sharedMaterial)
                    : new Material(Shader.Find("Standard"));
                material.color = color;
                renderer.sharedMaterial = material;
            }
        }
    }
}
