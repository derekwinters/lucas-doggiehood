using Doggiehood.Core.Dogs;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #165: the dog profile overlay, built under the #256 CanvasScaler and
    /// asserted against the approved wireframe's named constants
    /// (docs/specs/ui/dog-profile.md / mockups/dog-profile.html, #161/#177).
    /// Covers the centered card + scrim, the four data fields sourced from the
    /// Core <see cref="DogProfile"/>, the top-right close affordance, and the
    /// Home button that requests a camera fly-to that dog's house.
    /// </summary>
    public class DogProfileOverlayTests
    {
        private const string BundledFontPath = "Assets/UI/Fonts/Resources/DejaVuSans.ttf";

        private GameObject canvasHost;
        private GameObject overlayHost;
        private DogProfileOverlay overlay;

        [SetUp]
        public void CreateOverlay()
        {
            // #544: the modal-input gate is a process-global singleton; clear it
            // so a registration leaked by an earlier test can't make this
            // overlay's gate read as already blocking before it opens.
            Doggiehood.Core.Cameras.ModalInputGate.Shared.Clear();

            // #291: labels bind a bundled UI font via Resources.Load; force-import
            // it so a fresh CI Library resolves it before the overlay is built
            // (docs/engineering/unity-serialization.md §4).
            AssetDatabase.ImportAsset(BundledFontPath, ImportAssetOptions.ForceSynchronousImport);

            canvasHost = new GameObject("ui-canvas", typeof(Canvas));
            canvasHost.AddComponent<UiCanvas>().Configure();

            overlayHost = new GameObject("dog-profile-overlay");
            overlayHost.transform.SetParent(canvasHost.transform, false);
            overlay = overlayHost.AddComponent<DogProfileOverlay>();
            overlay.Init();
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(canvasHost);
        }

        [Test]
        public void Open_RegistersWithTheSharedModalGate_Close_Unregisters()
        {
            // #544: an open profile blocks taps on world objects behind its
            // scrim; closing it releases the block.
            Assert.That(Doggiehood.Core.Cameras.ModalInputGate.Shared.IsBlocking, Is.False,
                "no modal is registered before the profile opens");

            overlay.Open(SampleDog());
            Assert.That(Doggiehood.Core.Cameras.ModalInputGate.Shared.IsBlocking, Is.True,
                "an open dog profile registers with the shared modal gate");

            overlay.Close();
            Assert.That(Doggiehood.Core.Cameras.ModalInputGate.Shared.IsBlocking, Is.False,
                "closing the dog profile unregisters it from the shared modal gate");
        }

        [Test]
        public void ScrimTap_StillCloses_WhileTheModalGateBlocks()
        {
            // #544 regression guard: the scrim still dismisses (its Button.onClick
            // → Close runs through UGUI); only world pass-through is suppressed.
            overlay.Open(SampleDog());
            Assert.That(Doggiehood.Core.Cameras.ModalInputGate.Shared.IsBlocking, Is.True);

            overlay.ScrimRect.GetComponent<Button>().onClick.Invoke();

            Assert.That(overlay.IsOpen, Is.False,
                "a scrim tap still closes the profile");
            Assert.That(Doggiehood.Core.Cameras.ModalInputGate.Shared.IsBlocking, Is.False,
                "and leaves no modal registered afterwards");
        }

        private static Dog SampleDog()
        {
            // Bailey — Golden Retriever, Adventurous, house 2, adult.
            return new Dog("Bailey", Breed.GoldenRetriever, Personality.Adventurous, 2, false);
        }

        [Test]
        public void LayoutConstants_MatchTheApprovedWireframe()
        {
            Assert.That(DogProfileOverlay.ProfileWidthPx, Is.EqualTo(900f));
            Assert.That(DogProfileOverlay.ProfilePaddingPx, Is.EqualTo(48f));
            Assert.That(DogProfileOverlay.PortraitSizePx, Is.EqualTo(220f));
            Assert.That(DogProfileOverlay.CloseButtonSizePx, Is.EqualTo(72f));
            Assert.That(DogProfileOverlay.HomeButtonHeightPx, Is.EqualTo(96f),
                "the Home button reuses the shared 96px PillButton (#173)");
        }

        [Test]
        public void Card_IsCenteredAtTheWireframeWidth()
        {
            var rect = overlay.CardRect;

            Assert.That(rect.sizeDelta.x, Is.EqualTo(DogProfileOverlay.ProfileWidthPx));
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)),
                "the card is centered over the scrim (ProfileAnchor = Center)");
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
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
            Assert.That(overlay.CloseButtonRect.sizeDelta.x, Is.EqualTo(DogProfileOverlay.CloseButtonSizePx));
            Assert.That(overlay.CloseButtonRect.sizeDelta.y, Is.EqualTo(DogProfileOverlay.CloseButtonSizePx));
            Assert.That(overlay.CloseButtonRect.anchorMin, Is.EqualTo(Vector2.one),
                "the close affordance sits at the card's top-right corner");
            Assert.That(overlay.CloseButtonRect.anchorMax, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void Portrait_IsTheWireframeSize()
        {
            Assert.That(overlay.PortraitRect.sizeDelta.x, Is.EqualTo(DogProfileOverlay.PortraitSizePx));
            Assert.That(overlay.PortraitRect.sizeDelta.y, Is.EqualTo(DogProfileOverlay.PortraitSizePx));
        }

        [Test]
        public void HomeButton_IsTheSharedPillHeight()
        {
            Assert.That(overlay.HomeButtonRect.sizeDelta.y, Is.EqualTo(DogProfileOverlay.HomeButtonHeightPx));
        }

        [Test]
        public void Overlay_StartsClosed()
        {
            Assert.That(overlay.IsOpen, Is.False);
        }

        [Test]
        public void Open_ShowsTheFourFields_ReadFromTheDogsCoreData()
        {
            overlay.Open(SampleDog());

            Assert.That(overlay.IsOpen, Is.True);
            Assert.That(overlay.NameLabel.text, Is.EqualTo("Bailey"));
            Assert.That(overlay.BreedChipLabel.text, Is.EqualTo("Golden Retriever"));
            Assert.That(overlay.AgeValueLabel.text, Is.EqualTo("Adult"));
            Assert.That(overlay.PersonalityValueLabel.text, Is.EqualTo("Adventurous"));
        }

        [Test]
        public void Open_RendersTheDogsBreedModelSnapshot_IntoThePortrait()
        {
            // #464: the portrait box is now a RawImage filled with a
            // render-to-texture snapshot of the dog's breed-tinted model,
            // captured once on Open (not a flat placeholder color).
            overlay.Open(SampleDog());

            Assert.That(overlay.PortraitImage, Is.Not.Null,
                "the 220px portrait is a RawImage showing a rendered model, not a flat-color Image");
            Assert.That(overlay.PortraitImage.texture, Is.Not.Null,
                "Open captures the dog's model snapshot into the portrait's RawImage.texture");
            Assert.That(overlay.Portrait.RenderCount, Is.EqualTo(1),
                "exactly one snapshot per Open — captured once, not live every frame");
        }

        [Test]
        public void StatTiles_AreLabeledAgeAndPersonality()
        {
            overlay.Open(SampleDog());

            Assert.That(overlay.AgeKeyLabel.text, Is.EqualTo("Age"));
            Assert.That(overlay.PersonalityKeyLabel.text, Is.EqualTo("Personality"));
        }

        [Test]
        public void Close_HidesThePanel()
        {
            overlay.Open(SampleDog());
            overlay.Close();

            Assert.That(overlay.IsOpen, Is.False);
        }

        [Test]
        public void HomeButton_RequestsAFlyToThatDogsHouse_AndClosesTheProfile()
        {
            var requestedHouse = -1;
            overlay.HomeRequested += houseId => requestedHouse = houseId;

            overlay.Open(SampleDog());
            overlay.Home();

            Assert.That(requestedHouse, Is.EqualTo(2),
                "Home flies the camera to the tapped dog's house (#165)");
            Assert.That(overlay.IsOpen, Is.False,
                "the Home button closes the profile before the camera moves");
        }

        [Test]
        public void Labels_UseTheBundledFont_NotAnEditorOnlyBuiltinLookup()
        {
            overlay.Open(SampleDog());
            var font = overlay.NameLabel.font;

            Assert.That(font, Is.Not.Null,
                "the name label has no font — it would draw nothing in the Android build (#291)");
            Assert.That(font.name, Does.Contain("DejaVu"));
            Assert.That(font.name, Does.Not.Contain("Arial"));
            Assert.That(font.name, Does.Not.Contain("LegacyRuntime"));
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
        public void HomeButton_IsALeafPill_WithOutlineAndHardShadow_AtTheSharedPillHeight()
        {
            AssertHex(overlay.HomeButtonRect.GetComponent<Image>().color, 0x58, 0xC0, 0x6A,
                "the Home button takes the Leaf positive role tint");
            Assert.That(overlay.HomeButtonRect.sizeDelta.y, Is.EqualTo(DogProfileOverlay.HomeButtonHeightPx));
            AssertInkOutline(overlay.HomeButtonRect.gameObject);
            AssertHardShadow(overlay.HomeButtonRect.gameObject);
        }

        [Test]
        public void InlineElements_CarryAnInkOutline_WithNoDropShadow()
        {
            overlay.Open(SampleDog());

            AssertInkOutlineNoShadow(overlay.BreedChip.gameObject);
            AssertInkOutlineNoShadow(overlay.AgeTile.gameObject);
            AssertInkOutlineNoShadow(overlay.PersonalityTile.gameObject);
        }

        [Test]
        public void Portrait_CarriesAnInkOutlineFrame_PreservingTheRenderTextureSnapshot()
        {
            // #464 must not be disturbed: the portrait stays a RawImage showing the
            // render-to-texture snapshot; the chrome only adds an outline frame.
            overlay.Open(SampleDog());

            Assert.That(overlay.PortraitImage.texture, Is.Not.Null,
                "the #464 render-texture snapshot survives the chrome pass");
            AssertInkOutlineNoShadow(overlay.PortraitImage.gameObject);
        }

        [Test]
        public void NonPaletteAccentFills_AreLeftUnchanged()
        {
            overlay.Open(SampleDog());

            AssertHex(overlay.BreedChip.GetComponent<Image>().color, 0x6E, 0xC6, 0xE0,
                "the breed chip keeps its non-palette sky accent fill");
            AssertHex(overlay.AgeTile.GetComponent<Image>().color, 0xE7, 0xDF, 0xCE,
                "the stat tile keeps its non-palette stage-tan accent fill");
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
