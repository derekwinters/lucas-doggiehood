using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Onboarding;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Quests
{
    /// <summary>
    /// Owns quest instances and the daily rotation (#23, #26). The sole
    /// place coins are deposited — completion pays the flat payout (#24) —
    /// and the arbiter of each quest type's completion rule: tap the hidden
    /// item (#31), delivery after the dog sits waiting (#30), spray the
    /// right house (#53). Quests never expire (#28).
    /// </summary>
    public sealed class QuestManager
    {
        private const float LostItemTapRadius = 1.5f;

        /// <summary>#290: minimum clearance a lost toy keeps from the quest
        /// dog's house footprint, so the house geometry/collider never
        /// occludes the tap. Tunable placeholder (no spec pins a number):
        /// 2.0m clears the full <see cref="LostItemTapRadius"/> (1.5m) with
        /// margin, so the entire tap hit-radius lands off the footprint — not
        /// just the point. Named Core constant per #161.</summary>
        public const float HouseClearanceBuffer = 2f;

        /// <summary>#290: rejection-sampling attempt budget when placing a
        /// lost item clear of the quest dog's house footprint. The clear area
        /// is the overwhelming majority of that lot's quadrant bounds, so a
        /// valid point is found almost immediately; this cap only guarantees
        /// the loop always terminates.</summary>
        private const int MaxHiddenItemPlacementAttempts = 64;

        private static readonly QuestType[] RotationTypes =
        {
            QuestType.LostItem,
            QuestType.BuyGift,
            QuestType.PestControl,
        };

        /// <summary>#310: the no-cost quest types — accepting them never
        /// spends coins. The always-one-free-quest invariant draws from these
        /// so the player is never dead-ended at 0 coins. Confirmed against
        /// <see cref="Quest"/> (cost is carried only by BuyGift /
        /// DecorationRequest).</summary>
        private static readonly QuestType[] FreeQuestTypes =
        {
            QuestType.LostItem,
            QuestType.PestControl,
        };

        /// <summary>#436: raised from the single <see cref="Complete"/> funnel
        /// whenever a completed quest's move-in roll actually fills a vacant
        /// house — carrying the newly moved-in household (never empty). The
        /// Unity layer subscribes to reflect the move-in on screen the moment it
        /// happens (spawn the new dog(s), drop the filled house's vacancy tint);
        /// Core has already added the household to the roster and flipped the
        /// house occupied by the time this fires. All three completion paths
        /// (delivery, lost-item find, spray) route through <see cref="Complete"/>,
        /// so every one is covered.</summary>
        public event Action<IReadOnlyList<Dog>> MoveInOccurred;

        /// <summary>#541: raised from the single <see cref="Complete"/> funnel
        /// exactly once each time a quest completes — carrying the just-completed
        /// <see cref="Quest"/> and the flat payout deposited
        /// (<see cref="EconomyNumbers.QuestPayout"/>). Every completion path
        /// (delivery, lost-item find, spray) routes through <see cref="Complete"/>,
        /// so every one is covered. The thin Unity layer subscribes to raise the
        /// completion toast ("Quest complete! +N coins"); Core still owns the
        /// payout and never re-pays. No copy is carried here (engine-free Core):
        /// the event names only the quest + amount, and the Unity layer owns the
        /// message assembly.</summary>
        public event Action<Quest, int> QuestCompleted;

        private readonly GameState state;
        private readonly Random moveInRng;
        private readonly QuestPacingPolicy pacing = new QuestPacingPolicy();
        private readonly List<Quest> quests = new List<Quest>();
        private int nextQuestId = 1;

        public QuestManager(GameState state)
            : this(state, new Random())
        {
        }

        /// <summary>#58: the move-in pity-counter roll (docs/specs/expansion.md
        /// "RNG injectable for deterministic tests") needs a Random on
        /// every quest completion, but Complete() has no caller-supplied
        /// one to thread through (unlike StartNewDay's explicit parameter)
        /// — QuestManager owns it instead, defaulting to a real Random so
        /// production callers need no changes, with this overload for
        /// deterministic tests.</summary>
        public QuestManager(GameState state, Random moveInRng)
        {
            this.state = state;
            this.moveInRng = moveInRng;
        }

        public IEnumerable<Quest> ActiveQuests
        {
            get { return quests.Where(q => q.Status != QuestStatus.Completed); }
        }

        /// <summary>#312: first-launch quest seeding. Until onboarding
        /// completes, exactly one quest-free dog is seeded with a single easy
        /// <see cref="QuestType.LostItem"/> quest and the hourly trickle rotation
        /// is suppressed, so the tutorial has one gentle tap-to-find target
        /// and nothing else competes. Once onboarding is complete this is just
        /// the normal <see cref="StartNewDay"/> rotation. RNG is injectable
        /// for deterministic tests, matching <see cref="StartNewDay"/>.</summary>
        public void BeginInitialQuests(Random rng)
        {
            if (state.OnboardingComplete)
            {
                StartNewDay(rng);
                return;
            }

            var freeDogs = state.Dogs.Where(d => !d.HasActiveQuest).ToList();
            if (freeDogs.Count == 0)
            {
                return;
            }

            var dog = freeDogs[rng.Next(freeDogs.Count)];
            GiveQuestTo(dog, QuestType.LostItem, rng);
        }

        /// <summary>#316: the single launch-time quest-seeding decision the
        /// thin Unity bootstrap defers to, so no phase logic lives in the
        /// MonoBehaviour. Pre-chain (still on the first guided step, with no
        /// active quests) seeds the one tutorial quest; mid-chain (the guided
        /// upgrade/expand/build steps) stays suppressed; post-chain runs the
        /// #310/#543 recurring hourly trickle refresh. <paramref name="nowUtc"/> is a UTC instant
        /// (<c>DateTime.UtcNow</c> in production).</summary>
        public void EnsureQuestsForLaunch(DateTime nowUtc, Random rng)
        {
            if (state.RewardChain.IsComplete)
            {
                MaybeStartNewDay(nowUtc, rng);
                return;
            }

            if (state.RewardChain.CurrentStep == OnboardingRewardStep.FirstQuest
                && !ActiveQuests.Any())
            {
                BeginInitialQuests(rng);
            }

            // Mid-chain (steps 2-4): the guided upgrade/expand/build actions are
            // not quests, so the rotation stays suppressed until the chain
            // completes at the build step and releases it.
        }

        /// <summary>#316/#579: releases the onboarding reward-chain rotation
        /// suppression by seeding the first batch exactly once, when the 4-step
        /// chain completes at the build step. Unlike the recurring hourly
        /// trickle, this seeds an IMMEDIATE batch up to
        /// <see cref="QuestPacingPolicy.TargetActiveCount"/> — skipping the
        /// per-hour accumulator throttle for this one release event — so the
        /// player finishes onboarding to a populated board (2-3 ready dogs) and
        /// never an empty one that trickles up "over the following hours" (#579).
        /// The headroom cap, free-dog cap, and always-one-free-quest invariant
        /// still apply, so the seed can't exceed the target, double-book a dog,
        /// or leave an all-paid set. The pacing accumulator and
        /// <see cref="GameState.LastRotationUtc"/> are left untouched here, so the
        /// next hourly boundary continues the normal #310/#543 trickle cleanly
        /// from the now-populated set (headroom is already met, so it adds
        /// nothing extra). Uses the manager's own RNG since the build entry point
        /// has no caller-supplied one.</summary>
        public void ReleaseInitialRotation()
        {
            SeedBatch(pacing.TargetActiveCount(state), moveInRng);
        }

        /// <summary>#310/#543: the recurring refresh boundary. Asks
        /// <see cref="QuestPacingPolicy.ShouldRefresh"/> whether the hourly UTC
        /// cadence has been crossed and, if so, runs one <see cref="StartNewDay"/>
        /// trickle top-up and records the instant. Purely a boundary
        /// <em>check</em> — nothing is removed and no quest can fail (economy.md
        /// #28). Elapsed time only decides <em>whether</em> to refresh, never
        /// <em>how many</em> to add: away 1 hour or 4 days is one top-up (the
        /// accumulator advances a single hour's worth, never per missed hour), so
        /// there is no catch-up flood. <paramref name="nowUtc"/> is a UTC instant
        /// (<c>DateTime.UtcNow</c> in production).</summary>
        public void MaybeStartNewDay(DateTime nowUtc, Random rng)
        {
            if (!pacing.ShouldRefresh(nowUtc, state))
            {
                return;
            }

            StartNewDay(rng);
            state.RecordRotationUtc(nowUtc);
        }

        /// <summary>#457: the Debug-tab "Refresh quests now" seam. Runs the same
        /// <see cref="StartNewDay"/> top-up and rotation-timestamp record as
        /// <see cref="MaybeStartNewDay"/>, but <em>unconditionally</em> — skipping
        /// the <see cref="QuestPacingPolicy.ShouldRefresh"/> cadence gate — so a
        /// tester can trigger the new-quest randomization without waiting out the
        /// hourly timer. Recording <paramref name="nowUtc"/> also restarts that
        /// hourly window, so a forced refresh matches a natural one exactly except for
        /// <em>when</em> it is allowed to fire. Still purely additive (headroom-
        /// bounded, never removing or failing a quest — economy.md #28).
        /// <paramref name="nowUtc"/> is a UTC instant (<c>DateTime.UtcNow</c> in
        /// production).</summary>
        public void ForceRefresh(DateTime nowUtc, Random rng)
        {
            StartNewDay(rng);
            state.RecordRotationUtc(nowUtc);
        }

        /// <summary>Hourly trickle top-up (#26, #310, #543): tops up toward the
        /// pacing policy's population-scaled
        /// <see cref="QuestPacingPolicy.TargetActiveCount"/> by the per-hour
        /// error-diffusion amount — adds <c>min(wholeThisHour, target −
        /// activeCount, freeDogs)</c>, floored at 0, where
        /// <c>wholeThisHour = floor(accumulator + perHourRate)</c>
        /// (<see cref="QuestPacingPolicy.AdvanceAccumulator"/>). The leftover
        /// fraction is persisted on <see cref="GameState"/> <em>immediately</em> —
        /// regardless of the downstream headroom/free-dog clamp — so fractional
        /// progress is never lost and a quiet hour at the cap can never bank a
        /// flood. Once the neighborhood already holds the target number of
        /// uncompleted quests a top-up adds nothing. Dogs holding an uncompleted
        /// quest are never overwritten. Enforces the always-one-free-quest
        /// invariant (#310): if the top-up would leave an all-paid active set,
        /// one added quest is forced to a free type.</summary>
        public void StartNewDay(Random rng)
        {
            var wholeThisHour = pacing.AdvanceAccumulator(
                state.QuestPacingAccumulator, state, out var remainder);
            // Persist the carried fraction up front so it survives even when the
            // clamp below reduces the actual add (the accumulator never banks a
            // flood — the remainder is always < 1).
            state.RecordQuestPacingAccumulator(remainder);
            SeedBatch(wholeThisHour, rng);
        }

        /// <summary>#579: the shared batch-assignment step behind both the
        /// recurring hourly trickle (<see cref="StartNewDay"/>, which requests the
        /// accumulator's <c>wholeThisHour</c>) and the one-time onboarding release
        /// (<see cref="ReleaseInitialRotation"/>, which requests
        /// <see cref="QuestPacingPolicy.TargetActiveCount"/> directly). Adds up to
        /// <c>min(requested, target − activeCount, freeDogs)</c> quests, floored at
        /// 0: the population/headroom cap and free-dog cap are enforced here — not
        /// in the caller — so no path can exceed the target or double-book a dog.
        /// Dogs holding an uncompleted quest are never overwritten. Enforces the
        /// always-one-free-quest invariant (#310): if the batch would leave an
        /// all-paid active set, one added quest is forced to a free type. Owns no
        /// pacing state (no accumulator advance, no rotation-clock stamp) — the
        /// callers own that — so the release can seed without disturbing the
        /// recurring trickle's handoff.</summary>
        private void SeedBatch(int requested, Random rng)
        {
            var freeDogs = state.Dogs.Where(d => !d.HasActiveQuest).ToList();
            var headroom = pacing.TargetActiveCount(state) - ActiveQuests.Count();
            var toAdd = Math.Max(0, Math.Min(requested, Math.Min(headroom, freeDogs.Count)));

            // Decide every (dog, type) up front so the free-quest invariant can
            // inspect the whole would-be active set before any quest is created.
            var assignments = new List<(Dog Dog, QuestType Type)>();
            for (var i = 0; i < toAdd; i++)
            {
                var dog = freeDogs[rng.Next(freeDogs.Count)];
                freeDogs.Remove(dog);
                assignments.Add((dog, RotationTypes[rng.Next(RotationTypes.Length)]));
            }

            EnsureOneFreeQuest(assignments, rng);

            foreach (var assignment in assignments)
            {
                GiveQuestTo(assignment.Dog, assignment.Type, rng);
            }
        }

        /// <summary>#310 always-one-free-quest invariant (boundary-only, never
        /// on completion): if neither an existing active quest nor any quest
        /// this batch would add is a free type, the post-refresh set would be
        /// all-paid — a soft-lock at 0 coins. In that case force the first
        /// added quest to a free type. A no-op when the batch is empty (a
        /// temporary "no free quest" window of up to one interval is
        /// acceptable) or a free quest is already present/queued.</summary>
        private void EnsureOneFreeQuest(List<(Dog Dog, QuestType Type)> assignments, Random rng)
        {
            if (assignments.Count == 0)
            {
                return;
            }

            var existingHasFree = ActiveQuests.Any(q => IsFreeType(q.Type));
            var batchHasFree = assignments.Any(a => IsFreeType(a.Type));
            if (existingHasFree || batchHasFree)
            {
                return;
            }

            var freeType = FreeQuestTypes[rng.Next(FreeQuestTypes.Length)];
            assignments[0] = (assignments[0].Dog, freeType);
        }

        private static bool IsFreeType(QuestType type)
        {
            return Array.IndexOf(FreeQuestTypes, type) >= 0;
        }

        public Quest GiveQuestTo(Dog dog, QuestType type, Random rng)
        {
            string item;
            GridPoint? hidden = null;
            int? cost = null;
            int? targetHouse = null;

            switch (type)
            {
                case QuestType.LostItem:
                    // #463: a puppy dog can't lose its own puppy — exclude the
                    // puppy subject from the Lost pool for a puppy receiver.
                    // toy/ball remain, so the pool never empties.
                    var lostItems = ItemCatalog.NamesEligibleFor(ItemEligibility.Lost);
                    if (dog.IsPuppy)
                    {
                        lostItems = lostItems
                            .Where(name => name != ItemCatalog.PuppyItemName)
                            .ToList();
                    }
                    item = lostItems[rng.Next(lostItems.Count)];
                    // #520: hide the item on the quest dog's OWN home tile
                    // (keyed off dog.HouseId), so it is always findable near
                    // that dog's house at any map size — not in a fixed
                    // origin-centered square that drifts off a bigger map.
                    var lostLot = state.GetHouseLot(dog.HouseId);
                    // #606: resolve the lot's live tile type from the map so the
                    // road-corridor exclusion is tile-aware (#455) — a zone lot
                    // borders its own tile's road, not only the origin streets.
                    var lostTileType = state.Map.GetTileAt(
                        LotBounds.NearestTileCoordinate(lostLot.Position));
                    hidden = GenerateHiddenItemPosition(rng, lostLot, lostTileType);
                    break;
                case QuestType.BuyGift:
                    // #317: the purchasable subject pool is population-gated
                    // through the pacing seam — pricier gift entries only enter
                    // the candidate set as the neighborhood grows.
                    var giftItems = pacing.EligibleSubjectPool(ItemEligibility.Gift, state);
                    item = giftItems[rng.Next(giftItems.Count)];
                    cost = ItemCatalog.Get(item).Cost;
                    break;
                case QuestType.DecorationRequest:
                    // Generic request (#50): no pre-named item — the player
                    // chooses from the comfort options at acceptance. #317: the
                    // offered options are population-gated through the same seam.
                    var decoQuest = new Quest(nextQuestId++, type, dog.Name, null,
                        QuestTemplates.For(type).Render(dog, "something comfy", rng),
                        null, null, null, pacing.EligibleSubjectPool(ItemEligibility.Decoration, state));
                    quests.Add(decoQuest);
                    dog.GiveQuest();
                    return decoQuest;
                default:
                    item = "bug spray";
                    targetHouse = dog.HouseId;
                    break;
            }

            var quest = new Quest(nextQuestId++, type, dog.Name, item,
                QuestTemplates.For(type).Render(dog, item, rng), hidden, cost, targetHouse);
            quests.Add(quest);
            dog.GiveQuest();
            return quest;
        }

        /// <summary>Accepts a quest. Buy-type quests deduct their cost here
        /// (docs/specs/quests/quest-content.md) and are rejected — spend
        /// untouched — when unaffordable (#25).</summary>
        public bool Accept(Quest quest)
        {
            if (quest.Status != QuestStatus.Available)
            {
                return false;
            }

            if (quest.Type == QuestType.DecorationRequest)
            {
                // Generic requests need a chosen option (#50).
                return false;
            }

            if (quest.Cost.HasValue && !state.Wallet.TrySpend(quest.Cost.Value))
            {
                return false;
            }

            quest.Status = QuestStatus.Accepted;
            if (quest.Type == QuestType.BuyGift)
            {
                if (quest.ItemName == ItemCatalog.FenceItemName)
                {
                    // #318: the fence has no delivery-truck flow — the cost is
                    // already deducted above, so record the permanent placed
                    // fence and complete immediately, skipping the HeadingHome /
                    // WaitingForDelivery legs entirely ("no delivery, no
                    // animation"). Fence visibility then derives from this
                    // PlacedItem (LotFence.IsFenced).
                    var dog = FindDog(quest);
                    state.AddPlacedItem(dog.HouseId, quest.ItemName);
                    Complete(quest);
                }
                else
                {
                    quest.DeliveryPhase = DeliveryPhase.HeadingHome;
                    // #470: the dog now walks home under the QuestDirector's
                    // control — stop it wandering for the whole delivery leg.
                    FindDog(quest).BeginDelivery();
                }
            }

            return true;
        }

        /// <summary>#50: accept a generic decoration request with the
        /// player's chosen option — that item's specific cost is deducted
        /// and that item is what the truck will deliver.</summary>
        public bool AcceptWithChoice(Quest quest, string chosenItem)
        {
            if (quest.Status != QuestStatus.Available
                || quest.Type != QuestType.DecorationRequest
                || !quest.Options.Contains(chosenItem))
            {
                return false;
            }

            var cost = ItemCatalog.Get(chosenItem).Cost.Value;
            if (!state.Wallet.TrySpend(cost))
            {
                return false;
            }

            quest.ItemName = chosenItem;
            quest.Cost = cost;
            quest.Status = QuestStatus.Accepted;
            quest.DeliveryPhase = DeliveryPhase.HeadingHome;
            // #470: same delivery leg as a named buy-gift — gate wander off
            // while the dog walks home.
            FindDog(quest).BeginDelivery();
            return true;
        }

        /// <summary>#30: the dog reaches home and sits waiting for the truck.</summary>
        public void NotifyDogArrivedHome(Quest quest)
        {
            if (quest.DeliveryPhase != DeliveryPhase.HeadingHome)
            {
                return;
            }

            quest.DeliveryPhase = DeliveryPhase.WaitingForDelivery;
            FindDog(quest).TrySit(buyQuestAccepted: true, isAtHome: true);
        }

        /// <summary>
        /// #677: the delivery leg could not be carried out — the walk home could
        /// not be planned, or no road route to the door exists for the truck. The
        /// player has already been charged, so the safe outcome is the one they
        /// paid for: the item still lands, the dog is handed straight back to
        /// wander, and the quest completes. Crucially it does NOT sit the dog: a
        /// dog only ever enters the waiting pose at its own front door, and this
        /// is precisely the path that must not leave one stranded in that pose with
        /// no truck ever coming. A no-op for a quest with no delivery leg in
        /// flight (the fence purchase, an already-delivered quest).
        /// </summary>
        public void FailDelivery(Quest quest)
        {
            if (quest.DeliveryPhase != DeliveryPhase.HeadingHome
                && quest.DeliveryPhase != DeliveryPhase.WaitingForDelivery)
            {
                return;
            }

            quest.DeliveryPhase = DeliveryPhase.WaitingForDelivery;
            DeliverPackage(quest);
        }

        /// <summary>#30: the truck delivers — the item appears at the house
        /// permanently (#27) and only now does the quest complete and pay.</summary>
        public void DeliverPackage(Quest quest)
        {
            if (quest.DeliveryPhase != DeliveryPhase.WaitingForDelivery)
            {
                return;
            }

            quest.DeliveryPhase = DeliveryPhase.Delivered;
            var dog = FindDog(quest);

            if (quest.Type == QuestType.DecorationRequest)
            {
                // Automatic yard placement (#48); decorations raise the
                // requesting dog's happiness (#47) — flavor only.
                var slot = state.Decorations.Count(d => d.HouseId == dog.HouseId);
                state.AddDecoration(new Decorations.Decoration(
                    quest.ItemName, dog.HouseId,
                    Decorations.YardPlacement.PositionFor(dog.HouseId, slot)));
                dog.IncreaseHappiness(1);
            }
            else
            {
                state.AddPlacedItem(dog.HouseId, quest.ItemName);
            }

            dog.PlaceOnStreet();
            Complete(quest);
        }

        /// <summary>#31: hidden-object search — tapping the hidden spot of an
        /// accepted LostItem quest completes it; anywhere else, nothing.</summary>
        public bool TapWorldPosition(GridPoint tap)
        {
            var hit = quests.FirstOrDefault(q =>
                q.Type == QuestType.LostItem
                && q.Status == QuestStatus.Accepted
                && q.HiddenItemPosition.HasValue
                && TapResolver.IsHit(q.HiddenItemPosition.Value, tap, LostItemTapRadius));

            if (hit == null)
            {
                return false;
            }

            Complete(hit);
            return true;
        }

        /// <summary>#53/#157: houses currently showing a bug problem — those
        /// holding an accepted, not-yet-sprayed PestControl quest. The Unity
        /// layer asks Core this to decide where a bug swarm is visible; a
        /// house drops off the list the moment its quest completes.</summary>
        public IReadOnlyList<int> HousesAwaitingSpray()
        {
            return quests
                .Where(q => q.Type == QuestType.PestControl
                    && q.Status == QuestStatus.Accepted
                    && q.TargetHouseId.HasValue)
                .Select(q => q.TargetHouseId.Value)
                .Distinct()
                .ToList();
        }

        /// <summary>#670: does this one house currently have bugs on it — i.e.
        /// is it holding an accepted, not-yet-sprayed PestControl quest? The
        /// single-house form of <see cref="HousesAwaitingSpray"/>, and the
        /// predicate <c>HouseTapArbiter</c> resolves a house tap with: a bugged
        /// house sprays, a clear house opens its profile, never both.</summary>
        public bool IsAwaitingSpray(int houseId)
        {
            return quests.Any(q => q.Type == QuestType.PestControl
                && q.Status == QuestStatus.Accepted
                && q.TargetHouseId == houseId);
        }

        /// <summary>#53: spraying the afflicted house completes its accepted
        /// PestControl quest; spraying anything else is a no-op.</summary>
        public bool SprayHouse(int houseId)
        {
            var hit = quests.FirstOrDefault(q =>
                q.Type == QuestType.PestControl
                && q.Status == QuestStatus.Accepted
                && q.TargetHouseId == houseId);

            if (hit == null)
            {
                return false;
            }

            Complete(hit);
            return true;
        }

        private void Complete(Quest quest)
        {
            quest.Status = QuestStatus.Completed;
            FindDog(quest).ClearQuest();
            var payout = PayoutFor(quest);
            state.Wallet.Deposit(payout);
            QuestCompleted?.Invoke(quest, payout);
            var household = state.HandleQuestCompleted(moveInRng);
            if (household.Count > 0)
            {
                MoveInOccurred?.Invoke(household);
            }
        }

        /// <summary>#626: the coin payout for a completed quest. A paid-type
        /// quest (BuyGift / DecorationRequest / fence) carries a fronted item
        /// <see cref="Quest.Cost"/> and is an <em>earner</em> — reimbursed at
        /// <see cref="EconomyNumbers.PaidQuestPayout"/> (cost × markup), always
        /// net positive. A free-type quest (LostItem / PestControl) carries no
        /// cost and pays the flat <see cref="EconomyNumbers.QuestPayout"/>. The
        /// presence of <see cref="Quest.Cost"/> is the exact paid/free
        /// discriminator (confirmed against <see cref="GiveQuestTo"/>).</summary>
        private static int PayoutFor(Quest quest)
        {
            return quest.Cost.HasValue
                ? EconomyNumbers.PaidQuestPayout(quest.Cost.Value)
                : EconomyNumbers.QuestPayout;
        }

        private Dog FindDog(Quest quest)
        {
            return state.Dogs.First(d => d.Name == quest.DogName);
        }

        /// <summary>#31/#290/#520: a uniformly random point within the quest
        /// dog's own home-tile quadrant bounds
        /// (<see cref="LotBounds.QuadrantBounds"/>), rejection-sampled so it
        /// keeps at least <see cref="HouseClearanceBuffer"/> from that lot's
        /// house footprint (<see cref="HousePlacement.HouseFootprint"/>) — so
        /// the lost toy always sits in open, tappable ground on the quest
        /// dog's tile rather than behind its house. Only the lot's own
        /// footprint needs checking: a lot's quadrant bounds tile the map with
        /// no overlap into any neighbouring lot, so no other house's footprint
        /// can intrude on the sampled region.
        ///
        /// #606: the raw quadrant bounds tile the whole tile with no gap, so a
        /// FourWay quadrant's two inner edges sit ON the road centerlines —
        /// nothing kept a candidate off the paved road, verge, or sidewalk, and
        /// a lost item (incl. the lost puppy, #335) could land in the road,
        /// straight in the on-road delivery truck's path (#538). The sample
        /// region is now the quadrant bounds with the street corridor cleared
        /// (<see cref="LotBounds.ClearRoadCorridors"/> against the lot's own
        /// tile roads, <see cref="LotBounds.RoadsFor"/> — the same road-cleared
        /// region yard landscaping samples, #244/#455), so every candidate —
        /// including the bounded-attempt fallback — is road-clear.
        ///
        /// Deterministic per <paramref name="rng"/>: the draw count is bounded
        /// and the last candidate is returned if the (practically unreachable)
        /// attempt budget is exhausted, so the result stays within the cleared
        /// region and reproducible per seed.</summary>
        private static GridPoint GenerateHiddenItemPosition(
            Random rng, HouseLot lot, TileType tileType)
        {
            var bounds = LotBounds.ClearRoadCorridors(
                LotBounds.QuadrantBounds(lot), LotBounds.RoadsFor(lot, tileType));
            var footprint = HousePlacement.HouseFootprint(lot);
            var candidate = default(GridPoint);
            for (var attempt = 0; attempt < MaxHiddenItemPlacementAttempts; attempt++)
            {
                candidate = new GridPoint(
                    (float)(bounds.MinX + rng.NextDouble() * bounds.Width),
                    (float)(bounds.MinZ + rng.NextDouble() * bounds.Depth));

                if (footprint.DistanceTo(candidate) >= HouseClearanceBuffer)
                {
                    return candidate;
                }
            }

            return candidate;
        }
    }
}
