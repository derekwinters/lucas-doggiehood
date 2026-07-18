using System.IO;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// Covers the save-file delete seam behind the <c>Doggiehood ▸ Reset Save
    /// Data</c> editor menu (#187). The menu handler only adds an
    /// <c>EditorUtility.DisplayDialog</c> confirmation on top; the disk
    /// behavior lives on <see cref="SaveStore.DeleteSave"/> so it can be
    /// exercised headlessly here rather than from a menu click.
    /// </summary>
    public class SaveStoreTests
    {
        private static string SavePath
        {
            get { return Path.Combine(Application.persistentDataPath, SaveStore.SaveFileName); }
        }

        [SetUp]
        public void RemoveSaveBeforeTest()
        {
            DeleteSaveFileIfPresent();
        }

        [TearDown]
        public void RemoveSaveAfterTest()
        {
            DeleteSaveFileIfPresent();
        }

        private static void DeleteSaveFileIfPresent()
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
            }
        }

        [Test]
        public void DeleteSave_RemovesTheFile_AndReportsItWasRemoved()
        {
            SaveStore.Save(GameState.CreateNew());
            Assert.That(File.Exists(SavePath), Is.True, "precondition: a save file exists");

            var removed = SaveStore.DeleteSave();

            Assert.That(removed, Is.True, "DeleteSave should report it removed a file");
            Assert.That(File.Exists(SavePath), Is.False, "the save file should be gone");
        }

        [Test]
        public void LoadOrCreate_ReturnsAFreshState_AfterDeleteSave()
        {
            var progressed = GameState.CreateNew();
            progressed.MarkOnboardingComplete();
            SaveStore.Save(progressed);

            SaveStore.DeleteSave();
            var reloaded = SaveStore.LoadOrCreate();

            Assert.That(reloaded.OnboardingComplete, Is.False,
                "deleting the save must reset progress to a fresh game");
        }

        [Test]
        public void DeleteSave_IsASafeNoOp_WhenNoSaveExists()
        {
            Assert.That(File.Exists(SavePath), Is.False, "precondition: no save file present");

            var removed = true;
            Assert.DoesNotThrow(() => removed = SaveStore.DeleteSave());

            Assert.That(removed, Is.False, "DeleteSave should report nothing was removed");
        }
    }
}
