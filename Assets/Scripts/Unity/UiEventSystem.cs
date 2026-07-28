using UnityEngine;
using UnityEngine.EventSystems;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Thin wiring that guarantees the single <see cref="EventSystem"/> the
    /// runtime UGUI stack needs to dispatch pointer input (#327).
    ///
    /// The UI canvas already carries a <c>GraphicRaycaster</c>
    /// (<see cref="UiCanvas"/>), but a raycaster is inert without an active
    /// EventSystem + input module somewhere in the scene. Unity only
    /// auto-creates an EventSystem from the Editor's "GameObject ▸ UI ▸ …"
    /// menu, never for runtime-built UI, so nothing was driving the Settings
    /// panel's controls on device — the close ✕, the version-tap unlock, the
    /// scrim, and the debug toggles all received no input, while the IMGUI HUD
    /// gear (<see cref="HudOverlay"/>) kept working because it bypasses the
    /// EventSystem. This seam closes that gap.
    ///
    /// The project runs the legacy Input Manager
    /// (<c>ProjectSettings.asset</c> → <c>activeInputHandler: 0</c>), so the
    /// module is the classic <see cref="StandaloneInputModule"/>, not
    /// <c>InputSystemUIInputModule</c> — wiring the Input-System module here
    /// would silently no-op the same way the missing EventSystem does. No
    /// decision logic lives here; this is pure MonoBehaviour/scene wiring.
    /// </summary>
    public static class UiEventSystem
    {
        /// <summary>Name of the GameObject that hosts the app's EventSystem.</summary>
        public const string EventSystemObjectName = "EventSystem";

        /// <summary>
        /// Ensures exactly one active <see cref="EventSystem"/> (with a
        /// <see cref="StandaloneInputModule"/>) exists, creating it only when
        /// one isn't already present so a re-bootstrap can't spawn a duplicate
        /// (Unity warns and misbehaves with two active in a scene). Public and
        /// return-typed so EditMode tests can invoke and assert it directly
        /// without waiting on a Play-mode frame, mirroring
        /// <see cref="UiCanvas.Configure"/>.
        /// </summary>
        public static EventSystem Ensure()
        {
            var existing = Object.FindFirstObjectByType<EventSystem>();
            if (existing != null)
            {
                return existing;
            }

            var host = new GameObject(
                EventSystemObjectName,
                typeof(EventSystem),
                typeof(StandaloneInputModule));
            return host.GetComponent<EventSystem>();
        }
    }
}
