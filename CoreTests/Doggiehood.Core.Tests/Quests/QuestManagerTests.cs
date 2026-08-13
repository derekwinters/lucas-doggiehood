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
            // #543: quests trickle in hourly, so a full pacing window of
            // boundaries fills the neighborhood up to its population target.
            var state = NewState();

            Assert.That(state.Quests.ActiveQuests, Is.Empty);

            for (var hour = 0; hour < EconomyNumbers.PacingWindowHours; hour++)
            {
                state.Quests.StartNewDay(new System.Random(1 + hour));
            }

            var target = new QuestPacingPolicy().TargetActiveCount(state);
            Assert.That(state.Quests.ActiveQuests.Count(), Is.EqualTo(target));
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
            // #312/#543: once onboarding is complete the seam is just the normal
            // hourly trickle rotation — no more single-lost-item suppression. A
            // 3.0/hr population (100 dogs) adds the per-hour trickle amount (3),
            // distinguishing the rotation from the pre-onboarding single seed.
            for (var seed = 0; seed < 10; seed++)
            {
                var state = NewState();
                for (var i = state.Dogs.Count; i < 100; i++)
                {
                    state.AddDog(new Dog($"extra-{i}", Breed.GermanShepherd, Personality.Brave, 1, false));
                }

                state.MarkOnboardingComplete();

                state.Quests.BeginInitialQuests(new System.Random(seed));

                // #624: perHour = 12/4 = 3, so one trickle tick assigns exactly 3 dogs.
                Assert.That(state.Dogs.Count(d => d.HasActiveQuest), Is.EqualTo(3), $"seed {seed}");
            }
        }

        [Test]
        public void NewDay_TricklesQuestsUpTowardTheTarget()
        {
            // #26/#543/#624: the rotation trickles quests in hourly (target/4 per
            // hour) rather than a 2-4 batch; over a full pacing window it fills
            // the neighborhood up to its population target, and no single hour
            // ever floods more than one hour's worth (ceil of the per-hour rate)
            // at the 1.25/hr floor rate.
            for (var seed = 0; seed < 10; seed++)
            {
                var state = NewState();
                var pacing = new QuestPacingPolicy();
                var target = pacing.TargetActiveCount(state);
                var maxPerHour = (int)Math.Ceiling(pacing.PerHourRate(state));

                for (var hour = 0; hour < EconomyNumbers.PacingWindowHours; hour++)
                {
                    var before = state.Dogs.Count(d => d.HasActiveQuest);
                    state.Quests.StartNewDay(new System.Random(seed * 100 + hour));
                    var added = state.Dogs.Count(d => d.HasActiveQuest) - before;
                    Assert.That(added, Is.LessThanOrEqualTo(maxPerHour),
                        $"seed {seed}: never a catch-up flood beyond one hour's trickle");
                }

                Assert.That(state.Dogs.Count(d => d.HasActiveQuest), Is.EqualTo(target), $"seed {seed}");
            }
        }

        [Test]
        public void ReleaseInitialRotation_SeedsToTarget_NeverExceedingTargetOrFreeDogs()
        {
            // #579: the onboarding-completion release seeds an immediate batch up
            // to TargetActiveCount in one shot (skipping the hourly-trickle
            // throttle), but still honors the population/headroom cap and the
            // free-dog cap — min(target, target - active, freeDogs) — as dog
            // population and pre-existing active quests vary. It never exceeds the
            // target and never assigns more than the available free dogs.
            var pacing = new QuestPacingPolicy();
            for (var dogCount = 4; dogCount <= 40; dogCount += 6)
            {
                for (var preExisting = 0; preExisting <= 3; preExisting++)
                {
                    for (var seed = 0; seed < 8; seed++)
                    {
                        var state = NewState();
                        for (var i = state.Dogs.Count; i < dogCount; i++)
                        {
                            state.AddDog(new Dog($"extra-{i}", Breed.GermanShepherd,
                                Personality.Brave, 1, false));
                        }

                        var manager = new QuestManager(state, new System.Random(seed));
                        var freeDogs = state.Dogs.Where(d => !d.HasActiveQuest).ToList();
                        var seededExisting = Math.Min(preExisting, freeDogs.Count);
                        for (var i = 0; i < seededExisting; i++)
                        {
                            manager.GiveQuestTo(freeDogs[i], QuestType.LostItem, new System.Random(seed));
                        }

                        var target = pacing.TargetActiveCount(state);
                        var freeBefore = state.Dogs.Count(d => !d.HasActiveQuest);
                        var activeBefore = manager.ActiveQuests.Count();

                        manager.ReleaseInitialRotation();

                        var activeAfter = manager.ActiveQuests.Count();
                        var added = activeAfter - activeBefore;
                        Assert.That(activeAfter, Is.LessThanOrEqualTo(target),
                            $"dogs {dogCount}, pre {preExisting}, seed {seed}: seed exceeded target");
                        Assert.That(added, Is.LessThanOrEqualTo(freeBefore),
                            $"dogs {dogCount}, pre {preExisting}, seed {seed}: seed exceeded free-dog count");
                        Assert.That(added, Is.EqualTo(Math.Min(target - activeBefore, freeBefore)),
                            $"dogs {dogCount}, pre {preExisting}, seed {seed}: seed fills exactly to the cap");
                        Assert.That(manager.ActiveQuests.Select(q => q.DogName).Distinct().Count(),
                            Is.EqualTo(activeAfter),
                            $"dogs {dogCount}, pre {preExisting}, seed {seed}: no dog double-booked");
                    }
                }
            }
        }

        [Test]
        public void ReleaseInitialRotation_OnACoinStarvedSave_NeverSeedsAnAllPaidSet()
        {
            // #579: the immediate release seed still enforces the always-one-free-
            // quest guarantee (#310) — a brand-new save at 0 coins must never be
            // released into an all-paid (BuyGift-only) active set, or the player
            // is soft-locked with nothing affordable to do.
            for (var seed = 0; seed < 50; seed++)
            {
                var state = NewState();
                Assert.That(state.Wallet.Coins, Is.EqualTo(0), $"seed {seed}: coin-starved save");

                var manager = new QuestManager(state, new System.Random(seed));
                manager.ReleaseInitialRotation();

                var active = manager.ActiveQuests.ToList();
                Assert.That(active, Is.Not.Empty, $"seed {seed}: release seeds a non-empty board");
                Assert.That(active.Any(q => q.Type == QuestType.LostItem
                    || q.Type == QuestType.PestControl), Is.True,
                    $"seed {seed}: the released seed always includes a free quest");
            }
        }

        [Test]
        public void ReleaseInitialRotation_LeavesTheAccumulatorAndRotationClockUntouched()
        {
            // #579: the release itself skips the accumulator throttle and does not
            // stamp the rotation clock — that stays with the recurring #543 path
            // (StartNewDay / MaybeStartNewDay), so the very next hourly boundary
            // continues the normal trickle cleanly from the now-populated set.
            var state = NewState();
            var manager = new QuestManager(state, new System.Random(1));

            manager.ReleaseInitialRotation();

            Assert.That(state.QuestPacingAccumulator, Is.EqualTo(0d),
                "the release does not advance the pacing accumulator");
            Assert.That(state.LastRotationUtc, Is.Null,
                "the release does not stamp the rotation clock");
        }

        [Test]
        public void NewDay_NeverOverwritesAnUncompletedQuest()
        {
            // #26 precedence rule: a dog holding an uncompleted quest keeps
            // it; the rotation only assigns to quest-free dogs.
            // #543: fill the neighborhood over a pacing window first, since a
            // single hourly tick may trickle in nothing.
            var state = NewState();
            for (var hour = 0; hour < EconomyNumbers.PacingWindowHours; hour++)
            {
                state.Quests.StartNewDay(new System.Random(1 + hour));
            }

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
        public void CompletingEachQuestType_PaysItsPayout()
        {
            // #23/#24/#62/#626 + integration: full loop for each quest type.
            // Free types pay the flat payout; paid types pay cost × markup.
            var state = NewState();
            state.Wallet.Deposit(1000); // funds for the BuyGift acceptance cost

            // Lost item (free type): accept, then tap the hidden position (#12, #31).
            var lost = state.Quests.GiveQuestTo(state.Dogs[0], QuestType.LostItem, new System.Random(1));
            var before = state.Wallet.Coins;
            state.Quests.Accept(lost);
            Assert.That(state.Quests.TapWorldPosition(lost.HiddenItemPosition.Value), Is.True);
            Assert.That(lost.Status, Is.EqualTo(QuestStatus.Completed));
            Assert.That(state.Wallet.Coins, Is.EqualTo(before + Doggiehood.Core.Economy.EconomyNumbers.QuestPayout));

            // Buy gift (paid earner): accept deducts cost; payout only after delivery (#13, #30).
            var buy = state.Quests.GiveQuestTo(state.Dogs[1], QuestType.BuyGift, new System.Random(2));
            before = state.Wallet.Coins;
            state.Quests.Accept(buy);
            Assert.That(state.Wallet.Coins, Is.EqualTo(before - buy.Cost.Value));
            state.Quests.NotifyDogArrivedHome(buy);
            state.Quests.DeliverPackage(buy);
            Assert.That(buy.Status, Is.EqualTo(QuestStatus.Completed));
            Assert.That(state.Wallet.Coins,
                Is.EqualTo(before - buy.Cost.Value
                    + Doggiehood.Core.Economy.EconomyNumbers.PaidQuestPayout(buy.Cost.Value)));

            // Pest control (free type): spray the right house (#53).
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
            state.SetTargetMap(Doggiehood.Core.Tests.World.FrontierTestWorld.LoadAuthoredTargetMap());
            state.Wallet.Deposit(10_000);
            Assert.That(state.TryUnlockTile(Doggiehood.Core.Tests.World.FrontierTestWorld.FirstTile), Is.True);
            var lot = state.LotsForUnlockedTile(Doggiehood.Core.Tests.World.FrontierTestWorld.FirstTile)[0];
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
        public void LostItem_HiddenPositionClearsTheQuestDogsHouseFootprintByBuffer()
        {
            // #290/#520: a lost toy must never spawn within HouseClearanceBuffer
            // of the quest dog's own house footprint, or the house geometry/
            // collider occludes the tap and the item is unreachable.
            for (var seed = 0; seed < 500; seed++)
            {
                var state = NewState();
                var dog = state.Dogs[0];
                var pos = state.Quests
                    .GiveQuestTo(dog, QuestType.LostItem, new Random(seed))
                    .HiddenItemPosition.Value;

                var footprint = HousePlacement.HouseFootprint(state.GetHouseLot(dog.HouseId));
                Assert.That(footprint.DistanceTo(pos),
                    Is.GreaterThanOrEqualTo(QuestManager.HouseClearanceBuffer),
                    $"seed {seed}: lost item {pos} too close to the dog's house footprint");
            }
        }

        [Test]
        public void LostItem_HiddenPositionStaysWithinTheQuestDogsTileBounds()
        {
            // #520: rejection sampling must always terminate with a valid
            // position inside the quest dog's own home-tile quadrant bounds —
            // the starting-layout regression, mirroring the frontier-tile case.
            for (var seed = 0; seed < 500; seed++)
            {
                var state = NewState();
                var dog = state.Dogs[0];
                var pos = state.Quests
                    .GiveQuestTo(dog, QuestType.LostItem, new Random(seed))
                    .HiddenItemPosition.Value;

                var bounds = LotBounds.QuadrantBounds(state.GetHouseLot(dog.HouseId));
                Assert.That(bounds.Contains(pos), Is.True,
                    $"seed {seed}: lost item {pos} landed outside the quest dog's "
                    + $"tile bounds [{bounds.MinX}..{bounds.MaxX}]x[{bounds.MinZ}..{bounds.MaxZ}]");
            }
        }

        [Test]
        public void LostItem_ForADogOnANonOriginTile_HidesItemWithinThatDogsTileBounds()
        {
            // #520: the hidden item must land on the quest dog's OWN home
            // tile, not the fixed origin-centered spawn square. A dog whose
            // house sits on an unlocked frontier tile (well off the origin)
            // gets its lost item within that lot's quadrant bounds — and
            // clear of that lot's own house footprint by the buffer.
            var state = Doggiehood.Core.Tests.World.FrontierTestWorld.WithFirstTileUnlocked();
            var lot = state.LotsForUnlockedTile(
                Doggiehood.Core.Tests.World.FrontierTestWorld.FirstTile)[0];
            var dog = new Dog("Rover", Breed.Beagle, Personality.Brave, lot.HouseId, isPuppy: false);
            var bounds = LotBounds.QuadrantBounds(lot);
            var footprint = HousePlacement.HouseFootprint(lot);

            for (var seed = 0; seed < 200; seed++)
            {
                var pos = state.Quests
                    .GiveQuestTo(dog, QuestType.LostItem, new Random(seed))
                    .HiddenItemPosition.Value;

                Assert.That(bounds.Contains(pos), Is.True,
                    $"seed {seed}: lost item {pos} landed outside the quest dog's "
                    + $"tile bounds [{bounds.MinX}..{bounds.MaxX}]x[{bounds.MinZ}..{bounds.MaxZ}]");
                Assert.That(footprint.DistanceTo(pos),
                    Is.GreaterThanOrEqualTo(QuestManager.HouseClearanceBuffer),
                    $"seed {seed}: lost item {pos} too close to the dog's house footprint");
            }
        }

        [Test]
        public void LostItem_HiddenPositionClearsTheStreetCorridor_OnTheOriginTile()
        {
            // #606: a lost item (incl. the lost puppy) must never spawn in the
            // road corridor — the paved road, grass verge, or sidewalk — or it
            // collides with the on-road delivery truck (#538). Each origin lot
            // is one FourWay quadrant bordering TWO roads (X=0 and Z=0); the
            // sampled position must clear the StreetCorridorInset (road
            // half-width + verge + sidewalk) of every road bordering the lot,
            // across all quadrants and many seeds.
            var state = NewState();
            foreach (var dog in state.Dogs)
            {
                var lot = state.GetHouseLot(dog.HouseId);
                var roads = RoadsBorderingLot(state, lot);
                for (var seed = 0; seed < 200; seed++)
                {
                    var pos = state.Quests
                        .GiveQuestTo(dog, QuestType.LostItem, new Random(seed))
                        .HiddenItemPosition.Value;
                    AssertClearsStreetCorridor(pos, roads, dog.HouseId, seed);
                }
            }
        }

        [Test]
        public void LostItem_HiddenPositionClearsTheStreetCorridor_OnAZoneTile()
        {
            // #606/#455: the road-corridor exclusion must be tile-aware — a
            // zone lot borders its OWN tile's road (the cul-de-sac arm), not
            // only the origin FourWay streets. The lost item on a freshly
            // unlocked frontier lot must clear that tile's street corridor too.
            var state = Doggiehood.Core.Tests.World.FrontierTestWorld.WithFirstTileUnlocked();
            var lot = state.LotsForUnlockedTile(
                Doggiehood.Core.Tests.World.FrontierTestWorld.FirstTile)[0];
            var dog = new Dog("Rover", Breed.Beagle, Personality.Brave, lot.HouseId, isPuppy: false);
            var roads = RoadsBorderingLot(state, lot);

            for (var seed = 0; seed < 200; seed++)
            {
                var pos = state.Quests
                    .GiveQuestTo(dog, QuestType.LostItem, new Random(seed))
                    .HiddenItemPosition.Value;
                AssertClearsStreetCorridor(pos, roads, dog.HouseId, seed);
            }
        }

        private static IReadOnlyList<Road> RoadsBorderingLot(GameState state, HouseLot lot)
        {
            var tileType = state.Map.GetTileAt(LotBounds.NearestTileCoordinate(lot.Position));
            return LotBounds.RoadsFor(lot, tileType);
        }

        private static void AssertClearsStreetCorridor(
            GridPoint pos, IReadOnlyList<Road> roads, int houseId, int seed)
        {
            foreach (var road in roads)
            {
                if (road.Orientation == StreetOrientation.NorthSouth)
                {
                    // Centerline at constant X, running along Z over its extent.
                    if (pos.Z < road.Center.Z - road.HalfLength
                        || pos.Z > road.Center.Z + road.HalfLength)
                    {
                        continue;
                    }

                    Assert.That(Math.Abs(pos.X - road.Center.X),
                        Is.GreaterThanOrEqualTo(LotBounds.StreetCorridorInset),
                        $"house {houseId} (seed {seed}): {pos} sits in the "
                        + "north-south road's street corridor");
                }
                else
                {
                    // Centerline at constant Z, running along X over its extent.
                    if (pos.X < road.Center.X - road.HalfLength
                        || pos.X > road.Center.X + road.HalfLength)
                    {
                        continue;
                    }

                    Assert.That(Math.Abs(pos.Z - road.Center.Z),
                        Is.GreaterThanOrEqualTo(LotBounds.StreetCorridorInset),
                        $"house {houseId} (seed {seed}): {pos} sits in the "
                        + "east-west road's street corridor");
                }
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
            // the HeadingHome/WaitingForDelivery delivery phases. #626: as a paid
            // (Gift-tagged) job it pays back cost × markup (100 -> 150), not the
            // flat free-type payout.
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
                Is.EqualTo(coinsBefore - fenceCost + EconomyNumbers.PaidQuestPayout(fenceCost)));
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
        public void FenceQuestDialogue_NeverPromisesADeliveryTruckOrAWalkHome()
        {
            // #701: the fence is the one Gift subject with no delivery leg
            // (#318) — accepting installs it in place. Its dialogue is baked at
            // give time, so it must be selected by subject, not just by type:
            // a quest's dialogue never promises a mechanic that quest doesn't
            // run (docs/specs/quests/quest-content.md).
            var (_, quest, _) = ReadyFenceQuest();

            var dialogue = string.Join("\n", quest.DialogueLines).ToLowerInvariant();

            Assert.That(dialogue, Does.Not.Contain("delivery truck"),
                $"the fence has no delivery truck, but said: {dialogue}");
            Assert.That(dialogue, Does.Not.Contain("head home"),
                $"the fence has no walk-home leg, but said: {dialogue}");
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
