using Doggiehood.Core.Dogs;
using Doggiehood.Core.Quests;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Holds the currently open conversation (#11). Graybox IMGUI rendering
    /// until the Candy Cottage dialogue UI (#65) is built; game logic never
    /// lives here — the Conversation itself comes from Core. Cost/
    /// affordability display defers entirely to QuestPurchasePresentation
    /// (#186); this class only wires that Core query to GUILayout.
    /// </summary>
    public sealed class ConversationPresenter : MonoBehaviour
    {
        // docs/specs/ui/conversation-panel.md (#175): "greys out when
        // unaffordable" and a failed purchase must leave the panel open
        // (#186) rather than closing with no player-visible feedback.
        private const string InsufficientFundsMessage = "Not enough coins for that yet.";

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

        /// <summary>#186: non-null after a failed purchase attempt — the
        /// panel stays open and shows this instead of closing silently.</summary>
        public string StatusMessage { get; private set; }

        /// <summary>The accept pill's label: shows the cost for a buy-type
        /// quest (e.g. "Buy · 40"), otherwise the existing Accept/Complete
        /// text (#186).</summary>
        public string AcceptLabel
        {
            get
            {
                var ending = Current != null ? Current.Ending : ConversationEnding.Accept;
                return QuestPurchasePresentation.AcceptLabel(currentQuest, ending);
            }
        }

        /// <summary>Whether the wallet currently covers the open quest's
        /// cost (always true for quests with no cost) (#186).</summary>
        public bool AcceptIsAffordable
        {
            get { return QuestPurchasePresentation.IsAcceptAffordable(currentQuest, State != null ? State.Wallet : null); }
        }

        /// <summary>A decoration-request option's label: "{name} · {cost}"
        /// (#186).</summary>
        public string OptionLabel(string option)
        {
            return QuestPurchasePresentation.OptionLabel(option);
        }

        /// <summary>Whether the wallet currently covers a decoration
        /// option's catalog cost (#186).</summary>
        public bool OptionIsAffordable(string option)
        {
            return QuestPurchasePresentation.IsOptionAffordable(option, State != null ? State.Wallet : null);
        }

        /// <summary>Opens the dog's conversation; a no-op for dogs without
        /// an active quest (Core returns null for those).</summary>
        public bool TryOpen(Dog dog)
        {
            StatusMessage = null;

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

        /// <summary>Accepts the currently open quest (#33). On failure
        /// (#186, e.g. an unaffordable buy quest) the panel stays open with
        /// an insufficient-funds message rather than closing silently —
        /// only a successful accept closes it.</summary>
        public void AcceptCurrent()
        {
            if (currentQuest == null || State == null)
            {
                Close();
                return;
            }

            if (State.Quests.Accept(currentQuest))
            {
                FinishAccept(currentQuest);
                return;
            }

            StatusMessage = InsufficientFundsMessage;
        }

        /// <summary>#50: accept a generic decoration request with the chosen
        /// option — still one linear action, just parameterized. Same
        /// stay-open-on-failure behavior as AcceptCurrent (#186).</summary>
        public void AcceptChoice(string chosenItem)
        {
            if (currentQuest == null || State == null)
            {
                Close();
                return;
            }

            if (State.Quests.AcceptWithChoice(currentQuest, chosenItem))
            {
                FinishAccept(currentQuest);
                return;
            }

            StatusMessage = InsufficientFundsMessage;
        }

        private void FinishAccept(Doggiehood.Core.Quests.Quest accepted)
        {
            Doggiehood.Core.Audio.AudioEventBus.Publish(Doggiehood.Core.Audio.SfxEvent.UiConfirm);
            if (Director != null)
            {
                Director.OnQuestAccepted(accepted);
            }

            QuestAccepted?.Invoke(accepted);
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
            StatusMessage = null;
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
                // Generic decoration request (#50): one pill per option,
                // its cost shown and greyed out when unaffordable (#186).
                foreach (var option in currentQuest.Options)
                {
                    GUI.enabled = OptionIsAffordable(option);
                    if (GUILayout.Button(OptionLabel(option)))
                    {
                        AcceptChoice(option);
                        GUI.enabled = true;
                        break;
                    }
                }

                GUI.enabled = true;
            }
            else
            {
                GUI.enabled = AcceptIsAffordable;
                if (GUILayout.Button(AcceptLabel))
                {
                    AcceptCurrent();
                }

                GUI.enabled = true;
            }

            if (!string.IsNullOrEmpty(StatusMessage))
            {
                GUILayout.Label(StatusMessage);
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
