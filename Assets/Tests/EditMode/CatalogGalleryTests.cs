using System.IO;
using System.Linq;
using Doggiehood.Core.World;
using Doggiehood.Unity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// The #126 editor-only catalog gallery: a procedurally built authoring
    /// scene whose annotations come from the same Core layout math the game
    /// uses, and which can never cost APK size (the scene ships empty and
    /// is never in the build list; the content is generated with
    /// DontSaveInEditor flags).
    /// </summary>
    public class CatalogGalleryTests
    {
        private GameObject root;

        [TearDown]
        public void DestroyGallery()
        {
            WorldBuilder.ForcePrimitiveFallback = false;
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Build_PlacesOneAnnotatedEntryPerCatalogRow()
        {
            root = CatalogGalleryBuilder.Build();

            var entries = root.transform.Cast<Transform>()
                .Where(t => t.GetComponent<Light>() == null && t.name != "Ground")
                .ToList();

            Assert.That(entries.Count, Is.EqualTo(HouseModelCatalog.Models.Count));

            foreach (var model in HouseModelCatalog.Models)
            {
                var entry = entries.SingleOrDefault(e => e.name.StartsWith(model.ModelName));
                Assert.That(entry, Is.Not.Null, $"no gallery entry for {model.ModelName}");

                // The label carries the authored numbers Derek is refining.
                Assert.That(entry.name, Does.Contain(model.FootprintX.ToString("0.000")));
                Assert.That(entry.name, Does.Contain(model.FootprintZ.ToString("0.000")));

                Assert.That(entry.Find("Model"), Is.Not.Null, model.ModelName + " model visual");
                Assert.That(entry.Find(CatalogGalleryBuilder.DoorMarkerName), Is.Not.Null,
                    model.ModelName + " door marker");
                Assert.That(entry.Find(CatalogGalleryBuilder.WalkwayName), Is.Not.Null,
                    model.ModelName + " walkway placeholder");
                Assert.That(entry.Find(CatalogGalleryBuilder.FenceName), Is.Not.Null,
                    model.ModelName + " backyard fence outline");
            }
        }

        [Test]
        public void Build_WithPrimitiveFallback_StillPlacesEveryAnnotatedEntry()
        {
            WorldBuilder.ForcePrimitiveFallback = true;

            root = CatalogGalleryBuilder.Build();

            var entries = root.transform.Cast<Transform>()
                .Where(t => t.name.StartsWith("building-"))
                .ToList();
            Assert.That(entries.Count, Is.EqualTo(HouseModelCatalog.Models.Count));
            foreach (var entry in entries)
            {
                Assert.That(entry.Find("Model"), Is.Not.Null, entry.name);
                Assert.That(entry.Find(CatalogGalleryBuilder.DoorMarkerName), Is.Not.Null, entry.name);
            }
        }

        [Test]
        public void Build_DoorMarkers_SitExactlyWhereTheCoreGamePathApiSaysTheDoorIs()
        {
            // Same-API guardrail: the rendered marker must match
            // HouseModel.FrontDoorWorldPosition for the entry's placement —
            // the gallery renders Core numbers, it does not re-derive them.
            root = CatalogGalleryBuilder.Build();

            var layout = CatalogGalleryLayout.Compute(
                WorldBuilder.HouseKitScale, CatalogGalleryBuilder.EntrySpacing);

            foreach (var entry in layout)
            {
                var container = root.transform.Cast<Transform>()
                    .Single(t => t.name.StartsWith(entry.Model.ModelName));
                var marker = container.Find(CatalogGalleryBuilder.DoorMarkerName);

                var expected = entry.Model.FrontDoorWorldPosition(
                    entry.Position, entry.YawDegrees, entry.UniformScale);
                Assert.That(marker.position.x, Is.EqualTo(expected.X).Within(0.001f),
                    entry.Model.ModelName + " door X");
                Assert.That(marker.position.z, Is.EqualTo(expected.Z).Within(0.001f),
                    entry.Model.ModelName + " door Z");
            }
        }

        [Test]
        public void Build_BackyardFenceOutline_RendersOneRailPerCoreFenceRun()
        {
            // #146: the gallery shows the model's REAL backyard fence
            // (side-wall midpoint anchors + rear line) straight from the
            // Core entry's FenceRuns — one rail per run, centered on the
            // run's midpoint. Same-API guardrail as the door marker: the
            // gallery renders Core numbers, it does not re-derive them.
            root = CatalogGalleryBuilder.Build();

            var layout = CatalogGalleryLayout.Compute(
                WorldBuilder.HouseKitScale, CatalogGalleryBuilder.EntrySpacing);

            foreach (var entry in layout)
            {
                var container = root.transform.Cast<Transform>()
                    .Single(t => t.name.StartsWith(entry.Model.ModelName));
                var fence = container.Find(CatalogGalleryBuilder.FenceName);
                Assert.That(fence, Is.Not.Null, entry.Model.ModelName + " fence outline");

                var rails = fence.Cast<Transform>().ToList();
                Assert.That(rails.Count, Is.EqualTo(entry.FenceRuns.Count),
                    entry.Model.ModelName + " one rail per Core fence run");

                for (var i = 0; i < rails.Count; i++)
                {
                    var run = entry.FenceRuns[i];
                    Assert.That(rails[i].position.x,
                        Is.EqualTo((run.A.X + run.B.X) / 2f).Within(0.001f),
                        entry.Model.ModelName + $" rail {i} X");
                    Assert.That(rails[i].position.z,
                        Is.EqualTo((run.A.Z + run.B.Z) / 2f).Within(0.001f),
                        entry.Model.ModelName + $" rail {i} Z");
                }
            }
        }

        [Test]
        public void Build_GeneratedContentIsNeverSavedIntoTheSceneFile()
        {
            // The scene file stays near-empty by construction: everything
            // the builder creates carries DontSaveInEditor, so even a
            // Ctrl+S in the Editor can't serialize gallery content.
            root = CatalogGalleryBuilder.Build();

            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(
                    (transform.gameObject.hideFlags & HideFlags.DontSaveInEditor),
                    Is.EqualTo(HideFlags.DontSaveInEditor),
                    transform.name + " must be DontSaveInEditor");
            }
        }

        [Test]
        public void Build_IsRepeatable_ReplacingThePreviousGallery()
        {
            root = CatalogGalleryBuilder.Build();
            root = CatalogGalleryBuilder.Build();

            var galleries = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(go => go.name == CatalogGalleryBuilder.RootName
                    && go.scene.IsValid())
                .ToList();
            Assert.That(galleries.Count, Is.EqualTo(1));
        }

        [Test]
        public void GalleryScene_ExistsOnDisk_AndContainsNoSerializedGameObjects()
        {
            Assert.That(File.Exists(CatalogGalleryBuilder.ScenePath), Is.True,
                CatalogGalleryBuilder.ScenePath + " missing");

            var sceneText = File.ReadAllText(CatalogGalleryBuilder.ScenePath);
            Assert.That(sceneText, Does.Not.Contain("GameObject:"),
                "CatalogGallery.unity must stay empty — its content is generated, never serialized");
        }

        [Test]
        public void GalleryScene_IsNeverInTheBuildSettings()
        {
            // Zero APK cost by construction: Main.unity stays the single
            // enabled build scene, and the gallery is never listed at all.
            var scenePaths = EditorBuildSettings.scenes.Select(s => s.path).ToList();

            Assert.That(scenePaths, Does.Not.Contain(CatalogGalleryBuilder.ScenePath));
            Assert.That(
                EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path),
                Is.EqualTo(new[] { "Assets/Scenes/Main.unity" }));
        }
    }
}
