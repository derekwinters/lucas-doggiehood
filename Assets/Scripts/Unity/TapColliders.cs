using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Imported FBX models carry no collider, so anything tappable that is
    /// rendered from one needs a BoxCollider fitted to its combined renderer
    /// bounds for TapRouter's Physics.Raycast to hit (houses since #124,
    /// dogs since #148). The collider goes on the interactable root so
    /// GetComponentInParent finds the IInteractable from the hit.
    /// </summary>
    public static class TapColliders
    {
        /// <summary>Adds a BoxCollider to <paramref name="root"/> covering
        /// the combined world-space renderer bounds under
        /// <paramref name="visual"/>. Assumes the root currently has
        /// identity rotation and unit scale, so the world-space AABB
        /// converts to local space by translation only. A no-op when the
        /// visual has no renderers.</summary>
        public static void AddFitted(GameObject root, GameObject visual)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            var collider = root.AddComponent<BoxCollider>();
            collider.center = root.transform.InverseTransformPoint(bounds.center);
            collider.size = bounds.size;
        }
    }
}
