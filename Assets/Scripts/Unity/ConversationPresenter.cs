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

        // #273 interim graybox readability bump. Default IMGUI sizes are too
        // small to read on-device, so the dialogue text and action buttons are
        // roughly doubled — WITHOUT touching the panel box (its size/position
        // stay fixed, see the panel-box constants below). This is NOT the
        // permanent Candy-Cottage styling; the wireframe pixel values
        // (BodyFontPx 34, 96px pills) live in docs/specs/ui/conversation-panel.md
        // (#175) and are unchanged by this interim tuning.
        //
        // Baselines approximate the default IMGUI skin's effective sizes so the
        // doubled constants read as an explicit ~2x bump (#161: named, not inline).
        public const int BaselineFontPx = 12;
        public const int BaselineButtonHeightPx = 24;

        /// <summary>Dialogue/status/label font size — ~2x the default IMGUI size (#273).</summary>
        public const int DialogueFontPx = BaselineFontPx * 2;

        /// <summary>Action-button minimum height — ~2x the default IMGUI button height (#273).</summary>
        public const int ButtonMinHeightPx = BaselineButtonHeightPx * 2;

        /// <summary>Padding inside the enlarged accept/option/decline pills (#273).</summary>
        public const int ButtonPaddingPx = 12;

        // Panel-box geometry (#161: named, not inline). Unchanged by #273 — the
        // box keeps the same size and position while its content grows.
        private const float PanelMaxWidthPx = 600f;
        private const float PanelHorizontalMarginPx = 40f;
        private const float PanelTopFraction = 0.6f;
        private const float PanelHeightFraction = 0.35f;

        private Vector2 scrollPosition;

        /// <summary>The fixed graybox panel rect: centered, width clamped to
        /// min(600, screenWidth - 40), sitting at 60% down the screen and
        /// filling 35% of its height. Extracted so an EditMode test can pin
        /// that #273 did not move or resize the box.</summary>
        public static Rect ComputePanelRect(float screenWidth, float screenHeight)
        {
            var width = Mathf.Min(PanelMaxWidthPx, screenWidth - PanelHorizontalMarginPx);
            return new Rect(
                (screenWidth - width) / 2f,
                screenHeight * PanelTopFraction,
                width,
                screenHeight * PanelHeightFraction);
        }

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

            // #273: enlarged dialogue/label and button styles (~2x default IMGUI).
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = DialogueFontPx,
                wordWrap = true,
            };
            var buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = DialogueFontPx,
                padding = new RectOffset(ButtonPaddingPx, ButtonPaddingPx, ButtonPaddingPx, ButtonPaddingPx),
            };
            var buttonMinHeight = GUILayout.MinHeight(ButtonMinHeightPx);

            GUILayout.BeginArea(ComputePanelRect(Screen.width, Screen.height), GUI.skin.box);

            // #273: the box stays a fixed 0.35x height while the doubled text and
            // buttons grow, so the body scrolls rather than clipping when it
            // overflows (e.g. a multi-line request with all its action pills).
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            foreach (var line in Current.Lines)
            {
                GUILayout.Label(line, labelStyle);
            }

            if (currentQuest != null && currentQuest.Options.Count > 0)
            {
                // Generic decoration request (#50): one pill per option,
                // its cost shown and greyed out when unaffordable (#186).
                foreach (var option in currentQuest.Options)
                {
                    GUI.enabled = OptionIsAffordable(option);
                    if (GUILayout.Button(OptionLabel(option), buttonStyle, buttonMinHeight))
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
                if (GUILayout.Button(AcceptLabel, buttonStyle, buttonMinHeight))
                {
                    AcceptCurrent();
                }

                GUI.enabled = true;
            }

            if (!string.IsNullOrEmpty(StatusMessage))
            {
                GUILayout.Label(StatusMessage, labelStyle);
            }

            // #185: "Not now" is always present, regardless of accept-row
            // variant (standard/choice/buy-something) — a silent decline.
            if (GUILayout.Button("Not now", buttonStyle, buttonMinHeight))
            {
                DeclineCurrent();
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
