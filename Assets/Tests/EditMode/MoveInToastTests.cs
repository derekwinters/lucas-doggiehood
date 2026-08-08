using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Quests;
using Doggiehood.Core.Ui;
using Doggiehood.Core.World;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #675: a move-in is the toast's third trigger (docs/specs/ui/toast.md).
    /// <see cref="MoveInToastDirector"/> subscribes to the same Core signal the
    /// dog-spawn and welcome-pop-up paths use
    /// (<see cref="QuestManager.MoveInOccurred"/>) and enqueues exactly one
    /// "new resident" toast per household onto the shared single-slot
    /// <see cref="ToastQueue{T}"/>.
    ///
    /// <para>The copy branches on household size (Derek, 2026-08-08): one dog is
    /// named, two dogs are both named, and a household of three or more drops
    /// names for <see cref="ToastCopy.MoveInFamilyLine"/>. That third branch
    /// exists for a measured reason — three roster names plus "moved in!" plus
    /// the payout overflows the pill's one-line text budget, and the toast
    /// clips rather than wraps (#578). So the fit guard below derives the
    /// <em>worst case</em> of every branch from the real name roster and
    /// measures it with the real font, rather than sampling a hand-picked
    /// line.</para>
    /// </summary>
    public class MoveInToastTests
    {
        // The pill's real one-line text budget, derived from the production
        // geometry (a full-width pill's text rect) rather than re-adding the
        // chrome by hand — so it can never drift from what ToastView draws.
        private static readonly float TextBudgetPx = ToastView.ComputeTextRect(
            ToastView.ComputeToastRect(ToastView.ToastMaxWidthPx)).width;

        // The same cushion ToastViewTests uses: the line must fit comfortably,
        // not to the pixel, so a tiny font-metric difference can't flip the guard.
        private const float FitSafetyMarginPx = 16f;

        private const int HouseId = 1;

        private ToastQueue<ToastRequest> queue;
        private GameObject host;

        [SetUp]
        public void SetUp()
        {
            queue = new ToastQueue<ToastRequest>();
            host = new GameObject("move-in-toast-director");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
        }

        // #625 made the pity increment population-scaled; the late increment is
        // its minimum across all populations, so a bound built from it is the
        // safe worst case for "a move-in is guaranteed by now" (the same bound
        // MoveInReflectionTests / WelcomePopupDirectorTests use).
        private static readonly int MaxCompletionsToGuaranteeMoveIn =
            (int)System.Math.Ceiling(1.0 / Doggiehood.Core.Expansion.MoveInNumbers
                .MoveInChanceIncrementPerQuest) + 1;

        // A game with exactly one vacant house — a freshly built, never-occupied
        // frontier lot — so a completed quest's move-in roll has somewhere to
        // land, unlike the always-occupied starting houses.
        private static GameState StateWithAVacantHouse()
        {
            var state = FrontierEditModeWorld.WithFirstTileUnlocked(100_000);
            Assert.That(state.TryBuildHouse(FrontierEditModeWorld.FirstLotId), Is.True,
                "precondition: the frontier lot is buildable");
            return state;
        }

        // Completes quests through the real QuestManager funnel until its
        // move-in roll succeeds, and returns the household that arrived — so the
        // toast is driven by the same Core signal production uses, not a hook.
        private static IReadOnlyList<Dog> TriggerMoveIn(GameState state)
        {
            var dog = state.Dogs.First(d =>
                d.HouseId != FrontierEditModeWorld.FirstLotId && !d.HasActiveQuest);
            for (var i = 0; i < MaxCompletionsToGuaranteeMoveIn; i++)
            {
                var before = state.Dogs.Count;
                var quest = state.Quests.GiveQuestTo(dog, QuestType.PestControl, new System.Random(i));
                Assert.That(state.Quests.Accept(quest), Is.True);
                Assert.That(state.Quests.SprayHouse(quest.TargetHouseId.Value), Is.True);
                if (state.Dogs.Count > before)
                {
                    return state.Dogs.Skip(before).ToList();
                }
            }

            Assert.Fail("no move-in occurred within the guaranteed completion bound");
            return null;
        }

        private static Dog DogNamed(string name)
        {
            return new Dog(name, Breed.Puggle, Personality.Excited, HouseId, isPuppy: false);
        }

        private static IReadOnlyList<Dog> Household(params string[] names)
        {
            return names.Select(DogNamed).ToList();
        }

        // Every name a move-in can draw: the general pool plus the easter-egg
        // reserve (a household head can come from either).
        private static IReadOnlyList<string> EveryPossibleName()
        {
            return NamePool.Names.Concat(EasterEggDogs.ReservedNames).ToList();
        }

        // The names that render widest, longest first — the worst case is built
        // from measured width, not letter count.
        private static IReadOnlyList<string> WidestNamesFirst()
        {
            var style = ToastView.LabelStyle();
            return EveryPossibleName()
                .OrderByDescending(name => style.CalcSize(new GUIContent(name)).x)
                .ToList();
        }

        [Test]
        public void SingleDogMoveIn_NamesTheNewResident()
        {
            Assert.That(
                ToastCopy.MoveIn(Household("Biscuit"), 50),
                Is.EqualTo("Biscuit moved in! +50 coins"));
        }

        [Test]
        public void TwoDogMoveIn_NamesBoth_MatchingTheWelcomePopupsNaming()
        {
            Assert.That(
                ToastCopy.MoveIn(Household("Biscuit", "Pepper"), 50),
                Is.EqualTo("Biscuit & Pepper moved in! +50 coins"));
        }

        [Test]
        public void ThreeDogMoveIn_DropsTheNames_ForTheFamilyLine()
        {
            // Derek, 2026-08-08: three names would overflow the pill, so a
            // household of three or more is announced without names at all.
            Assert.That(
                ToastCopy.MoveIn(Household("Mochi", "Nori", "Yuzu"), 50),
                Is.EqualTo("A new family moved in! +50 coins"));
        }

        [Test]
        public void HouseholdsLargerThanThree_UseTheSameFamilyLine()
        {
            // The roster tops out at three today, but the branch is on "three or
            // more" so a bigger household could never fall back to naming
            // everyone and blow the width budget.
            Assert.That(
                ToastCopy.MoveIn(Household("Mochi", "Nori", "Yuzu", "Biscuit"), 50),
                Is.EqualTo("A new family moved in! +50 coins"));
        }

        [Test]
        public void EveryMoveInCopyBranch_FitsOnOneLineAtItsWorstCase()
        {
            // The line never wraps and is clipped at the pill edge (#578), so
            // every branch's worst case has to fit inside the pill's text budget
            // — measured with the real bold DejaVu Sans metrics, at the LIVE
            // payout, and derived from the actual roster rather than sampled.
            var widest = WidestNamesFirst();
            var reward = EconomyNumbers.MoveInReward;
            var worstCases = new[]
            {
                ToastCopy.MoveIn(Household(widest[0]), reward),
                ToastCopy.MoveIn(Household(widest[0], widest[1]), reward),
                ToastCopy.MoveIn(Household(widest[0], widest[1], widest[2]), reward),
            };

            var style = ToastView.LabelStyle();
            foreach (var message in worstCases)
            {
                var textWidth = style.CalcSize(new GUIContent(message)).x;
                Assert.That(
                    textWidth,
                    Is.LessThanOrEqualTo(TextBudgetPx - FitSafetyMarginPx),
                    $"'{message}' must fit on one line within the toast content budget " +
                    $"(measured {textWidth:0}px vs budget {TextBudgetPx:0}px)");
            }
        }

        [Test]
        public void ThreeNamedDogs_WouldNotHaveFit_WhichIsWhyTheFamilyLineExists()
        {
            // The measurement behind Derek's copy decision, pinned so nobody
            // "simplifies" the family branch back to naming everyone: three of
            // the roster's widest names, formatted like the one- and two-dog
            // branches, overflows the budget. The fix is the copy, never a wider
            // ToastMaxWidthPx (#578 sized that deliberately).
            var widest = WidestNamesFirst();
            var namedThree = $"{widest[0]}, {widest[1]} & {widest[2]} moved in! " +
                $"+{EconomyNumbers.MoveInReward} coins";

            var textWidth = ToastView.LabelStyle().CalcSize(new GUIContent(namedThree)).x;

            Assert.That(
                textWidth,
                Is.GreaterThan(TextBudgetPx - FitSafetyMarginPx),
                $"'{namedThree}' is expected to fail the one-line fit guard — if it now " +
                $"fits (measured {textWidth:0}px vs budget {TextBudgetPx:0}px), the " +
                "three-dog copy branch is worth revisiting with Derek rather than deleting");
        }

        [Test]
        public void AMoveIn_EnqueuesExactlyOneToast_CarryingThePayout()
        {
            var state = StateWithAVacantHouse();
            host.AddComponent<MoveInToastDirector>().Init(state, queue);

            var household = TriggerMoveIn(state);

            Assert.That(queue.HasCurrent, Is.True, "the move-in toast is showing");
            Assert.That(queue.PendingCount, Is.EqualTo(0), "one household, one toast");
            Assert.That(queue.Current.Message,
                Is.EqualTo(ToastCopy.MoveIn(household, EconomyNumbers.MoveInReward)),
                "the toast carries the approved line and the payout Core just deposited");
        }

        [Test]
        public void AMoveInDuringAnotherToast_QueuesBehindIt_RatherThanReplacingIt()
        {
            // ToastQueueSlotCount = 1: the in-flight toast keeps the lane and the
            // move-in waits its turn, first-come first-served (toast.md).
            var state = StateWithAVacantHouse();
            host.AddComponent<MoveInToastDirector>().Init(state, queue);
            var inFlight = new ToastRequest(ToastCopy.QuestComplete(EconomyNumbers.QuestPayout));
            queue.Enqueue(inFlight);

            var household = TriggerMoveIn(state);

            Assert.That(queue.Current.Message, Is.EqualTo(inFlight.Message),
                "the in-flight toast is not displaced");
            Assert.That(queue.PendingCount, Is.EqualTo(1), "the move-in toast waits its turn");

            queue.DismissCurrent();

            Assert.That(queue.Current.Message,
                Is.EqualTo(ToastCopy.MoveIn(household, EconomyNumbers.MoveInReward)),
                "the move-in toast plays once the lane clears");
        }
    }
}
