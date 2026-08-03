using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Expansion;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using NUnit.Framework;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #518: the "Welcome to the neighborhood!" move-in pop-up
    /// (<see cref="WelcomePopup"/>), built to the approved wireframe
    /// (docs/specs/ui/welcome-popup.md / mockups/welcome-popup.html). Reuses the
    /// onboarding reward panel composition — a portrait medal overlapping the top
    /// edge, a big fixed heading, one leaf pill — parameterized for an arrival:
    /// the new dog's name, one dynamic meta line, an optional per-dog member-chip
    /// row (hidden for a single-dog move-in), and a single "Say hi!" button that
    /// dismisses AND pans the camera to the new house. Always dismissible (button
    /// OR scrim), never a trap; the scrim dismisses WITHOUT panning. The dynamic
    /// copy comes from engine-free Core (<see cref="WelcomeMessage"/>).
    /// </summary>
    public class WelcomePopupTests
    {
        private const string BundledFontPath = "Assets/UI/Fonts/Resources/DejaVuSans.ttf";
        private const int HouseId = 3;

        private GameObject canvasHost;
        private GameObject overlayHost;
        private WelcomePopup popup;

        private static Dog Adult(string name, Breed breed) =>
            new Dog(name, breed, Personality.Brave, HouseId, isPuppy: false);

        private static Dog Puppy(string name, Breed breed) =>
            new Dog(name, breed, Personality.Excited, HouseId, isPuppy: true);

        [SetUp]
        public void CreatePopup()
        {
            // #544: the modal-input gate is a process-global singleton; clear it
            // so a registration leaked by an earlier test can't make this
            // pop-up's gate read as already blocking before it shows.
            Doggiehood.Core.Cameras.ModalInputGate.Shared.Clear();

            AssetDatabase.ImportAsset(BundledFontPath, ImportAssetOptions.ForceSynchronousImport);

            canvasHost = new GameObject("ui-canvas", typeof(Canvas));
            canvasHost.AddComponent<UiCanvas>().Configure();

            overlayHost = new GameObject("welcome-popup");
            overlayHost.transform.SetParent(canvasHost.transform, false);
            popup = overlayHost.AddComponent<WelcomePopup>();
            popup.Init();
        }

        [TearDown]
        public void Cleanup()
        {
            UnityEngine.Object.DestroyImmediate(canvasHost);
        }

        [Test]
        public void Show_RegistersWithTheSharedModalGate_Dismiss_Unregisters()
        {
            // #544: an open welcome pop-up blocks world taps behind its scrim.
            Assert.That(Doggiehood.Core.Cameras.ModalInputGate.Shared.IsBlocking, Is.False,
                "no modal is registered before the pop-up shows");

            popup.Show(SingleMessage(), () => { });
            Assert.That(Doggiehood.Core.Cameras.ModalInputGate.Shared.IsBlocking, Is.True,
                "an open welcome pop-up registers with the shared modal gate");

            popup.Dismiss();
            Assert.That(Doggiehood.Core.Cameras.ModalInputGate.Shared.IsBlocking, Is.False,
                "dismissing the pop-up unregisters it from the shared modal gate");
        }

        [Test]
        public void ScrimTap_StillDismisses_WhileTheModalGateBlocks()
        {
            // #544 regression guard: the scrim still dismisses; only world
            // pass-through is suppressed.
            popup.Show(SingleMessage(), () => { });
            Assert.That(Doggiehood.Core.Cameras.ModalInputGate.Shared.IsBlocking, Is.True);

            popup.ScrimRect.GetComponent<Button>().onClick.Invoke();

            Assert.That(popup.IsOpen, Is.False,
                "a scrim tap still dismisses the pop-up");
            Assert.That(Doggiehood.Core.Cameras.ModalInputGate.Shared.IsBlocking, Is.False,
                "and leaves no modal registered afterwards");
        }

        // --- Layout constants come verbatim from the approved wireframe ---

        [Test]
        public void LayoutConstants_MatchTheApprovedWireframe()
        {
            Assert.That(WelcomePopup.WelcomeWidthPx, Is.EqualTo(820f));
            Assert.That(WelcomePopup.WelcomePaddingPx, Is.EqualTo(56f));
            Assert.That(WelcomePopup.PortraitDiameterPx, Is.EqualTo(176f));
            Assert.That(WelcomePopup.PortraitOverlapPx, Is.EqualTo(88f));
            Assert.That(WelcomePopup.PortraitOutlineThicknessPx, Is.EqualTo(8f));
            Assert.That(WelcomePopup.PortraitTopGapPx, Is.EqualTo(28f));
            Assert.That(WelcomePopup.HeadingFontSizePx, Is.EqualTo(54));
            Assert.That(WelcomePopup.NameFontSizePx, Is.EqualTo(40));
            Assert.That(WelcomePopup.MetaFontSizePx, Is.EqualTo(30));
            Assert.That(WelcomePopup.HeadingNameGapPx, Is.EqualTo(18f));
            Assert.That(WelcomePopup.NameMetaGapPx, Is.EqualTo(8f));
            Assert.That(WelcomePopup.MetaActionMarginPx, Is.EqualTo(40f));
            Assert.That(WelcomePopup.ActionMinWidthPx, Is.EqualTo(320f));
            Assert.That(WelcomePopup.MemberChipDiameterPx, Is.EqualTo(72f));
            Assert.That(WelcomePopup.MemberChipGapPx, Is.EqualTo(20f));
            Assert.That(WelcomePopup.MemberRowMarginPx, Is.EqualTo(28f));
            Assert.That(WelcomePopup.WelcomePopupDelaySeconds, Is.EqualTo(1.5f));
            Assert.That(WelcomePopup.ButtonHeightPx, Is.EqualTo(96f),
                "the Say hi! button reuses the shared 96px PillButton (#173)");
        }

        [Test]
        public void HeadingIsTheFixedCelebratoryHeadline()
        {
            Assert.That(WelcomePopup.HeadingText, Is.EqualTo("Welcome to the neighborhood!"));
        }

        [Test]
        public void ActionLabelIsSayHi()
        {
            Assert.That(WelcomePopup.ActionText, Is.EqualTo("Say hi!"));
        }

        [Test]
        public void Overlay_StartsClosed()
        {
            Assert.That(popup.IsOpen, Is.False);
        }

        // --- Composition: references resolve to the built hierarchy ---

        [Test]
        public void Panel_IsCenteredAtTheWireframeWidth_OverAFullScreenScrim()
        {
            Assert.That(popup.CardRect.sizeDelta.x, Is.EqualTo(WelcomePopup.WelcomeWidthPx));
            Assert.That(popup.CardRect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)),
                "the card is centered over the scrim (WelcomeAnchor = Center)");
            Assert.That(popup.CardRect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(popup.ScrimRect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(popup.ScrimRect.anchorMax, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void Portrait_IsAGrayboxDiscOfTheWireframeSize_WithItsOwnInkRing()
        {
            Assert.That(popup.PortraitRect.sizeDelta.x, Is.EqualTo(WelcomePopup.PortraitDiameterPx));
            Assert.That(popup.PortraitRect.sizeDelta.y, Is.EqualTo(WelcomePopup.PortraitDiameterPx));

            var outline = popup.PortraitImage.GetComponent<Outline>();
            Assert.That(outline, Is.Not.Null, "the portrait carries its own ink ring");
            Assert.That(outline.effectDistance.x, Is.EqualTo(WelcomePopup.PortraitOutlineThicknessPx),
                "the portrait ring is the wireframe's 8px, not the 6px panel outline");
        }

        [Test]
        public void ActionButton_IsTheLeafPill_AtLeastTheWireframeMinimumWidth()
        {
            popup.Show(SingleMessage(), () => { });

            AssertColor(popup.ActionButtonImage.color, CandyChromeUgui.Leaf,
                "the Say hi! button is the positive/leaf pill");
            Assert.That(popup.ActionButtonRect.sizeDelta.x,
                Is.GreaterThanOrEqualTo(WelcomePopup.ActionMinWidthPx),
                "the pill is at least the wireframe minimum width");
        }

        // --- Household variants: name/meta copy + member-chip visibility ---

        [Test]
        public void Show_Single_RendersNameAndMeta_AndHidesTheMemberChipRow()
        {
            popup.Show(SingleMessage(), () => { });

            Assert.That(popup.IsOpen, Is.True);
            Assert.That(popup.HeadingLabel.text, Is.EqualTo("Welcome to the neighborhood!"));
            Assert.That(popup.NameLabel.text, Is.EqualTo("Waffles"));
            Assert.That(popup.MetaLabel.text, Is.EqualTo("French Bulldog · moved in next door"));
            Assert.That(popup.MemberRow.activeSelf, Is.False,
                "the member-chip row is hidden entirely for a single-dog move-in");
            Assert.That(popup.MemberChipCount, Is.EqualTo(0));
        }

        [Test]
        public void Show_ParentAndPuppy_ShowsTwoNamedChips()
        {
            var message = WelcomeMessage.ForHousehold(new List<Dog>
            {
                Adult("Biscuit", Breed.FrenchBulldog),
                Puppy("Pepper", Breed.FrenchBulldog),
            });

            popup.Show(message, () => { });

            Assert.That(popup.NameLabel.text, Is.EqualTo("Biscuit & Pepper"));
            Assert.That(popup.MetaLabel.text, Is.EqualTo("French Bulldog family of 2"));
            Assert.That(popup.MemberRow.activeSelf, Is.True);
            Assert.That(popup.MemberChipCount, Is.EqualTo(2));
            Assert.That(popup.MemberChipNames, Is.EqualTo(new[] { "Biscuit", "Pepper" }));
        }

        [Test]
        public void Show_ThreeDog_ShowsThreeNamedChips()
        {
            var message = WelcomeMessage.ForHousehold(new List<Dog>
            {
                Adult("Mochi", Breed.Beagle),
                Adult("Nori", Breed.Labrador),
                Puppy("Yuzu", Breed.Chihuahua),
            });

            popup.Show(message, () => { });

            Assert.That(popup.MetaLabel.text, Is.EqualTo("moved in — 3 dogs"));
            Assert.That(popup.MemberRow.activeSelf, Is.True);
            Assert.That(popup.MemberChipCount, Is.EqualTo(3));
        }

        [Test]
        public void Reshowing_ASingleAfterAMultiDog_HidesTheStaleChips()
        {
            popup.Show(WelcomeMessage.ForHousehold(new List<Dog>
            {
                Adult("Biscuit", Breed.FrenchBulldog),
                Puppy("Pepper", Breed.FrenchBulldog),
            }), () => { });
            Assert.That(popup.MemberChipCount, Is.EqualTo(2));

            popup.Show(SingleMessage(), () => { });

            Assert.That(popup.MemberRow.activeSelf, Is.False, "the row hides again for a single move-in");
            Assert.That(popup.MemberChipCount, Is.EqualTo(0), "the stale chips are cleared");
        }

        [Test]
        public void Card_GrowsVerticallyWhenTheMemberChipRowIsPresent()
        {
            popup.Show(SingleMessage(), () => { });
            var singleHeight = popup.CardRect.sizeDelta.y;

            popup.Show(WelcomeMessage.ForHousehold(new List<Dog>
            {
                Adult("Mochi", Breed.Beagle),
                Adult("Nori", Breed.Labrador),
                Puppy("Yuzu", Breed.Chihuahua),
            }), () => { });
            var multiHeight = popup.CardRect.sizeDelta.y;

            Assert.That(multiHeight, Is.GreaterThan(singleHeight),
                "the card grows to fit the member-chip row (welcome-popup.md)");
        }

        // --- Always dismissible + the one non-presentational behavior (pan) ---

        [Test]
        public void SayHi_DismissesAndPansTheCameraToTheNewHouse()
        {
            var panned = false;
            popup.Show(SingleMessage(), () => panned = true);

            popup.ActionButton.onClick.Invoke();

            Assert.That(popup.IsOpen, Is.False, "Say hi! dismisses the celebration");
            Assert.That(panned, Is.True, "Say hi! pans the camera to the new house");
        }

        [Test]
        public void TappingTheScrim_Dismisses_WithoutPanning()
        {
            var panned = false;
            popup.Show(SingleMessage(), () => panned = true);

            popup.ScrimRect.GetComponent<Button>().onClick.Invoke();

            Assert.That(popup.IsOpen, Is.False, "the scrim tap dismisses too (#329)");
            Assert.That(panned, Is.False, "the scrim tap does NOT pan the camera (welcome-popup.md Notes)");
        }

        [Test]
        public void Chrome_IsDeviceSafe_DefaultUiMaterialAndBundledFont()
        {
            popup.Show(SingleMessage(), () => { });

            Assert.That(popup.CardRect.GetComponent<Image>().material,
                Is.EqualTo(popup.CardRect.GetComponent<Image>().defaultMaterial),
                "the card assigns no custom material (#291)");
            Assert.That(popup.CardRect.GetComponent<Outline>(), Is.Not.Null,
                "the card carries the shared Candy Cottage chrome outline (#298)");
            Assert.That(popup.HeadingLabel.font, Is.Not.Null);
            Assert.That(popup.HeadingLabel.font.name, Does.Contain("DejaVu"));
        }

        private static WelcomeMessage SingleMessage()
        {
            return WelcomeMessage.ForHousehold(new List<Dog> { Adult("Waffles", Breed.FrenchBulldog) });
        }

        private static void AssertColor(Color actual, Color expected, string what)
        {
            var a = (Color32)actual;
            var e = (Color32)expected;
            Assert.That(a.r, Is.EqualTo(e.r), what + " red");
            Assert.That(a.g, Is.EqualTo(e.g), what + " green");
            Assert.That(a.b, Is.EqualTo(e.b), what + " blue");
        }
    }
}
