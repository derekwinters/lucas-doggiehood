using UnityEditor;
using UnityEngine;

namespace Doggiehood.Unity.Editor
{
    /// <summary>
    /// #187: one-click <c>Doggiehood ▸ Reset Save Data</c> menu that wipes the
    /// local save file so playtesting can start from a fresh game, instead of
    /// hand-deleting <c>doggiehood-save.txt</c> from the platform-specific
    /// <c>Application.persistentDataPath</c>.
    ///
    /// Editor-only by construction: this class lives in the Editor assembly, so
    /// it is stripped from player builds and can never fire on a device. A
    /// confirmation dialog guards against accidental clicks, and the actual
    /// disk work is delegated to <see cref="SaveStore.DeleteSave"/> so the
    /// behavior is exercised by EditMode tests rather than a menu handler.
    /// </summary>
    public static class ResetSaveDataMenu
    {
        public const string MenuPath = "Doggiehood/Reset Save Data";

        private const string ConfirmTitle = "Reset Save Data";
        private const string ConfirmMessage =
            "Delete the local save file (" + SaveStore.SaveFileName + ") and start " +
            "a fresh game?\n\nThis wipes all local progress and cannot be undone.";
        private const string ConfirmOk = "Delete Save";
        private const string ConfirmCancel = "Cancel";

        private const string ResultTitle = "Reset Save Data";
        private const string DeletedMessage =
            "Save data deleted. The next launch starts a fresh game.";
        private const string NothingToDeleteMessage =
            "No save file was found — nothing to delete.";
        private const string ResultOk = "OK";

        [MenuItem(MenuPath)]
        public static void ResetSaveData()
        {
            if (!EditorUtility.DisplayDialog(ConfirmTitle, ConfirmMessage, ConfirmOk, ConfirmCancel))
            {
                return;
            }

            var removed = SaveStore.DeleteSave();

            EditorUtility.DisplayDialog(
                ResultTitle,
                removed ? DeletedMessage : NothingToDeleteMessage,
                ResultOk);
        }
    }
}
