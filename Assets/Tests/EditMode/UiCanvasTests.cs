using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #256: every UI wireframe's constants are authored against a fixed
    /// 1920x1200 (16:10) tablet reference (see docs/specs/ui/index.md), so
    /// the UI canvas must scale from that reference — a `CanvasScaler` in
    /// Scale-With-Screen-Size mode at 1920x1200 makes a 64px chip / 96px
    /// button render at its intended size across tablet sizes.
    /// </summary>
    public class UiCanvasTests
    {
        private GameObject host;

        [SetUp]
        public void CreateHost()
        {
            host = new GameObject("ui-canvas-under-test");
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(host);
        }

        [Test]
        public void Configure_UsesScaleWithScreenSize_AtTheTabletReferenceResolution()
        {
            var uiCanvas = host.AddComponent<UiCanvas>();

            var scaler = uiCanvas.Configure();

            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize),
                "the UI canvas must Scale With Screen Size so px constants have a fixed meaning across tablets (#256)");
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1200f)),
                "the CanvasScaler reference resolution must be the 1920x1200 tablet reference (#256)");
        }

        [Test]
        public void Configure_RendersAsAScreenSpaceOverlayCanvas_WithARaycaster()
        {
            var uiCanvas = host.AddComponent<UiCanvas>();

            uiCanvas.Configure();

            var canvas = host.GetComponent<Canvas>();
            Assert.That(canvas, Is.Not.Null, "UiCanvas must carry a Canvas");
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(host.GetComponent<GraphicRaycaster>(), Is.Not.Null,
                "a screen-space UI canvas needs a GraphicRaycaster to receive taps");
        }

        [Test]
        public void ReferenceResolution_ExposesTheAuthoredTabletReference()
        {
            Assert.That(UiCanvas.ReferenceResolution, Is.EqualTo(new Vector2(1920f, 1200f)));
        }
    }
}
