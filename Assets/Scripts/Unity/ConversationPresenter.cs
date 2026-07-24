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

        /// <summary>Set by WorldBootstrap; when present, conversations use
        /// the dog's real quest instance and Accept flows into Core.</summary>
        public Doggiehood.Core.World.GameState State { get; set; }
        public QuestDirector Director { get; set; }

        private Doggiehood.Core.Quests.Quest currentQuest;

        /// <summary>Raised when a conversation opens (onboarding listens, #44).</summary>
        public event System.Action<Dog> Opened;

        /// <summary>Raised when a quest is accepted/completed via this panel.</summary>
        public event System.Action<Doggiehood.Core.Quests.Quest> QuestAccepted;

        public bool IsOpen
        {
            get { return Current != null; }
        }

        /// <summary>Opens the dog's conversation; a no-op for dogs without
        /// an active quest (Core returns null for those).</summary>
        public bool TryOpen(Dog dog)
        {
            if (State != null)
            {
                currentQuest = System.Linq.Enumerable.FirstOrDefault(
                    State.Quests.ActiveQuests,
                    q => q.DogName == dog.Name && q.Status == Doggiehood.Core.Quests.QuestStatus.Available);

                if (currentQuest != null)
                {
                    Current = new Conversation(currentQuest.DialogueLines, ConversationEnding.Accept);
                    Opened?.Invoke(dog);
                    return true;
                }
            }

            var conversation = ConversationStarter.TryOpen(dog);
            if (conversation == null)
            {
                return false;
            }

            Current = conversation;
            Opened?.Invoke(dog);
            return true;
        }

        /// <summary>Accepts the currently open quest (#33).</summary>
        public void AcceptCurrent()
        {
            if (currentQuest != null && State != null && State.Quests.Accept(currentQuest))
            {
                Doggiehood.Core.Audio.AudioEventBus.Publish(Doggiehood.Core.Audio.SfxEvent.UiConfirm);
                if (Director != null)
                {
                    Director.OnQuestAccepted(currentQuest);
                }

                QuestAccepted?.Invoke(currentQuest);
            }

            Close();
        }

        /// <summary>#50: accept a generic decoration request with the chosen
        /// option — still one linear action, just parameterized.</summary>
        public void AcceptChoice(string chosenItem)
        {
            if (currentQuest != null && State != null
                && State.Quests.AcceptWithChoice(currentQuest, chosenItem))
            {
                Doggiehood.Core.Audio.AudioEventBus.Publish(Doggiehood.Core.Audio.SfxEvent.UiConfirm);
                if (Director != null)
                {
                    Director.OnQuestAccepted(currentQuest);
                }

                QuestAccepted?.Invoke(currentQuest);
            }

            Close();
        }

        /// <summary>"Not now" (#185): a silent, non-punishing decline. Just
        /// closes the panel — the quest is left exactly as it was (still
        /// `Available` if it was), no dialogue line, no sound, no timer or
        /// cooldown. The dog's speech bubble stays up, so the same request
        /// is fully re-presented if the player taps it again.</summary>
        public void DeclineCurrent()
        {
            Close();
        }

        public void Close()
        {
            Current = null;
            currentQuest = null;
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

            if (currentQuest != null && currentQuest.Options.Count > 0)
            {
                // Generic decoration request (#50): one button per option.
                foreach (var option in currentQuest.Options)
                {
                    if (GUILayout.Button(option))
                    {
                        AcceptChoice(option);
                        break;
                    }
                }
            }
            else if (GUILayout.Button(Current.Ending == ConversationEnding.Accept ? "Accept" : "Complete"))
            {
                AcceptCurrent();
            }

            // #185: "Not now" is always present, regardless of accept-row
            // variant (standard/choice/buy-something) — a silent decline.
            if (GUILayout.Button("Not now"))
            {
                DeclineCurrent();
            }

            GUILayout.EndArea();
        }
    }
}
