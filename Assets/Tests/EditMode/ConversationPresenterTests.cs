using System.Linq;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #185: "Not now" must be a silent, non-punishing decline distinct from
    /// Accept. #186: the conversation panel had no affordance for a
    /// buy-something quest's cost, and a failed purchase used to close the
    /// panel with zero player-visible feedback. These guard both fixes: the
    /// decline action, cost/affordability surfaced via
    /// QuestPurchasePresentation, and a failed Accept/AcceptWithChoice
    /// leaving the panel open with a message instead of silently closing.
    ///
    /// #221: the reported softlock had two halves — Accept appeared to run
    /// through the whole message queue, and a decoration request offered only
    /// option buttons with no way out. The last three tests here lock in the
    /// non-softlocked behavior: Accept resolves exactly one quest and a second
    /// call never chains, and a decoration request is always dismissable.
    /// </summary>
    public class ConversationPresenterTests
    {
        private GameState state;
        private GameObject host;
        private ConversationPresenter presenter;

        [SetUp]
        public void CreatePresenter()
        {
            state = GameState.CreateNew();
            host = new GameObject("conversation-presenter-host");
            presenter = host.AddComponent<ConversationPresenter>();
            presenter.State = state;
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(host);
        }

        [Test]
        public void DeclineCurrent_ClosesThePanel_WithoutAcceptingTheQuest()
        {
            // #185: "Not now" must be a silent, non-punishing exit distinct
            // from Accept — the quest stays Available, not Accepted, and no
            // QuestAccepted notification fires.
            var dog = state.Dogs.First();
            var quest = state.Quests.GiveQuestTo(dog, QuestType.LostItem, new System.Random(1));
            Assert.That(presenter.TryOpen(dog), Is.True);

            var accepted = false;
            presenter.QuestAccepted += _ => accepted = true;

            presenter.DeclineCurrent();

            Assert.That(presenter.IsOpen, Is.False, "Not now closes the panel");
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Available), "declining must not accept the quest");
            Assert.That(accepted, Is.False, "declining must not raise QuestAccepted");
        }

        [Test]
        public void DeclineCurrent_LeavesTheQuestFullyReopenable()
        {
            // #185: the dog keeps its speech bubble and the exact same
            // request can be re-presented — decline is not a one-shot
            // dismissal, no cooldown, no re-offer delay.
            var dog = state.Dogs.First();
            var quest = state.Quests.GiveQuestTo(dog, QuestType.LostItem, new System.Random(2));
            presenter.TryOpen(dog);

            presenter.DeclineCurrent();

            Assert.That(presenter.TryOpen(dog), Is.True, "the conversation must be re-openable after declining");
            Assert.That(presenter.Current.Lines, Is.EqualTo(quest.DialogueLines),
                "re-opening presents the same request");
        }

        [Test]
        public void AcceptLabel_ShowsTheCost_ForABuyQuest()
        {
            var dog = state.Dogs[1];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new System.Random(3));
            presenter.TryOpen(dog);

            Assert.That(presenter.AcceptLabel, Is.EqualTo($"Buy · {quest.Cost.Value}"));
        }

        [Test]
        public void AcceptIsAffordable_ReflectsTheWalletBalance_ForABuyQuest()
        {
            var dog = state.Dogs[1];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new System.Random(3));
            presenter.TryOpen(dog);

            Assert.That(presenter.AcceptIsAffordable, Is.False, "a fresh wallet starts at 0 coins");

            state.Wallet.Deposit(quest.Cost.Value);
            Assert.That(presenter.AcceptIsAffordable, Is.True);
        }

        [Test]
        public void OptionLabel_And_OptionIsAffordable_ReflectTheCatalogCostAndWallet()
        {
            var dog = state.Dogs[2];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.DecorationRequest, new System.Random(7));
            presenter.TryOpen(dog);
            var option = quest.Options[0];
            var cost = Doggiehood.Core.Economy.ItemCatalog.Get(option).Cost.Value;

            Assert.That(presenter.OptionLabel(option), Is.EqualTo($"{option} · {cost}"));
            Assert.That(presenter.OptionIsAffordable(option), Is.False);

            state.Wallet.Deposit(cost);
            Assert.That(presenter.OptionIsAffordable(option), Is.True);
        }

        [Test]
        public void AcceptCurrent_OnAnUnaffordableBuyQuest_LeavesThePanelOpen_WithAnInsufficientFundsMessage()
        {
            var dog = state.Dogs[1];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new System.Random(3));
            presenter.TryOpen(dog);

            presenter.AcceptCurrent();

            Assert.That(presenter.IsOpen, Is.True, "a failed purchase must not silently close the panel");
            Assert.That(presenter.StatusMessage, Is.Not.Null.And.Not.Empty);
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Available), "no accept side effect on a rejected spend");
            Assert.That(state.Wallet.Coins, Is.EqualTo(0), "an unaffordable attempt spends nothing");
        }

        [Test]
        public void AcceptCurrent_OnAnAffordableBuyQuest_ClosesThePanel_WithNoStatusMessage()
        {
            var dog = state.Dogs[1];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new System.Random(3));
            state.Wallet.Deposit(quest.Cost.Value);
            presenter.TryOpen(dog);

            presenter.AcceptCurrent();

            Assert.That(presenter.IsOpen, Is.False);
            Assert.That(presenter.StatusMessage, Is.Null);
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Accepted));
        }

        [Test]
        public void AcceptChoice_OnAnUnaffordableDecorationOption_LeavesThePanelOpen_WithAnInsufficientFundsMessage()
        {
            var dog = state.Dogs[2];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.DecorationRequest, new System.Random(7));
            presenter.TryOpen(dog);
            var option = quest.Options[0];

            presenter.AcceptChoice(option);

            Assert.That(presenter.IsOpen, Is.True, "a failed purchase must not silently close the panel");
            Assert.That(presenter.StatusMessage, Is.Not.Null.And.Not.Empty);
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Available), "no accept side effect on a rejected spend");
            Assert.That(state.Wallet.Coins, Is.EqualTo(0), "an unaffordable attempt spends nothing");
        }

        [Test]
        public void AcceptCurrent_ResolvesExactlyOneQuest_AndASecondCallDoesNotChain()
        {
            // #221: the softlock's "Accept runs through every message" half.
            // A single Accept must resolve exactly one quest and hand control
            // back (panel closes); a second immediate Accept must be a harmless
            // no-op — no second QuestAccepted, no spurious status message, and
            // the already-accepted quest untouched — so Accept can never chain
            // through a queue.
            var dog = state.Dogs.First();
            var quest = state.Quests.GiveQuestTo(dog, QuestType.LostItem, new System.Random(11));
            presenter.TryOpen(dog);

            var acceptedCount = 0;
            presenter.QuestAccepted += _ => acceptedCount++;

            presenter.AcceptCurrent();

            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Accepted), "the one open quest is accepted");
            Assert.That(presenter.IsOpen, Is.False, "Accept hands control back by closing the panel");
            Assert.That(acceptedCount, Is.EqualTo(1), "exactly one quest resolves per Accept");

            presenter.AcceptCurrent();

            Assert.That(acceptedCount, Is.EqualTo(1), "a second Accept must not chain into another quest");
            Assert.That(presenter.IsOpen, Is.False, "the panel stays closed");
            Assert.That(presenter.StatusMessage, Is.Null, "a no-op Accept must not surface a stale/spurious message");
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Accepted), "the resolved quest is left as-is");
        }

        [Test]
        public void DeclineCurrent_OnADecorationRequest_ClosesWithoutAccepting_SoOptionsAreNeverADeadEnd()
        {
            // #221: the softlock's "panel can't be closed" half. A decoration
            // request presents one pill per option; the player must still be
            // able to back out without committing to any option — the decline
            // path is present alongside the options, not replaced by them.
            var dog = state.Dogs[2];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.DecorationRequest, new System.Random(7));
            presenter.TryOpen(dog);
            Assume.That(quest.Options.Count, Is.GreaterThan(0), "sanity: a decoration request offers options");

            var accepted = false;
            presenter.QuestAccepted += _ => accepted = true;

            presenter.DeclineCurrent();

            Assert.That(presenter.IsOpen, Is.False, "the decoration panel is dismissable without picking an option");
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Available), "declining must not accept any option");
            Assert.That(accepted, Is.False, "declining must not raise QuestAccepted");
            Assert.That(presenter.TryOpen(dog), Is.True, "the same request is fully re-openable after declining");
        }

        [Test]
        public void ComputePanelRect_KeepsTheGrayboxBoxSizeAndPosition_Unchanged()
        {
            // #273: the readability bump must NOT resize or reposition the box.
            // Locked to the pre-#273 formulas: width = min(600, w - 40),
            // height = h * 0.35, at y = h * 0.6, centered horizontally.
            const float w = 1920f;
            const float h = 1200f;
            var rect = ConversationPresenter.ComputePanelRect(w, h);

            var expectedWidth = Mathf.Min(600f, w - 40f);
            Assert.That(rect.width, Is.EqualTo(expectedWidth).Within(0.01f));
            Assert.That(rect.height, Is.EqualTo(h * 0.35f).Within(0.01f));
            Assert.That(rect.x, Is.EqualTo((w - expectedWidth) / 2f).Within(0.01f), "centered horizontally");
            Assert.That(rect.y, Is.EqualTo(h * 0.6f).Within(0.01f));
        }

        [Test]
        public void ComputePanelRect_ClampsWidthOnNarrowScreens_LikeTheOriginal()
        {
            // The width clamp (min(600, w - 40)) is preserved for small screens.
            const float w = 500f;
            const float h = 800f;
            var rect = ConversationPresenter.ComputePanelRect(w, h);

            Assert.That(rect.width, Is.EqualTo(w - 40f).Within(0.01f),
                "narrow screens clamp to width-40, not 600");
        }

        [Test]
        public void DialogueAndButtonSizes_AreRoughlyDouble_TheDefaultImguiBaseline()
        {
            // #273: interim graybox legibility — the dialogue text and the
            // action buttons render at ~2x their default IMGUI size, driven
            // by named constants (#161: no inline literals).
            Assert.That(ConversationPresenter.DialogueFontPx,
                Is.EqualTo(ConversationPresenter.BaselineFontPx * 2),
                "dialogue/status/label font is ~2x the default IMGUI size");
            Assert.That(ConversationPresenter.ButtonMinHeightPx,
                Is.EqualTo(ConversationPresenter.BaselineButtonHeightPx * 2),
                "action buttons are ~2x the default IMGUI button height");
            Assert.That(ConversationPresenter.ButtonPaddingPx, Is.GreaterThan(0),
                "the enlarged pills carry positive padding");
        }

        [Test]
        public void ReopeningThePanel_ClearsAnyStaleStatusMessage()
        {
            var dog = state.Dogs[1];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new System.Random(3));
            presenter.TryOpen(dog);
            presenter.AcceptCurrent();
            Assert.That(presenter.StatusMessage, Is.Not.Null, "sanity check: the failed attempt set a message");

            presenter.Close();
            presenter.TryOpen(dog);

            Assert.That(presenter.StatusMessage, Is.Null, "a fresh open should not show a stale message");
        }
    }
}
