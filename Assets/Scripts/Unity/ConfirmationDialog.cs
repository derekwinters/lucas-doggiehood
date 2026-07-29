using System;
using UnityEngine;
using UnityEngine.UI;

namespace Doggiehood.Unity
{
    /// <summary>
    /// The reusable "are you sure?" dialog (#343/#344, approved wireframe
    /// docs/specs/ui/confirmation-dialog.md): a compact centered card over a
    /// dim scrim with a DYNAMIC title + body supplied by whatever opens it,
    /// and an action row of two equal-width pill buttons — <b>No</b>
    /// (neutral/cream, left) and <b>Yes</b> (positive/leaf, right). Tapping
    /// the scrim or No cancels (never a trap — a deliberate contrast with the
    /// #329 stuck-dialog bug); Yes runs the caller's confirm callback. When
    /// the confirmed action spends coins, Yes carries a gold coin token + the
    /// caller's amount so a young player sees the price on the button.
    ///
    /// One instance, reused: any screen raises the same overlay by supplying
    /// its title, body, optional cost, and a confirm callback (labels + confirm
    /// tint default to Yes/No + leaf but are overridable for reuse). The first
    /// consumer is the map-expansion zone unlock (#343). Thin wiring only — it
    /// holds no game rules; the caller's callback owns the action.
    ///
    /// Chrome comes from the device-safe <see cref="CandyChromeUgui"/> (#298):
    /// rounded panel + pill buttons through the always-included
    /// <c>UI/Default</c> material (no custom shader to strip), and text uses the
    /// bundled font loaded via Resources (never an editor-only builtin) — both
    /// the #291 safety patterns the merged overlays use. Built under the #256
    /// <see cref="UiCanvas"/> CanvasScaler so each px constant keeps a fixed
    /// on-screen meaning across tablet sizes.
    /// </summary>
    public sealed class ConfirmationDialog : MonoBehaviour
    {
        // --- Layout constants from the approved wireframe (#161) ---
        public const float DialogWidthPx = 760f;
        public const float DialogPaddingPx = 48f;
        public const int TitleFontSizePx = 44;
        public const int BodyFontSizePx = 32;
        public const float TitleBodyGapPx = 20f;
        public const float ActionRowMarginPx = 40f;
        public const float ActionGapPx = 20f;
        public const float CostCoinDiameterPx = 40f;
        public const float CostGapPx = 8f;

        // Shared PillButton (#173, shared-components.md): the No/Yes buttons are
        // 96px pills, label inset PaddingXPx, IconGapPx from label to the coin.
        public const float ButtonHeightPx = 96f;
        private const float ButtonPaddingXPx = 48f;
        private const int ButtonFontSizePx = 36;
        private const float IconGapPx = 16f;
        private const int CostAmountFontSizePx = 32;

        // --- Default labels/tint (overridable per call, but Yes/No + leaf are
        // the shipped defaults per confirmation-dialog.md) ---
        private const string DefaultYesLabel = "Yes";
        private const string DefaultNoLabel = "No";

        /// <summary>#291: the bundled UI font, loaded from Resources so it ships
        /// in the Android build (runtime UGUI cannot use the editor-only builtin
        /// font). Same asset the dog/house profile overlays and settings use.</summary>
        private const string LabelFontResource = "DejaVuSans";
        private static Font labelFont;

        private static readonly Color ScrimColor = new Color(46f / 255f, 42f / 255f, 38f / 255f, 0.46f);

        private GameObject content;
        private RectTransform cardRect;
        private RectTransform scrimRect;
        private RectTransform titleRect;
        private RectTransform bodyRect;
        private RectTransform noButtonRect;
        private RectTransform yesButtonRect;
        private RectTransform costGroupRect;
        private RectTransform costCoinRect;
        private Text titleLabel;
        private Text bodyLabel;
        private Text noLabel;
        private Text yesLabel;
        private Text costAmountLabel;
        private Button noButton;
        private Button yesButton;
        private Image noButtonImage;
        private Image yesButtonImage;

        private Action pendingConfirm;

        // --- Test/wiring surface ---
        public RectTransform CardRect => cardRect;
        public RectTransform ScrimRect => scrimRect;
        public Text TitleLabel => titleLabel;
        public Text BodyLabel => bodyLabel;
        public Text NoLabel => noLabel;
        public Text YesLabel => yesLabel;
        public Text CostAmountLabel => costAmountLabel;
        public Button NoButton => noButton;
        public Button YesButton => yesButton;
        public Image NoButtonImage => noButtonImage;
        public Image YesButtonImage => yesButtonImage;
        public GameObject CostGroup => costGroupRect.gameObject;
        public RectTransform CostCoinRect => costCoinRect;

        /// <summary>Whether the dialog is currently shown.</summary>
        public bool IsOpen => content != null && content.activeSelf;

        /// <summary>Builds the card hierarchy (expected under a <see cref="UiCanvas"/>)
        /// and starts closed.</summary>
        public void Init()
        {
            Build();
            content.SetActive(false);
        }

