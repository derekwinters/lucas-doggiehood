using Doggiehood.Core.Cameras;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Forwards a tap to whatever IInteractable sits under it (#20). Pure
    /// pass-through: hit-testing here, decisions in the entity's handler.
    ///
    /// #422: an open dialog/menu is modal — a tap that lands on UI must never
    /// also reach a world IInteractable behind it. UGUI overlays (the
    /// ConfirmationDialog / house + dog profiles / the migrated conversation
    /// panel, each with a full-screen raycast-blocking scrim) are caught by
    /// <see cref="IsPointerOverUi"/> (EventSystem.IsPointerOverGameObject); the
    /// still-IMGUI HUD gear is caught by a screen-space rect check
    /// (<see cref="GearTapZone"/> + HudOverlay.ComputeGearRect) — interim
    /// scaffolding until #370 moves the gear onto the UGUI canvas. Both guards
    /// run first, so even the bubble/lost-item screen-space checks below are
    /// skipped for a tap over UI.
    ///
    /// #169: dog speech bubbles get a screen-space padded check
    /// (DogView.TryHandleBubbleTap) ahead of the physics raycast below.
    /// Physics.Raycast against the #148/#158 collider has zero forgiveness
    /// for touch imprecision — a mouse cursor is pixel-precise, a finger
    /// touch is not — so a tap that visually reads as "on the bubble" but
    /// lands a little outside its exact rendered mesh would otherwise miss
    /// outright on mobile.
    ///
    /// #311: the active lost item gets the same treatment
    /// (LostItemView.TryHandleLostItemTap) — its SphereCollider is tiny and
    /// sits atop the full-map ground Plane, so an imprecise tap otherwise
    /// lands on the ground instead of the ball and silently does nothing.
    /// </summary>
    public static class TapRouter
    {
        private const float MaxRayDistance = 1000f;

        /// <summary>
        /// "Is this tap over a UGUI graphic?" — the modal-overlay guard (#422).
        /// Defaults to the live EventSystem query
        /// (<see cref="DefaultIsPointerOverUi"/>); overridable so EditMode tests
        /// can drive it deterministically, because
        /// <c>EventSystem.IsPointerOverGameObject</c> reads the input module's
        /// last-processed pointer, which is empty under headless test runs.
        /// </summary>
        public static System.Func<int?, bool> IsPointerOverUi = DefaultIsPointerOverUi;

        /// <summary>
        /// Production pointer-over-UI check. Null <paramref name="pointerId"/>
        /// is the mouse (parameterless overload); a value is a touch fingerId
        /// (the <c>IsPointerOverGameObject(int)</c> overload). Null-safe: with
        /// no active EventSystem it never blocks, so world taps still route.
        /// </summary>
        public static bool DefaultIsPointerOverUi(int? pointerId)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            return pointerId.HasValue
                ? eventSystem.IsPointerOverGameObject(pointerId.Value)
                : eventSystem.IsPointerOverGameObject();
        }

        public static bool RouteTap(Camera camera, Vector2 screenPosition, int? pointerId = null)
        {
            // #422: a tap absorbed by UI (a modal UGUI overlay or the IMGUI HUD
            // gear) must never reach the world behind it. Evaluated at tap
            // release — the same moment the world routing below fires.
            if (IsPointerOverUi(pointerId))
            {
                return false;
            }

            if (TryHandleGearTaps(screenPosition))
            {
                return false;
            }

            if (TryHandleBubbleTaps(camera, screenPosition))
            {
                return true;
            }

            if (TryHandleLostItemTaps(camera, screenPosition))
            {
                return true;
            }

            var ray = camera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out var hit, MaxRayDistance))
            {
                return false;
            }

            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable == null)
            {
                return false;
            }

            interactable.OnTapped();
            return true;
        }

        /// <summary>
        /// #422: absorbs a tap that lands on the still-IMGUI HUD Settings gear
        /// so it doesn't fall through to the world raycast. IMGUI is outside the
        /// EventSystem, so <see cref="IsPointerOverUi"/> can't see the gear;
        /// this mirrors the bubble/lost-item screen-space pattern. Only blocks
        /// while a HudOverlay is actually present, so the top-right corner isn't
        /// dead when no HUD is up. Interim scaffolding — dissolves once #370
        /// moves the gear onto the UGUI canvas.
        /// </summary>
        private static bool TryHandleGearTaps(Vector2 screenPosition)
        {
            // The gear rect is a fixed screen-corner affordance, not per-instance;
            // only block while a HudOverlay is actually present so the corner
            // isn't dead when no HUD is up.
            if (Object.FindFirstObjectByType<HudOverlay>() == null)
            {
                return false;
            }

            // ComputeGearRect is GUI space (top-left origin); the tap is screen
            // space (bottom-left origin) — flip Y before comparing.
            var gearGui = HudOverlay.ComputeGearRect(Screen.width, Screen.height);
            var minY = Screen.height - gearGui.yMax;
            var maxY = Screen.height - gearGui.yMin;
            return GearTapZone.Contains(
                gearGui.xMin, minY, gearGui.xMax, maxY, screenPosition.x, screenPosition.y);
        }

        private static bool TryHandleBubbleTaps(Camera camera, Vector2 screenPosition)
        {
            foreach (var view in Object.FindObjectsByType<DogView>(FindObjectsSortMode.None))
            {
                if (view.TryHandleBubbleTap(camera, screenPosition))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryHandleLostItemTaps(Camera camera, Vector2 screenPosition)
        {
            foreach (var view in Object.FindObjectsByType<LostItemView>(FindObjectsSortMode.None))
            {
                if (view.TryHandleLostItemTap(camera, screenPosition))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
