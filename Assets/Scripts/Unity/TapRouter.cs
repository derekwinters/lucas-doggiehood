using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Forwards a tap to whatever IInteractable sits under it (#20). Pure
    /// pass-through: hit-testing here, decisions in the entity's handler.
    /// </summary>
    public static class TapRouter
    {
        private const float MaxRayDistance = 1000f;

        public static bool RouteTap(Camera camera, Vector2 screenPosition)
        {
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
    }
}