        /// <summary>
        /// Raises the dialog for one confirmation. <paramref name="title"/> and
        /// <paramref name="body"/> are the dynamic caller-supplied copy;
        /// <paramref name="onConfirm"/> runs on Yes. When
        /// <paramref name="cost"/> is non-null the Yes button shows a gold coin
        /// token + that amount (a spend); null shows just "Yes". Labels and the
        /// confirm tint default to Yes/No + leaf but are overridable for reuse.
        /// </summary>
        public void Open(string title, string body, Action onConfirm, int? cost = null,
            string yesLabelText = null, string noLabelText = null, Color? confirmTint = null)
        {
            pendingConfirm = onConfirm;

            titleLabel.text = title;
            bodyLabel.text = body;
            yesLabel.text = string.IsNullOrEmpty(yesLabelText) ? DefaultYesLabel : yesLabelText;
            noLabel.text = string.IsNullOrEmpty(noLabelText) ? DefaultNoLabel : noLabelText;
            yesButtonImage.color = confirmTint ?? CandyChromeUgui.Leaf;

            costGroupRect.gameObject.SetActive(cost.HasValue);
            if (cost.HasValue)
            {
                costAmountLabel.text = cost.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            LayoutCard();
            LayoutYesButtonContent(cost.HasValue);

            content.SetActive(true);
        }

        /// <summary>Yes: runs the caller's confirm callback, then closes. The
        /// callback is captured and cleared before closing so a callback that
        /// itself opens another dialog can't be double-fired.</summary>
        public void Confirm()
        {
            var callback = pendingConfirm;
            Close();
            callback?.Invoke();
        }

        /// <summary>No / scrim tap: dismisses without acting — the dialog can
        /// never soft-lock the player (#329).</summary>
        public void Cancel()
        {
            Close();
        }

        private void Close()
        {
            pendingConfirm = null;
            if (content != null)
            {
                content.SetActive(false);
            }
        }

        // ---------------------------------------------------------------
        // Building (thin, geometry-only — every number is a named constant)
        // ---------------------------------------------------------------

        private void Build()
        {
            content = new GameObject("ConfirmationDialogContent");
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.SetParent(transform, false);
            Stretch(contentRect);

            var scrimImage = CreateImage("Scrim", contentRect, ScrimColor);
            scrimRect = scrimImage.rectTransform;
            Stretch(scrimRect);
            scrimRect.gameObject.AddComponent<Button>().onClick.AddListener(Cancel);

            BuildCard(contentRect);
        }

        private void BuildCard(RectTransform parent)
        {
            var cardImage = CreateImage("Card", parent, CandyChromeUgui.Panel);
            cardRect = cardImage.rectTransform;
            Center(cardRect, DialogWidthPx, DialogPaddingPx * 2f);
            CandyChromeUgui.ApplyRounded(cardImage, CandyChromeUgui.Panel, CandyChromeUgui.PanelRadiusPx, withShadow: true);

            titleLabel = CreateLabel("Title", cardRect, string.Empty, TitleFontSizePx, TextAnchor.UpperLeft);
            titleRect = titleLabel.rectTransform;

            bodyLabel = CreateLabel("Body", cardRect, string.Empty, BodyFontSizePx, TextAnchor.UpperLeft);
            bodyLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyRect = bodyLabel.rectTransform;

            BuildActionRow(cardRect);
        }

        private void BuildActionRow(RectTransform parent)
        {
            var buttonWidth = (InnerWidth() - ActionGapPx) / 2f;

            noButtonImage = CreateImage("NoButton", parent, CandyChromeUgui.Cream);
            noButtonRect = noButtonImage.rectTransform;
            CandyChromeUgui.ApplyPill(noButtonImage, CandyChromeUgui.Cream, ButtonHeightPx, withShadow: true);
            noLabel = CreateLabel("NoLabel", noButtonRect, DefaultNoLabel, ButtonFontSizePx, TextAnchor.MiddleCenter);
            InsetX(noLabel.rectTransform, ButtonPaddingXPx);
            noButton = noButtonRect.gameObject.AddComponent<Button>();
            noButton.onClick.AddListener(Cancel);

            yesButtonImage = CreateImage("YesButton", parent, CandyChromeUgui.Leaf);
            yesButtonRect = yesButtonImage.rectTransform;
            CandyChromeUgui.ApplyPill(yesButtonImage, CandyChromeUgui.Leaf, ButtonHeightPx, withShadow: true);
            yesLabel = CreateLabel("YesLabel", yesButtonRect, DefaultYesLabel, ButtonFontSizePx, TextAnchor.MiddleCenter);

            BuildCostGroup(yesButtonRect);

            yesButton = yesButtonRect.gameObject.AddComponent<Button>();
            yesButton.onClick.AddListener(Confirm);

            // Widths are fixed here; vertical positions are set in LayoutCard so
            // the row sits below a body of any height.
            noButtonRect.sizeDelta = new Vector2(buttonWidth, ButtonHeightPx);
            yesButtonRect.sizeDelta = new Vector2(buttonWidth, ButtonHeightPx);
        }

        private void BuildCostGroup(RectTransform parent)
        {
            costGroupRect = CreateRect("CostGroup", parent);
            costGroupRect.anchorMin = new Vector2(0f, 0.5f);
            costGroupRect.anchorMax = new Vector2(0f, 0.5f);
            costGroupRect.pivot = new Vector2(0f, 0.5f);

            var coinImage = CreateImage("Coin", costGroupRect, CandyChromeUgui.Gold);
            costCoinRect = coinImage.rectTransform;
            costCoinRect.anchorMin = new Vector2(0f, 0.5f);
            costCoinRect.anchorMax = new Vector2(0f, 0.5f);
            costCoinRect.pivot = new Vector2(0f, 0.5f);
            costCoinRect.sizeDelta = new Vector2(CostCoinDiameterPx, CostCoinDiameterPx);
            costCoinRect.anchoredPosition = Vector2.zero;
            CandyChromeUgui.ApplyPill(coinImage, CandyChromeUgui.Gold, CostCoinDiameterPx, withShadow: false);

            costAmountLabel = CreateLabel("Amount", costGroupRect, string.Empty, CostAmountFontSizePx, TextAnchor.MiddleLeft);
            costAmountLabel.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            costAmountLabel.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            costAmountLabel.rectTransform.pivot = new Vector2(0f, 0.5f);
            costAmountLabel.rectTransform.anchoredPosition = new Vector2(CostCoinDiameterPx + CostGapPx, 0f);
            costGroupRect.gameObject.SetActive(false);
        }

        /// <summary>Stacks title, body (at its wrapped preferred height), and the
        /// action row down the card, then sizes the card to fit — so the card
        /// grows vertically with the body length (confirmation-dialog.md).</summary>
        private void LayoutCard()
        {
            PlaceTopLeft(titleRect, DialogPaddingPx, DialogPaddingPx, InnerWidth(), TitleFontSizePx);

            var bodyTop = DialogPaddingPx + TitleFontSizePx + TitleBodyGapPx;
            // Width first (anchorMin==anchorMax makes rect.width == sizeDelta.x
            // synchronously), so preferredHeight wraps at the inner width.
            PlaceTopLeft(bodyRect, DialogPaddingPx, bodyTop, InnerWidth(), BodyFontSizePx);
            var bodyHeight = Mathf.Max(BodyFontSizePx, bodyLabel.preferredHeight);
            bodyRect.sizeDelta = new Vector2(InnerWidth(), bodyHeight);

            var actionTop = bodyTop + bodyHeight + ActionRowMarginPx;
            var buttonWidth = (InnerWidth() - ActionGapPx) / 2f;
            PlaceTopLeft(noButtonRect, DialogPaddingPx, actionTop, buttonWidth, ButtonHeightPx);
            PlaceTopLeft(yesButtonRect, DialogPaddingPx + buttonWidth + ActionGapPx, actionTop,
                buttonWidth, ButtonHeightPx);

            var cardHeight = actionTop + ButtonHeightPx + DialogPaddingPx;
            cardRect.sizeDelta = new Vector2(DialogWidthPx, cardHeight);
        }

        /// <summary>Centers the Yes content ("Yes" label, plus the coin + amount
        /// when a cost is shown) inside the Yes button. Widths come from the
        /// labels' preferred sizes so the group stays centered whatever the copy
        /// — a best-effort visual; the asserted behavior (text, coin size, active
        /// state) is independent of these measurements.</summary>
        private void LayoutYesButtonContent(bool hasCost)
        {
            if (!hasCost)
            {
                yesLabel.alignment = TextAnchor.MiddleCenter;
                InsetX(yesLabel.rectTransform, ButtonPaddingXPx);
                return;
            }

            var labelWidth = yesLabel.preferredWidth;
            var amountWidth = costAmountLabel.preferredWidth;
            var groupWidth = IconGapPx + CostCoinDiameterPx + CostGapPx + amountWidth;
            var totalWidth = labelWidth + groupWidth;
            var startX = Mathf.Max(ButtonPaddingXPx, (yesButtonRect.sizeDelta.x - totalWidth) / 2f);

            yesLabel.alignment = TextAnchor.MiddleLeft;
            yesLabel.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            yesLabel.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            yesLabel.rectTransform.pivot = new Vector2(0f, 0.5f);
            yesLabel.rectTransform.sizeDelta = new Vector2(labelWidth, ButtonFontSizePx);
            yesLabel.rectTransform.anchoredPosition = new Vector2(startX, 0f);

            costGroupRect.sizeDelta = new Vector2(groupWidth, CostCoinDiameterPx);
            costGroupRect.anchoredPosition = new Vector2(startX + labelWidth + IconGapPx, 0f);
        }

        private static float InnerWidth()
        {
            return DialogWidthPx - DialogPaddingPx * 2f;
        }

        // --- small UGUI helpers (mirrors DogProfileOverlay / HouseProfileOverlay) ---

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
