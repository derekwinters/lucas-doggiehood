using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// The hidden lost item for a LostItem quest (#31): a small object at
    /// the quest's hidden world position. Tapping it forwards to Core,
    /// which decides whether the quest completes. No hints, no radar.
    /// </summary>
    public sealed class LostItemView : MonoBehaviour, IInteractable
    {
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
            if (state.Quests.TapWorldPosition(quest.HiddenItemPosition.Value))
            {
                Destroy(gameObject);
            }
        }
    }
}
