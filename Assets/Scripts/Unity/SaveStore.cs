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
        private static string SavePath
        {
            get { return Path.Combine(Application.persistentDataPath, "doggiehood-save.txt"); }
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
    }
}
