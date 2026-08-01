using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Off-screen portrait rig (#464): a dedicated <see cref="Camera"/>, kept
    /// disabled so Unity never auto-renders it, that snapshots a supplied model
    /// into a fresh <see cref="RenderTexture"/> only when
    /// <see cref="Capture"/> is called. Used by the house / dog profile overlays
    /// to fill their thumbnail / portrait / avatar boxes with a render of the
    /// subject's ACTUAL current model (Derek's Option A on #464) — captured once
    /// on overlay-open, not live every frame.
    ///
    /// The subject is parented onto a fixed off-scene "portrait stage" (far from
    /// the world so nothing else falls in frame), assigned to a dedicated
    /// culling <see cref="PortraitLayer"/> the camera alone renders, framed to
    /// its renderer bounds, rendered once, then destroyed. A stage-local
    /// directional light — masked to the same layer so it never touches the
    /// world — lights the subject regardless of scene lighting.
    ///
    /// Thin wiring: it holds no game logic. Model resolution and tinting are
    /// prepared by <see cref="PortraitSubjects"/> (which resolves a house's
    /// current model via the Core.Art <see cref="Doggiehood.Core.Art.HouseModelResolver"/>);
    /// this component only renders whatever GameObject it is handed.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class PortraitCamera : MonoBehaviour
    {
        /// <summary>Dedicated culling layer for the staged subject. Layer 31 is
        /// an unnamed built-in user layer; the project defines no custom layers
        /// (no ProjectSettings/TagManager.asset) and assigns no layer to any
        /// world object, so nothing else renders on it — guarded by
        /// PortraitCameraTests.CullingMask_IsADedicatedLayer_NoBuiltWorldRendererShares.</summary>
        public const int PortraitLayer = 31;

        /// <summary>Square snapshot resolution; >= the 220 px thumbnail /
        /// portrait boxes it fills so the image never up-samples.</summary>
        public const int TextureSizePx = 256;

        // --- Rig geometry (no inline literals per #161) ---
        private const int DepthBufferBits = 16;                       // depth needed for correct 3D framing
        private static readonly Vector3 StagePosition = new Vector3(0f, -10000f, 0f); // far off-world
        private static readonly Vector3 CameraViewDirection = new Vector3(1f, 0.85f, -1.4f); // 3/4 view
        private static readonly Vector3 LightEuler = new Vector3(50f, -30f, 0f);       // stage key light
        private const float FramingRadiusMultiplier = 1.15f;          // padding around the subject
        private const float CameraDistanceMultiplier = 3f;            // ortho camera pull-back
        private const float FarClipMarginMultiplier = 4f;             // far plane beyond the subject
        private const float NearClipPlaneUnits = 0.01f;
        private const float MinSubjectRadiusUnits = 0.5f;             // floor for an empty/degenerate subject

        private static readonly Color StageBackgroundColor = new Color(0.749f, 0.890f, 0.949f, 1f); // matches the prior graybox fill

        private Camera cam;
        private Transform stage;

        /// <summary>How many snapshots this rig has rendered — one per
        /// <see cref="Capture"/>. Never advanced by a per-frame path (there is
        /// none), so it is exactly the number of on-request captures.</summary>
        public int RenderCount { get; private set; }

        /// <summary>The underlying camera (disabled — rendered only on request).</summary>
        public Camera Camera => cam;

        /// <summary>Configures the disabled off-screen camera, the far-off stage,
        /// and the stage light. Idempotent-friendly: safe to call once after the
        /// component is added.</summary>
        public void Init()
        {
            cam = GetComponent<Camera>();
            if (cam == null)
            {
                cam = gameObject.AddComponent<Camera>();
            }

            cam.enabled = false; // one-shot only — never in Unity's render loop
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = StageBackgroundColor;
            cam.cullingMask = 1 << PortraitLayer;
            cam.nearClipPlane = NearClipPlaneUnits;

            var stageObject = new GameObject("PortraitStage");
            stage = stageObject.transform;
            stage.position = StagePosition;

            var lightObject = new GameObject("PortraitLight");
            lightObject.transform.SetParent(stage, false);
            lightObject.transform.localRotation = Quaternion.Euler(LightEuler);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.cullingMask = 1 << PortraitLayer; // never illuminates the world
        }

        /// <summary>Renders <paramref name="subject"/> once into a fresh
        /// <see cref="RenderTexture"/> and returns it. The subject is staged on
        /// the portrait layer, framed, rendered, then destroyed — so the caller
        /// receives only the pixels. Each call allocates a new texture; the
        /// caller owns releasing it.</summary>
        public RenderTexture Capture(GameObject subject)
        {
            subject.transform.SetParent(stage, false);
            subject.transform.localPosition = Vector3.zero;
            subject.transform.localRotation = Quaternion.identity;
            SetLayerRecursively(subject, PortraitLayer);

            FrameSubject(ComputeBounds(subject));

            var texture = new RenderTexture(TextureSizePx, TextureSizePx, DepthBufferBits);
            var previousTarget = cam.targetTexture;
            cam.targetTexture = texture;
            cam.Render();
            RenderCount++;
            cam.targetTexture = previousTarget;

            DestroyObject(subject);
            return texture;
        }

        private void FrameSubject(Bounds bounds)
        {
            var radius = Mathf.Max(bounds.extents.magnitude, MinSubjectRadiusUnits);
            var distance = radius * CameraDistanceMultiplier;

            cam.orthographicSize = radius * FramingRadiusMultiplier;
            cam.transform.position = bounds.center + CameraViewDirection.normalized * distance;
            cam.transform.LookAt(bounds.center);
            cam.farClipPlane = distance + radius * FarClipMarginMultiplier;
        }

        private static Bounds ComputeBounds(GameObject subject)
        {
            var renderers = subject.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(subject.transform.position, Vector3.zero);
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;
            foreach (Transform child in target.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static void DestroyObject(Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void OnDestroy()
        {
            if (stage != null)
            {
                DestroyObject(stage.gameObject);
            }
        }
    }
}
