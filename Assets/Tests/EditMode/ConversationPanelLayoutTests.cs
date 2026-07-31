using System.Linq;
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
            Assert.That(presenter.PanelRect.GetComponent<Outline>(), Is.Not.Null,
                "the panel carries the shared Candy Cottage chrome outline (#298)");

            Assert.That(presenter.BodyLabel.font, Is.Not.Null);
            Assert.That(presenter.BodyLabel.font.name, Does.Contain("DejaVu"));
            Assert.That(presenter.BodyLabel.font.name, Does.Not.Contain("Arial"));
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
