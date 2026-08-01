using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Quests;
using Doggiehood.Core.Tests.Expansion;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Quests
{
    public class QuestManagerTests
    {
        private static GameState NewState()
        {
            return GameState.CreateNew();
        }

        [Test]
        public void ExposesActiveQuestsAcrossAllDogs()
        {
            // #23: QuestManager is the one view over active quests.
            var state = NewState();

            Assert.That(state.Quests.ActiveQuests, Is.Empty);

            state.Quests.StartNewDay(new System.Random(1));

            Assert.That(state.Quests.ActiveQuests.Count(), Is.InRange(2, 4));
        }

        [Test]
        public void BeginInitialQuests_BeforeOnboarding_SeedsExactlyOneLostItemOnOneDog()
        {
            // #312: first launch seeds a single easy lost-item quest and
            // suppresses the 2-4 daily rotation until onboarding completes,
            // so the tutorial has exactly one gentle tap-to-find target.
            for (var seed = 0; seed < 20; seed++)
            {
                var state = NewState();
                Assert.That(state.OnboardingComplete, Is.False);

                state.Quests.BeginInitialQuests(new System.Random(seed));

                var active = state.Quests.ActiveQuests.ToList();
                Assert.That(active.Count, Is.EqualTo(1), $"seed {seed}: exactly one quest");
                Assert.That(active[0].Type, Is.EqualTo(QuestType.LostItem), $"seed {seed}");
                Assert.That(state.Dogs.Count(d => d.HasActiveQuest), Is.EqualTo(1),
                    $"seed {seed}: exactly one dog holds a quest");
            }
        }

        [Test]
        public void BeginInitialQuests_AfterOnboarding_RunsTheNormalRotation()
        {
            // #312: once onboarding is complete the seam is just the normal
            // 2-4 daily rotation — no more single-lost-item suppression.
            for (var seed = 0; seed < 10; seed++)
            {
                var state = NewState();
                state.MarkOnboardingComplete();

                state.Quests.BeginInitialQuests(new System.Random(seed));

                Assert.That(state.Dogs.Count(d => d.HasActiveQuest), Is.InRange(2, 4), $"seed {seed}");
            }
        }

        [Test]
        public void NewDay_AssignsQuestsToTwoToFourDogs()
        {
            // #26: daily rotation of a few active quests.
            for (var seed = 0; seed < 10; seed++)
            {
                var state = NewState();
                state.Quests.StartNewDay(new System.Random(seed));

                var dogsWithQuests = state.Dogs.Count(d => d.HasActiveQuest);
                Assert.That(dogsWithQuests, Is.InRange(2, 4), $"seed {seed}");
            }
        }

        [Test]
        public void NewDay_NeverOverwritesAnUncompletedQuest()
        {
            // #26 precedence rule: a dog holding an uncompleted quest keeps
            // it; the rotation only assigns to quest-free dogs.
            var state = NewState();
            state.Quests.StartNewDay(new System.Random(1));

            var held = state.Quests.ActiveQuests.First();
            var holder = held.DogName;

            for (var day = 0; day < 5; day++)
            {
                state.Quests.StartNewDay(new System.Random(100 + day));
            }

            var stillHeld = state.Quests.ActiveQuests.Single(q => q.DogName == holder);
            Assert.That(stillHeld.Id, Is.EqualTo(held.Id), "quest was overwritten by rotation");
        }

        [Test]
        public void Rotation_IsDeterministicForASeed()
        {
            var a = NewState();
            var b = NewState();
            a.Quests.StartNewDay(new System.Random(42));
            b.Quests.StartNewDay(new System.Random(42));

            Assert.That(
                a.Quests.ActiveQuests.Select(q => (q.DogName, q.Type, q.ItemName)),
                Is.EqualTo(b.Quests.ActiveQuests.Select(q => (q.DogName, q.Type, q.ItemName))));
        }

        [Test]
        public void CurrencyOnlyMovesOnQuestCompletion()
        {
            // #24: no idle income — days passing never change the balance.
            var state = NewState();

            for (var day = 0; day < 30; day++)
            {
                state.Quests.StartNewDay(new System.Random(day));
            }

            Assert.That(state.Wallet.Coins, Is.EqualTo(0), "coins appeared without any quest completing");
        }

        [Test]
        public void BuyGiftDelivery_GatesWanderOff_ThenSignalsAFreshTargetOnHandBack()
        {
            // #470: accepting a (non-fence) buy-gift quest puts the dog into
            // DeliveryPhase.HeadingHome — the QuestDirector walks it home. The
            // dog must not wander during HeadingHome or WaitingForDelivery, and
            // once the package is delivered (PlaceOnStreet) it must both wander
            // again and signal a fresh wander target (bumped reset token).
            var state = NewState();
            state.Wallet.Deposit(100);
            var dog = state.Dogs[1];
            var tokenBefore = dog.WanderResetToken;

            var buy = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new System.Random(2));
            Assert.That(state.Quests.Accept(buy), Is.True);
            Assert.That(buy.DeliveryPhase, Is.EqualTo(DeliveryPhase.HeadingHome));
            Assert.That(dog.WantsToWander, Is.False, "no wander while heading home");

            state.Quests.NotifyDogArrivedHome(buy);
            Assert.That(buy.DeliveryPhase, Is.EqualTo(DeliveryPhase.WaitingForDelivery));
            Assert.That(dog.WantsToWander, Is.False, "no wander while waiting for the truck");

            state.Quests.DeliverPackage(buy);
            Assert.That(dog.WantsToWander, Is.True, "wander resumes once delivery hands control back");
            Assert.That(dog.WanderResetToken, Is.GreaterThan(tokenBefore),
                "delivery hand-back must signal a fresh wander target");
        }

        [Test]
        public void CompletingAnyQuestType_PaysTheFlatPayout()
        {
            // #23/#24/#62 + integration: full loop for each quest type.
            var state = NewState();
            state.Wallet.Deposit(100); // funds for the BuyGift acceptance cost

            // Lost item: accept, then tap the hidden position (#12, #31).
            var lost = state.Quests.GiveQuestTo(state.Dogs[0], QuestType.LostItem, new System.Random(1));
            var before = state.Wallet.Coins;
            state.Quests.Accept(lost);
            Assert.That(state.Quests.TapWorldPosition(lost.HiddenItemPosition.Value), Is.True);
            Assert.That(lost.Status, Is.EqualTo(QuestStatus.Completed));
            Assert.That(state.Wallet.Coins, Is.EqualTo(before + Doggiehood.Core.Economy.EconomyNumbers.QuestPayout));

            // Buy gift: accept deducts cost; payout only after delivery (#13, #30).
            var buy = state.Quests.GiveQuestTo(state.Dogs[1], QuestType.BuyGift, new System.Random(2));
            before = state.Wallet.Coins;
            state.Quests.Accept(buy);
            Assert.That(state.Wallet.Coins, Is.EqualTo(before - buy.Cost.Value));
            state.Quests.NotifyDogArrivedHome(buy);
            state.Quests.DeliverPackage(buy);
            Assert.That(buy.Status, Is.EqualTo(QuestStatus.Completed));
            Assert.That(state.Wallet.Coins,
                Is.EqualTo(before - buy.Cost.Value + Doggiehood.Core.Economy.EconomyNumbers.QuestPayout));

            // Pest control: spray the right house (#53).
            var pest = state.Quests.GiveQuestTo(state.Dogs[4], QuestType.PestControl, new System.Random(3));
            before = state.Wallet.Coins;
            state.Quests.Accept(pest);
            Assert.That(state.Quests.SprayHouse(pest.TargetHouseId.Value), Is.True);
            Assert.That(pest.Status, Is.EqualTo(QuestStatus.Completed));
            Assert.That(state.Wallet.Coins, Is.EqualTo(before + Doggiehood.Core.Economy.EconomyNumbers.QuestPayout));
        }

        [Test]
        public void CompletingAQuest_RollsTheMoveInPityCounter_ButNeverFillsAnOccupiedHouse()
        {
            // #58: every quest completion is wired to GameState's move-in
            // hook (#54). With all 4 starting houses already occupied
            // there is nothing to fill, so completing a quest must never
            // grow the dog roster or touch house vacancy, no matter how
            // the roll would have landed.
            var state = NewState();
            var dogCountBefore = state.Dogs.Count;
            var pest = state.Quests.GiveQuestTo(state.Dogs[4], QuestType.PestControl, new System.Random(3));
            state.Quests.Accept(pest);

            Assert.That(state.Quests.SprayHouse(pest.TargetHouseId.Value), Is.True);

            Assert.That(state.Dogs.Count, Is.EqualTo(dogCountBefore));
            Assert.That(state.Houses, Has.All.Property("IsVacant").False);
        }

        /// <summary>#436: a game state carrying exactly one vacant house — a
        /// freshly built (never-occupied) zone lot (#58) — so a completed
        /// quest's move-in roll has somewhere to land, unlike the four
        /// always-occupied starting houses (#63).</summary>
        private static GameState StateWithOneVacantZoneHouse()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(10_000);
            Assert.That(state.TryUnlockNextZone(), Is.True);
            var lot = state.UnlockedZones[0].Lots[0];
            Assert.That(state.TryBuildHouse(lot.HouseId), Is.True);
            Assert.That(state.Houses.Single(h => h.Id == lot.HouseId).IsVacant, Is.True,
                "a freshly built zone house starts vacant");
            return state;
        }

        [Test]
        public void CompletingAQuest_WithAVacantHouse_AndASuccessfulRoll_RaisesMoveInOccurred_WithTheHousehold()
        {
            // #436: the move-in was invisible because QuestManager.Complete
            // discarded HandleQuestCompleted's household return. It now raises
            // MoveInOccurred with exactly the newly moved-in household, from the
            // single completion funnel so all three quest types are covered.
            // Deterministic via the injected move-in RNG (a NextDouble of 0.0
            // clears the 5% base chance) over the single vacant house.
            var state = StateWithOneVacantZoneHouse();
            var manager = new QuestManager(state, new SequenceRandom(0.0));
            var dog = state.Dogs.First();
            var pest = manager.GiveQuestTo(dog, QuestType.PestControl, new System.Random(5));
            Assert.That(manager.Accept(pest), Is.True);

            IReadOnlyList<Dog> raised = null;
            var raisedCount = 0;
            manager.MoveInOccurred += household =>
            {
                raised = household;
                raisedCount++;
            };

            var dogsBefore = state.Dogs.Count;
            Assert.That(manager.SprayHouse(pest.TargetHouseId.Value), Is.True);

            Assert.That(raisedCount, Is.EqualTo(1), "the move-in event fires exactly once per move-in");
            Assert.That(raised, Is.Not.Empty, "the event carries the newly moved-in household");
            Assert.That(state.Dogs.Count, Is.GreaterThan(dogsBefore), "the household joined the live roster");
            Assert.That(raised, Is.EqualTo(state.Dogs.Skip(dogsBefore).ToList()),
                "the event carries exactly the dogs that just moved in");
        }

        [Test]
        public void CompletingAQuest_WhenTheMoveInRollFails_RaisesNoMoveInEvent()
        {
            // #436: a completion that produces no move-in (the pity roll fails)
            // must raise nothing — the event fires only on an actual move-in.
            var state = StateWithOneVacantZoneHouse();
            var manager = new QuestManager(state, new SequenceRandom(0.99));
            var dog = state.Dogs.First();
            var pest = manager.GiveQuestTo(dog, QuestType.PestControl, new System.Random(5));
            Assert.That(manager.Accept(pest), Is.True);

            var raisedCount = 0;
            manager.MoveInOccurred += _ => raisedCount++;

            var dogsBefore = state.Dogs.Count;
            Assert.That(manager.SprayHouse(pest.TargetHouseId.Value), Is.True);

            Assert.That(raisedCount, Is.EqualTo(0), "no move-in means no event");
            Assert.That(state.Dogs.Count, Is.EqualTo(dogsBefore), "the roster is unchanged when nothing moves in");
        }

        [Test]
        public void QuestsNeverExpire_AcrossAnyNumberOfRotations()
        {
            // #28: an active quest stays active until explicitly completed.
            var state = NewState();
            var quest = state.Quests.GiveQuestTo(state.Dogs[0], QuestType.LostItem, new System.Random(1));

            for (var day = 0; day < 50; day++)
            {
                state.Quests.StartNewDay(new System.Random(day));
            }

            Assert.That(quest.Status, Is.Not.EqualTo(QuestStatus.Completed));
            Assert.That(state.Quests.ActiveQuests.Select(q => q.Id), Does.Contain(quest.Id));
        }

        [Test]
        public void HousesAwaitingSpray_ListsOnlyAcceptedUncompletedPestHouses()
        {
            // #53/#157: the visible bug state on a house is driven by Core —
            // a house shows a bug swarm exactly while it holds an accepted,
            // not-yet-sprayed pest-control quest.
            var state = NewState();
            var buggedDog = state.Dogs[4]; // Pepper, house 3
            var pest = state.Quests.GiveQuestTo(buggedDog, QuestType.PestControl, new System.Random(5));

            // Given but not yet accepted -> not actionable, no swarm yet.
            Assert.That(state.Quests.HousesAwaitingSpray(), Does.Not.Contain(buggedDog.HouseId));

            state.Quests.Accept(pest);
            Assert.That(state.Quests.HousesAwaitingSpray(), Is.EqualTo(new[] { buggedDog.HouseId }));

            // Other quest types never register a bug house.
            state.Wallet.Deposit(100);
            var buy = state.Quests.GiveQuestTo(state.Dogs[1], QuestType.BuyGift, new System.Random(3));
            state.Quests.Accept(buy);
            Assert.That(state.Quests.HousesAwaitingSpray(), Is.EqualTo(new[] { buggedDog.HouseId }));

            // Once sprayed (completed) the house drops off the list.
            Assert.That(state.Quests.SprayHouse(buggedDog.HouseId), Is.True);
            Assert.That(state.Quests.HousesAwaitingSpray(), Is.Empty);
        }

        [Test]
        public void LostItemPool_ExactlyMatchesTheCatalogsLostEligibleItems()
        {
            // #190: pools are queries over the single tagged catalog, not a
            // hand-maintained parallel list.
            var state = NewState();
            var expected = ItemCatalog.NamesEligibleFor(ItemEligibility.Lost);
            var observed = new HashSet<string>();

            for (var seed = 0; seed < 200; seed++)
            {
                var quest = state.Quests.GiveQuestTo(state.Dogs[0], QuestType.LostItem, new Random(seed));
                observed.Add(quest.ItemName);
            }

            Assert.That(observed, Is.EquivalentTo(expected));
        }

        [Test]
        public void GiftPool_ExactlyMatchesTheCatalogsGiftEligibleItems()
        {
            // #190 + #317/#318: GiveQuestTo draws BuyGift subjects from the
            // population-gated pool, which at the starting population excludes
            // the Premium-tier fence — so the observed draws must equal that
            // gated pool, not the full Gift-eligible catalog slice.
            var state = NewState();
            var expected = new QuestPacingPolicy().EligibleSubjectPool(ItemEligibility.Gift, state);
            var observed = new HashSet<string>();

            for (var seed = 0; seed < 200; seed++)
            {
                var quest = state.Quests.GiveQuestTo(state.Dogs[1], QuestType.BuyGift, new Random(seed));
                observed.Add(quest.ItemName);
            }

            Assert.That(observed, Is.EquivalentTo(expected));
        }

        [Test]
        public void DecorationRequestOptions_ExactlyMatchTheCatalogsDecorationEligibleItems()
        {
            // #190: the generic decoration request offers the
            // Decoration-eligible catalog slice, no second parallel list.
            var state = NewState();
            var expected = ItemCatalog.NamesEligibleFor(ItemEligibility.Decoration);

            var quest = state.Quests.GiveQuestTo(state.Dogs[0], QuestType.DecorationRequest, new Random(1));

            Assert.That(quest.Options, Is.EquivalentTo(expected));
        }

        [Test]
        public void FindOnlyItems_AreNeverChosenForABuyGiftQuest()
        {
            // Find-only items (e.g. "puppy") carry no cost and must never be
            // selectable for a purchase-driven quest type.
            var state = NewState();

            for (var seed = 0; seed < 200; seed++)
            {
                var quest = state.Quests.GiveQuestTo(state.Dogs[1], QuestType.BuyGift, new Random(seed));
                Assert.That(quest.ItemName, Is.Not.EqualTo("puppy"));
            }
        }

        [Test]
        public void BuyGiftSubjects_AreDrawnFromThePopulationGatedPool()
        {
            // #317: BuyGift subjects come through the population-gated seam,
            // never a catalog entry outside the population-eligible bands.
            var state = NewState();
            var eligible = new QuestPacingPolicy()
                .EligibleSubjectPool(ItemEligibility.Gift, state);

            for (var seed = 0; seed < 200; seed++)
            {
                var quest = state.Quests.GiveQuestTo(state.Dogs[1], QuestType.BuyGift, new Random(seed));
                Assert.That(eligible, Does.Contain(quest.ItemName), $"seed {seed}");
            }
        }

        [Test]
        public void TierGate_LeavesLostItemAndPestControlSubjectsUntouched()
        {
            // #317: the cost-tier gate filters only purchasable subjects.
            // LostItem draws the full Lost pool — including the null-cost
            // "puppy" a cost filter would drop — and PestControl carries no
            // item cost at all, so neither is affected by population.
            var state = NewState();
            var lostSubjects = new HashSet<string>();
            for (var seed = 0; seed < 200; seed++)
            {
                lostSubjects.Add(
                    state.Quests.GiveQuestTo(state.Dogs[0], QuestType.LostItem, new Random(seed)).ItemName);
            }

            Assert.That(lostSubjects, Is.EquivalentTo(
                ItemCatalog.NamesEligibleFor(ItemEligibility.Lost)));
            Assert.That(lostSubjects, Does.Contain("puppy"),
                "null-cost find-only subject must remain — the gate is purchasable-only");

            var pest = state.Quests.GiveQuestTo(state.Dogs[2], QuestType.PestControl, new Random(7));
            Assert.That(pest.ItemName, Is.EqualTo("bug spray"));
            Assert.That(pest.Cost, Is.Null);
        }

        [Test]
        public void LostItem_ForAPuppyDog_NeverSelectsThePuppyItem()
        {
            // #463: a puppy dog must never be handed a lost-"puppy" quest
            // (a puppy losing its own puppy). The puppy subject is excluded
            // from the Lost pool for puppy receivers; toy/ball remain, so the
            // pool never empties.
            var state = NewState();
            var puppyDog = state.Dogs[1];
            Assert.That(puppyDog.IsPuppy, Is.True, "roster fixture: Dogs[1] is a puppy");

            var observed = new HashSet<string>();
            for (var seed = 0; seed < 200; seed++)
            {
                var quest = state.Quests.GiveQuestTo(puppyDog, QuestType.LostItem, new Random(seed));
                Assert.That(quest.ItemName, Is.Not.EqualTo(ItemCatalog.PuppyItemName),
                    $"seed {seed}: puppy dog assigned a lost-puppy quest");
                observed.Add(quest.ItemName);
            }

            Assert.That(observed, Is.EquivalentTo(
                ItemCatalog.NamesEligibleFor(ItemEligibility.Lost)
                    .Where(n => n != ItemCatalog.PuppyItemName)),
                "the puppy-excluded pool (toy/ball) is still fully drawable");
        }

        [Test]
        public void LostItem_ForANonPuppyDog_CanStillSelectThePuppyItem()
        {
            // #463 over-filter guard: the exclusion is scoped to puppy
            // receivers only — a non-puppy dog can still lose a puppy.
            var state = NewState();
            var adultDog = state.Dogs[0];
            Assert.That(adultDog.IsPuppy, Is.False, "roster fixture: Dogs[0] is not a puppy");

            var observed = new HashSet<string>();
            for (var seed = 0; seed < 200; seed++)
            {
                observed.Add(
                    state.Quests.GiveQuestTo(adultDog, QuestType.LostItem, new Random(seed)).ItemName);
            }

            Assert.That(observed, Does.Contain(ItemCatalog.PuppyItemName),
                "non-puppy receiver must still be able to draw the puppy subject");
        }

        [Test]
        public void NoParallelItemArrays_RemainOnQuestManager()
        {
            // #190 guard: LostItems/GiftItems/DecorationItems are deleted —
            // pools must be queries over ItemCatalog, not hand-kept lists.
            // (Invariant.)
            var forbidden = new[] { "LostItems", "GiftItems", "DecorationItems" };

            var offenders = typeof(QuestManager)
                .GetFields(BindingFlags.NonPublic | BindingFlags.Public
                    | BindingFlags.Static | BindingFlags.Instance)
                .Select(f => f.Name)
                .Where(name => forbidden.Contains(name))
                .ToList();

            Assert.That(offenders, Is.Empty);
        }

        [Test]
        public void LostItem_HiddenPositionClearsEveryHouseFootprintByBuffer()
        {
            // #290: a lost toy must never spawn within HouseClearanceBuffer
            // of any house footprint, or the house geometry/collider
            // occludes the tap and the item is unreachable.
            for (var seed = 0; seed < 500; seed++)
            {
                var state = NewState();
                var quest = state.Quests.GiveQuestTo(
                    state.Dogs[0], QuestType.LostItem, new Random(seed));
                var pos = quest.HiddenItemPosition.Value;

                foreach (var lot in NeighborhoodLayout.HouseLots)
                {
                    var footprint = HousePlacement.HouseFootprint(lot);
                    Assert.That(footprint.DistanceTo(pos),
                        Is.GreaterThanOrEqualTo(QuestManager.HouseClearanceBuffer),
                        $"seed {seed}: lost item {pos} too close to house "
                        + $"{lot.HouseId} footprint");
                }
            }
        }

        [Test]
        public void LostItem_HiddenPositionStaysWithinExtentBounds()
        {
            // #290: rejection sampling must always terminate with a valid
            // position inside the existing placement bounds.
            for (var seed = 0; seed < 500; seed++)
            {
                var state = NewState();
                var pos = state.Quests
                    .GiveQuestTo(state.Dogs[0], QuestType.LostItem, new Random(seed))
                    .HiddenItemPosition.Value;

                Assert.That(Math.Abs(pos.X),
                    Is.LessThanOrEqualTo(QuestManager.HiddenItemExtent), $"seed {seed}");
                Assert.That(Math.Abs(pos.Z),
                    Is.LessThanOrEqualTo(QuestManager.HiddenItemExtent), $"seed {seed}");
            }
        }

        [Test]
        public void LostItem_HiddenPositionIsDeterministicPerSeed()
        {
            // #290: placement stays deterministic per quest/seed even with
            // rejection sampling in the loop.
            for (var seed = 0; seed < 50; seed++)
            {
                var first = NewState()
                    .Quests.GiveQuestTo(NewState().Dogs[0], QuestType.LostItem, new Random(seed));
                var second = NewState()
                    .Quests.GiveQuestTo(NewState().Dogs[0], QuestType.LostItem, new Random(seed));

                Assert.That(first.HiddenItemPosition.Value,
                    Is.EqualTo(second.HiddenItemPosition.Value), $"seed {seed}");
            }
        }

        /// <summary>#318: obtains a real fence BuyGift quest through the
        /// production path — a state at the premium population gate (so the
        /// fence enters the Gift subject pool) with a funded wallet, drawing
        /// BuyGift quests until the RNG yields the fence subject. Returns the
        /// state, the fence quest, and its (roster) dog.</summary>
        private static (GameState State, Quest Quest, Dog Dog) ReadyFenceQuest()
        {
            for (var seed = 0; seed < 500; seed++)
            {
                var state = NewState();
                for (var i = state.Dogs.Count; i < QuestCostTiers.PremiumPopulationGate; i++)
                {
                    state.AddDog(new Dog($"extra-{i}", Breed.GermanShepherd, Personality.Brave, 1, false));
                }

                state.Wallet.Deposit(1000);
                var dog = state.Dogs[0];
                var quest = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new System.Random(seed));
                if (quest.ItemName == ItemCatalog.FenceItemName)
                {
                    return (state, quest, dog);
                }
            }

            throw new InvalidOperationException("No fence BuyGift quest produced across seeds.");
        }

        [Test]
        public void AcceptingFenceQuest_DeductsCost_CompletesImmediately_WithNoDeliveryLeg()
        {
            // #318: the fence has no delivery-truck flow — accepting deducts the
            // 100-coin cost and completes the quest right away, never entering
            // the HeadingHome/WaitingForDelivery delivery phases, while still
            // paying the flat quest payout.
            var (state, quest, dog) = ReadyFenceQuest();
            var coinsBefore = state.Wallet.Coins;
            var fenceCost = ItemCatalog.Get(ItemCatalog.FenceItemName).Cost.Value;

            var accepted = state.Quests.Accept(quest);

            Assert.That(accepted, Is.True);
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Completed));
            Assert.That(quest.DeliveryPhase, Is.EqualTo(DeliveryPhase.None),
                "the fence skips the delivery legs entirely");
            Assert.That(dog.HasActiveQuest, Is.False);
            Assert.That(state.Wallet.Coins,
                Is.EqualTo(coinsBefore - fenceCost + EconomyNumbers.QuestPayout));
        }

        [Test]
        public void CompletingFenceQuest_RecordsPersistentPlacedItem_SurvivingSaveLoad()
        {
            // #318: completion records a PlacedItem(houseId, "fence") — permanent
            // world state that round-trips through SaveCodec, reusing the same
            // persistence mechanism as any delivered BuyGift item.
            var (state, quest, dog) = ReadyFenceQuest();
            var houseId = dog.HouseId;

            state.Quests.Accept(quest);

            Assert.That(state.PlacedItems.Any(p => p.HouseId == houseId
                && p.ItemName == ItemCatalog.FenceItemName), Is.True,
                "the fence is recorded as a placed item on completion");

            var reloaded = SaveCodec.Load(SaveCodec.Save(state));
            Assert.That(reloaded.PlacedItems.Any(p => p.HouseId == houseId
                && p.ItemName == ItemCatalog.FenceItemName), Is.True,
                "the placed fence survives a save/load round-trip");
        }

        [Test]
        public void QuestSchema_HasNoExpiryOrFailFields()
        {
            // #28: structurally no timers/fail states. (Invariant guard.)
            var forbidden = new[] { "expir", "fail", "timer", "deadline", "timeout" };

            var offenders = typeof(Quest).GetProperties()
                .Where(p => forbidden.Any(f => p.Name.ToLowerInvariant().Contains(f)))
                .Select(p => p.Name)
                .ToList();

            Assert.That(offenders, Is.Empty);
        }
    }
}
