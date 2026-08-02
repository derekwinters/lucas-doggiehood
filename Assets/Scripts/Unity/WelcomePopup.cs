using System;
using System.Collections.Generic;
using Doggiehood.Core.Expansion;
using UnityEngine;
using UnityEngine.UI;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #518: the "Welcome to the neighborhood!" move-in pop-up (approved
    /// wireframe docs/specs/ui/welcome-popup.md / mockups/welcome-popup.html).
    /// A modal celebration panel raised when a household moves into a vacant
    /// house — it announces the new arrival(s) so the player knows a move-in
    /// happened and roughly where.
    ///
    /// <para>Reuses the <see cref="OnboardingRewardPanel"/> composition
    /// (portrait medal overlapping the top edge, big fixed heading, one leaf
    /// pill), parameterized for an arrival: the new dog's name, one dynamic meta
    /// line, an optional per-dog member-chip row (hidden for a single-dog
    /// move-in), and a single "Say hi!" button. One pop-up per household.</para>
    ///
    /// <para>The dynamic copy is composed in engine-free Core
    /// (<see cref="WelcomeMessage"/>); this layer only renders it. Always
    /// dismissible (button OR scrim), never a trap (#329). The one
    /// non-presentational behavior is the camera pan: <b>Say hi!</b> dismisses
    /// AND invokes the caller-supplied pan-to-house callback; tapping the scrim
    /// dismisses WITHOUT panning. Chrome comes from the device-safe
    /// <see cref="CandyChromeUgui"/> (#298) and text from the bundled font
    /// (#291); built under the #256 <see cref="UiCanvas"/> CanvasScaler so each
    /// px constant keeps a fixed on-screen meaning across tablet sizes.</para>
    /// </summary>
    public sealed class WelcomePopup : MonoBehaviour
    {
        // --- Layout constants from the approved wireframe (#161, #439) ---
        public const float WelcomeWidthPx = 820f;
        public const float WelcomePaddingPx = 56f;
        public const float PortraitDiameterPx = 176f;
        public const float PortraitOverlapPx = 88f;
        public const float PortraitOutlineThicknessPx = 8f;
        public const float PortraitTopGapPx = 28f;
        public const int HeadingFontSizePx = 54;
        public const int NameFontSizePx = 40;
        public const int MetaFontSizePx = 30;
        public const float HeadingNameGapPx = 18f;
        public const float NameMetaGapPx = 8f;
        public const float MetaActionMarginPx = 40f;
        public const float ActionMinWidthPx = 320f;
        public const float MemberChipDiameterPx = 72f;
        public const float MemberChipGapPx = 20f;
        public const float MemberRowMarginPx = 28f;

        /// <summary>Beat after the prior panel closes before this pops
        /// (welcome-popup.md "Timing", range 1–3s). Owned here with the rest of
        /// the wireframe constants; the raising director
        /// (<see cref="WelcomePopupDirector"/>) reads it.</summary>
        public const float WelcomePopupDelaySeconds = 1.5f;

        // The named chip below each member portrait (mockup .member .mname).
        private const int MemberChipNameFontSizePx = 20;
        private const float MemberChipNameGapPx = 8f;
        private const float MemberChipOutlineThicknessPx = 5f;
        // Reserved height for a member row: the chip disc plus its name label.
        private const float MemberRowHeightPx =
            MemberChipDiameterPx + MemberChipNameGapPx + MemberChipNameFontSizePx;

        // Shared PillButton (#173, shared-components.md): the Say hi! button is a
        // 96px pill with the label inset ButtonPaddingXPx from the caps.
        public const float ButtonHeightPx = 96f;
        private const float ButtonPaddingXPx = 48f;
        private const int ButtonFontSizePx = 36;

        /// <summary>The fixed celebratory headline — constant across every
        /// move-in (only the name/meta/member lines are dynamic).</summary>
        public const string HeadingText = "Welcome to the neighborhood!";

        /// <summary>The single positive action's label.</summary>
        public const string ActionText = "Say hi!";

        // Portrait/chips are a graybox silhouette for now (welcome-popup.md
        // Notes) — the mockup's --graybox (#C9C1B2). A real tinted dog-model
        // portrait is a fast-follow, not part of this wireframe.
        private static readonly Color GrayboxColor = new Color32(0xC9, 0xC1, 0xB2, 0xFF);

        /// <summary>#291: the bundled UI font, loaded from Resources so it ships
        /// in the Android build. Same asset the reward panel uses.</summary>
        private const string LabelFontResource = "DejaVuSans";
        private static Font labelFont;

        // Scrim tint from the mockup (rgba(46,42,38,.42)).
        private static readonly Color ScrimColor = new Color(46f / 255f, 42f / 255f, 38f / 255f, 0.42f);

        private GameObject content;
        private RectTransform cardRect;
        private RectTransform scrimRect;
        private RectTransform portraitRect;
        private RectTransform headingRect;
        private RectTransform nameRect;
        private RectTransform metaRect;
        private RectTransform memberRowRect;
        private RectTransform actionButtonRect;
        private RectTransform actionLabelRect;
        private Image portraitImage;
        private Image actionButtonImage;
        private Text headingText;
        private Text nameText;
        private Text metaText;
        private Text actionText;
        private Button actionButton;

        private readonly List<GameObject> memberChips = new List<GameObject>();
        private Action sayHiAction;

        // --- Test/wiring surface ---
        public RectTransform CardRect => cardRect;
        public RectTransform ScrimRect => scrimRect;
        public RectTransform PortraitRect => portraitRect;
        public RectTransform ActionButtonRect => actionButtonRect;
        public Image PortraitImage => portraitImage;
        public Image ActionButtonImage => actionButtonImage;
        public Text HeadingLabel => headingText;
        public Text NameLabel => nameText;
        public Text MetaLabel => metaText;
        public Text ActionLabel => actionText;
        public Button ActionButton => actionButton;
        public GameObject MemberRow => memberRowRect.gameObject;
        public int MemberChipCount => memberChips.Count;

        /// <summary>The named labels under each member chip, in row order.</summary>
        public IReadOnlyList<string> MemberChipNames
        {
            get
            {
                var names = new List<string>(memberChips.Count);
                foreach (var chip in memberChips)
                {
                    names.Add(chip.GetComponentInChildren<Text>().text);
                }

                return names;
            }
        }

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
        /// Raises the celebration for one moved-in household. <paramref name="message"/>
        /// is the Core-composed copy (name line, meta line, member-chip
        /// visibility + names); <paramref name="onSayHi"/> is invoked when the
        /// player taps <b>Say hi!</b> — it pans the camera to the new house. The
        /// heading and chrome are constant.
        /// </summary>
        public void Show(WelcomeMessage message, Action onSayHi)
        {
            sayHiAction = onSayHi;
            nameText.text = message.NameLine;
            metaText.text = message.MetaLine;
            RebuildMemberChips(message);

            LayoutCard(message.ShowsMemberChips);
            content.SetActive(true);
        }

        /// <summary>Button: dismisses AND pans the camera to the new house — so
        /// tapping "Say hi!" is truthful, taking the player to meet their new
        /// neighbour (welcome-popup.md).</summary>
        public void SayHi()
        {
            var pan = sayHiAction;
            Dismiss();
            pan?.Invoke();
        }

        /// <summary>Scrim tap (or programmatic close): dismisses WITHOUT panning.
        /// A welcome is an acknowledgement, not a choice — no path leaves it stuck
        /// open (#329).</summary>
        public void Dismiss()
        {
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
            content = new GameObject("WelcomePopupContent");
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.SetParent(transform, false);
            Stretch(contentRect);

            var scrimImage = CreateImage("Scrim", contentRect, ScrimColor);
            scrimRect = scrimImage.rectTransform;
            Stretch(scrimRect);
            // Scrim dismisses WITHOUT panning — Dismiss, not SayHi.
            scrimRect.gameObject.AddComponent<Button>().onClick.AddListener(Dismiss);

            BuildCard(contentRect);
        }

        private void BuildCard(RectTransform parent)
        {
            var cardImage = CreateImage("Card", parent, CandyChromeUgui.Panel);
            cardRect = cardImage.rectTransform;
            Center(cardRect, WelcomeWidthPx, WelcomePaddingPx * 2f);
            CandyChromeUgui.ApplyRounded(cardImage, CandyChromeUgui.Panel, CandyChromeUgui.PanelRadiusPx, withShadow: true);

            headingText = CreateLabel("Heading", cardRect, HeadingText, HeadingFontSizePx, TextAnchor.UpperCenter);
            headingRect = headingText.rectTransform;

            nameText = CreateLabel("Name", cardRect, string.Empty, NameFontSizePx, TextAnchor.UpperCenter);
            nameRect = nameText.rectTransform;

            metaText = CreateLabel("Meta", cardRect, string.Empty, MetaFontSizePx, TextAnchor.UpperCenter);
            metaRect = metaText.rectTransform;

            var memberRowImage = CreateRect("MemberRow", cardRect);
            memberRowRect = memberRowImage;

            BuildActionButton(cardRect);
            BuildPortrait(cardRect);
        }

        /// <summary>The single leaf pill — "Say hi!" — that dismisses and pans the
        /// camera. One button only; a welcome is an acknowledgement, not a
        /// choice.</summary>
        private void BuildActionButton(RectTransform parent)
        {
            actionButtonImage = CreateImage("ActionButton", parent, CandyChromeUgui.Leaf);
            actionButtonRect = actionButtonImage.rectTransform;
            CandyChromeUgui.ApplyPill(actionButtonImage, CandyChromeUgui.Leaf, ButtonHeightPx, withShadow: true);

            actionText = CreateLabel("ActionLabel", actionButtonRect, ActionText, ButtonFontSizePx, TextAnchor.MiddleCenter);
            actionLabelRect = actionText.rectTransform;
            Stretch(actionLabelRect);

            actionButton = actionButtonRect.gameObject.AddComponent<Button>();
            actionButton.onClick.AddListener(SayHi);
        }

        /// <summary>The graybox portrait medal overlapping the panel's top edge,
        /// with its own thicker ink ring (<see cref="PortraitOutlineThicknessPx"/>).
        /// A graybox silhouette for now (welcome-popup.md Notes).</summary>
        private void BuildPortrait(RectTransform parent)
        {
            portraitImage = CreateImage("Portrait", parent, GrayboxColor);
            portraitRect = portraitImage.rectTransform;
            portraitRect.anchorMin = new Vector2(0.5f, 1f);
            portraitRect.anchorMax = new Vector2(0.5f, 1f);
            portraitRect.pivot = new Vector2(0.5f, 0.5f);
            portraitRect.sizeDelta = new Vector2(PortraitDiameterPx, PortraitDiameterPx);
            // Center sits (overlap - radius) above the card's top edge, so
            // PortraitOverlapPx of the medal rises above it.
            portraitRect.anchoredPosition = new Vector2(0f, PortraitOverlapPx - PortraitDiameterPx / 2f);
            CandyChromeUgui.ApplyPill(portraitImage, GrayboxColor, PortraitDiameterPx, withShadow: true);
            // Its own thicker ink ring, distinct from the shared 6px panel outline.
            CandyChromeUgui.AddOutline(portraitImage.gameObject).effectDistance =
                new Vector2(PortraitOutlineThicknessPx, PortraitOutlineThicknessPx);
        }

        /// <summary>Rebuilds the member-chip row for the current household —
        /// one named graybox chip per dog. Cleared and hidden entirely for a
        /// single-dog move-in (the wireframe's hidden-for-single rule), so a
        /// re-show never leaves stale chips behind.</summary>
        private void RebuildMemberChips(WelcomeMessage message)
        {
            foreach (var chip in memberChips)
            {
                DestroyImmediateSafe(chip);
            }

            memberChips.Clear();
            memberRowRect.gameObject.SetActive(message.ShowsMemberChips);

            if (!message.ShowsMemberChips)
            {
                return;
            }

            foreach (var memberName in message.MemberNames)
            {
                memberChips.Add(BuildMemberChip(memberName));
            }
        }

        private GameObject BuildMemberChip(string memberName)
        {
            var chip = new GameObject("MemberChip");
            var chipRect = chip.AddComponent<RectTransform>();
            chipRect.SetParent(memberRowRect, false);
            chipRect.anchorMin = new Vector2(0f, 1f);
            chipRect.anchorMax = new Vector2(0f, 1f);
            chipRect.pivot = new Vector2(0f, 1f);
            chipRect.sizeDelta = new Vector2(MemberChipDiameterPx, MemberRowHeightPx);

            var discImage = CreateImage("Disc", chipRect, GrayboxColor);
            var discRect = discImage.rectTransform;
            discRect.anchorMin = new Vector2(0.5f, 1f);
            discRect.anchorMax = new Vector2(0.5f, 1f);
            discRect.pivot = new Vector2(0.5f, 1f);
            discRect.sizeDelta = new Vector2(MemberChipDiameterPx, MemberChipDiameterPx);
            discRect.anchoredPosition = Vector2.zero;
            CandyChromeUgui.ApplyPill(discImage, GrayboxColor, MemberChipDiameterPx, withShadow: true);
            CandyChromeUgui.AddOutline(discImage.gameObject).effectDistance =
                new Vector2(MemberChipOutlineThicknessPx, MemberChipOutlineThicknessPx);

            var nameLabel = CreateLabel("Name", chipRect, memberName, MemberChipNameFontSizePx, TextAnchor.UpperCenter);
            var nameLabelRect = nameLabel.rectTransform;
            nameLabelRect.anchorMin = new Vector2(0.5f, 1f);
            nameLabelRect.anchorMax = new Vector2(0.5f, 1f);
            nameLabelRect.pivot = new Vector2(0.5f, 1f);
            nameLabelRect.sizeDelta = new Vector2(MemberChipDiameterPx, MemberChipNameFontSizePx);
            nameLabelRect.anchoredPosition = new Vector2(0f, -(MemberChipDiameterPx + MemberChipNameGapPx));

            return chip;
        }

        /// <summary>Stacks the portrait-cleared heading, the name, the meta, the
        /// optional member-chip row, and the button down the card, then sizes the
        /// card to fit — so it grows vertically with its content
        /// (welcome-popup.md).</summary>
        private void LayoutCard(bool showsMemberChips)
        {
            var headingTop = (PortraitDiameterPx - PortraitOverlapPx) + PortraitTopGapPx;
            PlaceTopLeft(headingRect, WelcomePaddingPx, headingTop, InnerWidth(), HeadingFontSizePx);
            var headingHeight = Mathf.Max(HeadingFontSizePx, headingText.preferredHeight);
            headingRect.sizeDelta = new Vector2(InnerWidth(), headingHeight);

            var nameTop = headingTop + headingHeight + HeadingNameGapPx;
            PlaceTopLeft(nameRect, WelcomePaddingPx, nameTop, InnerWidth(), NameFontSizePx);
            var nameHeight = Mathf.Max(NameFontSizePx, nameText.preferredHeight);
            nameRect.sizeDelta = new Vector2(InnerWidth(), nameHeight);

            var metaTop = nameTop + nameHeight + NameMetaGapPx;
            PlaceTopLeft(metaRect, WelcomePaddingPx, metaTop, InnerWidth(), MetaFontSizePx);
            var metaHeight = Mathf.Max(MetaFontSizePx, metaText.preferredHeight);
            metaRect.sizeDelta = new Vector2(InnerWidth(), metaHeight);

            var afterMeta = metaTop + metaHeight;
            var actionTop = afterMeta + MetaActionMarginPx;

            if (showsMemberChips)
            {
                var memberRowTop = afterMeta + MemberRowMarginPx;
                LayoutMemberRow(memberRowTop);
                actionTop = memberRowTop + MemberRowHeightPx + MetaActionMarginPx;
            }

            LayoutActionButton(actionTop);

            var cardHeight = actionTop + ButtonHeightPx + WelcomePaddingPx;
            cardRect.sizeDelta = new Vector2(WelcomeWidthPx, cardHeight);
        }

        /// <summary>Centers the member-chip row horizontally in the card and lays
        /// the chips out left-to-right with <see cref="MemberChipGapPx"/> between
        /// them.</summary>
        private void LayoutMemberRow(float memberRowTop)
        {
            var count = memberChips.Count;
            var rowWidth = count * MemberChipDiameterPx + Mathf.Max(0, count - 1) * MemberChipGapPx;
            var rowLeft = (WelcomeWidthPx - rowWidth) / 2f;
            PlaceTopLeft(memberRowRect, rowLeft, memberRowTop, rowWidth, MemberRowHeightPx);

            var x = 0f;
            foreach (var chip in memberChips)
            {
                var chipRect = (RectTransform)chip.transform;
                chipRect.anchoredPosition = new Vector2(x, 0f);
                x += MemberChipDiameterPx + MemberChipGapPx;
            }
        }

        /// <summary>Sizes the pill to fit its label (at least
        /// <see cref="ActionMinWidthPx"/>) and centers it horizontally in the
        /// card.</summary>
        private void LayoutActionButton(float actionTop)
        {
            var buttonWidth = Mathf.Max(ActionMinWidthPx, actionText.preferredWidth + 2f * ButtonPaddingXPx);
            var buttonLeft = (WelcomeWidthPx - buttonWidth) / 2f;
            PlaceTopLeft(actionButtonRect, buttonLeft, actionTop, buttonWidth, ButtonHeightPx);
        }

        private static float InnerWidth()
        {
            return WelcomeWidthPx - WelcomePaddingPx * 2f;
        }

        // --- small UGUI helpers (mirrors OnboardingRewardPanel) ---

        private static void DestroyImmediateSafe(GameObject go)
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
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
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
