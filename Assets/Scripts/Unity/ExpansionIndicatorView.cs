using System;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Scene-side rendering for the map-expansion lock indicator (#178):
    /// hovers at Core's <see cref="ExpansionIndicator.Resolve"/> position,
    /// tinted gold when affordable or grey/black when not, and hidden
    /// entirely once no locked zone remains to unlock. No decision logic
    /// here — Core resolves position/affordability fresh every call, this
    /// view only applies the result to a SpriteRenderer, re-reading live
    /// each frame, the same "never cache" contract HudOverlay uses for the
    /// wallet label. The sprite also billboards to the live camera yaw
    /// (#266) via <see cref="WorldMarkerBillboard"/> so the lock icon reads
    /// head-on at every camera rotation (#203), not just the fixed 45° yaw.
    ///
    /// #343: the lock itself is the unlock affordance (Derek's Option A) —
    /// this view is tappable (<see cref="IInteractable"/>). Tapping an
    /// affordable (gold) lock raises <see cref="UnlockRequested"/> (the
    /// scene wires that to the confirmation dialog → GameState.TryUnlockNextZone);
    /// a grey/unaffordable lock, or one with nothing left to unlock, is a
    /// no-op. The affordability gate is Core's own live
    /// <see cref="ExpansionIndicator.Resolve"/> — the same value that tints
    /// the icon, so what the tint promises is exactly what the tap does.
    /// </summary>
    public sealed class ExpansionIndicatorView : MonoBehaviour, IInteractable
    {
        /// <summary>World-Y the indicator hovers at. Purely visual —
        /// "hovering" carries no Core state, just an above-ground height so
        /// the marker reads as floating rather than sitting on the road.</summary>
        public const float HoverHeight = 3f;

        private GameState state;
        private SpriteRenderer spriteRenderer;
        private Sprite affordableSprite;
        private Sprite lockedSprite;
        private CameraRig cameraRig;
        private BoxCollider tapCollider;

        /// <summary>Raised when an affordable lock is tapped — the scene wires
        /// this to raise the confirmation dialog whose Yes calls
        /// GameState.TryUnlockNextZone (#343). Never raised for a
        /// grey/unaffordable lock or when nothing is left to unlock.</summary>
        public event Action UnlockRequested;

        public void Init(GameState state, Sprite affordableSprite, Sprite lockedSprite)
        {
            this.state = state;
            this.affordableSprite = affordableSprite;
            this.lockedSprite = lockedSprite;
            spriteRenderer = GetComponent<SpriteRenderer>();
            EnsureTapCollider();
            Refresh();
        }

        /// <summary>#343 (Option A): a lock tap is an unlock request, but only
        /// when the lock is currently affordable — the same live Core state
        /// that tints it gold gates the tap, so a grey lock does nothing.
        /// Fitting TapRouter's IInteractable contract.</summary>
        public void OnTapped()
        {
            if (state == null)
            {
                return;
            }

            var indicator = ExpansionIndicator.Resolve(state);
            if (indicator == null || !indicator.Value.IsAffordable)
            {
                return;
            }

            UnlockRequested?.Invoke();
        }

        /// <summary>Adds a BoxCollider sized to the sprite so TapRouter's
        /// Physics.Raycast can hit the billboarded lock (imported sprites carry
        /// none, same reason houses/dogs need a fitted collider). Sized from the
        /// sprite's local bounds; the transform scale applied by WorldBuilder
        /// grows it to the on-map footprint.</summary>
        private void EnsureTapCollider()
        {
            tapCollider = GetComponent<BoxCollider>();
            if (tapCollider == null)
            {
                tapCollider = gameObject.AddComponent<BoxCollider>();
            }

            var sprite = affordableSprite != null ? affordableSprite : lockedSprite;
            if (sprite != null)
            {
                tapCollider.size = sprite.bounds.size;
                tapCollider.center = sprite.bounds.center;
            }
        }

        private void Update()
        {
            Refresh();
        }

        /// <summary>
        /// Re-reads <see cref="ExpansionIndicator.Resolve"/> and applies
        /// it: the renderer is disabled entirely when nothing is left to
        /// unlock, otherwise the marker is positioned and tinted per the
        /// current balance/next-cost state. Public so tests can apply it
        /// directly without waiting on a Play-mode frame.
        /// </summary>
        public void Refresh()
        {
            if (state == null)
            {
                return;
            }

            var indicator = ExpansionIndicator.Resolve(state);
            if (indicator == null)
            {
                spriteRenderer.enabled = false;
                SetColliderEnabled(false);
                return;
            }

            spriteRenderer.enabled = true;
            SetColliderEnabled(true);
            var position = indicator.Value.Position;
            transform.position = new Vector3(position.X, HoverHeight, position.Z);
            WorldMarkerBillboard.Face(transform, ResolveCameraRig());
            spriteRenderer.sprite = indicator.Value.IsAffordable ? affordableSprite : lockedSprite;
        }

        /// <summary>Keeps the tap collider's enabled state in lockstep with the
        /// renderer — a hidden lock (nothing left to unlock) must not stay
        /// tappable.</summary>
        private void SetColliderEnabled(bool enabled)
        {
            if (tapCollider != null)
            {
                tapCollider.enabled = enabled;
            }
        }

        /// <summary>Lazily finds and caches the scene's <see cref="CameraRig"/>
        /// so the lock icon can read live yaw (#266) without a per-frame scene
        /// scan. Re-searches while null, so a rig created after Init is still
        /// picked up; null (no rig) makes <see cref="WorldMarkerBillboard"/>
        /// fall back to the fixed default yaw.</summary>
        private CameraRig ResolveCameraRig()
        {
            if (cameraRig == null)
            {
                cameraRig = FindFirstObjectByType<CameraRig>();
            }

            return cameraRig;
        }
    }
}
