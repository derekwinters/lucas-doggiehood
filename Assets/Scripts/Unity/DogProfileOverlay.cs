using System;
using Doggiehood.Core.Dogs;
using UnityEngine;
using UnityEngine.UI;

namespace Doggiehood.Unity
{
    /// <summary>
    /// The dog profile view (#165): a centered card over a dim scrim showing
    /// who a dog is — portrait, name, breed chip, an Age tile and a
    /// Personality tile, and a Home button that flies the camera to that dog's
    /// house. Opened by tapping a dog's body (<see cref="DogView.OnTapped"/>);
    /// the speech bubble stays the conversation surface (conversation-system.md).
    ///
    /// Thin wiring only: the four field values come from the engine-free
    /// <see cref="DogProfile"/> (Core), and every layout number is a named
    /// constant from the approved wireframe (docs/specs/ui/dog-profile.md,
    /// #161/#177), asserted by EditMode tests. Built under the #256
    /// <see cref="UiCanvas"/> CanvasScaler so each px keeps a fixed on-screen
    /// meaning across tablet sizes. Graybox chrome (flat fills, no outlines)
    /// until the #173 shared-component styling pass.
    /// </summary>
    public sealed class DogProfileOverlay : MonoBehaviour
    {
        // --- Layout constants from the approved #177 wireframe ---
        public const float ProfileWidthPx = 900f;
        public const float ProfilePaddingPx = 48f;
        public const float PortraitSizePx = 220f;
        public const float CloseButtonSizePx = 72f;

        // Shared components owned by #173: the Home button is a 96px PillButton
        // and its label inset is the PillButton PaddingXPx (shared-components.md).
        public const float HomeButtonHeightPx = 96f;
        private const float HomeButtonPaddingXPx = 48f;

        // --- Graybox geometry read off the mockup CSS (#161: no inline literals) ---
        private const float PortraitNameGapPx = 36f;   // .phead gap
        private const float NameChipGapPx = 12f;        // .pname margin-bottom
        private const float BreedChipHeightPx = 56f;    // .breedchip height
        private const float BreedChipPaddingXPx = 24f;  // .breedchip padding
        private const float StatsTopMarginPx = 40f;     // .stats margin-top
        private const float StatGapPx = 18f;            // .stats grid gap
        private const float StatTileHeightPx = 104f;    // .stat: padding 20*2 + key + value
        private const float StatPaddingXPx = 26f;       // .stat padding-x
        private const float StatPaddingYPx = 20f;       // .stat padding-y
        private const float StatKeyValueGapPx = 8f;     // key -> value spacing
        private const float FooterTopMarginPx = 36f;    // .pfoot margin-top
        private const float HomeButtonWidthPx = 240f;   // graybox width for the Home pill

        private const int NameFontPx = 56;              // .pname
        private const int BreedChipFontPx = 28;         // .breedchip
        private const int StatKeyFontPx = 20;           // .stat .k
        private const int StatValueFontPx = 36;         // .stat .v
        private const int HomeButtonFontPx = 36;        // .cc-btn
        private const int CloseGlyphFontPx = 38;        // .close

        // Card height is derived from its stacked regions, not an invented
        // magic number: padding, header (portrait is the tallest header row),
        // the stats row, and the footer action.
        private const float CardHeightPx =
            ProfilePaddingPx * 2f + PortraitSizePx + StatsTopMarginPx
            + StatTileHeightPx + FooterTopMarginPx + HomeButtonHeightPx;

        // --- Display strings ---
        private const string CloseGlyphText = "✕";
        private const string HomeButtonText = "Home";
        private const string AgeKeyText = "Age";
        private const string PersonalityKeyText = "Personality";

        /// <summary>#291: the bundled UI font, loaded from Resources so it ships
        /// in the Android build (runtime UGUI cannot use the Editor-only
        /// built-in font). Same asset SettingsPanel uses.</summary>
        private const string LabelFontResource = "DejaVuSans";
        private static Font labelFont;

        // --- Palette (graybox; restyled by the #173 shared chrome pass) ---
        private static readonly Color ScrimColor = new Color(46f / 255f, 42f / 255f, 38f / 255f, 0.46f);
        private static readonly Color PanelColor = new Color(1f, 0.99f, 0.97f, 1f);
        private static readonly Color CloseColor = new Color(1f, 0.953f, 0.851f, 1f);
        private static readonly Color PortraitColor = new Color(0.749f, 0.890f, 0.949f, 1f);
        private static readonly Color BreedChipColor = new Color(0.431f, 0.776f, 0.878f, 1f);
        private static readonly Color StatColor = new Color(0.906f, 0.875f, 0.808f, 1f);
        private static readonly Color HomeButtonColor = new Color(0.345f, 0.753f, 0.416f, 1f);
        private static readonly Color InkColor = new Color(0.180f, 0.165f, 0.149f, 1f);

