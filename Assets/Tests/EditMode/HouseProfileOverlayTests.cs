using System.Collections.Generic;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #208: the house profile overlay, built under the #256 CanvasScaler and
    /// asserted against the approved wireframe's named constants
    /// (docs/specs/ui/house-profile.md / mockups/house-profile.html,
    /// #161/#293). Covers the centered card + scrim, the level badge
    /// (`Lv N` + N-of-4 pips), 0–3 resident link rows sourced from the dogs'
    /// Core <see cref="DogProfile"/> (each opening that dog's profile, #165),
    /// the vacant empty-state, and the footer Upgrade action (#59/#294): its
    /// next cost, the disabled Max-level state, the live affordability greying
    /// against the wallet, and Option A direct-spend (tapping calls
    /// <see cref="GameState.TryUpgradeHouse"/> once, then re-renders).
    /// </summary>
    public class HouseProfileOverlayTests
    {
        private const string BundledFontPath = "Assets/UI/Fonts/Resources/DejaVuSans.ttf";

        private GameObject canvasHost;
        private GameObject overlayHost;
        private HouseProfileOverlay overlay;

        [SetUp]
        public void CreateOverlay()
        {
            // #291: labels bind a bundled UI font via Resources.Load; force-import
            // it so a fresh CI Library resolves it before the overlay is built.
            AssetDatabase.ImportAsset(BundledFontPath, ImportAssetOptions.ForceSynchronousImport);

            canvasHost = new GameObject("ui-canvas", typeof(Canvas));
            canvasHost.AddComponent<UiCanvas>().Configure();

            overlayHost = new GameObject("house-profile-overlay");
            overlayHost.transform.SetParent(canvasHost.transform, false);
            overlay = overlayHost.AddComponent<HouseProfileOverlay>();
            overlay.Init();
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(canvasHost);
        }

        private static House HouseAt(int level, bool vacant)
        {
            return new House(2, Quadrant.NorthWest, isVacant: vacant, level: level);
        }

        private static Dog ResidentDog(string name, Breed breed)
        {
            return new Dog(name, breed, Personality.Brave, 2, isPuppy: false);
        }

        [Test]
        public void LayoutConstants_MatchTheApprovedWireframe()
        {
            Assert.That(HouseProfileOverlay.ProfileWidthPx, Is.EqualTo(900f));
            Assert.That(HouseProfileOverlay.ProfilePaddingPx, Is.EqualTo(48f));
            Assert.That(HouseProfileOverlay.ThumbnailSizePx, Is.EqualTo(220f));
            Assert.That(HouseProfileOverlay.CloseButtonSizePx, Is.EqualTo(72f));
            Assert.That(HouseProfileOverlay.LevelPipCount, Is.EqualTo(4));
            Assert.That(HouseProfileOverlay.LevelPipDiameterPx, Is.EqualTo(28f));
            Assert.That(HouseProfileOverlay.LevelPipGapPx, Is.EqualTo(12f));
            Assert.That(HouseProfileOverlay.ResidentRowHeightPx, Is.EqualTo(120f));
            Assert.That(HouseProfileOverlay.ResidentRowGapPx, Is.EqualTo(16f));
            Assert.That(HouseProfileOverlay.ResidentAvatarSizePx, Is.EqualTo(96f));
            Assert.That(HouseProfileOverlay.UpgradeButtonHeightPx, Is.EqualTo(96f),
                "the Upgrade button reuses the shared 96px PillButton (#173)");
        }

        [Test]
        public void Card_IsCenteredAtTheWireframeWidth()
        {
            Assert.That(overlay.CardRect.sizeDelta.x, Is.EqualTo(HouseProfileOverlay.ProfileWidthPx));
            Assert.That(overlay.CardRect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)),
                "the card is centered over the scrim (ProfileAnchor = Center)");
            Assert.That(overlay.CardRect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        }

        [Test]
        public void Scrim_StretchesAcrossTheWholeCanvas()
        {
            Assert.That(overlay.ScrimRect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(overlay.ScrimRect.anchorMax, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void CloseButton_IsTheWireframeSizeAndAnchoredTopRight()
        {
            Assert.That(overlay.CloseButtonRect.sizeDelta.x, Is.EqualTo(HouseProfileOverlay.CloseButtonSizePx));
            Assert.That(overlay.CloseButtonRect.sizeDelta.y, Is.EqualTo(HouseProfileOverlay.CloseButtonSizePx));
            Assert.That(overlay.CloseButtonRect.anchorMin, Is.EqualTo(Vector2.one));
            Assert.That(overlay.CloseButtonRect.anchorMax, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void Thumbnail_IsTheWireframeSize()
        {
            Assert.That(overlay.ThumbnailRect.sizeDelta.x, Is.EqualTo(HouseProfileOverlay.ThumbnailSizePx));
            Assert.That(overlay.ThumbnailRect.sizeDelta.y, Is.EqualTo(HouseProfileOverlay.ThumbnailSizePx));
        }

        [Test]
        public void LevelBadge_ShowsLvNPlusFourPips_WithTheCurrentLevelFilled()
        {
            overlay.Open(HouseAt(2, false), new List<Dog> { ResidentDog("Biscuit", Breed.FrenchBulldog) });

            Assert.That(overlay.LevelLabel.text, Is.EqualTo("Lv 2"));
            Assert.That(overlay.LevelPips.Count, Is.EqualTo(4));
            Assert.That(overlay.FilledPipCount, Is.EqualTo(2),
                "filled pips = current level");
        }

        [Test]
        public void Overlay_StartsClosed()
        {
            Assert.That(overlay.IsOpen, Is.False);
        }

        [Test]
        public void Open_RendersTheHousesActualModelSnapshot_IntoTheThumbnail()
        {
            // #464: the thumbnail box is now a RawImage filled with a
            // render-to-texture snapshot of the house's actual current model
            // (respecting its level, variant, and vacancy tint), not a flat color.
            overlay.Open(HouseAt(2, false), new List<Dog> { ResidentDog("Biscuit", Breed.FrenchBulldog) });

            Assert.That(overlay.ThumbnailImage, Is.Not.Null,
                "the 220px thumbnail is a RawImage showing a rendered house model, not a flat-color Image");
            Assert.That(overlay.ThumbnailImage.texture, Is.Not.Null,
                "Open captures the house's model snapshot into the thumbnail's RawImage.texture");
        }

        [Test]
        public void Open_RendersEachResidentsBreedModelSnapshot_IntoTheAvatar()
        {
            var residents = new List<Dog>
            {
                ResidentDog("Biscuit", Breed.FrenchBulldog),
                ResidentDog("Nugget", Breed.Beagle),
            };

            overlay.Open(HouseAt(2, false), residents);

            Assert.That(overlay.Residents.Count, Is.EqualTo(2));
            foreach (var row in overlay.Residents)
            {
                Assert.That(row.Avatar, Is.Not.Null,
                    "each 96px resident avatar is a RawImage showing a rendered model");
                Assert.That(row.Avatar.texture, Is.Not.Null,
                    "each resident's avatar gets a breed-tinted model snapshot");
            }
        }

        [Test]
        public void SnapshotCapture_IsOneShotPerOpen_OneHousePlusOnePerResident()
        {
            // #464 / Derek's rationale: capture once on Open, not live every
            // frame. One render for the house thumbnail plus one per resident.
            overlay.Open(HouseAt(2, false), new List<Dog>
            {
                ResidentDog("Biscuit", Breed.FrenchBulldog),
                ResidentDog("Nugget", Breed.Beagle),
            });

            Assert.That(overlay.Portrait.RenderCount, Is.EqualTo(3),
                "1 house thumbnail + 2 resident avatars = 3 one-shot captures");
        }

        [Test]
        public void Open_BuildsAResidentRowPerDog_FromTheirCoreData()
        {
            var residents = new List<Dog>
            {
                ResidentDog("Biscuit", Breed.FrenchBulldog),
                ResidentDog("Nugget", Breed.FrenchBulldog),
            };

            overlay.Open(HouseAt(2, false), residents);

            Assert.That(overlay.IsOpen, Is.True);
            Assert.That(overlay.Residents.Count, Is.EqualTo(2));
            Assert.That(overlay.Residents[0].NameLabel.text, Is.EqualTo("Biscuit"));
            Assert.That(overlay.Residents[0].BreedChipLabel.text, Is.EqualTo("French Bulldog"));
            Assert.That(overlay.Residents[1].NameLabel.text, Is.EqualTo("Nugget"));
            Assert.That(overlay.EmptyStateLabel.gameObject.activeSelf, Is.False,
                "the empty state is hidden when the house has residents");
        }

        [Test]
        public void TappingAResidentRow_RequestsThatDogsProfile()
        {
            var biscuit = ResidentDog("Biscuit", Breed.FrenchBulldog);
            var nugget = ResidentDog("Nugget", Breed.FrenchBulldog);
            Dog selected = null;
            overlay.ResidentSelected += dog => selected = dog;

            overlay.Open(HouseAt(2, false), new List<Dog> { biscuit, nugget });
            overlay.Residents[1].Button.onClick.Invoke();

            Assert.That(selected, Is.SameAs(nugget),
                "a resident row opens that dog's profile (#165)");
        }

        [Test]
        public void VacantHouse_ShowsTheEmptyState_AndOffersNoUpgrade()
        {
            overlay.Open(HouseAt(1, true), new List<Dog>());

            Assert.That(overlay.Residents.Count, Is.EqualTo(0));
            Assert.That(overlay.EmptyStateLabel.gameObject.activeSelf, Is.True);
            Assert.That(overlay.EmptyStateLabel.text, Is.EqualTo("No dogs live here yet."));
            Assert.That(overlay.UpgradeButtonRect.gameObject.activeSelf, Is.False,
                "no Upgrade action is offered for a vacant house (house-profile.md)");
        }

        [Test]
        public void OccupiedHouse_ShowsTheUpgradeButtonWithTheNextCost()
        {
            // #294: the button is enabled only when the live wallet can cover the
            // next step; wire a balance that comfortably affords the 200-coin step.
            overlay.ConfigureUpgrade(() => 1000, _ => false);
            overlay.Open(HouseAt(2, false), new List<Dog> { ResidentDog("Biscuit", Breed.FrenchBulldog) });

            Assert.That(overlay.UpgradeButtonRect.gameObject.activeSelf, Is.True);
            Assert.That(overlay.UpgradeButtonLabel.text, Is.EqualTo("Upgrade · 200"));
            Assert.That(overlay.UpgradeButton.interactable, Is.True);
            Assert.That(overlay.UpgradeButtonRect.sizeDelta.y, Is.EqualTo(HouseProfileOverlay.UpgradeButtonHeightPx));
        }

        [Test]
        public void UpgradeButton_IsEnabled_WhenTheWalletCoversTheNextCost()
        {
            overlay.ConfigureUpgrade(() => 200, _ => false); // exactly the level 2 -> 3 cost
            overlay.Open(HouseAt(2, false), new List<Dog> { ResidentDog("Biscuit", Breed.FrenchBulldog) });

            Assert.That(overlay.UpgradeButton.interactable, Is.True,
                "a balance equal to the next cost affords the upgrade (#294)");
        }

        [Test]
        public void UpgradeButton_IsDisabled_WhenTheWalletCannotCoverTheNextCost()
        {
            overlay.ConfigureUpgrade(() => 199, _ => false); // one coin short of 200
            overlay.Open(HouseAt(2, false), new List<Dog> { ResidentDog("Biscuit", Breed.FrenchBulldog) });

            Assert.That(overlay.UpgradeButton.interactable, Is.False,
                "the button greys out when the player can't afford the next step (#294)");
            Assert.That(overlay.UpgradeButtonLabel.text, Is.EqualTo("Upgrade · 200"),
                "the label still names the cost even while unaffordable");
        }

        [Test]
        public void MaxLevelHouse_DisablesTheUpgradeButtonIntoAMaxLevelState()
        {
            overlay.Open(HouseAt(4, false), new List<Dog> { ResidentDog("Biscuit", Breed.FrenchBulldog) });

            Assert.That(overlay.UpgradeButtonRect.gameObject.activeSelf, Is.True);
            Assert.That(overlay.UpgradeButtonLabel.text, Is.EqualTo("Max level"));
            Assert.That(overlay.UpgradeButton.interactable, Is.False);
        }

        [Test]
        public void TappingUpgrade_SpendsCoinsDirectlyViaCore_ThenReRendersTheProfile()
        {
            // #294 Option A: no confirmation — tapping calls TryUpgradeHouse
            // once and the open profile re-renders to the new level / next cost /
            // affordability. Exercise the real Core entry point end to end.
            var state = GameState.CreateNew();
            var house = state.Houses[0];           // occupied, level 1 (cost L1->L2 = 100)
            state.Wallet.Deposit(100);             // exactly the first step, nothing left over

            var upgradeCalls = 0;
            overlay.ConfigureUpgrade(
                () => state.Wallet.Coins,
                houseId => { upgradeCalls++; return state.TryUpgradeHouse(houseId); });
            overlay.Open(house, new List<Dog> { ResidentDog("Rex", Breed.Beagle) });

            Assert.That(overlay.UpgradeButton.interactable, Is.True, "100 coins affords the 100-coin step");
            Assert.That(overlay.UpgradeButtonLabel.text, Is.EqualTo("Upgrade · 100"));

            overlay.UpgradeButton.onClick.Invoke(); // tap

            Assert.That(upgradeCalls, Is.EqualTo(1), "the Core entry point is called exactly once");
            Assert.That(house.Level, Is.EqualTo(2), "the house upgraded one level via Core");
            Assert.That(state.Wallet.Coins, Is.EqualTo(0), "the coins were spent directly (no confirmation)");
            Assert.That(overlay.LevelLabel.text, Is.EqualTo("Lv 2"), "the profile re-rendered the new level");
            Assert.That(overlay.FilledPipCount, Is.EqualTo(2));
            Assert.That(overlay.UpgradeButtonLabel.text, Is.EqualTo("Upgrade · 200"),
                "the button now shows the next step's cost");
            Assert.That(overlay.UpgradeButton.interactable, Is.False,
                "affordability re-read against the reduced balance disables the now-unaffordable button");
        }

        [Test]
        public void UpgradingIntoTheCap_ReRendersIntoTheDisabledMaxLevelState()
        {
            var state = GameState.CreateNew();
            var house = state.Houses[0];  // level 1
            state.Wallet.Deposit(100 + 200 + 400);
            state.TryUpgradeHouse(house.Id); // -> 2
            state.TryUpgradeHouse(house.Id); // -> 3 (now 400 left, one step from the cap)

            overlay.ConfigureUpgrade(() => state.Wallet.Coins, houseId => state.TryUpgradeHouse(houseId));
            overlay.Open(house, new List<Dog> { ResidentDog("Rex", Breed.Beagle) });
            Assert.That(overlay.UpgradeButtonLabel.text, Is.EqualTo("Upgrade · 400"));

            overlay.UpgradeButton.onClick.Invoke(); // tap into the cap

            Assert.That(house.Level, Is.EqualTo(4));
            Assert.That(overlay.UpgradeButtonLabel.text, Is.EqualTo("Max level"),
                "reaching the cap re-renders into the Max level state (#294)");
            Assert.That(overlay.UpgradeButton.interactable, Is.False);
            Assert.That(overlay.UpgradeButtonRect.gameObject.activeSelf, Is.True,
                "the footer still shows the disabled Max-level button");
        }

        [Test]
        public void Close_HidesThePanel()
        {
            overlay.Open(HouseAt(2, false), new List<Dog> { ResidentDog("Biscuit", Breed.FrenchBulldog) });
            overlay.Close();

            Assert.That(overlay.IsOpen, Is.False);
        }

        [Test]
        public void Reopening_RebuildsTheResidentRows_ForTheNewHouse()
        {
            overlay.Open(HouseAt(2, false), new List<Dog>
            {
                ResidentDog("Biscuit", Breed.FrenchBulldog),
                ResidentDog("Nugget", Breed.FrenchBulldog),
            });
            overlay.Open(HouseAt(1, false), new List<Dog> { ResidentDog("Rex", Breed.Beagle) });

            Assert.That(overlay.Residents.Count, Is.EqualTo(1));
            Assert.That(overlay.Residents[0].NameLabel.text, Is.EqualTo("Rex"));
        }

        [Test]
        public void Labels_UseTheBundledFont_NotAnEditorOnlyBuiltinLookup()
        {
            overlay.Open(HouseAt(2, false), new List<Dog> { ResidentDog("Biscuit", Breed.FrenchBulldog) });
            var font = overlay.LevelLabel.font;

            Assert.That(font, Is.Not.Null,
                "the level label has no font — it would draw nothing in the Android build (#291)");
            Assert.That(font.name, Does.Contain("DejaVu"));
            Assert.That(font.name, Does.Not.Contain("Arial"));
        }

        // --- #465: Candy Cottage chrome restyle (CandyChromeUgui, shared-components.md) ---

        [Test]
        public void Card_HasCandyCottageChrome_FillOutlineRadiusAndHardShadow()
        {
            var image = overlay.CardRect.GetComponent<Image>();

            AssertHex(image.color, 0xFF, 0xFD, 0xF7, "panel fill (#FFFDF7)");
            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(image.sprite, Is.Not.Null);
            Assert.That(image.sprite.border,
                Is.EqualTo(new Vector4(CandyChromeUgui.PanelRadiusPx, CandyChromeUgui.PanelRadiusPx,
                    CandyChromeUgui.PanelRadiusPx, CandyChromeUgui.PanelRadiusPx)),
                "the card corner radius is the shared PanelRadiusPx = 40");

            AssertInkOutline(overlay.CardRect.gameObject);
            AssertHardShadow(overlay.CardRect.gameObject);
        }

        [Test]
        public void CloseButton_IsACreamPill_WithOutlineAndHardShadow()
        {
            AssertInkOutline(overlay.CloseButtonRect.gameObject);
            AssertHardShadow(overlay.CloseButtonRect.gameObject);
            AssertHex(overlay.CloseButtonRect.GetComponent<Image>().color, 0xFF, 0xF3, 0xD9,
                "the close affordance is a cream pill");
        }

        [Test]
        public void UpgradeButton_IsACoralPill_WithOutlineAndHardShadow_AtTheSharedPillHeight()
        {
            overlay.ConfigureUpgrade(() => 1000, _ => false);
            overlay.Open(HouseAt(2, false), new List<Dog> { ResidentDog("Biscuit", Breed.FrenchBulldog) });

            AssertHex(overlay.UpgradeButtonRect.GetComponent<Image>().color, 0xFF, 0x7A, 0x5C,
                "the affordable Upgrade button takes the Coral spend role tint");
            Assert.That(overlay.UpgradeButtonRect.sizeDelta.y, Is.EqualTo(HouseProfileOverlay.UpgradeButtonHeightPx));
            AssertInkOutline(overlay.UpgradeButtonRect.gameObject);
            AssertHardShadow(overlay.UpgradeButtonRect.gameObject);
        }

        [Test]
        public void UpgradeButton_DisabledState_RemapsOntoTheDisabledRole()
        {
            overlay.ConfigureUpgrade(() => 0, _ => false); // cannot afford
            overlay.Open(HouseAt(2, false), new List<Dog> { ResidentDog("Biscuit", Breed.FrenchBulldog) });

            Assert.That(overlay.UpgradeButton.interactable, Is.False);
            AssertHex(overlay.UpgradeButtonRect.GetComponent<Image>().color, 0xD8, 0xD2, 0xC6,
                "the unaffordable/Max-level Upgrade button greys onto the Disabled role (#298 palette)");
        }

        [Test]
        public void IneligibleHouse_DuringOnboardingUpgradeStep_GreysTheUpgradeButtonOntoTheDisabledRole()
        {
            // #469: while the onboarding "upgrade a house" step is scoped to the
            // first-quest dog's house, a non-target house's Upgrade button reads
            // as unavailable through the EXISTING disabled affordance — even when
            // the wallet could afford the step — so the player isn't nudged to
            // spend on a house that won't advance the chain.
            overlay.ConfigureUpgrade(() => 1000, _ => false, _ => false); // affordable, but not the eligible house
            overlay.Open(HouseAt(2, false), new List<Dog> { ResidentDog("Biscuit", Breed.FrenchBulldog) });

            Assert.That(overlay.UpgradeButton.interactable, Is.False,
                "an ineligible house's Upgrade button is disabled even when affordable");
            AssertHex(overlay.UpgradeButtonRect.GetComponent<Image>().color, 0xD8, 0xD2, 0xC6,
                "the ineligible Upgrade button greys onto the same Disabled role as an unaffordable one");
        }

        [Test]
        public void EligibleAffordableHouse_KeepsTheUpgradeButtonEnabled()
        {
            // #469 guard: the eligibility gate only disables the ineligible case;
            // an eligible, affordable house's button stays enabled as before.
            overlay.ConfigureUpgrade(() => 1000, _ => false, _ => true);
            overlay.Open(HouseAt(2, false), new List<Dog> { ResidentDog("Biscuit", Breed.FrenchBulldog) });

            Assert.That(overlay.UpgradeButton.interactable, Is.True);
            AssertHex(overlay.UpgradeButtonRect.GetComponent<Image>().color, 0xFF, 0x7A, 0x5C,
                "the eligible affordable Upgrade button keeps the Coral spend role tint");
        }

        [Test]
        public void InlineElements_CarryAnInkOutline_WithNoDropShadow()
        {
            overlay.Open(HouseAt(2, false), new List<Dog> { ResidentDog("Biscuit", Breed.FrenchBulldog) });

            AssertInkOutlineNoShadow(overlay.LevelBadge.gameObject);
            foreach (var pip in overlay.LevelPips)
            {
                AssertInkOutlineNoShadow(pip.gameObject);
            }

            var row = overlay.Residents[0];
            AssertInkOutlineNoShadow(row.Rect.gameObject);
            AssertInkOutlineNoShadow(row.BreedChip.gameObject);
            AssertInkOutlineNoShadow(row.Avatar.gameObject);
        }

        [Test]
        public void Thumbnail_CarriesAnInkOutlineFrame_PreservingTheRenderTextureSnapshot()
        {
            // #464 must not be disturbed: the thumbnail stays a RawImage showing the
            // render-to-texture snapshot; the chrome only adds an outline frame.
            overlay.Open(HouseAt(2, false), new List<Dog> { ResidentDog("Biscuit", Breed.FrenchBulldog) });

            Assert.That(overlay.ThumbnailImage.texture, Is.Not.Null,
                "the #464 render-texture snapshot survives the chrome pass");
            AssertInkOutlineNoShadow(overlay.ThumbnailImage.gameObject);
        }

        [Test]
        public void NonPaletteAccentFills_AreLeftUnchanged()
        {
            overlay.Open(HouseAt(2, false), new List<Dog> { ResidentDog("Biscuit", Breed.FrenchBulldog) });

            var row = overlay.Residents[0];
            AssertHex(row.Rect.GetComponent<Image>().color, 0xE7, 0xDF, 0xCE,
                "the resident row keeps its non-palette stage-tan accent fill");
            AssertHex(row.BreedChip.GetComponent<Image>().color, 0x6E, 0xC6, 0xE0,
                "the breed chip keeps its non-palette sky accent fill");
        }

        private static void AssertInkOutline(GameObject go)
        {
            var outline = go.GetComponent<Outline>();
            Assert.That(outline, Is.Not.Null, go.name + " has no Candy Cottage outline");
            AssertHex(outline.effectColor, 0x2E, 0x2A, 0x26, go.name + " outline");
            Assert.That(outline.effectDistance,
                Is.EqualTo(new Vector2(CandyChromeUgui.OutlineThicknessPx, CandyChromeUgui.OutlineThicknessPx)),
                go.name + " outline thickness is not the shared OutlineThicknessPx = 6");
        }

        private static void AssertHardShadow(GameObject go)
        {
            var shadow = PureShadowOf(go);
            Assert.That(shadow, Is.Not.Null, go.name + " has no hard drop-shadow");
            AssertHex(shadow.effectColor, 0x2E, 0x2A, 0x26, go.name + " shadow");
            Assert.That(shadow.effectDistance, Is.EqualTo(new Vector2(0f, -CandyChromeUgui.ShadowOffsetPx)),
                go.name + " shadow is not a single hard offset at the shared ShadowOffsetPx = 8 (no blur)");
        }

        private static void AssertInkOutlineNoShadow(GameObject go)
        {
            AssertInkOutline(go);
            Assert.That(PureShadowOf(go), Is.Null,
                go.name + " is an inline element and must carry no drop-shadow (Settings toggle/knob precedent)");
        }

        private static void AssertHex(Color color, byte r, byte g, byte b, string what)
        {
            var c32 = (Color32)color;
            Assert.That(c32.r, Is.EqualTo(r), what + " red channel");
            Assert.That(c32.g, Is.EqualTo(g), what + " green channel");
            Assert.That(c32.b, Is.EqualTo(b), what + " blue channel");
        }

        private static Shadow PureShadowOf(GameObject go)
        {
            foreach (var shadow in go.GetComponents<Shadow>())
            {
                if (shadow.GetType() == typeof(Shadow))
                {
                    return shadow;
                }
            }

            return null;
        }
    }
}
