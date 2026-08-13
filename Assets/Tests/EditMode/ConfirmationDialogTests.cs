using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using NUnit.Framework;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #343/#344: the reusable confirmation dialog, built to the approved
    /// wireframe (docs/specs/ui/confirmation-dialog.md /
    /// mockups/confirmation-dialog.html). A compact centered card over a dim
    /// scrim with a DYNAMIC title + body (caller-supplied), and an action row
    /// of two equal-width pill buttons — No (cream, left) and Yes (leaf,
    /// right). Tapping the scrim or No cancels (never a trap, a deliberate
    /// contrast with #329); Yes runs the caller's confirm callback. When the
    /// action costs coins, Yes carries a coin token + amount. Chrome comes from
    /// the device-safe <see cref="CandyChromeUgui"/> (#298) and text from the
    /// bundled font (#291) — no custom shader, no editor-only builtin.
    /// </summary>
    public class ConfirmationDialogTests
    {
        private const string BundledFontPath = "Assets/UI/Fonts/Resources/DejaVuSans.ttf";

        private GameObject canvasHost;
        private GameObject overlayHost;
        private ConfirmationDialog dialog;

        [SetUp]
        public void CreateDialog()
        {
            // #544: the modal-input gate is a process-global singleton; clear it
            // so a registration leaked by an earlier test can't make this
            // dialog's gate read as already blocking before it opens.
            Doggiehood.Core.Cameras.ModalInputGate.Shared.Clear();

            // #291: the labels bind a bundled UI font via Resources.Load; force
            // its import so a fresh CI Library resolves it before the build.
            AssetDatabase.ImportAsset(BundledFontPath, ImportAssetOptions.ForceSynchronousImport);

            canvasHost = new GameObject("ui-canvas", typeof(Canvas));
            canvasHost.AddComponent<UiCanvas>().Configure();

            overlayHost = new GameObject("confirmation-dialog");
            overlayHost.transform.SetParent(canvasHost.transform, false);
            dialog = overlayHost.AddComponent<ConfirmationDialog>();
            dialog.Init();
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(canvasHost);
        }

        [Test]
        public void Open_RegistersWithTheSharedModalGate_Close_Unregisters()
        {
            // #544: while open, the dialog blocks world taps behind its scrim;
            // once closed (via No/scrim → Cancel), it stops blocking.
            Assert.That(Doggiehood.Core.Cameras.ModalInputGate.Shared.IsBlocking, Is.False,
                "no modal is registered before the dialog opens");

            dialog.Open("Title", "Body", () => { });
            Assert.That(Doggiehood.Core.Cameras.ModalInputGate.Shared.IsBlocking, Is.True,
                "an open confirmation dialog registers with the shared modal gate");

            dialog.Cancel();
            Assert.That(Doggiehood.Core.Cameras.ModalInputGate.Shared.IsBlocking, Is.False,
                "cancelling the dialog unregisters it from the shared modal gate");
        }

        [Test]
        public void ScrimTap_StillDismisses_WhileTheModalGateBlocks()
        {
            // #544 regression guard: blocking world pass-through must NOT break
            // the intended scrim-dismiss — the scrim's own Button.onClick still
            // runs through UGUI. Tapping the scrim cancels and leaves no modal
            // registered.
            dialog.Open("Title", "Body", () => { });
            Assert.That(Doggiehood.Core.Cameras.ModalInputGate.Shared.IsBlocking, Is.True);

            dialog.ScrimRect.GetComponent<Button>().onClick.Invoke();

            Assert.That(dialog.IsOpen, Is.False,
                "a scrim tap still dismisses the dialog");
            Assert.That(Doggiehood.Core.Cameras.ModalInputGate.Shared.IsBlocking, Is.False,
                "and leaves no modal registered afterwards");
        }

        [Test]
        public void LayoutConstants_MatchTheApprovedWireframe()
        {
            Assert.That(ConfirmationDialog.DialogWidthPx, Is.EqualTo(760f));
            Assert.That(ConfirmationDialog.DialogPaddingPx, Is.EqualTo(48f));
            Assert.That(ConfirmationDialog.TitleFontSizePx, Is.EqualTo(44));
            Assert.That(ConfirmationDialog.BodyFontSizePx, Is.EqualTo(32));
            Assert.That(ConfirmationDialog.TitleBodyGapPx, Is.EqualTo(20f));
            Assert.That(ConfirmationDialog.ActionRowMarginPx, Is.EqualTo(40f));
            Assert.That(ConfirmationDialog.ActionGapPx, Is.EqualTo(20f));
            Assert.That(ConfirmationDialog.CostCoinDiameterPx, Is.EqualTo(40f));
            Assert.That(ConfirmationDialog.CostGapPx, Is.EqualTo(8f));
            Assert.That(ConfirmationDialog.ButtonHeightPx, Is.EqualTo(96f),
                "the No/Yes buttons reuse the shared 96px PillButton (#173)");
        }

        [Test]
        public void Overlay_StartsClosed()
        {
            Assert.That(dialog.IsOpen, Is.False);
        }

        [Test]
        public void Card_IsCenteredAtTheWireframeWidth_OverAFullScreenScrim()
        {
            Assert.That(dialog.CardRect.sizeDelta.x, Is.EqualTo(ConfirmationDialog.DialogWidthPx));
            Assert.That(dialog.CardRect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)),
                "the card is centered over the scrim (DialogAnchor = Center)");
            Assert.That(dialog.CardRect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(dialog.ScrimRect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(dialog.ScrimRect.anchorMax, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void Open_SetsTheDynamicTitleAndBody_AndShowsTheDialog()
        {
            dialog.Open("Unlock this area?", "Open up the next zone.", () => { });

            Assert.That(dialog.IsOpen, Is.True);
            Assert.That(dialog.TitleLabel.text, Is.EqualTo("Unlock this area?"));
            Assert.That(dialog.BodyLabel.text, Is.EqualTo("Open up the next zone."));
        }

        [Test]
        public void Buttons_DefaultToLiteralYesAndNo_WithYesTintedLeaf()
        {
            dialog.Open("Q?", "body", () => { });

            Assert.That(dialog.YesLabel.text, Is.EqualTo("Yes"));
            Assert.That(dialog.NoLabel.text, Is.EqualTo("No"));
            AssertColor(dialog.YesButtonImage.color, CandyChromeUgui.Leaf, "Yes is the positive/leaf confirm");
            AssertColor(dialog.NoButtonImage.color, CandyChromeUgui.Cream, "No is the neutral/cream decline");
        }

        [Test]
        public void TappingYes_RunsTheConfirmCallbackOnce_ThenCloses()
        {
            var confirmed = 0;
            dialog.Open("Q?", "body", () => confirmed++);

            dialog.YesButton.onClick.Invoke();

            Assert.That(confirmed, Is.EqualTo(1), "Yes runs the caller's confirm callback exactly once");
            Assert.That(dialog.IsOpen, Is.False, "the dialog closes after confirming");
        }

        [Test]
        public void TappingNo_Dismisses_WithoutRunningConfirm()
        {
            var confirmed = 0;
            dialog.Open("Q?", "body", () => confirmed++);

            dialog.NoButton.onClick.Invoke();

            Assert.That(confirmed, Is.EqualTo(0), "No never runs the confirm action");
            Assert.That(dialog.IsOpen, Is.False);
        }

        [Test]
        public void TappingTheScrim_Cancels_LikeNo_SoTheDialogIsNeverATrap()
        {
            var confirmed = 0;
            dialog.Open("Q?", "body", () => confirmed++);

            dialog.ScrimRect.GetComponent<Button>().onClick.Invoke();

            Assert.That(confirmed, Is.EqualTo(0), "the scrim tap cancels (= No), never confirms (#329)");
            Assert.That(dialog.IsOpen, Is.False);
        }

        [Test]
        public void CostFreeConfirmation_ShowsJustYes_WithNoCoin()
        {
            dialog.Open("Q?", "body", () => { });

            Assert.That(dialog.CostGroup.activeSelf, Is.False,
                "a cost-free confirmation shows no coin token");
        }

        [Test]
        public void SpendConfirmation_ShowsTheCoinTokenAndAmountOnYes()
        {
            dialog.Open("Unlock this area?", "Open up the next zone.", () => { }, cost: 100);

            Assert.That(dialog.CostGroup.activeSelf, Is.True, "a spend shows the coin token + amount");
            Assert.That(dialog.CostAmountLabel.text, Is.EqualTo("100"),
                "the caller-supplied cost is shown on the Yes button");
            Assert.That(dialog.CostCoinRect.sizeDelta.x, Is.EqualTo(ConfirmationDialog.CostCoinDiameterPx));
        }

        [Test]
        public void Reopening_WithoutACost_HidesACoinLeftFromAPriorSpend()
        {
            dialog.Open("Q?", "body", () => { }, cost: 100);
            dialog.Open("Q2?", "body2", () => { });

            Assert.That(dialog.CostGroup.activeSelf, Is.False,
                "reopening cost-free must not carry over the previous coin");
        }

        [Test]
        public void ConfirmTintAndLabels_AreOverridable_ForReuse()
        {
            dialog.Open("Q?", "body", () => { }, cost: null,
                yesLabel: "Okay", noLabel: "Nope", confirmTint: CandyChromeUgui.Coral);

            Assert.That(dialog.YesLabel.text, Is.EqualTo("Okay"));
            Assert.That(dialog.NoLabel.text, Is.EqualTo("Nope"));
            AssertColor(dialog.YesButtonImage.color, CandyChromeUgui.Coral, "an override tints the confirm");
        }

        [Test]
        public void Open_DefaultsToAnEnabledConfirm_SoExistingCallersAreUnchanged()
        {
            // #690: the new confirmEnabled argument defaults to true, so the
            // tile-unlock caller (ExpansionUnlockDirector) keeps today's
            // behavior without passing anything.
            dialog.Open("Unlock this area?", "Open up the next zone.", () => { }, cost: 100);

            Assert.That(dialog.YesButton.interactable, Is.True,
                "a confirmation with no explicit enabled state stays pressable");
            AssertColor(dialog.YesButtonImage.color, CandyChromeUgui.Leaf,
                "and keeps the leaf confirm tint");
        }

        [Test]
        public void OpeningWithTheConfirmDisabled_GreysYes_AndMakesItNonInteractable()
        {
            // #690: a spend the wallet can't cover is presented as a greyed-out,
            // unpressable Yes — never a normal-looking button that does nothing.
            dialog.Open("Build a house here?", "Spend coins to build a house on this lot.",
                () => { }, cost: 50, confirmEnabled: false);

            Assert.That(dialog.IsOpen, Is.True, "the dialog still opens so the price is visible");
            Assert.That(dialog.YesButton.interactable, Is.False, "a disabled Yes is not pressable");
            AssertColor(dialog.YesButtonImage.color, CandyChromeUgui.Disabled,
                "a disabled Yes takes the shared Disabled role tint");
        }

        [Test]
        public void ADisabledConfirm_StillShowsTheCostTokenAndAmount()
        {
            // #690: the point of still opening the dialog is that the player sees
            // the price they are short of.
            dialog.Open("Build a house here?", "body", () => { }, cost: 50, confirmEnabled: false);

            Assert.That(dialog.CostGroup.activeSelf, Is.True,
                "the cost token stays on a disabled Yes");
            Assert.That(dialog.CostAmountLabel.text, Is.EqualTo("50"));
        }

        [Test]
        public void ADisabledConfirm_IsStillDismissableByNoAndTheScrim()
        {
            // #329 guard: greying the confirm must never grey the way out.
            dialog.Open("Build a house here?", "body", () => { }, cost: 50, confirmEnabled: false);
            dialog.NoButton.onClick.Invoke();
            Assert.That(dialog.IsOpen, Is.False, "No dismisses an unaffordable prompt");

            dialog.Open("Build a house here?", "body", () => { }, cost: 50, confirmEnabled: false);
            Assert.That(dialog.NoButton.interactable, Is.True, "No never greys out");
            dialog.ScrimRect.GetComponent<Button>().onClick.Invoke();
            Assert.That(dialog.IsOpen, Is.False, "the scrim dismisses an unaffordable prompt");
        }

        [Test]
        public void ReopeningEnabled_AfterADisabledOpen_RestoresALiveLeafYes()
        {
            // The one reused overlay must not carry a previous open's disabled
            // state into the next caller's prompt.
            dialog.Open("Q?", "body", () => { }, cost: 50, confirmEnabled: false);
            dialog.Open("Q2?", "body2", () => { }, cost: 50);

            Assert.That(dialog.YesButton.interactable, Is.True);
            AssertColor(dialog.YesButtonImage.color, CandyChromeUgui.Leaf,
                "an affordable reopen is leaf-tinted again");
        }

        [Test]
        public void ADisabledConfirmTint_WinsOverACallerSuppliedConfirmTint()
        {
            // Disabled is a state, not a role: it overrides the override.
            dialog.Open("Q?", "body", () => { }, cost: 50,
                confirmTint: CandyChromeUgui.Coral, confirmEnabled: false);

            AssertColor(dialog.YesButtonImage.color, CandyChromeUgui.Disabled,
                "a disabled Yes greys out even when the caller asked for another tint");
        }

        [Test]
        public void Card_GrowsVerticallyWithLongerBodyCopy()
        {
            dialog.Open("Q?", "One line.", () => { });
            var shortHeight = dialog.CardRect.sizeDelta.y;

            dialog.Open("Q?", "Line 1\nLine 2\nLine 3\nLine 4\nLine 5", () => { });
            var tallHeight = dialog.CardRect.sizeDelta.y;

            Assert.That(tallHeight, Is.GreaterThan(shortHeight),
                "the card grows to fit a longer body (confirmation-dialog.md)");
        }

        [Test]
        public void Chrome_IsDeviceSafe_DefaultUiMaterialAndBundledFont()
        {
            dialog.Open("Q?", "body", () => { }, cost: 100);

            // #291: chrome renders through the always-included UI/Default
            // material (no custom shader to strip) and text uses the bundled
            // font, never an editor-only builtin.
            Assert.That(dialog.CardRect.GetComponent<Image>().material,
                Is.EqualTo(dialog.CardRect.GetComponent<Image>().defaultMaterial),
                "the card assigns no custom material (#291)");
            Assert.That(CandyChromeUgui.OutlineInk(dialog.CardRect.gameObject), Is.Not.Null,
                "the card carries the shared Candy Cottage chrome outline (#298, #616 contour band)");

            Assert.That(dialog.TitleLabel.font, Is.Not.Null);
            Assert.That(dialog.TitleLabel.font.name, Does.Contain("DejaVu"));
            Assert.That(dialog.TitleLabel.font.name, Does.Not.Contain("Arial"));
        }

        [Test]
        public void OutlineBands_FollowTheirFills_ThroughTheOpenTimeLayout()
        {
            // #663: BuildActionRow chromes both buttons and only sets their
            // sizeDelta at the END of the method, with their positions landing
            // later still in LayoutCard — and the card's own height is set after
            // its chrome too. A band that snapshots its fill's rect at apply time
            // is stranded by all three.
            dialog.Open("Sell the bone?", "A body long enough to wrap onto more than one line.", () => { });

            // EditMode runs no frame loop, so drive the bands' per-frame sync.
            OutlineBandFollower.SyncAll(canvasHost);

            AssertBandSurroundsFill(dialog.CardRect.gameObject);
            AssertBandSurroundsFill(dialog.NoButtonImage.gameObject);
            AssertBandSurroundsFill(dialog.YesButtonImage.gameObject);
        }

        private static void AssertBandSurroundsFill(GameObject fill)
        {
            var ink = CandyChromeUgui.OutlineInk(fill);
            Assert.That(ink, Is.Not.Null, fill.name + " has no Ink contour band");

            var fillRect = (RectTransform)fill.transform;
            var bandRect = ink.rectTransform;
            var w = CandyChromeUgui.OutlineThicknessPx;
            Assert.That(bandRect.anchorMin, Is.EqualTo(fillRect.anchorMin), fill.name + " band anchorMin");
            Assert.That(bandRect.anchorMax, Is.EqualTo(fillRect.anchorMax), fill.name + " band anchorMax");
            Assert.That(bandRect.pivot, Is.EqualTo(fillRect.pivot), fill.name + " band pivot");
            Assert.That(fillRect.offsetMin.x - bandRect.offsetMin.x, Is.EqualTo(w).Within(0.01f),
                fill.name + " band left edge");
            Assert.That(fillRect.offsetMin.y - bandRect.offsetMin.y, Is.EqualTo(w).Within(0.01f),
                fill.name + " band bottom edge");
            Assert.That(bandRect.offsetMax.x - fillRect.offsetMax.x, Is.EqualTo(w).Within(0.01f),
                fill.name + " band right edge");
            Assert.That(bandRect.offsetMax.y - fillRect.offsetMax.y, Is.EqualTo(w).Within(0.01f),
                fill.name + " band top edge");
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