        private GameObject content;
        private RectTransform cardRect;
        private RectTransform scrimRect;
        private RectTransform closeButtonRect;
        private RectTransform portraitRect;
        private RectTransform homeButtonRect;
        private Text nameLabel;
        private Text breedChipLabel;
        private Text ageKeyLabel;
        private Text ageValueLabel;
        private Text personalityKeyLabel;
        private Text personalityValueLabel;

        private Dog currentDog;

        /// <summary>Raised when the Home button is pressed, carrying the tapped
        /// dog's house id. The bootstrap wires this to a camera fly-to; Core
        /// (<see cref="Doggiehood.Core.Cameras.CameraController.FocusOn"/>)
        /// owns the actual move.</summary>
        public event Action<int> HomeRequested;

        public RectTransform CardRect => cardRect;
        public RectTransform ScrimRect => scrimRect;
        public RectTransform CloseButtonRect => closeButtonRect;
        public RectTransform PortraitRect => portraitRect;
        public RectTransform HomeButtonRect => homeButtonRect;
        public Text NameLabel => nameLabel;
        public Text BreedChipLabel => breedChipLabel;
        public Text AgeKeyLabel => ageKeyLabel;
        public Text AgeValueLabel => ageValueLabel;
        public Text PersonalityKeyLabel => personalityKeyLabel;
        public Text PersonalityValueLabel => personalityValueLabel;

        /// <summary>The dog whose profile is currently shown, or null when closed.</summary>
        public Dog CurrentDog => currentDog;

        /// <summary>Whether the profile is currently shown.</summary>
        public bool IsOpen => content != null && content.activeSelf;

        /// <summary>Builds the card hierarchy (expected under a <see cref="UiCanvas"/>)
        /// and starts closed.</summary>
        public void Init()
        {
            Build();
            content.SetActive(false);
        }

        /// <summary>Opens the profile for the given dog, filling the four fields
        /// from its Core <see cref="DogProfile"/>.</summary>
        public void Open(Dog dog)
        {
            currentDog = dog;
            var profile = DogProfile.For(dog);

            nameLabel.text = profile.Name;
            breedChipLabel.text = profile.Breed;
            ageValueLabel.text = profile.Age;
            personalityValueLabel.text = profile.Personality;

            if (content != null)
            {
                content.SetActive(true);
            }
        }

        /// <summary>Hides the profile.</summary>
        public void Close()
        {
            currentDog = null;
            if (content != null)
            {
                content.SetActive(false);
            }
        }

        /// <summary>Home button (#165): closes the profile and requests a
        /// camera fly-to the current dog's house. A no-op if nothing is open.</summary>
        public void Home()
        {
            if (currentDog == null)
            {
                return;
            }

            var houseId = currentDog.HouseId;
            Close();
            HomeRequested?.Invoke(houseId);
        }

        // ---------------------------------------------------------------
        // Building (thin, geometry-only — every number is a named constant)
        // ---------------------------------------------------------------

        private void Build()
        {
            content = new GameObject("DogProfileContent");
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.SetParent(transform, false);
            Stretch(contentRect);

            scrimRect = CreateImage("Scrim", contentRect, ScrimColor).rectTransform;
            Stretch(scrimRect);
            scrimRect.gameObject.AddComponent<Button>().onClick.AddListener(Close);

            BuildCard(contentRect);
        }

        private void BuildCard(RectTransform parent)
        {
            cardRect = CreateImage("Card", parent, PanelColor).rectTransform;
            Center(cardRect, ProfileWidthPx, CardHeightPx);

            BuildCloseButton(cardRect);
            BuildHeader(cardRect);
            BuildStats(cardRect);
            BuildFooter(cardRect);
        }

        private void BuildCloseButton(RectTransform parent)
        {
            closeButtonRect = CreateImage("Close", parent, CloseColor).rectTransform;
            closeButtonRect.anchorMin = Vector2.one;
            closeButtonRect.anchorMax = Vector2.one;
            closeButtonRect.pivot = Vector2.one;
            closeButtonRect.sizeDelta = new Vector2(CloseButtonSizePx, CloseButtonSizePx);
            closeButtonRect.anchoredPosition = Vector2.zero;

            CreateLabel("Glyph", closeButtonRect, CloseGlyphText, CloseGlyphFontPx, TextAnchor.MiddleCenter);
            closeButtonRect.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        }

