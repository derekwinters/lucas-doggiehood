using UnityEngine;
using UnityEngine.UI;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #372/#374: the single, reusable onboarding reward/celebration panel
    /// (approved wireframe docs/specs/ui/onboarding-reward.md /
    /// mockups/onboarding-reward.html). Raised each time an onboarding
    /// reward-chain step (#316) pays out — finish the first quest, upgrade a
    /// house, expand the map, build a house — it tells a young player,
    /// unmistakably, "you did that, and here's your reward," calling out the coin
    /// payout that today lands silently.
    ///
    /// <para>Deliberately a bespoke celebration, not the neutral
    /// <see cref="ConfirmationDialog"/>: a single big gold star medal overlapping
    /// the panel's top edge, a fixed "You did it!" heading, one dynamic
    /// accomplishment line (the card grows vertically with it), and one leaf pill
    /// button that IS the payout — "+100 coins" with a gold coin token. One
    /// instance, parameterized by message + amount; the heading and chrome are
    /// constant.</para>
    ///
    /// <para>Always dismissible (button OR scrim), never a trap (#329). Pure
    /// presentation over the existing Core deposit — it shows the coins the
    /// reward chain already granted and never moves any itself; the currency chip
    /// updates on its own off <c>Wallet.Coins</c>. Chrome comes from the
    /// device-safe <see cref="CandyChromeUgui"/> (#298) and text from the bundled
    /// font (#291); no custom shader, no editor-only builtin. Built under the #256
    /// <see cref="UiCanvas"/> CanvasScaler so each px constant keeps a fixed
    /// on-screen meaning across tablet sizes.</para>
    /// </summary>
    public sealed class OnboardingRewardPanel : MonoBehaviour
    {
        // --- Layout constants from the approved wireframe (#161, #374) ---
        public const float RewardWidthPx = 820f;
        public const float RewardPaddingPx = 56f;
        public const float MedalDiameterPx = 176f;
        public const float MedalOverlapPx = 88f;
        public const float MedalOutlineThicknessPx = 8f;
        public const float MedalTopGapPx = 28f;
        public const int HeadingFontSizePx = 60;
        public const int MessageFontSizePx = 34;
        public const float HeadingMessageGapPx = 16f;
        public const float MessageActionMarginPx = 44f;
        public const float ActionMinWidthPx = 320f;
        public const float ButtonCoinDiameterPx = 56f;
        public const float ButtonCoinGapPx = 18f;

        // Shared PillButton (#173, shared-components.md): the +N coins button is a
        // 96px pill with the label inset ButtonPaddingXPx from the caps.
        public const float ButtonHeightPx = 96f;
        private const float ButtonPaddingXPx = 48f;
        private const int ButtonFontSizePx = 36;

        /// <summary>The fixed celebratory headline — constant across every step
        /// (only the message line and amount are dynamic).</summary>
        public const string HeadingText = "You did it!";

        // The big ink star inside the medal. The mockup renders it as a text glyph
        // at 92px (mockups/onboarding-reward.html .medal .star); kept as a named
        // constant rather than an inline literal (#161).
        private const string MedalStarGlyph = "★";
        private const int MedalStarFontSizePx = 92;

        /// <summary>#291: the bundled UI font, loaded from Resources so it ships
        /// in the Android build (runtime UGUI cannot use the editor-only builtin
        /// font). Same asset the confirmation dialog and profile overlays use.</summary>
        private const string LabelFontResource = "DejaVuSans";
        private static Font labelFont;

        // Scrim tint from the mockup (rgba(46,42,38,.42)).
        private static readonly Color ScrimColor = new Color(46f / 255f, 42f / 255f, 38f / 255f, 0.42f);

        private GameObject content;
        private RectTransform cardRect;
        private RectTransform scrimRect;
        private RectTransform medalRect;
        private RectTransform headingRect;
        private RectTransform messageRect;
        private RectTransform actionButtonRect;
        private RectTransform buttonCoinRect;
        private RectTransform actionLabelRect;
        private Image medalImage;
        private Image actionButtonImage;
        private Text medalStarText;
        private Text headingText;
        private Text messageText;
        private Text actionText;
        private Button actionButton;

        // --- Test/wiring surface ---
        public RectTransform CardRect => cardRect;
        public RectTransform ScrimRect => scrimRect;
        public RectTransform MedalRect => medalRect;
        public RectTransform ButtonCoinRect => buttonCoinRect;
        public RectTransform ActionButtonRect => actionButtonRect;
        public Image MedalImage => medalImage;
        public Image ActionButtonImage => actionButtonImage;
        public Text MedalStarLabel => medalStarText;
        public Text HeadingLabel => headingText;
        public Text MessageLabel => messageText;
        public Text ActionLabel => actionText;
        public Button ActionButton => actionButton;

        /// <summary>Whether the celebration is currently shown.</summary>
        public bool IsOpen => content != null && content.activeSelf;

        /// <summary>Builds the panel hierarchy (expected under a
        /// <see cref="UiCanvas"/>) and starts closed.</summary>
        public void Init()
        {
            Build();
            content.SetActive(false);
        }

        /// <summary>
        /// Raises the celebration for one onboarding step. <paramref name="message"/>
        /// is the dynamic accomplishment line (from
        /// <see cref="OnboardingRewardCopy"/>); <paramref name="amount"/> is the
        /// coin payout the reward chain already deposited, which becomes the
        /// button label "+N coins". The heading and chrome are constant.
        /// </summary>
        public void Show(string message, int amount)
        {
            messageText.text = message;
            actionText.text = "+" + amount.ToString(System.Globalization.CultureInfo.InvariantCulture) + " coins";

            LayoutCard();
            content.SetActive(true);

            // #544: this modal now blocks world taps behind its scrim.
            Doggiehood.Core.Cameras.ModalInputGate.Shared.Register(this);
        }

        /// <summary>Button or scrim tap: dismisses the celebration. A reward is an
        /// acknowledgement, not a choice — there is no decline, and no path leaves
        /// it stuck open (#329).</summary>
        public void Dismiss()
        {
            if (content != null)
            {
                content.SetActive(false);
            }

            // #544: dismissed panel no longer blocks world taps.
            Doggiehood.Core.Cameras.ModalInputGate.Shared.Unregister(this);
        }

        private void OnDestroy()
        {
            // #544: a destroyed panel is never "open" — release the modal block
            // so it can't leak past teardown / scene unload.
            Doggiehood.Core.Cameras.ModalInputGate.Shared.Unregister(this);
        }

        // ---------------------------------------------------------------
        // Building (thin, geometry-only — every number is a named constant)
        // ---------------------------------------------------------------

        private void Build()
        {
            content = new GameObject("OnboardingRewardContent");
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.SetParent(transform, false);
            Stretch(contentRect);

            var scrimImage = CreateImage("Scrim", contentRect, ScrimColor);
            scrimRect = scrimImage.rectTransform;
            Stretch(scrimRect);
            scrimRect.gameObject.AddComponent<Button>().onClick.AddListener(Dismiss);

            BuildCard(contentRect);
        }

        private void BuildCard(RectTransform parent)
        {
            var cardImage = CreateImage("Card", parent, CandyChromeUgui.Panel);
            cardRect = cardImage.rectTransform;
            Center(cardRect, RewardWidthPx, RewardPaddingPx * 2f);
            CandyChromeUgui.ApplyRounded(cardImage, CandyChromeUgui.Panel, CandyChromeUgui.PanelRadiusPx, withShadow: true);

            headingText = CreateLabel("Heading", cardRect, HeadingText, HeadingFontSizePx, TextAnchor.UpperCenter);
            headingRect = headingText.rectTransform;

            messageText = CreateLabel("Message", cardRect, string.Empty, MessageFontSizePx, TextAnchor.UpperCenter);
            messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            messageRect = messageText.rectTransform;

            BuildActionButton(cardRect);
            BuildMedal(cardRect);
        }

        /// <summary>The single leaf pill that IS the payout: a gold coin token +
        /// the "+N coins" label, both centered as a group inside the pill.</summary>
        private void BuildActionButton(RectTransform parent)
        {
            actionButtonImage = CreateImage("ActionButton", parent, CandyChromeUgui.Leaf);
            actionButtonRect = actionButtonImage.rectTransform;
            CandyChromeUgui.ApplyPill(actionButtonImage, CandyChromeUgui.Leaf, ButtonHeightPx, withShadow: true);

            var coinImage = CreateImage("Coin", actionButtonRect, CandyChromeUgui.Gold);
            buttonCoinRect = coinImage.rectTransform;
            buttonCoinRect.anchorMin = new Vector2(0f, 0.5f);
            buttonCoinRect.anchorMax = new Vector2(0f, 0.5f);
            buttonCoinRect.pivot = new Vector2(0f, 0.5f);
            buttonCoinRect.sizeDelta = new Vector2(ButtonCoinDiameterPx, ButtonCoinDiameterPx);
            CandyChromeUgui.ApplyPill(coinImage, CandyChromeUgui.Gold, ButtonCoinDiameterPx, withShadow: false);

            actionText = CreateLabel("ActionLabel", actionButtonRect, string.Empty, ButtonFontSizePx, TextAnchor.MiddleLeft);
            actionLabelRect = actionText.rectTransform;
            actionLabelRect.anchorMin = new Vector2(0f, 0.5f);
            actionLabelRect.anchorMax = new Vector2(0f, 0.5f);
            actionLabelRect.pivot = new Vector2(0f, 0.5f);

            actionButton = actionButtonRect.gameObject.AddComponent<Button>();
            actionButton.onClick.AddListener(Dismiss);
        }

        /// <summary>The gold star medal overlapping the panel's top edge, with its
        /// own thicker ink ring (<see cref="MedalOutlineThicknessPx"/>) and a big
        /// ink star glyph.</summary>
        private void BuildMedal(RectTransform parent)
        {
            medalImage = CreateImage("Medal", parent, CandyChromeUgui.Gold);
            medalRect = medalImage.rectTransform;
            medalRect.anchorMin = new Vector2(0.5f, 1f);
            medalRect.anchorMax = new Vector2(0.5f, 1f);
            medalRect.pivot = new Vector2(0.5f, 0.5f);
            medalRect.sizeDelta = new Vector2(MedalDiameterPx, MedalDiameterPx);
            // Center sits (overlap - radius) above the card's top edge, so
            // MedalOverlapPx of the medal rises above it.
            medalRect.anchoredPosition = new Vector2(0f, MedalOverlapPx - MedalDiameterPx / 2f);
            CandyChromeUgui.ApplyPill(medalImage, CandyChromeUgui.Gold, MedalDiameterPx, withShadow: true);
            // The medal wears its own thicker ink ring, distinct from the shared
            // 6px panel/button outline — re-sizes the shared contour band (#616).
            CandyChromeUgui.AddOutline(medalImage.gameObject,
                MedalDiameterPx / 2f, MedalOutlineThicknessPx);

            medalStarText = CreateLabel("Star", medalRect, MedalStarGlyph, MedalStarFontSizePx, TextAnchor.MiddleCenter);
            Stretch(medalStarText.rectTransform);
        }

        /// <summary>Stacks the medal-cleared heading, the message (at its wrapped
        /// height), and the payout button down the card, then sizes the card to
        /// fit — so it grows vertically with the message (onboarding-reward.md).</summary>
        private void LayoutCard()
        {
            // Heading sits below the medal's lower half + the medal->heading gap.
            var headingTop = (MedalDiameterPx - MedalOverlapPx) + MedalTopGapPx;
            PlaceTopLeft(headingRect, RewardPaddingPx, headingTop, InnerWidth(), HeadingFontSizePx);

            var messageTop = headingTop + HeadingFontSizePx + HeadingMessageGapPx;
            // Width first (anchorMin==anchorMax makes rect.width == sizeDelta.x
            // synchronously) so preferredHeight wraps at the inner width.
            PlaceTopLeft(messageRect, RewardPaddingPx, messageTop, InnerWidth(), MessageFontSizePx);
            var messageHeight = Mathf.Max(MessageFontSizePx, messageText.preferredHeight);
            messageRect.sizeDelta = new Vector2(InnerWidth(), messageHeight);

            var actionTop = messageTop + messageHeight + MessageActionMarginPx;
            LayoutActionButton(actionTop);

            var cardHeight = actionTop + ButtonHeightPx + RewardPaddingPx;
            cardRect.sizeDelta = new Vector2(RewardWidthPx, cardHeight);
        }

        /// <summary>Sizes the pill to fit its content (at least
        /// <see cref="ActionMinWidthPx"/>), centers it horizontally in the card,
        /// and centers the coin + label group inside it.</summary>
        private void LayoutActionButton(float actionTop)
        {
            var labelWidth = actionText.preferredWidth;
            var groupWidth = ButtonCoinDiameterPx + ButtonCoinGapPx + labelWidth;
            var buttonWidth = Mathf.Max(ActionMinWidthPx, groupWidth + 2f * ButtonPaddingXPx);
            var buttonLeft = (RewardWidthPx - buttonWidth) / 2f;
            PlaceTopLeft(actionButtonRect, buttonLeft, actionTop, buttonWidth, ButtonHeightPx);

            var startX = (buttonWidth - groupWidth) / 2f;
            buttonCoinRect.anchoredPosition = new Vector2(startX, 0f);
            actionLabelRect.anchoredPosition = new Vector2(startX + ButtonCoinDiameterPx + ButtonCoinGapPx, 0f);
            actionLabelRect.sizeDelta = new Vector2(labelWidth, ButtonFontSizePx);
        }

        private static float InnerWidth()
        {
            return RewardWidthPx - RewardPaddingPx * 2f;
        }

        // --- small UGUI helpers (mirrors ConfirmationDialog) ---

        private static RectTransform PlaceTopLeft(RectTransform rect, float x, float yFromTop, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -yFromTop);
            return rect;
        }

        private static RectTransform CreateRect(string name, RectTransform parent)
        {
            var go = new GameObject(name);
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Image CreateImage(string name, RectTransform parent, Color color)
        {
            var image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateLabel(string name, RectTransform parent, string value, int fontSize, TextAnchor anchor)
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

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Center(RectTransform rect, float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
