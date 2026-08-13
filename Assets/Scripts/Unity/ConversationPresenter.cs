using System.Collections.Generic;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Holds the currently open conversation (#11) and renders it in the shared
    /// Candy Cottage UGUI chrome (#65/#408): a bottom-center <c>DialogueBox</c>
    /// shell (name-tag tab + linear request body + right-aligned
    /// <c>PillButton</c> action row) built under the #256 <see cref="UiCanvas"/>,
    /// matching the approved wireframe (docs/specs/ui/conversation-panel.md,
    /// #175). Game logic never lives here — the Conversation itself comes from
    /// Core, and cost/affordability display defers entirely to
    /// <see cref="QuestPurchasePresentation"/> (#186); this class only wires
    /// those Core queries onto the panel.
    ///
    /// Chrome comes from the device-safe <see cref="CandyChromeUgui"/> (#298):
    /// rounded panel + pill buttons through the always-included <c>UI/Default</c>
    /// material (no custom shader to strip) and text uses the bundled font loaded
    /// via Resources (never an editor-only builtin) — both the #291 safety
    /// patterns the merged overlays use. Mirrors the retained-UGUI structure of
    /// <see cref="ConfirmationDialog"/> rather than IMGUI-in-OnGUI.
    /// </summary>
    public sealed class ConversationPresenter : MonoBehaviour
    {
        // docs/specs/ui/conversation-panel.md (#175): "greys out when
        // unaffordable" and a failed purchase must leave the panel open
        // (#186) rather than closing with no player-visible feedback.
        private const string InsufficientFundsMessage = "Not enough coins for that yet.";

        private const string NotNowLabel = "Not now";

        // #472: the reminder's dismiss pill reuses the exact non-punishing
        // "Not now" close, just relabeled for the active-quest context.
        private const string StillLookingLabel = "Still looking";

        // --- Wireframe layout constants (docs/specs/ui/conversation-panel.md,
        // #175 / #161: named, never inline) ---

        /// <summary>Panel width, centered (PanelWidthPx).</summary>
        public const float PanelWidthPx = 1040f;

        /// <summary>Gap below the bottom-center panel (PanelBottomMarginPx).</summary>
        public const float PanelBottomMarginPx = 64f;

        /// <summary>Request text size (BodyFontPx).</summary>
        public const int BodyFontPx = 34;

        // --- Shared DialogueBox shell constants (#173, shared-components.md) ---
        public const float PaddingPx = 40f;
        public const float PanelRadiusPx = CandyChromeUgui.PanelRadiusPx;
        public const float PanelShadowPx = 12f;
        public const float NameTagOffsetPx = 28f;
        public const float ActionGapPx = 20f;

        // Shared PillButton (#173): 96px pills, label inset by ButtonPaddingXPx.
        public const float ButtonHeightPx = 96f;
        private const int ButtonFontPx = 36;
        private const float ButtonPaddingXPx = 48f;

        // Name tag (gold overlapping tab) sizing.
        private const int NameTagFontPx = 26;
        private const float NameTagHeightPx = 56f;
        private const float NameTagPaddingXPx = 30f;
        private const float NameTagLeftPx = 36f;

        // Vertical rhythm inside the panel.
        private const float BodyTopGapPx = 14f;
        private const float StatusGapPx = 12f;
        private const float BodyActionGapPx = 30f;

        /// <summary>#291: the bundled UI font, loaded from Resources so it ships
        /// in the Android build (runtime UGUI cannot use the editor-only builtin
        /// font). Same asset the sibling overlays use.</summary>
        private const string LabelFontResource = "DejaVuSans";
        private static Font labelFont;

        // --- UGUI view (built by Init, null in logic-only tests) ---
        private GameObject content;
        private RectTransform cardRect;
        private RectTransform nameTagRect;
        private Text nameTagText;
        private Text bodyText;
        private Text statusText;

        private readonly List<ActionPill> allPills = new List<ActionPill>();
        private readonly List<ActionPill> acceptPills = new List<ActionPill>();
        private ActionPill declinePill;

        private string currentDogName;

        public Conversation Current { get; private set; }

        /// <summary>Set by WorldBootstrap; when present, conversations use
        /// the dog's real quest instance and Accept flows into Core.</summary>
        public Doggiehood.Core.World.GameState State { get; set; }
        public QuestDirector Director { get; set; }

        private Doggiehood.Core.Quests.Quest currentQuest;

        /// <summary>#472: true when the open conversation is a reminder for an
        /// already-Accepted quest (dismiss-only), false for an Available offer
        /// (accept/decline). Drives the action row's single "Still looking" pill.</summary>
        private bool isReminder;

        /// <summary>#472: reminder lines are pure-random each fire (no anti-repeat,
        /// no persisted state), matching the template's Model 2 convention — a
        /// plain unseeded RNG in this Unity-facing layer.</summary>
        private readonly System.Random reminderRng = new System.Random();

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

        // --- Test/wiring surface ---
        public RectTransform PanelRect => cardRect;
        public Text NameTagLabel => nameTagText;
        public Text BodyLabel => bodyText;
        public Text StatusLabel => statusText;
        public ActionPill DeclinePill => declinePill;
        public IReadOnlyList<ActionPill> AcceptPills => acceptPills;

        /// <summary>One action-row pill: the tap <see cref="Button"/>, its Candy
        /// Cottage <see cref="Image"/> chrome (greyed to
        /// <see cref="CandyChromeUgui.Disabled"/> when unaffordable), and the
        /// label. Exposed so EditMode tests can assert the row without a Play-mode
        /// frame.</summary>
        public sealed class ActionPill
        {
            public ActionPill(Button button, Image image, Text label)
            {
                Button = button;
                Image = image;
                Label = label;
            }

            public Button Button { get; }
            public Image Image { get; }
            public Text Label { get; }
        }

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

        /// <summary>Builds the DialogueBox hierarchy (expected under a
        /// <see cref="UiCanvas"/>) and starts closed. Logic-only tests that
        /// never call this exercise the Core wiring with no view attached.</summary>
        public void Init()
        {
            Build();
            content.SetActive(false);
        }

        /// <summary>Opens the dog's conversation; a no-op for dogs without
        /// an active quest (Core returns null for those). An <c>Available</c>
        /// quest opens the templated accept/decline offer; an already-<c>Accepted</c>
        /// quest opens a dismiss-only contextual reminder instead of falling
        /// through to the stale <see cref="ConversationStarter"/> placeholder
        /// (#472).</summary>
        public bool TryOpen(Dog dog)
        {
            StatusMessage = null;
            isReminder = false;
            currentDogName = dog.Name;

            if (State != null)
            {
                currentQuest = System.Linq.Enumerable.FirstOrDefault(
                    State.Quests.ActiveQuests,
                    q => q.DogName == dog.Name && q.Status == Doggiehood.Core.Quests.QuestStatus.Available);

                if (currentQuest != null)
                {
                    Current = new Conversation(currentQuest.DialogueLines, ConversationEnding.Accept);
                    ShowView();
                    Opened?.Invoke(dog);
                    return true;
                }

                // #472: the "active" quest re-tapping should remind about — a
                // quest already accepted for this dog. Render its pooled reminder
                // line and show a dismiss-only "Still looking" action row.
                var acceptedQuest = System.Linq.Enumerable.FirstOrDefault(
                    State.Quests.ActiveQuests,
                    q => q.DogName == dog.Name && q.Status == Doggiehood.Core.Quests.QuestStatus.Accepted);

                if (acceptedQuest != null)
                {
                    currentQuest = acceptedQuest;
                    isReminder = true;
                    // #701: subject-aware selection here too, so the reminder is
                    // drawn from the same pools the opener/closer came from and
                    // never re-promises a mechanic this quest doesn't run.
                    var reminderLine = Doggiehood.Core.Quests.QuestTemplates
                        .For(acceptedQuest.Type, acceptedQuest.ItemName)
                        .RenderReminder(dog, acceptedQuest.ItemName, reminderRng);
                    Current = new Conversation(new[] { reminderLine }, ConversationEnding.Accept);
                    ShowView();
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
            ShowView();
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
            SyncView();
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
            SyncView();
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
            isReminder = false;
            StatusMessage = null;
            if (content != null)
            {
                content.SetActive(false);
            }

            // #568: a closed conversation no longer blocks world taps. The gate's
            // ClosedThisFrame latch (cleared in InputRouter.LateUpdate) keeps the
            // dismissing tap consumed for the rest of this frame, so it can't fall
            // through to the object underneath.
            Doggiehood.Core.Cameras.ModalInputGate.Shared.Unregister(this);
        }

        // ---------------------------------------------------------------
        // View (thin, geometry-only — every number is a named constant)
        // ---------------------------------------------------------------

        private void ShowView()
        {
            // #568: the conversation panel is modal for world taps too. Unlike
            // the center-anchored overlays it has no full-screen scrim, so the
            // #422 IsPointerOverUi guard only ever covered its own button/panel
            // rects — a tap just outside them leaked to the world. Registering
            // with the shared gate on open (and unregistering in Close) blocks
            // the world raycast while it's up, matching DogProfileOverlay /
            // HouseProfileOverlay / ConfirmationDialog / WelcomePopup /
            // OnboardingRewardPanel. Registered before the view null-guard so the
            // gate is driven even in logic-only tests with no view attached.
            Doggiehood.Core.Cameras.ModalInputGate.Shared.Register(this);

            if (content == null)
            {
                return;
            }

            SyncView();
            content.SetActive(true);
        }

        /// <summary>Repopulates the name tag, body, status, and action row from
        /// the current Core state and re-lays out the panel. Null-guarded so the
        /// Core wiring works with no view attached (logic-only tests).</summary>
        private void SyncView()
        {
            if (content == null)
            {
                return;
            }

            nameTagText.text = currentDogName ?? string.Empty;
            bodyText.text = Current != null ? string.Join("\n", Current.Lines) : string.Empty;

            var hasStatus = !string.IsNullOrEmpty(StatusMessage);
            statusText.text = StatusMessage ?? string.Empty;
            statusText.gameObject.SetActive(hasStatus);

            RebuildActionRow();
            LayoutPanel(hasStatus);
        }

        /// <summary>Rebuilds the right-aligned action row for the open quest:
        /// "Not now" (leftmost, always present, #185) then either one option pill
        /// per decoration choice (#50) or a single Accept/Complete/Buy pill —
        /// each greyed + non-interactive when unaffordable (#186). For an
        /// active-quest reminder (#472) the row is that single dismiss pill alone,
        /// relabeled "Still looking" with no accept/complete affordance.</summary>
        private void RebuildActionRow()
        {
            foreach (var pill in allPills)
            {
                DestroyView(pill.Button.gameObject);
            }

            allPills.Clear();
            acceptPills.Clear();

            declinePill = CreatePill(
                "NotNowPill",
                isReminder ? StillLookingLabel : NotNowLabel,
                CandyChromeUgui.Cream,
                interactable: true);
            declinePill.Button.onClick.AddListener(DeclineCurrent);
            allPills.Add(declinePill);

            if (isReminder)
            {
                // #472: an active-quest reminder is dismiss-only — no accept or
                // complete pill, matching Option A (no give-up mechanic here).
                return;
            }

            if (currentQuest != null && currentQuest.Options.Count > 0)
            {
                foreach (var option in currentQuest.Options)
                {
                    var chosen = option;
                    var affordable = OptionIsAffordable(chosen);
                    var pill = CreatePill(
                        "OptionPill",
                        OptionLabel(chosen),
                        affordable ? CandyChromeUgui.Coral : CandyChromeUgui.Disabled,
                        affordable);
                    pill.Button.onClick.AddListener(() => AcceptChoice(chosen));
                    acceptPills.Add(pill);
                    allPills.Add(pill);
                }
            }
            else
            {
                var affordable = AcceptIsAffordable;
                var isBuy = currentQuest != null && currentQuest.Cost.HasValue;
                var tint = affordable
                    ? (isBuy ? CandyChromeUgui.Coral : CandyChromeUgui.Leaf)
                    : CandyChromeUgui.Disabled;
                var pill = CreatePill("AcceptPill", AcceptLabel, tint, affordable);
                pill.Button.onClick.AddListener(AcceptCurrent);
                acceptPills.Add(pill);
                allPills.Add(pill);
            }
        }

        /// <summary>Stacks the name tab, body (at its wrapped height), the status
        /// line (when present), and the action row down the panel, then sizes and
        /// anchors the panel bottom-center — so it grows upward with the request
        /// length (conversation-panel.md).</summary>
        private void LayoutPanel(bool hasStatus)
        {
            var nameWidth = nameTagText.preferredWidth + NameTagPaddingXPx * 2f;
            PlaceTopLeft(nameTagRect, NameTagLeftPx, -NameTagOffsetPx, nameWidth, NameTagHeightPx);

            var bodyTop = PaddingPx + BodyTopGapPx;
            // Width first (anchorMin==anchorMax makes rect.width == sizeDelta.x
            // synchronously), so preferredHeight wraps at the inner width.
            PlaceTopLeft(bodyText.rectTransform, PaddingPx, bodyTop, InnerWidth(), BodyFontPx);
            var bodyHeight = Mathf.Max(BodyFontPx, bodyText.preferredHeight);
            bodyText.rectTransform.sizeDelta = new Vector2(InnerWidth(), bodyHeight);

            var cursor = bodyTop + bodyHeight;
            if (hasStatus)
            {
                var statusTop = cursor + StatusGapPx;
                PlaceTopLeft(statusText.rectTransform, PaddingPx, statusTop, InnerWidth(), BodyFontPx);
                var statusHeight = Mathf.Max(BodyFontPx, statusText.preferredHeight);
                statusText.rectTransform.sizeDelta = new Vector2(InnerWidth(), statusHeight);
                cursor = statusTop + statusHeight;
            }

            var actionTop = cursor + BodyActionGapPx;
            LayoutActionRow(actionTop);

            var panelHeight = actionTop + ButtonHeightPx + PaddingPx;
            AnchorBottomCenter(panelHeight);
        }

        /// <summary>Right-aligns the pills within the inner content width, left to
        /// right in build order ("Not now" then the accept/option pills), each
        /// sized to its label (conversation-panel.md action row).</summary>
        private void LayoutActionRow(float actionTop)
        {
            var widths = new float[allPills.Count];
            var total = 0f;
            for (var i = 0; i < allPills.Count; i++)
            {
                widths[i] = allPills[i].Label.preferredWidth + ButtonPaddingXPx * 2f;
                total += widths[i];
            }

            total += ActionGapPx * Mathf.Max(0, allPills.Count - 1);

            var runningX = InnerWidth() - total;
            for (var i = 0; i < allPills.Count; i++)
            {
                var rect = (RectTransform)allPills[i].Button.transform;
                PlaceTopLeft(rect, PaddingPx + runningX, actionTop, widths[i], ButtonHeightPx);
                runningX += widths[i] + ActionGapPx;
            }
        }

        private void Build()
        {
            var cardImage = CreateImage("ConversationPanel", transform, CandyChromeUgui.Panel);
            content = cardImage.gameObject;
            cardRect = cardImage.rectTransform;
            CandyChromeUgui.ApplyRounded(cardImage, CandyChromeUgui.Panel, PanelRadiusPx, withShadow: true);
            // DialogueBox's drop-shadow is PanelShadowPx (distinct from the shared
            // baseline offset); override the offset ApplyRounded set.
            CandyChromeUgui.AddShadow(cardImage.gameObject).effectDistance = new Vector2(0f, -PanelShadowPx);
            AnchorBottomCenter(NameTagHeightPx + PaddingPx * 2f + ButtonHeightPx);

            var nameTagImage = CreateImage("NameTag", cardRect, CandyChromeUgui.Gold);
            nameTagRect = nameTagImage.rectTransform;
            CandyChromeUgui.ApplyPill(nameTagImage, CandyChromeUgui.Gold, NameTagHeightPx, withShadow: true);
            nameTagText = CreateLabel("NameTagLabel", nameTagRect, string.Empty, NameTagFontPx, TextAnchor.MiddleCenter);
            InsetX(nameTagText.rectTransform, NameTagPaddingXPx);

            bodyText = CreateLabel("Body", cardRect, string.Empty, BodyFontPx, TextAnchor.UpperLeft);
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;

            statusText = CreateLabel("Status", cardRect, string.Empty, BodyFontPx, TextAnchor.UpperLeft);
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.gameObject.SetActive(false);
        }

        private void AnchorBottomCenter(float panelHeight)
        {
            cardRect.anchorMin = new Vector2(0.5f, 0f);
            cardRect.anchorMax = new Vector2(0.5f, 0f);
            cardRect.pivot = new Vector2(0.5f, 0f);
            cardRect.sizeDelta = new Vector2(PanelWidthPx, panelHeight);
            cardRect.anchoredPosition = new Vector2(0f, PanelBottomMarginPx);
        }

        private ActionPill CreatePill(string name, string label, Color fill, bool interactable)
        {
            var image = CreateImage(name, cardRect, fill);
            CandyChromeUgui.ApplyPill(image, fill, ButtonHeightPx, withShadow: true);
            var text = CreateLabel(name + "Label", image.rectTransform, label, ButtonFontPx, TextAnchor.MiddleCenter);
            InsetX(text.rectTransform, ButtonPaddingXPx);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = interactable;
            return new ActionPill(button, image, text);
        }

        private void DestroyView(GameObject go)
        {
            if (Application.isPlaying)
            {
                Destroy(go);
            }
            else
            {
                DestroyImmediate(go);
            }
        }

        private static float InnerWidth()
        {
            return PanelWidthPx - PaddingPx * 2f;
        }

        // --- small UGUI helpers (mirror ConfirmationDialog) ---

        private static RectTransform PlaceTopLeft(RectTransform rect, float x, float yFromTop, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -yFromTop);
            return rect;
        }

        private static void InsetX(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, 0f);
            rect.offsetMax = new Vector2(-inset, 0f);
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateLabel(string name, Transform parent, string value, int fontSize, TextAnchor anchor)
        {
            var text = CreateRect(name, parent).gameObject.AddComponent<Text>();
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = CandyChromeUgui.Ink;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.font = LabelFont();
            return text;
        }

        private static Font LabelFont()
        {
            if (labelFont == null)
            {
                labelFont = Resources.Load<Font>(LabelFontResource);
            }

            return labelFont;
        }
    }
}
