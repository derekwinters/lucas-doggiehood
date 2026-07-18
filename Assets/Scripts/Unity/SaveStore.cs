using System.IO;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Local-only save file (docs/specs/product-scope.md: offline, local
    /// save, no accounts). Serialization itself lives in Core (SaveCodec);
    /// this is just the disk boundary.
    /// </summary>
    public static class SaveStore
    {
        /// <summary>Name of the single local save file on disk.</summary>
        public const string SaveFileName = "doggiehood-save.txt";

        private static string SavePath
        {
            get { return Path.Combine(Application.persistentDataPath, SaveFileName); }
        }

        public static void Save(GameState state)
        {
            File.WriteAllText(SavePath, SaveCodec.Save(state));
        }

        public static GameState LoadOrCreate()
        {
            if (File.Exists(SavePath))
            {
                return SaveCodec.Load(File.ReadAllText(SavePath));
            }

            return GameState.CreateNew();
        }

        /// <summary>
        /// Deletes the local save file so the next <see cref="LoadOrCreate"/>
        /// starts a fresh game (#187). Returns whether a file was actually
        /// removed; a missing save is a safe no-op that returns false.
        /// The whole save is this one file, so deleting it is a full reset.
        /// </summary>
        public static bool DeleteSave()
        {
            if (!File.Exists(SavePath))
            {
                return false;
            }

            File.Delete(SavePath);
            return true;
        }
    }
}
