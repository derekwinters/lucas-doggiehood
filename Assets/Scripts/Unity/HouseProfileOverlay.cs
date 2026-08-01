using System;
using System.Collections.Generic;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace Doggiehood.Unity
{
    /// <summary>
    /// The house profile view (#208, docs/specs/ui/house-profile.md): a
    /// centered card over a dim scrim — the mirror of the dog profile (#165) —
    /// showing a house's <b>level</b> (`Lv N` + N-of-4 pips), its <b>resident
    /// dog(s)</b> as tappable link rows that open each dog's own profile, and a
    /// footer <b>Upgrade</b> button that is the entry point to the house-upgrade
    /// action (#59). A vacant house shows an empty-state line and offers no
    /// Upgrade.
    ///
    /// Thin wiring only: the header/upgrade display values come from the
    /// engine-free <see cref="HouseProfile"/> (Core) and each resident row's
    /// name/breed from that dog's <see cref="DogProfile"/> (Core); every layout
    /// number is a named constant from the approved wireframe
    /// (#161/#293), asserted by EditMode tests. Built under the #256
    /// <see cref="UiCanvas"/> CanvasScaler. Graybox chrome (flat fills, no
    /// outlines) until the #173 shared-component styling pass.
    ///
    /// Upgrade action (#294, Derek's Option A): tapping the Upgrade button
    /// spends coins <b>directly</b> — no confirmation screen — through the Core
    /// entry point wired via <see cref="ConfigureUpgrade"/>
    /// (<see cref="World.GameState.TryUpgradeHouse"/>). The button shows the
    /// next level's cost, disables into a <i>Max level</i> state at the cap, and
    /// greys out when the live wallet balance can't cover the next step
    /// (affordability is re-read at each render, never cached — the same "never
    /// cache" contract the HUD currency chip uses). On a successful upgrade the
    /// open profile re-renders so the new level / next cost / affordability show
    /// immediately.
    /// </summary>
    public sealed class HouseProfileOverlay : MonoBehaviour
    {
        // --- Layout constants from the approved #293 wireframe ---
        public const float ProfileWidthPx = 900f;
        public const float ProfilePaddingPx = 48f;
        public const float ThumbnailSizePx = 220f;
        public const float CloseButtonSizePx = 72f;
        public const int LevelPipCount = 4;             // = HouseUpgradeNumbers.MaxLevel
        public const float LevelPipDiameterPx = 28f;
        public const float LevelPipGapPx = 12f;
        public const float ResidentRowHeightPx = 120f;
        public const float ResidentRowGapPx = 16f;
        public const float ResidentAvatarSizePx = 96f;

        // Shared components owned by #173: the Upgrade button is a 96px
        // PillButton and its label inset is the PillButton PaddingXPx.
        public const float UpgradeButtonHeightPx = 96f;
        private const float UpgradeButtonPaddingXPx = 48f;

        // --- Graybox geometry read off the mockup CSS (#161: no inline literals) ---
        private const float ThumbnailTitleGapPx = 36f;         // .phead gap
        private const float TitleRowGapPx = 14f;               // .htitle gap
        private const float LevelBadgeGapPx = 18f;             // .levelbadge gap
        private const float LevelBadgeWidthPx = 150f;          // graybox width of the `Lv N` pill
        private const float LevelBadgeHeightPx = 56f;          // .lv height
        private const float LevelBadgePaddingXPx = 24f;        // .lv padding-x
        private const float ResidentsHeaderTopMarginPx = 40f;  // .reshead margin-top
        private const float ResidentsHeaderHeightPx = 26f;     // .reshead line
        private const float ResidentsHeaderBottomMarginPx = 16f; // .reshead margin-bottom
        private const float ResidentRowPaddingXPx = 26f;       // .resrow padding-x
        private const float ResidentAvatarGapPx = 24f;         // .resrow gap
        private const float BreedChipHeightPx = 52f;           // .breedchip height
        private const float BreedChipPaddingXPx = 22f;         // .breedchip padding-x
        private const float BreedChipWidthPx = 300f;           // graybox width of the breed chip
        private const float EmptyStateHeightPx = 72f;          // muted empty-state line block
        private const float FooterTopMarginPx = 36f;           // .pfoot margin-top
        private const float UpgradeButtonWidthPx = 320f;       // graybox width for the Upgrade pill

        private const int HouseLabelFontPx = 56;               // .hname
        private const int LevelBadgeFontPx = 30;               // .lv
        private const int ResidentsHeaderFontPx = 22;          // .reshead
        private const int ResidentNameFontPx = 34;             // .resname
        private const int BreedChipFontPx = 26;                // .breedchip
        private const int EmptyStateFontPx = 26;               // .mvacant
        private const int UpgradeButtonFontPx = 36;            // .cc-btn
        private const int CloseGlyphFontPx = 38;               // .close

        // --- Display strings ---
        private const string CloseGlyphText = "✕";
        private const string HouseLabelText = "House";
        private const string ResidentsHeaderText = "Residents";

        /// <summary>#291: the bundled UI font, loaded from Resources so it ships
        /// in the Android build (runtime UGUI cannot use the Editor-only
        /// built-in font). Same asset the dog profile / settings panel use.</summary>
        private const string LabelFontResource = "DejaVuSans";
        private static Font labelFont;

        // --- Palette (graybox; restyled by the #173 shared chrome pass) ---
        private static readonly Color ScrimColor = new Color(46f / 255f, 42f / 255f, 38f / 255f, 0.46f);
        private static readonly Color PanelColor = new Color(1f, 0.99f, 0.97f, 1f);
        private static readonly Color CloseColor = new Color(1f, 0.953f, 0.851f, 1f);
        private static readonly Color LevelBadgeColor = new Color(1f, 0.761f, 0.235f, 1f);
        private static readonly Color PipFilledColor = new Color(1f, 0.761f, 0.235f, 1f);
        private static readonly Color PipEmptyColor = new Color(1f, 0.99f, 0.97f, 1f);
        private static readonly Color ResidentRowColor = new Color(0.906f, 0.875f, 0.808f, 1f);
        private static readonly Color BreedChipColor = new Color(0.431f, 0.776f, 0.878f, 1f);
        private static readonly Color UpgradeButtonColor = new Color(1f, 0.478f, 0.361f, 1f);
        private static readonly Color UpgradeDisabledColor = new Color(0.847f, 0.824f, 0.776f, 1f);
        private static readonly Color EmptyStateColor = new Color(0.522f, 0.486f, 0.431f, 1f);
        private static readonly Color InkColor = new Color(0.180f, 0.165f, 0.149f, 1f);

        /// <summary>One resident link row: its rect + tap button, the name and
        /// breed labels sourced from the dog's <see cref="DogProfile"/>, and the
        /// <see cref="Dog"/> it links to (opened via <see cref="ResidentSelected"/>).</summary>
        public sealed class ResidentRowView
        {
            public RectTransform Rect { get; internal set; }
            public Button Button { get; internal set; }
            public Text NameLabel { get; internal set; }
            public Text BreedChipLabel { get; internal set; }

            /// <summary>#464: the 96px avatar box, now a render-to-texture
            /// snapshot of this resident's breed-tinted model.</summary>
            public RawImage Avatar { get; internal set; }
            public Dog Dog { get; internal set; }
        }

        private GameObject content;
        private RectTransform cardRect;
        private RectTransform scrimRect;
        private RectTransform closeButtonRect;
        private RectTransform thumbnailRect;
        private RawImage thumbnailImage;
        private RectTransform residentsContainer;
        private RectTransform upgradeButtonRect;
        private Text levelLabel;
        private Text emptyStateLabel;
        private Text upgradeButtonLabel;
        private Button upgradeButton;
        private Image upgradeButtonImage;

        private readonly List<Image> levelPips = new List<Image>();
        private readonly List<ResidentRowView> residents = new List<ResidentRowView>();

        // #464: the off-screen rig that snapshots the house / resident models,
        // lazily created and owned by this overlay, plus the live snapshot
        // textures released and re-captured each render.
        private PortraitCamera portraitCamera;
        private readonly List<RenderTexture> snapshots = new List<RenderTexture>();

        private House currentHouse;
        private IReadOnlyList<Dog> currentResidents = System.Array.Empty<Dog>();

        // #294: the live wallet read + the Core upgrade entry point, injected by
        // the bootstrap. Both stay null in display-only tests, where the button
        // simply reads a zero balance and performs no spend.
        private Func<int> coinBalanceProvider;
        private Func<int, bool> upgradeAction;

        /// <summary>Raised when a resident row is tapped, carrying that dog —
        /// the bootstrap wires this to open the dog profile (#165).</summary>
        public event Action<Dog> ResidentSelected;

        public RectTransform CardRect => cardRect;
        public RectTransform ScrimRect => scrimRect;
        public RectTransform CloseButtonRect => closeButtonRect;
        public RectTransform ThumbnailRect => thumbnailRect;

        /// <summary>#464: the thumbnail box, now a <see cref="RawImage"/> that
        /// shows a render-to-texture snapshot of the house's actual current
        /// model, captured on <see cref="Open"/>.</summary>
        public RawImage ThumbnailImage => thumbnailImage;

        /// <summary>#464: the off-screen portrait rig this overlay owns (created
        /// lazily on first render). Exposed so tests can confirm captures are
        /// one-shot per <see cref="Open"/>.</summary>
        public PortraitCamera Portrait => portraitCamera;
        public RectTransform UpgradeButtonRect => upgradeButtonRect;
        public Text LevelLabel => levelLabel;
        public Text EmptyStateLabel => emptyStateLabel;
        public Text UpgradeButtonLabel => upgradeButtonLabel;
        public Button UpgradeButton => upgradeButton;
        public IReadOnlyList<Image> LevelPips => levelPips;
        public IReadOnlyList<ResidentRowView> Residents => residents;

        /// <summary>How many level pips are filled = the house's current level.</summary>
        public int FilledPipCount { get; private set; }

        /// <summary>The house whose profile is currently shown, or null when closed.</summary>
        public House CurrentHouse => currentHouse;

        /// <summary>Whether the profile is currently shown.</summary>
        public bool IsOpen => content != null && content.activeSelf;

        /// <summary>Builds the card hierarchy (expected under a <see cref="UiCanvas"/>)
        /// and starts closed.</summary>
        public void Init()
        {
            Build();
            content.SetActive(false);
        }

        /// <summary>Wires the Upgrade button (#294) to Core: a live wallet-balance
        /// read (for the affordability state) and the upgrade entry point
        /// (<see cref="World.GameState.TryUpgradeHouse"/>) tapping calls directly.
        /// The bootstrap injects these; display-only tests may leave them unset.</summary>
        public void ConfigureUpgrade(Func<int> coinBalanceProvider, Func<int, bool> upgradeAction)
        {
            this.coinBalanceProvider = coinBalanceProvider;
            this.upgradeAction = upgradeAction;
        }

        /// <summary>Opens the profile for the given house, filling the level
        /// badge, resident rows (from the passed <paramref name="residentDogs"/>),
        /// and Upgrade button from the house's Core <see cref="HouseProfile"/>.</summary>
        public void Open(House house, IReadOnlyList<Dog> residentDogs)
        {
            currentHouse = house;
            currentResidents = residentDogs ?? System.Array.Empty<Dog>();

            Render();

            if (content != null)
            {
                content.SetActive(true);
            }
        }

        /// <summary>Renders the open profile from the current house + residents,
        /// re-reading the live wallet balance for the Upgrade button's
        /// affordability. Called on <see cref="Open"/> and again after a
        /// successful <see cref="Upgrade"/> so the new level / next cost /
        /// affordability show immediately (#294).</summary>
        private void Render()
        {
            var profile = HouseProfile.For(currentHouse);

            // #464: release the previous snapshots, then capture a fresh render
            // of the house's CURRENT model (variant + level + vacancy tint) into
            // the thumbnail. Residents are re-snapshotted in RebuildResidents.
            ReleaseSnapshots();
            thumbnailImage.texture = Snapshot(PortraitSubjects.ForHouse(currentHouse));

            levelLabel.text = profile.LevelText;
            FilledPipCount = profile.FilledPipCount;
            for (var i = 0; i < levelPips.Count; i++)
            {
                levelPips[i].color = i < FilledPipCount ? PipFilledColor : PipEmptyColor;
            }

            RebuildResidents(currentResidents);
            emptyStateLabel.text = profile.EmptyStateText;
            emptyStateLabel.gameObject.SetActive(residents.Count == 0);

            LayoutBelowResidents(residents.Count);

            upgradeButtonRect.gameObject.SetActive(profile.ShowsUpgradeAction);
            if (profile.ShowsUpgradeAction)
            {
                upgradeButtonLabel.text = profile.UpgradeButtonText;
                var affordable = profile.CanAffordUpgrade(CurrentCoinBalance());
                upgradeButton.interactable = affordable;
                upgradeButtonImage.color = affordable ? UpgradeButtonColor : UpgradeDisabledColor;
            }
        }

        /// <summary>The live wallet balance from the wired provider (#294), or 0
        /// when none is configured (display-only tests) — a zero balance simply
        /// leaves the button un-affordable, never crashes.</summary>
        private int CurrentCoinBalance()
        {
            return coinBalanceProvider != null ? coinBalanceProvider() : 0;
        }

        /// <summary>Hides the profile.</summary>
        public void Close()
        {
            currentHouse = null;
            if (content != null)
            {
                content.SetActive(false);
            }
        }

        /// <summary>Upgrade button (#294, Option A): spends coins directly via
        /// the wired Core entry point (<see cref="World.GameState.TryUpgradeHouse"/>)
        /// for the open house — no confirmation screen. On success the open
        /// profile re-renders so the new level / next cost / affordability show
        /// immediately. A no-op if nothing is open or no upgrade action is wired;
        /// the Core call itself is the sole authority on whether the spend
        /// happens (it rejects an unaffordable / max-level / unknown house).</summary>
        public void Upgrade()
        {
            if (currentHouse == null || upgradeAction == null)
            {
                return;
            }

            if (upgradeAction(currentHouse.Id))
            {
                Render();
            }
        }

        // ---------------------------------------------------------------
        // Building (thin, geometry-only — every number is a named constant)
        // ---------------------------------------------------------------

        private void Build()
        {
            content = new GameObject("HouseProfileContent");
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
            Center(cardRect, ProfileWidthPx, ComputeCardHeight(0));

            BuildCloseButton(cardRect);
            BuildHeader(cardRect);
            BuildResidentsHeader(cardRect);
            BuildResidentsContainer(cardRect);
            BuildEmptyState(cardRect);
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
                ProfilePaddingPx, ProfilePaddingPx, InnerWidth(), ThumbnailSizePx);

            thumbnailImage = CreateRawImage("Thumbnail", header);
            thumbnailRect = thumbnailImage.rectTransform;
            thumbnailRect.anchorMin = new Vector2(0f, 0.5f);
            thumbnailRect.anchorMax = new Vector2(0f, 0.5f);
            thumbnailRect.pivot = new Vector2(0f, 0.5f);
            thumbnailRect.sizeDelta = new Vector2(ThumbnailSizePx, ThumbnailSizePx);
            thumbnailRect.anchoredPosition = Vector2.zero;

            var textX = ThumbnailSizePx + ThumbnailTitleGapPx;
            var textWidth = InnerWidth() - textX;

            var houseLabel = CreateLabel("HouseLabel", header, HouseLabelText, HouseLabelFontPx, TextAnchor.LowerLeft);
            PlaceTopLeft(houseLabel.rectTransform, textX,
                ThumbnailSizePx / 2f - HouseLabelFontPx - TitleRowGapPx, textWidth, HouseLabelFontPx);

            BuildLevelBadge(header, textX, ThumbnailSizePx / 2f + TitleRowGapPx);
        }

        private void BuildLevelBadge(RectTransform parent, float x, float yFromTop)
        {
            var badge = CreateImage("LevelBadge", parent, LevelBadgeColor).rectTransform;
            PlaceTopLeft(badge, x, yFromTop, LevelBadgeWidthPx, LevelBadgeHeightPx);
            levelLabel = CreateLabel("LevelLabel", badge, string.Empty, LevelBadgeFontPx, TextAnchor.MiddleLeft);
            InsetX(levelLabel.rectTransform, LevelBadgePaddingXPx);

            var pipsX = x + LevelBadgeWidthPx + LevelBadgeGapPx;
            var pipY = yFromTop + (LevelBadgeHeightPx - LevelPipDiameterPx) / 2f;
            for (var i = 0; i < LevelPipCount; i++)
            {
                var pip = CreateImage("Pip", parent, PipEmptyColor).rectTransform;
                PlaceTopLeft(pip, pipsX + i * (LevelPipDiameterPx + LevelPipGapPx), pipY,
                    LevelPipDiameterPx, LevelPipDiameterPx);
                levelPips.Add(pip.GetComponent<Image>());
            }
        }

        private void BuildResidentsHeader(RectTransform parent)
        {
            var header = CreateLabel("ResidentsHeader", parent, ResidentsHeaderText,
                ResidentsHeaderFontPx, TextAnchor.LowerLeft);
            PlaceTopLeft(header.rectTransform, ProfilePaddingPx,
                ProfilePaddingPx + ThumbnailSizePx + ResidentsHeaderTopMarginPx,
                InnerWidth(), ResidentsHeaderHeightPx);
        }

        private void BuildResidentsContainer(RectTransform parent)
        {
            residentsContainer = CreateRect("Residents", parent);
            PlaceTopLeft(residentsContainer, ProfilePaddingPx, ResidentsTop(), InnerWidth(), ResidentRowHeightPx);
        }

        private void BuildEmptyState(RectTransform parent)
        {
            emptyStateLabel = CreateLabel("EmptyState", parent, string.Empty, EmptyStateFontPx, TextAnchor.UpperLeft);
            emptyStateLabel.color = EmptyStateColor;
            PlaceTopLeft(emptyStateLabel.rectTransform, ProfilePaddingPx, ResidentsTop(),
                InnerWidth(), EmptyStateHeightPx);
            emptyStateLabel.gameObject.SetActive(false);
        }

        private void BuildFooter(RectTransform parent)
        {
            upgradeButtonImage = CreateImage("UpgradeButton", parent, UpgradeButtonColor);
            upgradeButtonRect = upgradeButtonImage.rectTransform;
            PlaceTopLeft(upgradeButtonRect,
                ProfileWidthPx - ProfilePaddingPx - UpgradeButtonWidthPx, FooterTop(0),
                UpgradeButtonWidthPx, UpgradeButtonHeightPx);

            upgradeButtonLabel = CreateLabel("Label", upgradeButtonRect, string.Empty,
                UpgradeButtonFontPx, TextAnchor.MiddleCenter);
            InsetX(upgradeButtonLabel.rectTransform, UpgradeButtonPaddingXPx);

            upgradeButton = upgradeButtonRect.gameObject.AddComponent<Button>();
            upgradeButton.onClick.AddListener(Upgrade);
        }

        // --- Open-time (dynamic) layout ---

        private void RebuildResidents(IReadOnlyList<Dog> residentDogs)
        {
            foreach (var row in residents)
            {
                if (Application.isPlaying)
                {
                    Destroy(row.Rect.gameObject);
                }
                else
                {
                    DestroyImmediate(row.Rect.gameObject);
                }
            }

            residents.Clear();

            for (var i = 0; i < residentDogs.Count; i++)
            {
                residents.Add(BuildResidentRow(residentDogs[i], i));
            }
        }

        private ResidentRowView BuildResidentRow(Dog dog, int index)
        {
            var profile = DogProfile.For(dog);

            var rowImage = CreateImage("ResidentRow", residentsContainer, ResidentRowColor);
            var rowRect = rowImage.rectTransform;
            PlaceTopLeft(rowRect, 0f, index * (ResidentRowHeightPx + ResidentRowGapPx),
                InnerWidth(), ResidentRowHeightPx);

            var avatarImage = CreateRawImage("Avatar", rowRect);
            var avatar = avatarImage.rectTransform;
            avatar.anchorMin = new Vector2(0f, 0.5f);
            avatar.anchorMax = new Vector2(0f, 0.5f);
            avatar.pivot = new Vector2(0f, 0.5f);
            avatar.sizeDelta = new Vector2(ResidentAvatarSizePx, ResidentAvatarSizePx);
            avatar.anchoredPosition = new Vector2(ResidentRowPaddingXPx, 0f);
            // #464: fill the avatar with a snapshot of this dog's breed-tinted model.
            avatarImage.texture = Snapshot(PortraitSubjects.ForDog(dog));

            var nameX = ResidentRowPaddingXPx + ResidentAvatarSizePx + ResidentAvatarGapPx;
            var nameLabel = CreateLabel("Name", rowRect, profile.Name, ResidentNameFontPx, TextAnchor.MiddleLeft);
            nameLabel.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            nameLabel.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            nameLabel.rectTransform.pivot = new Vector2(0f, 0.5f);
            nameLabel.rectTransform.sizeDelta = new Vector2(
                InnerWidth() - nameX - BreedChipWidthPx - ResidentRowPaddingXPx, ResidentNameFontPx);
            nameLabel.rectTransform.anchoredPosition = new Vector2(nameX, 0f);

            var chip = CreateImage("BreedChip", rowRect, BreedChipColor).rectTransform;
            chip.anchorMin = new Vector2(1f, 0.5f);
            chip.anchorMax = new Vector2(1f, 0.5f);
            chip.pivot = new Vector2(1f, 0.5f);
            chip.sizeDelta = new Vector2(BreedChipWidthPx, BreedChipHeightPx);
            chip.anchoredPosition = new Vector2(-ResidentRowPaddingXPx, 0f);
            var chipLabel = CreateLabel("BreedLabel", chip, profile.Breed, BreedChipFontPx, TextAnchor.MiddleCenter);
            InsetX(chipLabel.rectTransform, BreedChipPaddingXPx);

            var row = new ResidentRowView
            {
                Rect = rowRect,
                NameLabel = nameLabel,
                BreedChipLabel = chipLabel,
                Avatar = avatarImage,
                Dog = dog,
            };

            var button = rowImage.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => ResidentSelected?.Invoke(dog));
            row.Button = button;

            return row;
        }

        /// <summary>Grows the container/empty-state/footer down the card so it
        /// fits the resident-row count (0–3, never scrolls) and resizes the
        /// card to match.</summary>
        private void LayoutBelowResidents(int rowCount)
        {
            var residentsBlock = ResidentsBlockHeight(rowCount);
            residentsContainer.sizeDelta = new Vector2(InnerWidth(), residentsBlock);
            emptyStateLabel.rectTransform.sizeDelta = new Vector2(InnerWidth(), residentsBlock);

            upgradeButtonRect.anchoredPosition = new Vector2(
                ProfileWidthPx - ProfilePaddingPx - UpgradeButtonWidthPx, -FooterTop(rowCount));

            cardRect.sizeDelta = new Vector2(ProfileWidthPx, ComputeCardHeight(rowCount));
        }

        private static float InnerWidth()
        {
            return ProfileWidthPx - ProfilePaddingPx * 2f;
        }

        private static float ResidentsTop()
        {
            return ProfilePaddingPx + ThumbnailSizePx + ResidentsHeaderTopMarginPx
                + ResidentsHeaderHeightPx + ResidentsHeaderBottomMarginPx;
        }

        private static float ResidentsBlockHeight(int rowCount)
        {
            return rowCount > 0
                ? rowCount * ResidentRowHeightPx + (rowCount - 1) * ResidentRowGapPx
                : EmptyStateHeightPx;
        }

        private static float FooterTop(int rowCount)
        {
            return ResidentsTop() + ResidentsBlockHeight(rowCount) + FooterTopMarginPx;
        }

        private static float ComputeCardHeight(int rowCount)
        {
            return FooterTop(rowCount) + UpgradeButtonHeightPx + ProfilePaddingPx;
        }

        // --- small UGUI helpers (mirrors DogProfileOverlay) ---

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

        /// <summary>#464: a box that displays a render-to-texture snapshot
        /// (thumbnail / resident avatar) instead of a flat placeholder color.</summary>
        private static RawImage CreateRawImage(string name, RectTransform parent)
        {
            return CreateRect(name, parent).gameObject.AddComponent<RawImage>();
        }

        // ---------------------------------------------------------------
        // #464: render-to-texture snapshots (one-shot per Open/Render)
        // ---------------------------------------------------------------

        private RenderTexture Snapshot(GameObject subject)
        {
            var texture = ResolvePortraitCamera().Capture(subject);
            snapshots.Add(texture);
            return texture;
        }

        private PortraitCamera ResolvePortraitCamera()
        {
            if (portraitCamera == null)
            {
                var rigObject = new GameObject("HouseProfilePortraitCamera", typeof(Camera));
                portraitCamera = rigObject.AddComponent<PortraitCamera>();
                portraitCamera.Init();
            }

            return portraitCamera;
        }

        private void ReleaseSnapshots()
        {
            foreach (var texture in snapshots)
            {
                DestroyTexture(texture);
            }

            snapshots.Clear();
        }

        private static void DestroyTexture(RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            texture.Release();
            if (Application.isPlaying)
            {
                Destroy(texture);
            }
            else
            {
                DestroyImmediate(texture);
            }
        }

        private void OnDestroy()
        {
            ReleaseSnapshots();
            if (portraitCamera != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(portraitCamera.gameObject);
                }
                else
                {
                    DestroyImmediate(portraitCamera.gameObject);
                }
            }
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
