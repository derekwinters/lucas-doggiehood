using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>Builds the neighborhood and its dogs when the scene starts.</summary>
    public sealed class WorldBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            var state = SaveStore.LoadOrCreate();
            var root = WorldBuilder.Build(state);
            DogSpawner.SpawnDogs(state, root.transform);

            var director = gameObject.AddComponent<QuestDirector>();
            director.Init(state, root.transform);

            var presenter = FindFirstObjectByType<ConversationPresenter>();
            if (presenter == null)
            {
                presenter = gameObject.AddComponent<ConversationPresenter>();
            }

            presenter.State = state;
            presenter.Director = director;

            // Day-one rotation. Real once-per-calendar-day gating lands with
            // the vertical-slice integration (milestone 08).
            if (!System.Linq.Enumerable.Any(state.Quests.ActiveQuests))
            {
                state.Quests.StartNewDay(new System.Random());
            }
        }
    }
}
