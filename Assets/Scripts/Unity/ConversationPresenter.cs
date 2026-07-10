using Doggiehood.Core.Dogs;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Holds the currently open conversation (#11). Graybox IMGUI rendering
    /// until the Candy Cottage dialogue UI (#65) is built; game logic never
    /// lives here — the Conversation itself comes from Core.
    /// </summary>
    public sealed class ConversationPresenter : MonoBehaviour
    {
        public Conversation Current { get; private set; }

        public bool IsOpen
        {
            get { return Current != null; }
        }

        /// <summary>Opens the dog's conversation; a no-op for dogs without
        /// an active quest (Core returns null for those).</summary>
        public bool TryOpen(Dog dog)
        {
            var conversation = ConversationStarter.TryOpen(dog);
            if (conversation == null)
            {
                return false;
            }

            Current = conversation;
            return true;
        }

        public void Close()
        {
            Current = null;
        }

        private void OnGUI()
        {
            if (!IsOpen)
            {
                return;
            }

            var width = Mathf.Min(600f, Screen.width - 40f);
            GUILayout.BeginArea(new Rect((Screen.width - width) / 2f, Screen.height * 0.6f, width, Screen.height * 0.35f), GUI.skin.box);
            foreach (var line in Current.Lines)
            {
                GUILayout.Label(line);
            }

            if (GUILayout.Button(Current.Ending == ConversationEnding.Accept ? "Accept" : "Complete"))
            {
                Close();
            }

            GUILayout.EndArea();
        }
    }
}
