using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #327: the runtime Settings panel is built in UGUI, so its controls only
    /// receive taps if an active <see cref="EventSystem"/> + input module drives
    /// the canvas's <c>GraphicRaycaster</c>. The canvas half (<see cref="UiCanvas"/>)
    /// was present, but nothing ever created an EventSystem — Unity never
    /// auto-creates one for runtime-built UI — so every control in the panel
    /// (close ✕, version-tap unlock, scrim, debug toggles) was inert on device
    /// while the IMGUI HUD gear kept working. These guard the EventSystem seam
    /// so the input-delivery wiring can't regress silently.
    /// </summary>
    public class UiEventSystemTests
    {
        [TearDown]
        public void Cleanup()
        {
            foreach (var es in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(es.gameObject);
            }
        }

        [Test]
        public void Ensure_CreatesAnActiveEventSystem_WithAStandaloneInputModule()
        {
            var eventSystem = UiEventSystem.Ensure();

            Assert.That(eventSystem, Is.Not.Null,
                "runtime UGUI input needs an EventSystem to drive the GraphicRaycaster (#327)");
            Assert.That(eventSystem.isActiveAndEnabled, Is.True,
                "an inactive EventSystem dispatches no pointer input — the panel would stay dead");
            Assert.That(eventSystem.GetComponent<StandaloneInputModule>(), Is.Not.Null,
                "the project runs the legacy Input Manager (activeInputHandler: 0), so the " +
                "module must be the classic StandaloneInputModule, not the Input-System module (#327)");
        }

        [Test]
        public void Ensure_IsIdempotent_NeverCreatesASecondEventSystem()
        {
            UiEventSystem.Ensure();
            UiEventSystem.Ensure();

            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length,
                Is.EqualTo(1),
                "a re-bootstrap must not spawn a duplicate EventSystem — Unity warns and " +
                "misbehaves with two active in a scene (#327)");
        }
    }
}
