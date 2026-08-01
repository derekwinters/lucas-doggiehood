using System.Reflection;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #464: the off-screen portrait rig. A dedicated, disabled
    /// <see cref="Camera"/> that renders a supplied (tinted) model into a
    /// <see cref="RenderTexture"/> only when <see cref="PortraitCamera.Capture"/>
    /// is called — never every frame (Derek's snapshot-on-open performance
    /// rationale). Verified headless (-batchmode -nographics), matching the
    /// <see cref="CameraRigTests"/> precedent for camera + RenderTexture under CI.
    /// </summary>
    public class PortraitCameraTests
    {
        private GameObject rigObject;
        private PortraitCamera rig;

        [SetUp]
        public void CreateRig()
        {
            rigObject = new GameObject("portrait-rig", typeof(Camera));
            rig = rigObject.AddComponent<PortraitCamera>();
            rig.Init();
        }

        [TearDown]
        public void DestroyRig()
        {
            Object.DestroyImmediate(rigObject);
        }

        [Test]
        public void Capture_AllocatesAndAssignsARenderTexture_OnRequest()
        {
            // Capture always allocates+returns the snapshot texture and counts
            // one one-shot capture; the actual GPU Camera.Render() is guarded
            // inside Capture on graphics-device availability, so this holds on
            // the headless CI agent (graphicsDeviceType == Null) without ever
            // reaching a Render() that would SIGSEGV there. On a real device the
            // same call renders the model into that texture.
            var subject = GameObject.CreatePrimitive(PrimitiveType.Cube);

            var texture = rig.Capture(subject);

            Assert.That(texture, Is.Not.Null, "Capture returns the snapshot RenderTexture");
            Assert.That(texture.width, Is.EqualTo(PortraitCamera.TextureSizePx));
            Assert.That(texture.height, Is.EqualTo(PortraitCamera.TextureSizePx));
            Assert.That(rig.RenderCount, Is.EqualTo(1), "exactly one one-shot capture, on request");

            texture.Release();
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void Camera_IsDisabled_AndHasNoPerFrameTick_SoItRendersOnlyOnRequest()
        {
            // Derek's rationale: capture once on overlay-open, not live every
            // frame. A disabled camera is never in Unity's auto-render loop, and
            // the component defines no Update/LateUpdate that could call Render.
            Assert.That(rig.Camera.enabled, Is.False,
                "the portrait camera is disabled so Unity never auto-renders it every frame");

            const BindingFlags any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Assert.That(typeof(PortraitCamera).GetMethod("Update", any), Is.Null,
                "no Update tick — snapshots are one-shot per Capture");
            Assert.That(typeof(PortraitCamera).GetMethod("LateUpdate", any), Is.Null,
                "no LateUpdate tick either");
        }

        [Test]
        public void CullingMask_IsADedicatedLayer_NoBuiltWorldRendererShares()
        {
            // Rule #6: verify the dedicated portrait layer against real project
            // state rather than assuming it. The camera culls to exactly the
            // portrait layer, and a freshly built world puts no renderer on it,
            // so a snapshot sees only the staged subject.
            Assert.That(rig.Camera.cullingMask, Is.EqualTo(1 << PortraitCamera.PortraitLayer));

            var world = WorldBuilder.Build(Doggiehood.Core.World.GameState.CreateNew());
            try
            {
                foreach (var renderer in world.GetComponentsInChildren<Renderer>())
                {
                    Assert.That(renderer.gameObject.layer, Is.Not.EqualTo(PortraitCamera.PortraitLayer),
                        $"world renderer {renderer.name} must not share the portrait layer");
                }
            }
            finally
            {
                Object.DestroyImmediate(world);
            }
        }
    }
}
