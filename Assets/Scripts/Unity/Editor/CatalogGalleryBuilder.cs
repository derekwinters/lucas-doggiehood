using Doggiehood.Core.Art;
using Doggiehood.Core.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Doggiehood.Unity.Editor
{
    /// <summary>
    /// #126: procedurally builds the house-catalog authoring gallery —
    /// every HouseModelCatalog model in a row, annotated with its door
    /// marker, walkway placeholder, and real backyard fence outline
    /// (#146). All numbers come
    /// from Core's CatalogGalleryLayout (which itself only calls the APIs
    /// the game path uses), so what Derek sees is exactly what the game
    /// would do with the authored catalog values.
    ///
    /// Editor-only by construction: this class lives in the Editor
    /// assembly, the gallery scene is never in the build list, and every
    /// generated object carries DontSaveInEditor so even saving the scene
    /// can't serialize content into it. Zero APK cost.
    /// </summary>
    public static class CatalogGalleryBuilder
    {
        public const string ScenePath = "Assets/Scenes/CatalogGallery.unity";
        public const string RootName = "CatalogGallery";
        public const string DoorMarkerName = "DoorMarker";
        public const string WalkwayName = "WalkwayPlaceholder";
        public const string FenceName = "BackyardFence";

        /// <summary>Row spacing: at the fixed ×7 kit scale (#145) the
        /// widest model (building-type-b) is 12.80m wide, so 16m keeps a
        /// clear gap between entries and footprint differences read side
        /// by side.</summary>
        public const float EntrySpacing = 16f;

        [MenuItem("Doggiehood/Build Catalog Gallery")]
        public static void OpenSceneAndBuild()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Build();
        }

        /// <summary>Builds (or rebuilds) the gallery in the currently open
        /// scene and returns its root. Split from the menu entry so
        /// EditMode tests can run it without switching scenes.</summary>
        public static GameObject Build()
        {
            var existing = GameObject.Find(RootName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var root = new GameObject(RootName);

            var entries = CatalogGalleryLayout.Compute(WorldBuilder.HouseKitScale, EntrySpacing);

            BuildGround(root.transform, entries.Count);
            BuildLight(root.transform);
            foreach (var entry in entries)
            {
                BuildEntry(root.transform, entry);
            }

            MarkGeneratedContentDontSave(root);
            return root;
        }

        private static void BuildEntry(Transform parent, CatalogGalleryEntry entry)
        {
            var model = entry.Model;
            // Label numbers are all MODEL-LOCAL authored values (matching
            // the footprint pair): the door is the 2D local point Derek
            // measured in gallery pass 1, not the scaled world offset.
            var container = new GameObject(
                model.ModelName
                + " — " + model.FootprintX.ToString("0.000")
                + " x " + model.FootprintZ.ToString("0.000")
                + ", door (" + model.FrontDoorLocalX.ToString("0.000")
                + ", " + model.FrontDoorLocalZ.ToString("0.000") + ")");
            container.transform.SetParent(parent);
            container.transform.position = new Vector3(entry.Position.X, 0f, entry.Position.Z);

            BuildModelVisual(container.transform, entry);
            BuildDoorMarker(container.transform, entry);
            BuildWalkwayPlaceholder(container.transform, entry);
            BuildBackyardFence(container.transform, entry);
        }

        /// <summary>The model exactly as the game would place it: same
        /// Resources key, same uniform scale rule, and the Core entry's yaw
        /// passed straight through (HouseModel documents gallery/game yaw
        /// as the model transform's yaw). Falls back to a footprint-sized
        /// graybox cube when the kit assets aren't importable — the same
        /// degradation WorldBuilder has.</summary>
        private static void BuildModelVisual(Transform container, CatalogGalleryEntry entry)
        {
            var prefab = WorldBuilder.ForcePrimitiveFallback
                ? null
                : Resources.Load<GameObject>(entry.Model.ModelName);

            if (prefab != null)
            {
                var visual = Object.Instantiate(prefab, container);
                visual.name = "Model";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.Euler(0f, entry.YawDegrees, 0f);
                visual.transform.localScale = Vector3.one * entry.UniformScale;
                return;
            }

            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Model";
            box.transform.SetParent(container);
            box.transform.localScale = new Vector3(
                entry.UniformScale * entry.Model.FootprintX, 2.5f,
                entry.UniformScale * entry.Model.FootprintZ);
            box.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            box.transform.localRotation = Quaternion.Euler(0f, entry.YawDegrees, 0f);
        }

        private static void BuildDoorMarker(Transform container, CatalogGalleryEntry entry)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = DoorMarkerName;
            marker.transform.SetParent(container);
            marker.transform.localScale = Vector3.one * 0.5f;
            marker.transform.position = new Vector3(entry.DoorPosition.X, 0.25f, entry.DoorPosition.Z);
            Tint(marker, new Color(0.9f, 0.15f, 0.15f));
        }

        private static void BuildWalkwayPlaceholder(Transform container, CatalogGalleryEntry entry)
        {
            var walkway = GameObject.CreatePrimitive(PrimitiveType.Cube);
            walkway.name = WalkwayName;
            walkway.transform.SetParent(container);
            walkway.transform.position = new Vector3(
                (entry.WalkwayStart.X + entry.WalkwayEnd.X) / 2f, 0.025f,
                (entry.WalkwayStart.Z + entry.WalkwayEnd.Z) / 2f);
            walkway.transform.localScale = new Vector3(
                0.4f, 0.05f, entry.WalkwayStart.Z - entry.WalkwayEnd.Z);
            Tint(walkway, CoreColors.FromHex(Palette.SidewalkHex));
        }

        /// <summary>The model's REAL backyard fence outline (#146): one
        /// thin rail per Core FenceRun (the entry's FenceRuns come from
        /// LotFence.BackyardRuns, the exact API the game path uses —
        /// side-wall midpoint anchors wrapping the back yard, front open),
        /// at the real in-game fence height (FenceTiling), so Derek can
        /// judge each model's enclosure. Replaces the old
        /// authored-footprint-outline placeholder — the rendered model
        /// itself shows the silhouette now.</summary>
        private static void BuildBackyardFence(Transform container, CatalogGalleryEntry entry)
        {
            var fence = new GameObject(FenceName);
            fence.transform.SetParent(container);
            fence.transform.position = Vector3.zero;

            var height = FenceTiling.Scale * FenceTiling.PieceModelHeight;
            for (var i = 0; i < entry.FenceRuns.Count; i++)
            {
                var run = entry.FenceRuns[i];
                var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = "Rail " + i;
                rail.transform.SetParent(fence);
                rail.transform.position = new Vector3(
                    (run.A.X + run.B.X) / 2f, height / 2f, (run.A.Z + run.B.Z) / 2f);

                // Runs are axis-aligned at gallery yaw 0.
                var alongX = Mathf.Abs(run.A.Z - run.B.Z) < 0.001f;
                rail.transform.localScale = alongX
                    ? new Vector3(run.Length, height, 0.15f)
                    : new Vector3(0.15f, height, run.Length);
                Tint(rail, new Color(0.55f, 0.4f, 0.25f));
            }
        }

        private static void BuildGround(Transform parent, int entryCount)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(parent);
            var rowLength = entryCount * EntrySpacing;
            // A default plane is 10x10m at scale 1; center it on the row.
            ground.transform.localScale = new Vector3(rowLength / 10f + 1f, 1f, 3f);
            ground.transform.position = new Vector3((entryCount - 1) * EntrySpacing / 2f, -0.02f, 0f);
            Tint(ground, CoreColors.FromHex(Palette.GrassHex));
        }

        /// <summary>Same sun the game builds (LightingPreset), so models
        /// read in the gallery exactly as they do in the neighborhood.</summary>
        private static void BuildLight(Transform parent)
        {
            var sun = new GameObject("GalleryLight");
            sun.transform.SetParent(parent);
            sun.transform.rotation = Quaternion.Euler(LightingPreset.SunPitchDegrees, LightingPreset.SunYawDegrees, 0f);

            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = LightingPreset.SunIntensity;
            light.color = CoreColors.FromHex(LightingPreset.SunColorHex);
        }

        /// <summary>Every generated object gets DontSaveInEditor: the
        /// gallery scene file must stay empty even if someone hits Ctrl+S
        /// with the gallery built.</summary>
        private static void MarkGeneratedContentDontSave(GameObject root)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                transform.gameObject.hideFlags |= HideFlags.DontSaveInEditor;
            }
        }

        private static void Tint(GameObject target, Color color)
        {
            var material = new Material(Shader.Find("Standard")) { color = color };
            target.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
