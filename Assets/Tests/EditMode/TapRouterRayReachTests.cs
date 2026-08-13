using Doggiehood.Core.Cameras;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #680: how far a tap ray may travel is not a tunable number — it is "as
    /// far as the camera can see". <see cref="TapRouter"/> used to raycast to a
    /// fixed <c>MaxRayDistance = 1000f</c>, while the distance from the lens to
    /// the far edge of the frame is <c>RigDistanceFor(zoom) +
    /// GroundDepthReach(zoom)</c> — which grows with the zoom, and the max
    /// zoom-out grows with the map (#510/#524). Past a map roughly 940 m across
    /// the top of the screen sat beyond 1000 m, so taps near the top would
    /// silently stop registering while taps lower down still worked: the same
    /// fixed-constant-vs-map-scaled-zoom shape as #679.
    ///
    /// The reach is now the camera's own <c>farClipPlane</c>, which #679 already
    /// derives from the live zoom and holds a named <c>RigDistance</c> margin
    /// past the far edge of the visible ground — so anything the player can see
    /// is inside the ray's reach by construction, at every map size.
    /// </summary>
    public class TapRouterRayReachTests
    {
        /// <summary>The reach <see cref="TapRouter"/> used to be pinned to
        /// (#680). Kept here, in the test only, as the threshold the grown-map
        /// case must exceed and the playtest-scale case must stay under.</summary>
        private const float ObsoleteMaxRayDistanceMeters = 1000f;

        /// <summary>A north-south run of FourWay tiles roughly twice the width
        /// of today's playtest neighborhood (20 × <c>TileSize</c> = 1200 m), so
        /// the far edge of the frame at max zoom-out sits well beyond
        /// <see cref="ObsoleteMaxRayDistanceMeters"/>.</summary>
        private const int GrownMapTilesAcross = 20;

        /// <summary>A run at roughly today's playtest scale (10 ×
        /// <c>TileSize</c> = 600 m), where the far edge of the frame still sits
        /// inside the old fixed reach — the regression guard that widening the
        /// reach changes nothing at present map sizes.</summary>
        private const int PlaytestMapTilesAcross = 10;

        private const int RenderTargetWidthPixels = 1920;
        private const int RenderTargetHeightPixels = 1080;

        /// <summary>How far along the visible ground slab the target sits, as a
        /// fraction of the reach to the frame's far edge. Just short of 1.0 so
        /// the target is unambiguously on-screen (and its screen point inside
        /// the pixel rect) while still being a tap near the top of the
        /// screen.</summary>
        private const float FarFrameEdgeInset = 0.98f;

        /// <summary>The screen fraction above which a tap counts as "near the
        /// top of the screen" — the region that went dead before #680.</summary>
        private const float TopOfScreenFraction = 0.9f;

        /// <summary>A thin slab facing the lens, so the raycast hit lands at the
        /// target's own view depth rather than somewhere on a deep solid.</summary>
        private const float TargetSideMeters = 40f;
        private const float TargetThicknessMeters = 1f;

        private sealed class CountingInteractable : MonoBehaviour, IInteractable
        {
            public int TapCount { get; private set; }

            public void OnTapped()
            {
                TapCount++;
            }
        }

        private GameObject rigObject;
        private CameraRig rig;
        private Camera cam;
        private RenderTexture texture;
        private GameObject targetObject;

        [SetUp]
        public void SetUp()
        {
            // Both TapRouter guards are process-global seams; pin them to "no UI
            // in the way" so these tests isolate the ray's reach.
            TapRouter.IsPointerOverUi = _ => false;
            TapRouter.IsModalOpen = () => false;
            ModalInputGate.Shared.Clear();

            rigObject = new GameObject("ray-reach-rig", typeof(Camera));
            cam = rigObject.GetComponent<Camera>();
            rig = rigObject.AddComponent<CameraRig>();
            rig.ApplyConfiguration();

            // A real pixel rect so ScreenPointToRay/WorldToScreenPoint work headless.
            texture = new RenderTexture(RenderTargetWidthPixels, RenderTargetHeightPixels, 0);
            cam.targetTexture = texture;
        }

        [TearDown]
        public void TearDown()
        {
            TapRouter.IsPointerOverUi = TapRouter.DefaultIsPointerOverUi;
            TapRouter.IsModalOpen = TapRouter.DefaultIsModalOpen;
            ModalInputGate.Shared.Clear();

            cam.targetTexture = null;
            Object.DestroyImmediate(texture);
            if (targetObject != null)
            {
                Object.DestroyImmediate(targetObject);
                targetObject = null;
            }

            Object.DestroyImmediate(rigObject);
        }

        [Test]
        public void OnAGrownMap_ATapAtTheTopOfTheScreen_StillReachesTheFarEdgeOfTheFrame()
        {
            // #680 core: with the reach pinned to 1000 m this tap missed
            // outright — the collider is visible on screen but further away than
            // the ray was allowed to travel.
            ZoomOutOverAMapOf(GrownMapTilesAcross);
            var target = PlaceTargetNearTheFarEdgeOfTheFrame();
            float depth = cam.WorldToViewportPoint(targetObject.transform.position).z;

            Assert.That(depth, Is.GreaterThan(ObsoleteMaxRayDistanceMeters),
                "the map must be large enough that the far edge of the frame is beyond the old fixed reach, or this proves nothing");
            Assert.That(cam.farClipPlane, Is.GreaterThan(depth),
                "#679 keeps everything visible inside the far clip plane — which is what makes it the correct tap reach");

            var tap = TapPointFor(targetObject.transform.position);
            Assert.That(tap.y, Is.GreaterThan(cam.pixelHeight * TopOfScreenFraction),
                "this must be a tap near the top of the screen — the region that went dead");

            rig.HandleTap(tap);

            Assert.That(target.TapCount, Is.EqualTo(1),
                "a tap on a visible collider at the far edge of the frame must register, however far the map has grown");
        }

        [Test]
        public void AtTodaysPlaytestMapScale_TheSameTap_BehavesExactlyAsBefore()
        {
            // Regression guard: at present map sizes the far edge of the frame is
            // still well inside the old 1000 m constant, so widening the reach to
            // the far clip plane cannot have changed anything the player sees today.
            ZoomOutOverAMapOf(PlaytestMapTilesAcross);
            var target = PlaceTargetNearTheFarEdgeOfTheFrame();
            float depth = cam.WorldToViewportPoint(targetObject.transform.position).z;

            Assert.That(depth, Is.LessThan(ObsoleteMaxRayDistanceMeters),
                "at today's map scale the far edge of the frame is inside the old reach, so this case is a no-change guard");

            rig.HandleTap(TapPointFor(targetObject.transform.position));

            Assert.That(target.TapCount, Is.EqualTo(1),
                "the tap registers at today's map scale exactly as it did before the reach was widened");
        }

        [Test]
        public void ATapThatHitsNothing_StillResolvesToTheNoHitPath()
        {
            // Widening the reach must not change miss behaviour: with nothing in
            // the world, RouteTap reports the tap unhandled rather than throwing
            // or claiming a hit.
            ZoomOutOverAMapOf(GrownMapTilesAcross);

            var handled = TapRouter.RouteTap(
                cam, new Vector2(cam.pixelWidth / 2f, cam.pixelHeight / 2f));

            Assert.That(handled, Is.False,
                "a tap with no collider anywhere along the ray must resolve cleanly to the no-hit path");
        }

        /// <summary>Grows the map to <paramref name="tilesAcross"/> FourWay
        /// tiles in a north-south run — the same map-driven path a zone unlock
        /// takes — and zooms out to the cap that map produces.</summary>
        private void ZoomOutOverAMapOf(int tilesAcross)
        {
            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            for (int north = 1; north < tilesAcross; north++)
            {
                map.Place(new TileCoordinate(0, north), TileType.FourWay);
            }

            rig.Controller.RecomputeBoundsFromMap(map);
            rig.Controller.ZoomBy(rig.Controller.MaxZoom - rig.Controller.Zoom);
            rig.ApplyConfiguration();
        }

        /// <summary>Puts a tappable collider just inside the far edge of the
        /// visible ground slab: <c>zoom / sin(pitch)</c> metres past the focus
        /// point along the camera's ground heading, which is the ground that
        /// lands along the top edge of the frame.</summary>
        private CountingInteractable PlaceTargetNearTheFarEdgeOfTheFrame()
        {
            var focus = new Vector3(rig.Controller.Position.X, 0f, rig.Controller.Position.Z);
            var groundHeading = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            float groundReach =
                rig.Controller.Zoom / Mathf.Sin(CameraRigConfig.PitchDegrees * Mathf.Deg2Rad);

            targetObject = new GameObject("far-edge-target", typeof(BoxCollider));
            targetObject.transform.position = focus + groundHeading * (groundReach * FarFrameEdgeInset);
            targetObject.transform.rotation = Quaternion.LookRotation(-cam.transform.forward);
            targetObject.transform.localScale =
                new Vector3(TargetSideMeters, TargetSideMeters, TargetThicknessMeters);
            var interactable = targetObject.AddComponent<CountingInteractable>();
            Physics.SyncTransforms();
            return interactable;
        }

        private Vector2 TapPointFor(Vector3 worldPosition)
        {
            var point = cam.WorldToScreenPoint(worldPosition);
            return new Vector2(point.x, point.y);
        }
    }
}
