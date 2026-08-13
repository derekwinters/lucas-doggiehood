using System.Linq;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #408: the conversation/quest dialog is restyled from graybox IMGUI to the
    /// shared Candy Cottage UGUI chrome, implementing the approved wireframe
    /// (docs/specs/ui/conversation-panel.md / mockups/conversation-panel.html,
    /// #175). A bottom-center <c>DialogueBox</c> shell (name-tag tab + body +
    /// right-aligned <c>PillButton</c> action row) built under the #256
    /// <see cref="UiCanvas"/> from the device-safe <see cref="CandyChromeUgui"/>
    /// (#291) — no custom shader, bundled font only. This is a rendering swap:
    /// behavior (accept/complete, decoration options #50, "Not now" decline
    /// #185, buy cost + stay-open-on-insufficient-funds #186) is unchanged and
    /// still guarded by <see cref="ConversationPresenterTests"/>; these tests
    /// pin the new UGUI layout and that each behavior is now reachable through
    /// the real pill buttons.
    /// </summary>
    public class ConversationPanelLayoutTests
    {
        private const string BundledFontPath = "Assets/UI/Fonts/Resources/DejaVuSans.ttf";

        private GameState state;
        private GameObject canvasHost;
        private GameObject presenterHost;
        private ConversationPresenter presenter;

        [SetUp]
        public void CreatePanel()
        {
            // #291: the labels bind a bundled UI font via Resources.Load; force
            // its import so a fresh CI Library resolves it before the build.
            AssetDatabase.ImportAsset(BundledFontPath, ImportAssetOptions.ForceSynchronousImport);

            state = GameState.CreateNew();

            canvasHost = new GameObject("ui-canvas", typeof(Canvas));
            canvasHost.AddComponent<UiCanvas>().Configure();

            presenterHost = new GameObject("conversation-presenter");
            presenterHost.transform.SetParent(canvasHost.transform, false);
            presenter = presenterHost.AddComponent<ConversationPresenter>();
            presenter.State = state;
            presenter.Init();
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(canvasHost);
        }

        [Test]
        public void LayoutConstants_MatchTheApprovedWireframe()
        {
            Assert.That(ConversationPresenter.PanelWidthPx, Is.EqualTo(1040f));
            Assert.That(ConversationPresenter.BodyFontPx, Is.EqualTo(34));
            Assert.That(ConversationPresenter.PaddingPx, Is.EqualTo(40f));
            Assert.That(ConversationPresenter.PanelRadiusPx, Is.EqualTo(40f));
            Assert.That(ConversationPresenter.NameTagOffsetPx, Is.EqualTo(28f));
            Assert.That(ConversationPresenter.ActionGapPx, Is.EqualTo(20f));
            Assert.That(ConversationPresenter.PanelBottomMarginPx, Is.EqualTo(64f));
            Assert.That(ConversationPresenter.ButtonHeightPx, Is.EqualTo(96f),
                "the action row reuses the shared 96px PillButton (#173)");
        }

        [Test]
        public void Panel_StartsClosed()
        {
            Assert.That(presenter.IsOpen, Is.False);
            Assert.That(presenter.PanelRect.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Panel_IsBottomCenteredAtTheWireframeWidth()
        {
            Assert.That(presenter.PanelRect.sizeDelta.x, Is.EqualTo(ConversationPresenter.PanelWidthPx));
            Assert.That(presenter.PanelRect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0f)),
                "the panel sits bottom-center (PanelAnchor = BottomCenter)");
            Assert.That(presenter.PanelRect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0f)));
            Assert.That(presenter.PanelRect.anchoredPosition.y,
                Is.EqualTo(ConversationPresenter.PanelBottomMarginPx),
                "the panel floats PanelBottomMarginPx above the screen bottom");
        }

        [Test]
        public void Open_PopulatesTheNameTagAndBody_AndActivatesThePanel()
        {
            var dog = state.Dogs.First();
            var quest = state.Quests.GiveQuestTo(dog, QuestType.LostItem, new System.Random(1));

            Assert.That(presenter.TryOpen(dog), Is.True);

            Assert.That(presenter.IsOpen, Is.True);
            Assert.That(presenter.PanelRect.gameObject.activeSelf, Is.True);
            Assert.That(presenter.NameTagLabel.text, Is.EqualTo(dog.Name),
                "the name tag shows the talking dog's name");
            Assert.That(presenter.BodyLabel.text, Is.EqualTo(string.Join("\n", quest.DialogueLines)),
                "the body renders the linear request lines from Core");
        }

        [Test]
        public void StandardQuest_ShowsOneAcceptPill_PlusNotNow()
        {
            var dog = state.Dogs.First();
            state.Quests.GiveQuestTo(dog, QuestType.LostItem, new System.Random(1));
            presenter.TryOpen(dog);

            Assert.That(presenter.AcceptPills.Count, Is.EqualTo(1), "a standard quest has one accept affordance");
            Assert.That(presenter.AcceptPills[0].Label.text, Is.EqualTo(presenter.AcceptLabel));
            Assert.That(presenter.DeclinePill.Label.text, Is.EqualTo("Not now"),
                "the decline (#185) is always present");
        }

        [Test]
        public void ActiveQuestReminder_ForALostItem_ShowsExactlyOneStillLookingPill_AndNoAcceptPill()
        {
            // #472: re-tapping a dog whose quest is Accepted shows a dismiss-only
            // reminder — the single pill is the "Not now" close relabeled, with
            // no accept/complete affordance. #708: for a lost item the player
            // really does still owe the next action, so it stays "Still looking".
            var dog = state.Dogs.First();
            var quest = state.Quests.GiveQuestTo(dog, QuestType.LostItem, new System.Random(1));
            state.Quests.Accept(quest);
            presenter.TryOpen(dog);

            Assert.That(presenter.AcceptPills, Is.Empty, "a reminder offers no accept/complete pill");
            Assert.That(presenter.DeclinePill, Is.Not.Null, "the reminder still has its dismiss pill");
            Assert.That(presenter.DeclinePill.Label.text, Is.EqualTo("Still looking"),
                "a quest the player still owes work on keeps the 'Still looking' dismiss (#472)");
            Assert.That(presenter.DeclinePill.Label.text,
                Is.EqualTo(QuestTemplates.For(quest.Type, quest.ItemName).ReminderDismissLabel),
                "the label comes from Core's template, never a switch in the presenter (#708)");
        }

        [Test]
        public void ActiveQuestReminder_ForAnAcceptedGift_ShowsAnOnItsWayPill_AndNoAcceptPill()
        {
            // #708: accepting a buy-gift quest IS the purchase — the coins are
            // spent and the truck is dispatched — so re-tapping must acknowledge
            // the delivery instead of asking the player to keep looking. The
            // label is read off the same Core template the reminder line comes
            // from, so the line and the pill can never disagree.
            var dog = state.Dogs.First();
            var quest = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new System.Random(1));
            Assert.That(quest.ItemName, Is.Not.EqualTo(ItemCatalog.FenceItemName),
                "sanity: the fence completes at accept and has no reminder window (#318)");
            state.Wallet.Deposit(quest.Cost.Value);
            Assert.That(state.Quests.Accept(quest), Is.True, "sanity: the gift quest accepts");
            presenter.TryOpen(dog);

            Assert.That(presenter.AcceptPills, Is.Empty, "a reminder offers no accept/complete pill");
            Assert.That(presenter.DeclinePill.Label.text, Is.EqualTo("On its way"),
                "a paid-for gift's dismiss acknowledges the delivery, not a search (#708)");
            Assert.That(presenter.DeclinePill.Label.text,
                Is.EqualTo(QuestTemplates.For(quest.Type, quest.ItemName).ReminderDismissLabel),
                "the label comes from Core's template, never a switch in the presenter (#708)");
            Assert.That(presenter.BodyLabel.text.ToLowerInvariant(), Does.Not.Contain("any luck"),
                "the reminder line must not ask for a purchase the player already made");
        }

        [Test]
        public void ActiveQuestReminder_ForAnAcceptedDecorationChoice_ShowsAnOnItsWayPill()
        {
            // #708: the decoration request is paid at accept too, with the item
            // the player chose — the reminder acknowledges that choice is coming.
            var dog = state.Dogs[2];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.DecorationRequest, new System.Random(7));
            var chosen = quest.Options.First();
            state.Wallet.Deposit(ItemCatalog.Get(chosen).Cost.Value);
            Assert.That(state.Quests.AcceptWithChoice(quest, chosen), Is.True,
                "sanity: the decoration request accepts with a chosen option");
            presenter.TryOpen(dog);

            Assert.That(presenter.AcceptPills, Is.Empty, "a reminder offers no accept/complete pill");
            Assert.That(presenter.DeclinePill.Label.text, Is.EqualTo("On its way"));
            Assert.That(presenter.BodyLabel.text, Does.Contain(chosen),
                "the reminder names the item chosen at accept time");
        }

        [Test]
        public void StandardQuest_ReminderDoesNotAffectTheAvailableOffer()
        {
            // #472 regression: an Available (not-yet-accepted) quest is unchanged
            // — still the templated opener/closer offer with a working Accept pill
            // and a "Not now" decline.
            var dog = state.Dogs.First();
            var quest = state.Quests.GiveQuestTo(dog, QuestType.LostItem, new System.Random(1));
            presenter.TryOpen(dog);

            Assert.That(presenter.BodyLabel.text, Is.EqualTo(string.Join("\n", quest.DialogueLines)),
                "an Available quest still renders its templated opener/closer");
            Assert.That(presenter.AcceptPills.Count, Is.EqualTo(1), "the Available offer keeps its accept pill");
            Assert.That(presenter.DeclinePill.Label.text, Is.EqualTo("Not now"),
                "the Available offer's decline is still 'Not now', not 'Still looking'");
        }

        [Test]
        public void DecorationQuest_ShowsOnePillPerOption_WithNameAndCostLabels()
        {
            var dog = state.Dogs[2];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.DecorationRequest, new System.Random(7));
            presenter.TryOpen(dog);

            Assert.That(presenter.AcceptPills.Count, Is.EqualTo(quest.Options.Count),
                "a decoration/choice quest shows one option pill per choice (#50)");
            for (var i = 0; i < quest.Options.Count; i++)
            {
                Assert.That(presenter.AcceptPills[i].Label.text, Is.EqualTo(presenter.OptionLabel(quest.Options[i])),
                    "each option pill uses the Name · Cost label");
            }

            Assert.That(presenter.DeclinePill.Label.text, Is.EqualTo("Not now"),
                "options are never a dead end — Not now is still present (#221)");
        }

        [Test]
        public void BuyQuest_ShowsTheCostOnTheAcceptPill()
        {
            var dog = state.Dogs[1];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new System.Random(3));
            presenter.TryOpen(dog);

            Assert.That(presenter.AcceptPills.Count, Is.EqualTo(1));
            Assert.That(presenter.AcceptPills[0].Label.text, Is.EqualTo($"Buy · {quest.Cost.Value}"));
        }

        [Test]
        public void UnaffordableAcceptPill_IsGreyedAndNonInteractive()
        {
            var dog = state.Dogs[1];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new System.Random(3));
            presenter.TryOpen(dog);

            var pill = presenter.AcceptPills[0];
            AssertColor(pill.Image.color, CandyChromeUgui.Disabled, "an unaffordable pill greys out (#186)");
            Assert.That(pill.Button.interactable, Is.False, "an unaffordable pill cannot be tapped");

            state.Wallet.Deposit(quest.Cost.Value);
            presenter.TryOpen(dog);
            var affordablePill = presenter.AcceptPills[0];
            Assert.That(affordablePill.Image.color, Is.Not.EqualTo(CandyChromeUgui.Disabled),
                "once affordable the pill regains its Candy Cottage tint");
            Assert.That(affordablePill.Button.interactable, Is.True);
        }

        [Test]
        public void TappingAnAffordableAccept_AcceptsAndClosesThePanel()
        {
            var dog = state.Dogs[1];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new System.Random(3));
            state.Wallet.Deposit(quest.Cost.Value);
            presenter.TryOpen(dog);

            var accepted = 0;
            presenter.QuestAccepted += _ => accepted++;

            presenter.AcceptPills[0].Button.onClick.Invoke();

            Assert.That(accepted, Is.EqualTo(1), "the accept pill's onClick runs the Core accept");
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Accepted));
            Assert.That(presenter.IsOpen, Is.False, "a successful accept closes the panel");
        }

        [Test]
        public void TappingAnOptionPill_AcceptsThatChoice()
        {
            var dog = state.Dogs[2];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.DecorationRequest, new System.Random(7));
            var option = quest.Options[0];
            var cost = Doggiehood.Core.Economy.ItemCatalog.Get(option).Cost.Value;
            state.Wallet.Deposit(cost);
            presenter.TryOpen(dog);

            presenter.AcceptPills[0].Button.onClick.Invoke();

            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Accepted), "tapping the option accepts that choice (#50)");
            Assert.That(presenter.IsOpen, Is.False);
        }

        [Test]
        public void RejectedAccept_LeavesThePanelOpen_AndShowsTheStatusMessage()
        {
            var dog = state.Dogs[1];
            state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new System.Random(3));
            presenter.TryOpen(dog);

            // The proactive greying disables the tap, so drive the Core attempt
            // directly (as #186's guard test does): the panel must stay open and
            // surface the message rather than closing silently.
            presenter.AcceptCurrent();

            Assert.That(presenter.IsOpen, Is.True, "a failed purchase must not close the panel");
            Assert.That(presenter.StatusLabel.gameObject.activeSelf, Is.True);
            Assert.That(presenter.StatusLabel.text, Is.EqualTo(presenter.StatusMessage));
            Assert.That(presenter.StatusLabel.text, Is.Not.Empty);
        }

        [Test]
        public void TappingNotNow_ClosesThePanel()
        {
            var dog = state.Dogs.First();
            state.Quests.GiveQuestTo(dog, QuestType.LostItem, new System.Random(1));
            presenter.TryOpen(dog);

            presenter.DeclinePill.Button.onClick.Invoke();

            Assert.That(presenter.IsOpen, Is.False, "Not now dismisses the panel (#185)");
            Assert.That(presenter.PanelRect.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Chrome_IsDeviceSafe_DefaultUiMaterialAndBundledFont()
        {
            var dog = state.Dogs.First();
            state.Quests.GiveQuestTo(dog, QuestType.LostItem, new System.Random(1));
            presenter.TryOpen(dog);

            var panelImage = presenter.PanelRect.GetComponent<Image>();
            Assert.That(panelImage.material, Is.EqualTo(panelImage.defaultMaterial),
                "the panel assigns no custom material (#291)");
            Assert.That(CandyChromeUgui.OutlineInk(presenter.PanelRect.gameObject), Is.Not.Null,
                "the panel carries the shared Candy Cottage chrome outline (#298, #616 contour band)");

            Assert.That(presenter.BodyLabel.font, Is.Not.Null);
            Assert.That(presenter.BodyLabel.font.name, Does.Contain("DejaVu"));
            Assert.That(presenter.BodyLabel.font.name, Does.Not.Contain("Arial"));
        }

        [Test]
        public void Open_LeavesEveryOutlineBandSurroundingItsFill_NoneStrandedInItsParentsCentre()
        {
            // #663: this panel chromes the card, the name tag and each pill at
            // CREATION and places their rects afterwards, so every band used to
            // keep Unity's default 100x100 centred rect — two black boxes on
            // screen (one per parent) and no visible outline anywhere on the
            // panel. Chrome-then-layout must now be as correct as layout-then-
            // chrome.
            var dog = state.Dogs.First();
            state.Quests.GiveQuestTo(dog, QuestType.LostItem, new System.Random(1));
            presenter.TryOpen(dog);

            SyncBands();

            var followers = presenterHost.GetComponentsInChildren<OutlineBandFollower>(true);
            Assert.That(followers.Length, Is.EqualTo(ExpectedBandCount),
                "the card, the name tag and both action pills each carry exactly one band");

            foreach (var follower in followers)
            {
                AssertBandSurroundsItsFill(follower);
            }
        }

        [Test]
        public void Close_LeavesNoOutlineBandDrawnUnderThePresenter()
        {
            // #663: `content` IS the card, so the card's band is a sibling
            // OUTSIDE it — content.SetActive(false) could never hide the band,
            // which is why one black box stayed on screen permanently.
            var dog = state.Dogs.First();
            state.Quests.GiveQuestTo(dog, QuestType.LostItem, new System.Random(1));
            presenter.TryOpen(dog);
            SyncBands();

            presenter.Close();
            SyncBands();

            foreach (var follower in presenterHost.GetComponentsInChildren<OutlineBandFollower>(true))
            {
                Assert.That(follower.IsShowing, Is.False,
                    follower.name + " is still drawn after the conversation closed");
            }
        }

        [Test]
        public void ReopeningTheConversation_DoesNotLeakOutlineBands()
        {
            // #663: RebuildActionRow destroys each pill GameObject but not its
            // sibling band, and an orphan is never re-found — so a fresh band was
            // created and abandoned on every single open.
            var dog = state.Dogs.First();
            state.Quests.GiveQuestTo(dog, QuestType.LostItem, new System.Random(1));

            presenter.TryOpen(dog);
            SyncBands();
            var afterFirstOpen = OutlineBandObjectCount();

            presenter.TryOpen(dog);
            presenter.TryOpen(dog);
            SyncBands();

            Assert.That(OutlineBandObjectCount(), Is.EqualTo(afterFirstOpen),
                "three opens leave the same number of '<name> Outline' objects as one");
        }

        // The card, the name tag, "Not now" and the accept pill (#663).
        private const int ExpectedBandCount = 4;

        // Unity's default RectTransform is 100x100 in its parent's centre; a band
        // stranded on one measures that inflated by the band width on each side.
        private const float StrandedBandSizePx = 100f + CandyChromeUgui.OutlineThicknessPx * 2f;

        private const string OutlineSuffix = " Outline";

        /// <summary>EditMode runs no frame loop, so stand in for the frame's
        /// worth of <c>LateUpdate</c> calls that keep each band on its fill.</summary>
        private void SyncBands()
        {
            OutlineBandFollower.SyncAll(presenterHost);
        }

        private int OutlineBandObjectCount()
        {
            var count = 0;
            foreach (var rect in presenterHost.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect.name.EndsWith(OutlineSuffix))
                {
                    count++;
                }
            }

            return count;
        }

        private static void AssertBandSurroundsItsFill(OutlineBandFollower follower)
        {
            var band = (RectTransform)follower.transform;
            var fill = follower.Fill;
            Assert.That(fill, Is.Not.Null, band.name + " follows no fill");

            var w = CandyChromeUgui.OutlineThicknessPx;
            Assert.That(fill.offsetMin.x - band.offsetMin.x, Is.EqualTo(w).Within(0.01f), band.name + " left edge");
            Assert.That(fill.offsetMin.y - band.offsetMin.y, Is.EqualTo(w).Within(0.01f), band.name + " bottom edge");
            Assert.That(band.offsetMax.x - fill.offsetMax.x, Is.EqualTo(w).Within(0.01f), band.name + " right edge");
            Assert.That(band.offsetMax.y - fill.offsetMax.y, Is.EqualTo(w).Within(0.01f), band.name + " top edge");

            Assert.That(band.rect.width, Is.Not.EqualTo(StrandedBandSizePx).Within(0.01f),
                band.name + " is stranded on the default 100x100 rect");
            Assert.That(band.rect.height, Is.Not.EqualTo(StrandedBandSizePx).Within(0.01f),
                band.name + " is stranded on the default 100x100 rect");
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
