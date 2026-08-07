using UnityEngine;
using UnityEngine.UI;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Keeps a Candy Cottage Ink outline band glued to the fill it outlines
    /// (#663). It lives on the <b>band</b> GameObject and is attached by
    /// <see cref="CandyChromeUgui.AddOutline"/>, so every chromed element gets
    /// the tracking for free — no call site has to remember to re-apply chrome
    /// after it lays a rect out.
    ///
    /// <para><b>Why this exists.</b> #616 replaced Unity's <c>Outline</c> mesh
    /// effect — a component <i>on</i> the element, which rode its
    /// <see cref="RectTransform"/> for free — with a constant-width contour band
    /// drawn as a <i>sibling</i> behind the fill (UGUI draws children in front of
    /// parents, so the band cannot be a child). A sibling follows nothing: the
    /// band snapshotted the fill's rect at apply time and was stranded there.
    /// Any element chromed <i>before</i> it was laid out kept Unity's default
    /// 100x100 centred rect, which is what put two black boxes on screen.</para>
    ///
    /// <para>The follower has to sit on the band rather than the fill, because
    /// <c>OnRectTransformDimensionsChange</c> on the fill does not fire for a
    /// pure <see cref="RectTransform.anchoredPosition"/> move — so tracking is
    /// driven from <see cref="LateUpdate"/> (after all layout for the frame,
    /// before rendering) and from the public <see cref="Sync"/> /
    /// <see cref="SyncAll"/> entry points, which the EditMode suite calls
    /// directly since it runs no frame loop
    /// (docs/engineering/testing.md).</para>
    ///
    /// <para>Visibility is mirrored onto the band's <see cref="Graphic.enabled"/>
    /// rather than by deactivating the band GameObject: an inactive GameObject
    /// receives no <c>LateUpdate</c>, so a deactivated band could never notice
    /// its fill coming back and would be gone for good after the first
    /// hide.</para>
    /// </summary>
    public sealed class OutlineBandFollower : MonoBehaviour
    {
        [SerializeField] private RectTransform fill;
        [SerializeField] private float thicknessPx;

        private RectTransform bandRect;
        private Graphic bandGraphic;

        /// <summary>The fill this band outlines.</summary>
        public RectTransform Fill => fill;

        /// <summary>The band's uniform width — the fill's rect is inflated by
        /// this on every side.</summary>
        public float ThicknessPx => thicknessPx;

        /// <summary>Whether the band is currently drawn (it mirrors its fill's
        /// visibility).</summary>
        public bool IsShowing => BandGraphic() != null
            && BandGraphic().enabled
            && gameObject.activeInHierarchy;

        /// <summary>Points this band at the fill it outlines, at the given band
        /// width. Re-binding is safe (and expected — <c>AddOutline</c> is
        /// idempotent and may hand an existing band a new fill or a new
        /// thickness).</summary>
        public void Bind(RectTransform fillRect, float bandThicknessPx)
        {
            fill = fillRect;
            thicknessPx = bandThicknessPx;
            Sync();
        }

        /// <summary>Re-mirrors the fill's anchors, pivot, rect and visibility
        /// onto the band; destroys the band when its fill is gone. Public so the
        /// EditMode suite can drive the sync explicitly — there is no frame loop
        /// outside Play mode.</summary>
        public void Sync()
        {
            if (fill == null)
            {
                DestroyBand();
                return;
            }

            var rect = BandRect();
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = fill.anchorMin;
            rect.anchorMax = fill.anchorMax;
            rect.pivot = fill.pivot;
            var inflate = new Vector2(thicknessPx, thicknessPx);
            rect.offsetMin = fill.offsetMin - inflate;
            rect.offsetMax = fill.offsetMax + inflate;

            var graphic = BandGraphic();
            if (graphic != null)
            {
                graphic.enabled = fill.gameObject.activeInHierarchy;
            }
        }

        /// <summary>Syncs every band under <paramref name="root"/> — the
        /// EditMode stand-in for a frame's worth of <see cref="LateUpdate"/>
        /// calls.</summary>
        public static void SyncAll(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            var followers = root.GetComponentsInChildren<OutlineBandFollower>(true);
            foreach (var follower in followers)
            {
                // A follower whose fill was destroyed destroys its own band, so
                // an earlier entry in this batch may already be gone.
                if (follower == null)
                {
                    continue;
                }

                follower.Sync();
            }
        }

        private void LateUpdate()
        {
            Sync();
        }

        private RectTransform BandRect()
        {
            if (bandRect == null)
            {
                bandRect = GetComponent<RectTransform>();
            }

            return bandRect;
        }

        private Graphic BandGraphic()
        {
            if (bandGraphic == null)
            {
                bandGraphic = GetComponent<Graphic>();
            }

            return bandGraphic;
        }

        private void DestroyBand()
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }
    }
}
