using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Forwards a tap to whatever IInteractable sits under it (#20). Pure
    /// pass-through: hit-testing here, decisions in the entity's handler.
    ///
    /// #169: dog speech bubbles get a screen-space padded check
    /// (DogView.TryHandleBubbleTap) ahead of the physics raycast below.
    /// Physics.Raycast against the #148/#158 collider has zero forgiveness
    /// for touch imprecision — a mouse cursor is pixel-precise, a finger
    /// touch is not — so a tap that visually reads as "on the bubble" but
    /// lands a little outside its exact rendered mesh would otherwise miss
    /// outright on mobile.
    /// </summary>
    public static class TapRouter
    {
        private const float MaxRayDistance = 1000f;

        public static bool RouteTap(Camera camera, Vector2 screenPosition)
        {
            if (TryHandleBubbleTaps(camera, screenPosition))
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
    }
}