        private void BuildHeader(RectTransform parent)
        {
            var header = PlaceTopLeft(CreateRect("Header", parent),
                ProfilePaddingPx, ProfilePaddingPx, InnerWidth(), PortraitSizePx);

            portraitRect = CreateImage("Portrait", header, PortraitColor).rectTransform;
            portraitRect.anchorMin = new Vector2(0f, 0.5f);
            portraitRect.anchorMax = new Vector2(0f, 0.5f);
            portraitRect.pivot = new Vector2(0f, 0.5f);
            portraitRect.sizeDelta = new Vector2(PortraitSizePx, PortraitSizePx);
            portraitRect.anchoredPosition = Vector2.zero;

            var textX = PortraitSizePx + PortraitNameGapPx;
            var textWidth = InnerWidth() - textX;

            nameLabel = CreateLabel("Name", header, string.Empty, NameFontPx, TextAnchor.LowerLeft);
            PlaceTopLeft(nameLabel.rectTransform, textX, PortraitSizePx / 2f - NameFontPx - NameChipGapPx,
                textWidth, NameFontPx);

            var chip = CreateImage("BreedChip", header, BreedChipColor).rectTransform;
            PlaceTopLeft(chip, textX, PortraitSizePx / 2f + NameChipGapPx, BreedChipContentWidth(), BreedChipHeightPx);
            breedChipLabel = CreateLabel("BreedLabel", chip, string.Empty, BreedChipFontPx, TextAnchor.MiddleLeft);
            InsetX(breedChipLabel.rectTransform, BreedChipPaddingXPx);
        }

        private void BuildStats(RectTransform parent)
        {
            var top = ProfilePaddingPx + PortraitSizePx + StatsTopMarginPx;
            var tileWidth = (InnerWidth() - StatGapPx) / 2f;

            var ageTile = PlaceTopLeft(CreateImage("AgeTile", parent, StatColor).rectTransform,
                ProfilePaddingPx, top, tileWidth, StatTileHeightPx);
            ageKeyLabel = BuildStatKey(ageTile, AgeKeyText);
            ageValueLabel = BuildStatValue(ageTile);

            var personalityTile = PlaceTopLeft(CreateImage("PersonalityTile", parent, StatColor).rectTransform,
                ProfilePaddingPx + tileWidth + StatGapPx, top, tileWidth, StatTileHeightPx);
            personalityKeyLabel = BuildStatKey(personalityTile, PersonalityKeyText);
            personalityValueLabel = BuildStatValue(personalityTile);
        }

        private Text BuildStatKey(RectTransform tile, string key)
        {
            var label = CreateLabel("Key", tile, key, StatKeyFontPx, TextAnchor.UpperLeft);
            PlaceTopLeft(label.rectTransform, StatPaddingXPx, StatPaddingYPx,
                tile.sizeDelta.x - StatPaddingXPx * 2f, StatKeyFontPx);
            return label;
        }

        private Text BuildStatValue(RectTransform tile)
        {
            var label = CreateLabel("Value", tile, string.Empty, StatValueFontPx, TextAnchor.UpperLeft);
            PlaceTopLeft(label.rectTransform, StatPaddingXPx, StatPaddingYPx + StatKeyFontPx + StatKeyValueGapPx,
                tile.sizeDelta.x - StatPaddingXPx * 2f, StatValueFontPx);
            return label;
        }

        private void BuildFooter(RectTransform parent)
        {
            homeButtonRect = CreateImage("HomeButton", parent, HomeButtonColor).rectTransform;
            homeButtonRect.anchorMin = new Vector2(1f, 0f);
            homeButtonRect.anchorMax = new Vector2(1f, 0f);
            homeButtonRect.pivot = new Vector2(1f, 0f);
            homeButtonRect.sizeDelta = new Vector2(HomeButtonWidthPx, HomeButtonHeightPx);
            homeButtonRect.anchoredPosition = new Vector2(-ProfilePaddingPx, ProfilePaddingPx);

            var label = CreateLabel("Label", homeButtonRect, HomeButtonText, HomeButtonFontPx, TextAnchor.MiddleCenter);
            InsetX(label.rectTransform, HomeButtonPaddingXPx);

            homeButtonRect.gameObject.AddComponent<Button>().onClick.AddListener(Home);
        }

        private static float InnerWidth()
        {
            return ProfileWidthPx - ProfilePaddingPx * 2f;
        }

        private static float BreedChipContentWidth()
        {
            // Graybox chip width: content-sized pills land with #173; for now a
            // portion of the header text column so it reads as a chip, not a bar.
            return (InnerWidth() - (PortraitSizePx + PortraitNameGapPx)) * 0.6f;
        }

        // --- small UGUI helpers (mirrors SettingsPanel) ---

        /// <summary>Anchors a child to the parent's top-left, offset right by
        /// <paramref name="x"/> and down by <paramref name="yFromTop"/>.</summary>
        private static RectTransform PlaceTopLeft(RectTransform rect, float x, float yFromTop, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -yFromTop);
            return rect;
        }

        /// <summary>Stretches a child to fill its parent with a horizontal
        /// inset on both sides (used for pill/chip label padding).</summary>
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
            text.color = InkColor;
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
