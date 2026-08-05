using Doggiehood.Core.Quests;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #571: the red highlight on the onboarding "fix up a home" target house.
    /// It reuses the established finder-glow visual (#535) — a flat, non-pulsing,
    /// translucent red ground contact ring in <c>Palette.LostItemGlowHex</c>,
    /// with no halo/size-pulse/sparkle — so onboarding guidance stays visually
    /// consistent instead of inventing a new cue. Purely feedback: the ring is
    /// collider-free and never intercepts the house's own tap (the house stays
    /// the tap target that routes to the upgrade).
    ///
    /// <para>The one difference from <see cref="LostItemView"/>'s finder glow is
    /// SIZE: a lost item's ring is a fixed <see cref="LostItemGlow.GroundRingScale"/>
    /// relative to a small item, which would look lost under a much bigger house
    /// mesh. So the ring's footprint is derived from the target house's OWN
    /// encapsulated renderer bounds (the same approach <see cref="BugSwarmView"/>
    /// uses to sit above a house's real roofline), reading correctly under any
    /// house variant/level. The flat height/thickness reuse the named
    /// <see cref="LostItemGlow"/> constants (#161) so the ring stays the same thin
    /// pool of light. Lifecycle (attach/teardown) is the
    /// <see cref="OnboardingHouseHighlightDirector"/>'s.</para>
    /// </summary>
    public sealed class OnboardingHouseHighlightView : MonoBehaviour
    {
        private const string RingName = "GroundRing";

        /// <summary>The ring extends this far beyond the house's own XZ footprint,
        /// so it reads as a pool of light framing the house rather than a disc
        /// hidden under it. Named per #161.</summary>
        private const float FootprintMultiplier = 1.15f;

        /// <summary>Translucency of the ring material — matches the finder glow so
        /// the ring blends over the ground rather than occluding it (#535). Named
        /// per #161.</summary>
        private const float GlowAlpha = 0.55f;

        /// <summary>Unity Standard-shader keyword/constants for switching a cloned
        /// material to transparent rendering, mirroring
        /// <see cref="LostItemView"/>'s glow paint.</summary>
        private const string ShaderModeProperty = "_Mode";
        private const float ShaderTransparentMode = 3f;

        public int HouseId { get; private set; }

        /// <summary>Builds the flat red ground ring under <paramref name="houseTransform"/>,
        /// parented to <paramref name="parent"/> (the world root), sized from the
        /// house's own renderer bounds. Returns the view for the director to track.</summary>
        public static OnboardingHouseHighlightView Spawn(int houseId, Transform houseTransform, Transform parent)
        {
            var bounds = HouseBounds(houseTransform);
            var footprint = Mathf.Max(bounds.size.x, bounds.size.z) * FootprintMultiplier;

            var root = new GameObject("OnboardingHouseHighlight - house " + houseId);
            root.transform.SetParent(parent);
            // Sit on the ground under the house (its bounds' base), centered on the
            // house's XZ — a contact ring, not a floating disc.
            root.transform.position = new Vector3(houseTransform.position.x, bounds.min.y, houseTransform.position.z);

            var glowColor = CoreColors.FromHex(Doggiehood.Core.Art.Palette.LostItemGlowHex);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = RingName;
            ring.transform.SetParent(root.transform, worldPositionStays: false);
            ring.transform.localPosition = new Vector3(0f, LostItemGlow.GroundRingHeight, 0f);
            ring.transform.localScale = new Vector3(footprint, LostItemGlow.GroundRingThickness, footprint);
            MakeFeedbackOnly(ring);
            PaintGlow(ring, glowColor);

            var view = root.AddComponent<OnboardingHouseHighlightView>();
            view.HouseId = houseId;
            return view;
        }

        /// <summary>The house's combined world renderer bounds (mirroring
        /// <see cref="BugSwarmView"/>'s encapsulate approach), so the ring can be
        /// sized to the real mesh. Falls back to a unit box at the house's
        /// position if it has no renderers yet.</summary>
        private static Bounds HouseBounds(Transform houseTransform)
        {
            var renderers = houseTransform.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(houseTransform.position, Vector3.one);
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        /// <summary>Strips the primitive's collider so the ring never intercepts a
        /// tap meant for the house. Mode-aware (mirrors
        /// <see cref="BugSwarmView"/>) — DestroyImmediate so EditMode tests see it
        /// gone at once, Destroy under Play.</summary>
        private static void MakeFeedbackOnly(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(collider);
            }
            else
            {
                DestroyImmediate(collider);
            }
        }

        /// <summary>Paints the ring with a translucent clone of the finder-glow
        /// colour so it blends over the ground rather than occluding it — the same
        /// approach as <see cref="LostItemView"/>'s glow paint.</summary>
        private static void PaintGlow(GameObject ring, Color color)
        {
            var renderer = ring.GetComponent<Renderer>();
            var material = renderer.sharedMaterial != null
                ? new Material(renderer.sharedMaterial)
                : new Material(Shader.Find("Standard"));
            material.color = new Color(color.r, color.g, color.b, GlowAlpha);
            if (material.HasProperty(ShaderModeProperty))
            {
                material.SetFloat(ShaderModeProperty, ShaderTransparentMode);
            }

            renderer.sharedMaterial = material;
        }
    }
}
