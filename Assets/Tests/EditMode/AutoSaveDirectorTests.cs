using System.Collections.Generic;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #704: saving used to be event-only — eight scattered call sites and no
    /// pause/quit hook — so anything that changed without one of those events
    /// (the quest-pacing clock advancing, a house upgrade) was rolled back by
    /// the next relaunch. The autosave director closes that: a periodic write
    /// while the app runs, plus Android's backgrounding and quit callbacks.
    /// The disk boundary is injected here so the test never touches the real
    /// save file.
    /// </summary>
    public class AutoSaveDirectorTests
    {
        private GameObject host;
        private GameState state;
        private List<GameState> saves;
        private AutoSaveDirector director;

        [SetUp]
        public void BuildDirector()
        {
            state = GameState.CreateNew();
            saves = new List<GameState>();
            host = new GameObject("autosave-host");
            director = host.AddComponent<AutoSaveDirector>();
            director.Init(state, saved => saves.Add(saved));
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(host);
        }

        [Test]
        public void Tick_SavesOnceTheIntervalElapses_AndNotBefore()
        {
            director.Tick(AutoSaveDirector.AutoSaveInterval - 1f);
            Assert.That(saves, Is.Empty, "a partial interval writes nothing");

            director.Tick(1f);

            Assert.That(saves.Count, Is.EqualTo(1), "the interval writes exactly once");
            Assert.That(saves[0], Is.SameAs(state), "and writes the live state");
        }

        [Test]
        public void Tick_KeepsSavingOnEveryFurtherInterval()
        {
            director.Tick(AutoSaveDirector.AutoSaveInterval);
            director.Tick(AutoSaveDirector.AutoSaveInterval);

            Assert.That(saves.Count, Is.EqualTo(2));
        }

        [Test]
        public void BackgroundingTheApp_SavesImmediately()
        {
            // Android backgrounds the app long before it quits; without this the
            // session's progress was simply lost.
            director.OnApplicationPause(true);

            Assert.That(saves.Count, Is.EqualTo(1));
        }

        [Test]
        public void ReturningToTheApp_DoesNotSave()
        {
            director.OnApplicationPause(false);

            Assert.That(saves, Is.Empty, "resuming has nothing new to write");
        }

        [Test]
        public void QuittingTheApp_SavesImmediately()
        {
            director.OnApplicationQuit();

            Assert.That(saves.Count, Is.EqualTo(1));
        }

        [Test]
        public void AnUninitialisedDirector_NeverWrites()
        {
            var bare = new GameObject("bare-autosave").AddComponent<AutoSaveDirector>();
            try
            {
                Assert.DoesNotThrow(() => bare.OnApplicationQuit());
                Assert.DoesNotThrow(() => bare.Tick(AutoSaveDirector.AutoSaveInterval));
            }
            finally
            {
                Object.DestroyImmediate(bare.gameObject);
            }
        }
    }
}
