using System.Linq;
using Doggiehood.Core.Decorations;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Decorations
{
    public class ComfortDecorationTests
    {
        [Test]
        public void ExactlyThreeComfortItems_BedCushionBlanket()
        {
            // #51: the v1 decoration category.
            Assert.That(ComfortDecorations.ItemNames,
                Is.EquivalentTo(new[] { "bed", "cushion", "blanket" }));
        }

        [Test]
        public void EachComfortItem_CostsWithinTheDocumentedRange()
        {
            // #51/#62.
            foreach (var name in ComfortDecorations.ItemNames)
            {
                Assert.That(ItemCatalog.Get(name).Cost, Is.InRange(30, 50), name);
            }
        }
    }

    public class DecorationScopeTests
    {
        [Test]
        public void ADecoration_AlwaysBelongsToExactlyOneHouse()
        {
            // #46: yard-scoped, one house each.
            var decoration = new Decoration("bed", 2, new GridPoint(1f, 1f));

            Assert.That(decoration.HouseId, Is.EqualTo(2));
        }

        [Test]
        public void NoSharedSpaceDecorationTarget_ExistsInTheModel()
        {
            // #46 guard: no public/shared-space targeting anywhere. (Invariant.)
            var forbidden = new[] { "shared", "public", "street" };

            var offenders = typeof(Decoration).Assembly.GetTypes()
                .Where(t => t.Namespace != null
                    && t.Namespace.StartsWith("Doggiehood.Core.Decorations"))
                .SelectMany(t => t.GetProperties().Select(p => t.Name + "." + p.Name))
                .Where(name => forbidden.Any(f => name.ToLowerInvariant().Contains(f)))
                .ToList();

            Assert.That(offenders, Is.Empty);
        }
    }

    public class AutomaticPlacementTests
    {
        [Test]
        public void DeliveredDecorations_GetAValidYardPositionAutomatically()
        {
            // #48: placement is automatic and lands in the owning house's yard.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                for (var index = 0; index < 3; index++)
                {
                    var position = YardPlacement.PositionFor(lot.HouseId, index);

                    var dx = System.Math.Abs(position.X - lot.Position.X);
                    var dz = System.Math.Abs(position.Z - lot.Position.Z);
                    Assert.That(dx, Is.LessThanOrEqualTo(8f), $"house {lot.HouseId} idx {index}");
                    Assert.That(dz, Is.LessThanOrEqualTo(8f), $"house {lot.HouseId} idx {index}");

                    // Never in the street.
                    var half = NeighborhoodLayout.StreetWidth / 2f;
                    Assert.That(System.Math.Abs(position.X) > half || System.Math.Abs(position.Z) > half, Is.True);
                }
            }
        }

        [Test]
        public void SuccessivePlacements_DoNotStack()
        {
            var first = YardPlacement.PositionFor(1, 0);
            var second = YardPlacement.PositionFor(1, 1);

            Assert.That(first, Is.Not.EqualTo(second));
        }

        [Test]
        public void NoManualPlacementApi_ExistsAnywhereInCore()
        {
            // #48 guard: no drag/arrange/move entry point on the decoration
            // system. Scoped to the Decorations namespace — camera gestures
            // (GestureMapper.DragToPan) legitimately use "drag". (Invariant.)
            var forbidden = new[] { "manual", "arrange", "drag", "move" };

            var offenders = typeof(Decoration).Assembly.GetTypes()
                .Where(t => t.Namespace != null
                    && t.Namespace.StartsWith("Doggiehood.Core.Decorations"))
                .SelectMany(t => t.GetMethods().Select(m => t.Name + "." + m.Name))
                .Where(name => forbidden.Any(f => name.ToLowerInvariant().Contains(f)))
                .ToList();

            Assert.That(offenders, Is.Empty);
        }
    }

    public class DecorationRequestFlowTests
    {
        [Test]
        public void RequestOffersTwoOrMoreOptions_NotOneNamedItem()
        {
            // #50: generic request, player chooses.
            var state = GameState.CreateNew();
            var quest = state.Quests.GiveQuestTo(state.Dogs[0], QuestType.DecorationRequest, new System.Random(1));

            Assert.That(quest.Options.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(quest.ItemName, Is.Null.Or.Empty, "no pre-named item on a generic request");
        }

        [Test]
        public void AcceptingWithAChoice_DeliversThatItemAndDeductsItsCost()
        {
            // #50/#25: the chosen option's specific cost is deducted and that
            // specific item is what gets delivered.
            var state = GameState.CreateNew();
            state.Wallet.Deposit(100);
            var dog = state.Dogs[0];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.DecorationRequest, new System.Random(1));
            var choice = quest.Options[1];
            var expectedCost = ItemCatalog.Get(choice).Cost;
            var before = state.Wallet.Coins;

            Assert.That(state.Quests.AcceptWithChoice(quest, choice), Is.True);
            Assert.That(state.Wallet.Coins, Is.EqualTo(before - expectedCost));

            state.Quests.NotifyDogArrivedHome(quest);
            state.Quests.DeliverPackage(quest);

            Assert.That(state.Decorations.Single().ItemName, Is.EqualTo(choice));
            Assert.That(state.Decorations.Single().HouseId, Is.EqualTo(dog.HouseId));
        }

        [Test]
        public void DecorationsOnlyOriginateFromDecorationRequestQuests()
        {
            // #49: no standalone shop path — completing the quest is the only
            // way a Decoration appears.
            var state = GameState.CreateNew();
            state.Wallet.Deposit(100);

            Assert.That(state.Decorations, Is.Empty);

            // Non-decoration quests never create decorations.
            var buy = state.Quests.GiveQuestTo(state.Dogs[1], QuestType.BuyGift, new System.Random(2));
            state.Quests.Accept(buy);
            state.Quests.NotifyDogArrivedHome(buy);
            state.Quests.DeliverPackage(buy);
            Assert.That(state.Decorations, Is.Empty);
        }

        [Test]
        public void NoStandaloneShopEntryPoint_ExistsInCore()
        {
            // #49 guard. (Invariant.)
            var forbidden = new[] { "shop", "store", "buydecoration", "purchasedecoration" };

            var offenders = typeof(Decoration).Assembly.GetTypes()
                .Where(t => t.Namespace != null
                    && t.Namespace.StartsWith("Doggiehood.Core")
                    && !t.Namespace.Contains("Tests"))
                .Where(t => forbidden.Any(f => t.Name.ToLowerInvariant().Contains(f)))
                .Select(t => t.FullName)
                .ToList();

            Assert.That(offenders, Is.Empty);
        }
    }

    public class HappinessTests
    {
        [Test]
        public void DeliveringADecoration_RaisesTheOwningDogsHappiness()
        {
            // #47.
            var state = GameState.CreateNew();
            state.Wallet.Deposit(100);
            var dog = state.Dogs[0];
            var before = dog.Happiness;

            var quest = state.Quests.GiveQuestTo(dog, QuestType.DecorationRequest, new System.Random(1));
            state.Quests.AcceptWithChoice(quest, quest.Options[0]);
            state.Quests.NotifyDogArrivedHome(quest);
            state.Quests.DeliverPackage(quest);

            Assert.That(dog.Happiness, Is.GreaterThan(before));
        }

        [Test]
        public void Happiness_NeverGatesQuestBehavior()
        {
            // #47: flavor only. Identical quest-system behavior across the
            // whole happiness range.
            foreach (var happiness in new[] { 0, 5, 100 })
            {
                var state = GameState.CreateNew();
                foreach (var dog in state.Dogs)
                {
                    dog.SetHappinessForFlavor(happiness);
                }

                // #543: quests trickle in hourly, so drive a full pacing window
                // of boundaries to fill the neighborhood up to its target.
                for (var hour = 0; hour < EconomyNumbers.PacingWindowHours; hour++)
                {
                    state.Quests.StartNewDay(new System.Random(7 + hour));
                }

                Assert.That(state.Quests.ActiveQuests.Count(), Is.GreaterThan(0));
                var quest = state.Quests.GiveQuestTo(
                    state.Dogs.First(d => !d.HasActiveQuest), QuestType.LostItem, new System.Random(3));
                state.Quests.Accept(quest);
                Assert.That(state.Quests.TapWorldPosition(quest.HiddenItemPosition.Value), Is.True);
                Assert.That(state.Wallet.Coins, Is.EqualTo(EconomyNumbers.QuestPayout),
                    $"payout changed at happiness {happiness}");
            }
        }
    }

    public class AutoRestTests
    {
        private static WalkNetwork Network()
        {
            return WalkNetwork.BuildFrom(NeighborhoodLayout.Roads, NeighborhoodLayout.HouseLots);
        }

        [Test]
        public void DogWithAComfortDecoration_PeriodicallyWalksOverAndRestsOnItsOwn()
        {
            // #52/#112: no player trigger needed, and the dog walks over the
            // walk network to the decoration before settling — no teleport.
            var state = GameState.CreateNew();
            var dog = state.Dogs[0];
            state.AddDecoration(new Decoration("bed", dog.HouseId, YardPlacement.PositionFor(dog.HouseId, 0)));

            var rng = new System.Random(11);
            var network = Network();
            var dogPosition = NeighborhoodLayout.Intersection;

            RestApproach approach = null;
            for (var tick = 0; tick < 200 && approach == null; tick++)
            {
                approach = RestBehavior.TryBeginApproach(dog, state, dogPosition, network, rng);
            }

            Assert.That(approach, Is.Not.Null, "dog never chose to approach its comfort decoration");
            Assert.That(dog.State, Is.EqualTo(Doggiehood.Core.Dogs.DogState.IdleWander),
                "the dog must still be walking, not resting, before it arrives");

            for (var step = 0; step < 1000 && !approach.HasArrived; step++)
            {
                approach.Advance(RestApproach.ApproachSpeed);
            }

            Assert.That(approach.HasArrived, Is.True, "dog never reached the decoration");
            dog.TryRest(comfortDecorationSelected: true);
            Assert.That(dog.State, Is.EqualTo(Doggiehood.Core.Dogs.DogState.Rest),
                "dog rests only after arriving at the decoration");
        }

        [Test]
        public void DogWithNoComfortDecoration_NeverApproachesOrRests()
        {
            var state = GameState.CreateNew();
            var dog = state.Dogs[0];

            var rng = new System.Random(11);
            var network = Network();
            for (var tick = 0; tick < 500; tick++)
            {
                var approach = RestBehavior.TryBeginApproach(
                    dog, state, NeighborhoodLayout.Intersection, network, rng);
                Assert.That(approach, Is.Null);
                Assert.That(dog.State, Is.Not.EqualTo(Doggiehood.Core.Dogs.DogState.Rest));
            }
        }
    }
}
