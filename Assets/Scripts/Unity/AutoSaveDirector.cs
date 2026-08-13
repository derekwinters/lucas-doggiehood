using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #704: the backstop that makes "the neighborhood is durable" true in
    /// practice. Saving used to be event-only — a handful of call sites, no
    /// pause or quit hook — so state that changed without one of those events
    /// (the quest-pacing clock advancing, a house upgrade) was silently rolled
    /// back by the next launch, and backgrounding the app on Android could lose
    /// a whole session. This writes the save on a fixed interval while the app
    /// runs, and immediately when the app is backgrounded or quit.
    ///
    /// <para>It is deliberately dumb: it holds no game logic and decides
    /// nothing about the state it writes — every decision stays in Core, and
    /// the disk boundary stays <see cref="SaveStore"/>. Nothing continuous is
    /// persisted (dog positions and animations are presentation and stay
    /// session-only), so the interval only has to be short enough that an
    /// unexpected kill costs nothing meaningful.</para>
    /// </summary>
    public sealed class AutoSaveDirector : MonoBehaviour
    {
        /// <summary>Seconds between routine autosaves. The whole save is one
        /// small text file, so this is cheap; it is a backstop rather than the
        /// primary path (discrete actions still save the moment they
        /// happen). Named constant per #161.</summary>
        public const float AutoSaveInterval = 30f;

        private GameState state;
        private System.Action<GameState> save;
        private float sinceLastSave;

        /// <summary><paramref name="save"/> is the disk boundary, defaulting to
        /// <see cref="SaveStore.Save"/>; EditMode tests inject their own so the
        /// suite never writes over a real save file.</summary>
        public void Init(GameState state, System.Action<GameState> save = null)
        {
            this.state = state;
            this.save = save ?? SaveStore.Save;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Advances the autosave interval. Called by Update at runtime
        /// and directly by EditMode tests, mirroring
        /// <see cref="QuestDirector.Tick"/>.</summary>
        public void Tick(float deltaTime)
        {
            sinceLastSave += deltaTime;
            if (sinceLastSave < AutoSaveInterval)
            {
                return;
            }

            sinceLastSave = 0f;
            SaveNow();
        }

        /// <summary>Unity's backgrounding callback — on Android this is the
        /// last moment that reliably runs before the app can be killed, so the
        /// save happens here rather than only at quit. Public so an EditMode
        /// test can invoke it; Unity dispatches it by name either way.</summary>
        public void OnApplicationPause(bool paused)
        {
            if (!paused)
            {
                return;
            }

            SaveNow();
        }

        /// <summary>Unity's quit callback. Public for the same reason as
        /// <see cref="OnApplicationPause"/>.</summary>
        public void OnApplicationQuit()
        {
            SaveNow();
        }

        /// <summary>Writes the live state through the injected boundary. A
        /// no-op before <see cref="Init"/> has supplied one.</summary>
        public void SaveNow()
        {
            if (state == null || save == null)
            {
                return;
            }

            save(state);
        }
    }
}
