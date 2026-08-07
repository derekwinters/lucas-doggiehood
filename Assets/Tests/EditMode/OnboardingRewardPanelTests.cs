using System;
using Doggiehood.Core.Onboarding;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using NUnit.Framework;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #372: the standard onboarding reward/celebration panel
    /// (OnboardingRewardPanel), built to the approved wireframe
    /// (docs/specs/ui/onboarding-reward.md / mockups/onboarding-reward.html). A
    /// single reusable celebration panel — a big gold star medal overlapping the
    /// top edge, a fixed "You did it!" heading, one dynamic accomplishment line,
    /// and a single leaf pill button that IS the payout ("+100 coins") — raised
    /// each time an onboarding reward-chain step pays out. Always dismissible
    /// (button OR scrim), never a trap (#329). Copy stays out of Core: the panel
    /// reacts to the Core reward event and the Unity-side step-to-message table
    /// (OnboardingRewardCopy) supplies the accomplishment line. Chrome comes from
    /// the device-safe <see cref="CandyChromeUgui"/> (#298) and text from the
    /// bundled font (#291) — no custom shader, no editor-only builtin.
    /// </summary>
    public class OnboardingRewardPanelTests
    {
        private const string BundledFontPath = "Assets/UI/Fonts/Resources/DejaVuSans.ttf";

        private GameObject canvasHost;
        private GameObject overlayHost;
        private OnboardingRewardPanel panel;

        [SetUp]
        public void CreatePanel()
        {
            // #544: the modal-input gate is a process-global singleton; clear it
            // so a registration leaked by an earlier test can't make this
            // panel's gate read as already blocking before it shows.
            Doggiehood.Core.Cameras.ModalInputGate.Shared.Clear();

            // #291: the labels bind a bundled UI font via Resources.Load; force
            // its import so a fresh CI Library resolves it before the build.
            AssetDatabase.ImportAsset(BundledFontPath, ImportAssetOptions.ForceSynchronousImport);

            canvasHost = new GameObject("ui-canvas", typeof(Canvas));
            canvasHost.AddComponent<UiCanvas>().Configure();

            overlayHost = new GameObject("onboarding-reward-panel");
            overlayHost.transform.SetParent(canvasHost.transform, false);
            panel = overlayHost.AddComponent<OnboardingRewardPanel>();
            panel.Init();
        }

        [TearDown]
        public void Cleanup()
        {
            UnityEngine.Object.DestroyImmediate(canvasHost);
        }

        [Test]
        public void Show_RegistersWithTheSharedModalGate_Dismiss_Unregisters()
        {
            // #544: an open reward celebration blocks world taps behind its scrim.
            Assert.That(Doggiehood.Core.Cameras.ModalInputGate.Shared.IsBlocking, Is.False,
                "no modal is registered before the panel shows");

            panel.Show("You reached 3 dogs!", 100);
            Assert.That(Doggiehood.Core.Cameras.ModalInputGate.Shared.IsBlocking, Is.True,
                "an open onboarding reward panel registers with the shared modal gate");

            panel.Dismiss();
            Assert.That(Doggiehood.Core.Cameras.ModalInputGate.Shared.IsBlocking, Is.False,
                "dismissing the panel unregisters it from the shared modal gate");
        }

        [Test]
        public void ScrimTap_StillDismisses_WhileTheModalGateBlocks()
        {
            // #544 regression guard: the scrim still dismisses; only world
            // pass-through is suppressed.
            panel.Show("You reached 3 dogs!", 100);
            Assert.That(Doggiehood.Core.Cameras.ModalInputGate.Shared.IsBlocking, Is.True);

            panel.ScrimRect.GetComponent<Button>().onClick.Invoke();

            Assert.That(panel.IsOpen, Is.False,
                "a scrim tap still dismisses the panel");
            Assert.That(Doggiehood.Core.Cameras.ModalInputGate.Shared.IsBlocking, Is.False,
                "and leaves no modal registered afterwards");
        }

        // --- Layout constants come verbatim from the approved wireframe ---

        [Test]
        public void LayoutConstants_MatchTheApprovedWireframe()
        {
            Assert.That(OnboardingRewardPanel.RewardWidthPx, Is.EqualTo(820f));
            Assert.That(OnboardingRewardPanel.RewardPaddingPx, Is.EqualTo(56f));
            Assert.That(OnboardingRewardPanel.MedalDiameterPx, Is.EqualTo(176f));
            Assert.That(OnboardingRewardPanel.MedalOverlapPx, Is.EqualTo(88f));
            Assert.That(OnboardingRewardPanel.MedalOutlineThicknessPx, Is.EqualTo(8f));
            Assert.That(OnboardingRewardPanel.MedalTopGapPx, Is.EqualTo(28f));
            Assert.That(OnboardingRewardPanel.HeadingFontSizePx, Is.EqualTo(60));
            Assert.That(OnboardingRewardPanel.MessageFontSizePx, Is.EqualTo(34));
            Assert.That(OnboardingRewardPanel.HeadingMessageGapPx, Is.EqualTo(16f));
            Assert.That(OnboardingRewardPanel.MessageActionMarginPx, Is.EqualTo(44f));
            Assert.That(OnboardingRewardPanel.ActionMinWidthPx, Is.EqualTo(320f));
            Assert.That(OnboardingRewardPanel.ButtonCoinDiameterPx, Is.EqualTo(56f));
            Assert.That(OnboardingRewardPanel.ButtonCoinGapPx, Is.EqualTo(18f));
            Assert.That(OnboardingRewardPanel.ButtonHeightPx, Is.EqualTo(96f),
                "the +N coins button reuses the shared 96px PillButton (#173)");
        }

        [Test]
        public void HeadingIsTheFixedCelebratoryHeadline()
        {
            Assert.That(OnboardingRewardPanel.HeadingText, Is.EqualTo("You did it!"));
        }

        [Test]
        public void Overlay_StartsClosed()
        {
            Assert.That(panel.IsOpen, Is.False);
        }

        // --- Composition: references resolve to the built hierarchy ---

        [Test]
        public void Panel_IsCenteredAtTheWireframeWidth_OverAFullScreenScrim()
        {
            Assert.That(panel.CardRect.sizeDelta.x, Is.EqualTo(OnboardingRewardPanel.RewardWidthPx));
            Assert.That(panel.CardRect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)),
                "the card is centered over the scrim (RewardAnchor = Center)");
            Assert.That(panel.CardRect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(panel.ScrimRect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(panel.ScrimRect.anchorMax, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void Medal_IsAGoldStarDiscOfTheWireframeSize_WithItsOwnInkRing()
        {
            Assert.That(panel.MedalRect.sizeDelta.x, Is.EqualTo(OnboardingRewardPanel.MedalDiameterPx));
            Assert.That(panel.MedalRect.sizeDelta.y, Is.EqualTo(OnboardingRewardPanel.MedalDiameterPx));
            AssertColor(panel.MedalImage.color, CandyChromeUgui.Gold, "the medal is a gold disc");

            // #616: the ring is a constant-width Ink contour band inflated by its
            // own thickness, not the offset-copy Outline mesh effect.
            var ink = CandyChromeUgui.OutlineInk(panel.MedalImage.gameObject);
            Assert.That(ink, Is.Not.Null, "the medal carries its own ink ring");
            Assert.That(panel.MedalRect.offsetMin.x - ink.rectTransform.offsetMin.x,
                Is.EqualTo(OnboardingRewardPanel.MedalOutlineThicknessPx).Within(0.01f),
                "the medal ring is the wireframe's 8px, not the 6px panel outline");

            Assert.That(panel.MedalStarLabel.text, Is.EqualTo("★"), "one big ink star inside the medal");
            AssertColor(panel.MedalStarLabel.color, CandyChromeUgui.Ink, "the star is ink");
        }

        [Test]
        public void Show_SetsTheDynamicMessage_AndTheButtonNamesThePayout()
        {
            panel.Show("You finished your first quest!", 100);

            Assert.That(panel.IsOpen, Is.True);
            Assert.That(panel.HeadingLabel.text, Is.EqualTo("You did it!"),
                "the heading is fixed regardless of step");
            Assert.That(panel.MessageLabel.text, Is.EqualTo("You finished your first quest!"));
            Assert.That(panel.ActionLabel.text, Is.EqualTo("+100 coins"),
                "the single button IS the payout — its label names the coins");
        }

        [Test]
        public void Show_ReflectsTheCallerSuppliedAmount_OnTheButton()
        {
            panel.Show("You built a brand-new house!", 250);

            Assert.That(panel.ActionLabel.text, Is.EqualTo("+250 coins"));
        }

        [Test]
        public void ActionButton_IsTheLeafPill_WithAGoldCoinToken()
        {
            panel.Show("msg", 100);

            AssertColor(panel.ActionButtonImage.color, CandyChromeUgui.Leaf,
                "the reward button is the positive/leaf pill");
            Assert.That(panel.ButtonCoinRect.sizeDelta.x, Is.EqualTo(OnboardingRewardPanel.ButtonCoinDiameterPx),
                "the gold coin token is the wireframe diameter");
            Assert.That(panel.CardRect.sizeDelta.x - panel.ActionButtonRect.sizeDelta.x, Is.GreaterThan(0f));
            Assert.That(panel.ActionButtonRect.sizeDelta.x,
                Is.GreaterThanOrEqualTo(OnboardingRewardPanel.ActionMinWidthPx),
                "the pill is at least the wireframe minimum width, growing with the label");
        }

        // --- Always dismissible: button OR scrim, never a trap (#329) ---

        [Test]
        public void TappingTheButton_Dismisses()
        {
            panel.Show("msg", 100);

            panel.ActionButton.onClick.Invoke();

            Assert.That(panel.IsOpen, Is.False, "the single button dismisses the celebration");
        }

        [Test]
        public void TappingTheScrim_AlsoDismisses_SoTheCelebrationIsNeverATrap()
        {
            panel.Show("msg", 100);

            panel.ScrimRect.GetComponent<Button>().onClick.Invoke();

            Assert.That(panel.IsOpen, Is.False, "the scrim tap dismisses too (#329)");
        }

        [Test]
        public void Card_GrowsVerticallyWithLongerMessageCopy()
        {
            panel.Show("One line.", 100);
            var shortHeight = panel.CardRect.sizeDelta.y;

            panel.Show("Line 1\nLine 2\nLine 3\nLine 4\nLine 5", 100);
            var tallHeight = panel.CardRect.sizeDelta.y;

            Assert.That(tallHeight, Is.GreaterThan(shortHeight),
                "the card grows to fit a longer message (onboarding-reward.md)");
        }

        [Test]
        public void Chrome_IsDeviceSafe_DefaultUiMaterialAndBundledFont()
        {
            panel.Show("msg", 100);

            // #291: chrome renders through the always-included UI/Default material
            // (no custom shader to strip) and text uses the bundled font, never an
            // editor-only builtin.
            Assert.That(panel.CardRect.GetComponent<Image>().material,
                Is.EqualTo(panel.CardRect.GetComponent<Image>().defaultMaterial),
                "the card assigns no custom material (#291)");
            Assert.That(CandyChromeUgui.OutlineInk(panel.CardRect.gameObject), Is.Not.Null,
                "the card carries the shared Candy Cottage chrome outline (#298, #616 contour band)");

            Assert.That(panel.HeadingLabel.font, Is.Not.Null);
            Assert.That(panel.HeadingLabel.font.name, Does.Contain("DejaVu"));
            Assert.That(panel.HeadingLabel.font.name, Does.Not.Contain("Arial"));
        }

        // --- Step-to-message copy table (Unity layer; exact approved lines) ---

        [Test]
        public void CopyTable_MapsEachStepToItsApprovedAccomplishmentLine()
        {
            Assert.That(OnboardingRewardCopy.MessageFor(OnboardingRewardStep.FirstQuest),
                Is.EqualTo("You finished your first quest!"));
            Assert.That(OnboardingRewardCopy.MessageFor(OnboardingRewardStep.UpgradeHouse),
                Is.EqualTo("You made a house even nicer!"));
            Assert.That(OnboardingRewardCopy.MessageFor(OnboardingRewardStep.ExpandMap),
                Is.EqualTo("You opened up a brand-new street!"));
            Assert.That(OnboardingRewardCopy.MessageFor(OnboardingRewardStep.BuildHouse),
                Is.EqualTo("You built a brand-new house!"));
        }

        [Test]
        public void CopyTable_HasNoMessageForTheTerminalDoneStep_AndFailsLoudly()
        {
            // Done is never a completed reward step — asking for its copy is a
            // programming error, surfaced immediately rather than as blank text.
            Assert.That(() => OnboardingRewardCopy.MessageFor(OnboardingRewardStep.Done),
                Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        // #541: the OnboardingRewardDirector no longer drives this panel — the
        // reward-chain step feedback moved onto the non-modal toast
        // (CompletionToastDirectorTests). This panel is retained for history
        // (onboarding-reward.md) with no live consumer; the layout/dismiss/copy
        // tests above still validate the retired design.

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
