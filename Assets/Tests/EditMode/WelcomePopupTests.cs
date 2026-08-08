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
    /// dismisses AND pans the camera to the new house. Always dismissible — and
    /// since #671 visibly so, via a top-right ✕ alongside the scrim — never a
    /// trap; both the ✕ and the scrim dismiss WITHOUT panning. The dynamic copy
    /// comes from engine-free Core (<see cref="WelcomeMessage"/>).
    /// </summary>
    public class WelcomePopupTests
    {
        private const string BundledFontPath = "Assets/UI/Fonts/Resources/DejaVuSans.ttf";
        private const int HouseId = 3;

        /// <summary>#671 / welcome-popup.md: the centered medal spans x 872–1048
        /// and the ✕ spans x 1298–1370 within the 820px card, so they clear each
        /// other by 250px horizontally — constant across every household
        /// variant, because the medal is centered regardless of content.</summary>
        private const float CloseToPortraitClearancePx = 250f;

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
            Assert.That(WelcomePopup.CloseButtonSizePx, Is.EqualTo(72f),
                "#671: the close (✕) matches the dog/house profile at 72px");
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

            // #616: the ring is a constant-width Ink contour band inflated by its
            // own thickness, not the offset-copy Outline mesh effect.
            var ink = CandyChromeUgui.OutlineInk(popup.PortraitImage.gameObject);
            Assert.That(ink, Is.Not.Null, "the portrait carries its own ink ring");
            Assert.That(popup.PortraitRect.offsetMin.x - ink.rectTransform.offsetMin.x,
                Is.EqualTo(WelcomePopup.PortraitOutlineThicknessPx).Within(0.01f),
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

        // --- #671: the top-right ✕ — the visible way out ---

        [Test]
        public void CloseButton_IsFlushInTheCardsTopRightCorner_AtTheWireframeSize()
        {
            // welcome-popup.md "Close" region: flush in the card's corner, zero
            // inset, matching DogProfileOverlay/HouseProfileOverlay exactly.
            Assert.That(popup.CloseButtonRect, Is.Not.Null, "the pop-up renders a close (✕) button");
            Assert.That(popup.CloseButtonRect.sizeDelta,
                Is.EqualTo(new Vector2(WelcomePopup.CloseButtonSizePx, WelcomePopup.CloseButtonSizePx)));
            Assert.That(popup.CloseButtonRect.anchorMin, Is.EqualTo(Vector2.one), "anchored top-right");
            Assert.That(popup.CloseButtonRect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(popup.CloseButtonRect.pivot, Is.EqualTo(Vector2.one), "pivoted top-right");
            Assert.That(popup.CloseButtonRect.anchoredPosition, Is.EqualTo(Vector2.zero),
                "flush in the corner — zero inset (welcome-popup.md)");
            Assert.That(popup.CloseButtonRect.parent, Is.EqualTo(popup.CardRect),
                "the ✕ lives on the card, so it travels with it");
        }

        [Test]
        public void TappingTheClose_Dismisses_WithoutPanningOrOpeningTheHouseProfile()
        {
            // The whole point of #671: the ✕ routes to the EXISTING Dismiss(),
            // never through SayHi(). Asserting on the callback (not just that the
            // panel closed) is what stops a future refactor re-routing it.
            var sayHiInvoked = false;
            popup.Show(SingleMessage(), () => sayHiInvoked = true);

            popup.CloseButton.onClick.Invoke();

            Assert.That(popup.IsOpen, Is.False, "the ✕ dismisses the celebration");
            Assert.That(sayHiInvoked, Is.False,
                "the ✕ does NOT pan the camera and does NOT open the house profile (#604 is Say hi!'s job)");
        }

        [Test]
        public void CloseTap_UnregistersFromTheModalGate_AndStaysLatchedForTheRestOfTheFrame()
        {
            // #568: Dismiss() unregisters, which latches ClosedThisFrame, so the
            // very tap that closed the pop-up cannot also fire the world object
            // behind it — InputAuthority blocks on IsBlocking || ClosedThisFrame.
            popup.Show(SingleMessage(), () => { });
            Assert.That(Doggiehood.Core.Cameras.ModalInputGate.Shared.IsBlocking, Is.True);

            popup.CloseButton.onClick.Invoke();

            Assert.That(Doggiehood.Core.Cameras.ModalInputGate.Shared.IsBlocking, Is.False,
                "the ✕ unregisters the pop-up from the shared modal gate");
            Assert.That(Doggiehood.Core.Cameras.ModalInputGate.Shared.ClosedThisFrame, Is.True,
                "and the closing tap stays consumed for the rest of the frame (#568), "
                + "so it does not leak to the world behind the pop-up");

            Doggiehood.Core.Cameras.ModalInputGate.Shared.EndFrame();
            Assert.That(Doggiehood.Core.Cameras.ModalInputGate.Shared.ClosedThisFrame, Is.False,
                "and the latch is clear again before the next frame's unrelated tap");
        }

        [Test]
        public void CloseButton_ClearsThePortraitMedalAndMemberRow_InEveryHouseholdVariant()
        {
            // welcome-popup.md: the medal is centered regardless of content, so
            // the ✕ clears it by 250px in all three variants. Enforced, not
            // assumed — and the member-chip row is centered too, so it clears as
            // well however many chips it carries.
            AssertCloseButtonClearsTheCardContents(SingleMessage(), "single");

            AssertCloseButtonClearsTheCardContents(WelcomeMessage.ForHousehold(new List<Dog>
            {
                Adult("Biscuit", Breed.FrenchBulldog),
                Puppy("Pepper", Breed.FrenchBulldog),
            }), "parent+puppy");

            AssertCloseButtonClearsTheCardContents(WelcomeMessage.ForHousehold(new List<Dog>
            {
                Adult("Mochi", Breed.Beagle),
                Adult("Nori", Breed.Labrador),
                Puppy("Yuzu", Breed.Chihuahua),
            }), "three-dog");
        }

        private void AssertCloseButtonClearsTheCardContents(WelcomeMessage message, string variant)
        {
            popup.Show(message, () => { });

            var close = CardLocalRect(popup.CloseButtonRect, popup.CardRect);
            var portrait = CardLocalRect(popup.PortraitRect, popup.CardRect);

            Assert.That(close.Overlaps(portrait), Is.False,
                variant + ": the ✕ must not overlap the portrait medal");
            Assert.That(close.xMin - portrait.xMax, Is.EqualTo(CloseToPortraitClearancePx).Within(0.01f),
                variant + ": the ✕ clears the centered medal by the wireframe's 250px");

            if (popup.MemberRow.activeSelf)
            {
                var memberRow = (RectTransform)popup.MemberRow.transform;
                Assert.That(close.Overlaps(CardLocalRect(memberRow, popup.CardRect)), Is.False,
                    variant + ": the ✕ must not overlap the member-chip row");
            }
        }

        /// <summary>The child's rect in the card's own coordinate space (origin
        /// bottom-left), so overlap can be checked without a canvas layout pass.
        /// Point-anchored children only, which is every element on this card.</summary>
        private static Rect CardLocalRect(RectTransform child, RectTransform card)
        {
            Assert.That(child.anchorMin, Is.EqualTo(child.anchorMax),
                child.name + " is stretch-anchored; this helper assumes point anchors");

            var parentSize = card.rect.size;
            var anchor = new Vector2(child.anchorMin.x * parentSize.x, child.anchorMin.y * parentSize.y);
            var size = child.sizeDelta;
            var bottomLeft = anchor + child.anchoredPosition
                - new Vector2(child.pivot.x * size.x, child.pivot.y * size.y);
            return new Rect(bottomLeft, size);
        }

        [Test]
        public void Chrome_IsDeviceSafe_DefaultUiMaterialAndBundledFont()
        {
            popup.Show(SingleMessage(), () => { });

            Assert.That(popup.CardRect.GetComponent<Image>().material,
                Is.EqualTo(popup.CardRect.GetComponent<Image>().defaultMaterial),
                "the card assigns no custom material (#291)");
            Assert.That(CandyChromeUgui.OutlineInk(popup.CardRect.gameObject), Is.Not.Null,
                "the card carries the shared Candy Cottage chrome outline (#298, #616 contour band)");
            Assert.That(popup.HeadingLabel.font, Is.Not.Null);
            Assert.That(popup.HeadingLabel.font.name, Does.Contain("DejaVu"));
        }

        [Test]
        public void OutlineBands_FollowTheirFills_ThroughTheShowTimeLayout()
        {
            // #663: BuildActionButton chromes the "Say hi!" pill before
            // LayoutActionButton places it, and the card's height is set at the
            // end of LayoutCard — long after its own chrome. Each band has to
            // track its fill rather than snapshot it. The portrait is included
            // because its ring is a CUSTOM thickness, which tracking must keep.
            popup.Show(SingleMessage(), () => { });

            // EditMode runs no frame loop, so drive the bands' per-frame sync.
            OutlineBandFollower.SyncAll(canvasHost);

            AssertBandSurroundsFill(popup.CardRect.gameObject, CandyChromeUgui.OutlineThicknessPx);
            AssertBandSurroundsFill(popup.ActionButtonImage.gameObject, CandyChromeUgui.OutlineThicknessPx);
            AssertBandSurroundsFill(popup.PortraitImage.gameObject, WelcomePopup.PortraitOutlineThicknessPx);
        }

        private static void AssertBandSurroundsFill(GameObject fill, float thicknessPx)
        {
            var ink = CandyChromeUgui.OutlineInk(fill);
            Assert.That(ink, Is.Not.Null, fill.name + " has no Ink contour band");

            var fillRect = (RectTransform)fill.transform;
            var bandRect = ink.rectTransform;
            Assert.That(bandRect.anchorMin, Is.EqualTo(fillRect.anchorMin), fill.name + " band anchorMin");
            Assert.That(bandRect.anchorMax, Is.EqualTo(fillRect.anchorMax), fill.name + " band anchorMax");
            Assert.That(bandRect.pivot, Is.EqualTo(fillRect.pivot), fill.name + " band pivot");
            Assert.That(fillRect.offsetMin.x - bandRect.offsetMin.x, Is.EqualTo(thicknessPx).Within(0.01f),
                fill.name + " band left edge");
            Assert.That(fillRect.offsetMin.y - bandRect.offsetMin.y, Is.EqualTo(thicknessPx).Within(0.01f),
                fill.name + " band bottom edge");
            Assert.That(bandRect.offsetMax.x - fillRect.offsetMax.x, Is.EqualTo(thicknessPx).Within(0.01f),
                fill.name + " band right edge");
            Assert.That(bandRect.offsetMax.y - fillRect.offsetMax.y, Is.EqualTo(thicknessPx).Within(0.01f),
                fill.name + " band top edge");
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
