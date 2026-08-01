using System;
using System.Linq;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Scene-side rendering for ONE map-expansion lock indicator (#178/#453),
    /// bound to a single frontier <see cref="TileCoordinate"/>: hovers just past
    /// that coordinate's shared edge (<see cref="ExpansionIndicatorPlacement"/>),
    /// tinted gold when the wallet can afford the flat tile-unlock cost
    /// (<see cref="TileUnlock"/>) or grey/black when not, and hidden the moment
    /// its coordinate leaves the unlockable frontier (placed, or gated back out by
    /// onboarding). No decision logic here — Core resolves membership /
    /// position / affordability fresh every call; this view only applies the
    /// result to a SpriteRenderer, re-reading live each frame, the same "never
    /// cache" contract HudOverlay uses for the wallet label. The sprite also
    /// billboards to the live camera yaw (#266) via
    /// <see cref="WorldMarkerBillboard"/>.
    ///
    /// #453 supersedes the single fixed marker: one of these views exists per
    /// currently-unlockable frontier coordinate, spawned/destroyed by
    /// <see cref="ExpansionUnlockDirector"/> as the frontier changes. Tapping an
    /// affordable (gold) lock raises <see cref="UnlockRequested"/> with ITS OWN
    /// coordinate (the scene wires that to the confirmation dialog →
    /// GameState.TryUnlockTile); a grey/unaffordable lock, or one no longer on
    /// the frontier, is a no-op.
    /// </summary>
    public sealed class ExpansionIndicatorView : MonoBehaviour, IInteractable
    {
        /// <summary>World-Y the indicator hovers at. Purely visual —
        /// "hovering" carries no Core state, just an above-ground height so
        /// the marker reads as floating rather than sitting on the road.</summary>
        public const float HoverHeight = 3f;

        private GameState state;
        private TileCoordinate coordinate;
        private SpriteRenderer spriteRenderer;
        private Sprite affordableSprite;
        private Sprite lockedSprite;
        private CameraRig cameraRig;
        private BoxCollider tapCollider;

        /// <summary>The frontier coordinate this lock marks.</summary>
        public TileCoordinate Coordinate
        {
            get { return coordinate; }
        }

        /// <summary>Raised when an affordable lock is tapped, carrying THIS view's
        /// coordinate — the scene wires it to raise the confirmation dialog whose
        /// Yes calls GameState.TryUnlockTile(coordinate) (#453). Never raised for
        /// a grey/unaffordable lock or one no longer on the frontier.</summary>
        public event Action<TileCoordinate> UnlockRequested;

        public void Init(GameState state, TileCoordinate coordinate, Sprite affordableSprite, Sprite lockedSprite)
        {
            this.state = state;
            this.coordinate = coordinate;
            this.affordableSprite = affordableSprite;
            this.lockedSprite = lockedSprite;
            spriteRenderer = GetComponent<SpriteRenderer>();
            EnsureTapCollider();
            Refresh();
        }

        /// <summary>#453 (Option A): a lock tap is an unlock request for THIS
        /// view's coordinate, but only when the lock is currently affordable and
        /// still unlockable — the same live Core state that tints it gold gates
        /// the tap, so a grey or stale lock does nothing. Fits TapRouter's
        /// IInteractable contract.</summary>
        public void OnTapped()
        {
            if (state == null || !IsUnlockable() || !IsAffordable())
            {
                return;
            }

            UnlockRequested?.Invoke(coordinate);
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
        /// Re-reads this coordinate's live state and applies it: the renderer is
        /// disabled entirely when the coordinate is no longer unlockable,
        /// otherwise the marker is positioned past its own frontier edge and
        /// tinted per the current balance/tile-cost. Public so tests can apply it
        /// directly without waiting on a Play-mode frame.
        /// </summary>
        public void Refresh()
        {
            if (state == null)
            {
                return;
            }

            if (!IsUnlockable())
            {
                spriteRenderer.enabled = false;
                SetColliderEnabled(false);
                return;
            }

            spriteRenderer.enabled = true;
            SetColliderEnabled(true);
            var position = ExpansionIndicatorPlacement.Resolve(state.Map, coordinate);
            transform.position = new Vector3(position.X, HoverHeight, position.Z);
            WorldMarkerBillboard.Face(transform, ResolveCameraRig());
            spriteRenderer.sprite = IsAffordable() ? affordableSprite : lockedSprite;
        }

        /// <summary>Whether this view's coordinate is currently on the
        /// player-unlockable frontier (onboarding-gated Core decision).</summary>
        private bool IsUnlockable()
        {
            return state.UnlockableFrontier().Contains(coordinate);
        }

        /// <summary>Whether the live wallet covers the flat per-tile unlock cost
        /// (the #295 pricing path) — the same value that tints the lock and gates
        /// its tap.</summary>
        private bool IsAffordable()
        {
            return state.Wallet.Coins >= TileUnlock.Cost(state.Map.Tiles.Count);
        }

        /// <summary>Keeps the tap collider's enabled state in lockstep with the
        /// renderer — a hidden lock must not stay tappable.</summary>
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
