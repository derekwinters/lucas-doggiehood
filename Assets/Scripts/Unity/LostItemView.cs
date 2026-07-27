using Doggiehood.Core.Cameras;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// The hidden lost item for a LostItem quest (#31): a small object at
    /// the quest's hidden world position. Tapping it forwards to Core,
    /// which decides whether the quest completes. No hints, no radar.
    ///
    /// #311: also offers a screen-space padded tap fallback
    /// (<see cref="TryHandleLostItemTap"/>), checked by TapRouter ahead of
    /// its physics raycast — mirroring the dog speech bubble precedent
    /// (#169, DogView.TryHandleBubbleTap). The ball's SphereCollider (radius
    /// 0.3) projects to a tiny on-screen target under the fixed 45-degree
    /// rig, and the full-map ground Plane collider underlies the whole spawn
    /// area, so a bare Physics.Raycast has effectively zero forgiveness for
    /// touch imprecision — without the padded fallback, the intended
    /// QuestManager.LostItemTapRadius tolerance never actually gets
    /// exercised at runtime, since a hit always arrives with the item's own
    /// exact position (distance 0).
    /// </summary>
    public sealed class LostItemView : MonoBehaviour, IInteractable
    {
        /// <summary>An axis-aligned bounding box has 8 corners; used when
        /// projecting the item's world bounds to screen space for
        /// <see cref="TryHandleLostItemTap"/> (#311).</summary>
        private const int BoundsCornerCount = 8;

        private GameState state;
        private Quest quest;

        public static LostItemView Spawn(GameState state, Quest quest, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "LostItem - " + quest.ItemName;
            go.transform.SetParent(parent);
            go.transform.localScale = Vector3.one * 0.6f;
            go.transform.position = new Vector3(
                quest.HiddenItemPosition.Value.X, 0.3f, quest.HiddenItemPosition.Value.Z);

            var view = go.AddComponent<LostItemView>();
            view.state = state;
            view.quest = quest;
            return view;
        }

        public void OnTapped()
        {
            Collect();
        }

        /// <summary>#311: true when the given screen-space tap falls within
        /// the item's projected bounds, padded per Core's LostItemTapZone —
        /// mirrors DogView.TryHandleBubbleTap (#169). A mouse cursor is
        /// pixel-precise; a finger touch is not, and the tiny SphereCollider
        /// sitting atop the full-map ground Plane has zero forgiveness for
        /// that imprecision — a tap that visually reads as "on the ball" but
        /// lands a little outside its rendered mesh would otherwise land on
        /// the ground and do nothing. Once this padded zone registers a hit,
        /// proximity is already established, so it completes the quest the
        /// same way OnTapped does (forwarding the item's own position keeps
        /// QuestManager.LostItemTapRadius as the single source of truth for
        /// the game-logic tolerance). Returns true on a hit; otherwise a
        /// no-op false (no renderers yet, or the tap missed even the padded
        /// zone). TapRouter checks this ahead of its physics raycast.</summary>
        public bool TryHandleLostItemTap(Camera camera, Vector2 screenPosition)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return false;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            for (var i = 0; i < BoundsCornerCount; i++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                    (i & 1) == 0 ? -1f : 1f,
                    (i & 2) == 0 ? -1f : 1f,
                    (i & 4) == 0 ? -1f : 1f));
                var screen = camera.WorldToScreenPoint(corner);
                minX = Mathf.Min(minX, screen.x);
                maxX = Mathf.Max(maxX, screen.x);
                minY = Mathf.Min(minY, screen.y);
                maxY = Mathf.Max(maxY, screen.y);
            }

            if (!LostItemTapZone.Contains(minX, minY, maxX, maxY, screenPosition.x, screenPosition.y))
            {
                return false;
            }

            Collect();
            return true;
        }

        private void Collect()
        {
            if (state.Quests.TapWorldPosition(quest.HiddenItemPosition.Value))
            {
                // Match the mode-aware teardown RefreshBugSwarms uses (#157):
                // Destroy is deferred in edit mode, so EditMode tests (and any
                // edit-time caller) need DestroyImmediate to see it removed.
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
}
