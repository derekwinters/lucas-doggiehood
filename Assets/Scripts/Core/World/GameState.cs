using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Art;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Onboarding;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// Root of the mutable game state. A new game starts with the four
    /// starting houses, one per quadrant of the intersection (#38), and the
    /// eight roster dogs living in them (#63).
    /// </summary>
    public sealed class GameState
    {
        /// <summary>Grid coordinate of the starting FourWay intersection
        /// (#38, #109) — the map's fixed seed, matching the confirmed
        /// first-zone layout's "starting FourWay at grid (0,0)".</summary>
        private static readonly TileCoordinate StartingIntersectionCoordinate = new TileCoordinate(0, 0);

        /// <summary>#295: the single scripted tile the onboarding "expand the
        /// map" step unlocks. Before that step completes it is the only
        /// unlockable frontier coordinate (Derek's "this only works after the
        /// first onboarding expansion happens"). Sourced from the authored first
        /// zone so the scripted tile stays in one place.</summary>
        private static TileCoordinate OnboardingExpansionTile
        {
            get { return ZoneCatalog.FirstZone.TilePlacements[0].Coordinate; }
        }

        private readonly List<House> houses;
        private readonly List<PlacedItem> placedItems = new List<PlacedItem>();
        private readonly List<Decorations.Decoration> decorations = new List<Decorations.Decoration>();
        private readonly List<Dog> dogs;
        private readonly List<Zone> unlockedZones = new List<Zone>();

        /// <summary>#434: the <see cref="HouseVariant"/> rolled and locked in for
        /// each zone lot the moment its zone unlocks (its lots first appear),
        /// keyed by house id — so an empty lot already knows the house that will
        /// stand on it (its footprint, its trees) before it is built. Populated
        /// by <see cref="TryUnlockNextZone"/>, persisted through
        /// <see cref="SaveCodec"/>, and read (with a deterministic fallback) by
        /// <see cref="TryBuildHouse"/>. A built lot keeps its entry — the built
        /// <see cref="House"/> is the source of truth from then on, so the save
        /// emits it as a house= line rather than an unbuilt lotvariant= line.</summary>
        private readonly Dictionary<int, HouseVariant> assignedLotVariants = new Dictionary<int, HouseVariant>();

        /// <summary>#295: the coordinates unlocked one-at-a-time through the
        /// player-choice frontier (<see cref="TryUnlockTile"/>), in unlock
        /// order. Distinct from the legacy <see cref="unlockedZones"/> group
        /// model this supersedes; both place onto <see cref="Map"/>.</summary>
        private readonly List<TileCoordinate> unlockedTiles = new List<TileCoordinate>();

        /// <summary>Owns the shared move-in pity counter and easter-egg
        /// reserve (#54). Persisted through SaveCodec (#437): read via the
        /// MoveIn* accessors on save and rebuilt on load by
        /// <see cref="RestoreMoveInState"/> — which is why the field is not
        /// readonly (restore replaces it wholesale, no dice rolled).</summary>
        private Expansion.MoveInSystem moveInSystem = new Expansion.MoveInSystem();

        public IReadOnlyList<House> Houses
        {
            get { return houses; }
        }

        public IReadOnlyList<Dog> Dogs
        {
            get { return dogs; }
        }

        public Economy.Wallet Wallet { get; }
        public Quests.QuestManager Quests { get; }

        /// <summary>#316: the one-time first-run reward chain (first-quest ->
        /// upgrade -> expand -> build), paying a flat reward per step and
        /// gating normal rotation until it completes. Persisted in the save so
        /// it is never restarted on reload.</summary>
        public OnboardingRewardChain RewardChain { get; } = new OnboardingRewardChain();

        /// <summary>#469: the house id the onboarding "upgrade a house" reward
        /// step is scoped to — the first-quest dog's house
        /// (<see cref="Onboarding.OnboardingSequence.TargetDog"/>'s
        /// <see cref="Dog.HouseId"/>), recorded by
        /// <see cref="GrantOnboardingCompletionReward"/> when the guided quest
        /// completes and persisted through <see cref="SaveCodec"/>. Null until
        /// that handoff (and for a legacy save mid-chain), in which case no
        /// upgrade is restricted. It must live here — not be re-resolved from
        /// <c>TargetDog</c> — because that dog stops reporting an active quest
        /// the instant the quest resolves and across a reload.</summary>
        private int? onboardingUpgradeTargetHouseId;

        /// <summary>#469: the house id the onboarding
        /// <see cref="OnboardingRewardStep.UpgradeHouse"/> step is scoped to, or
        /// null when there is no such restriction. See
        /// <see cref="IsHouseUpgradeEligible"/>.</summary>
        public int? OnboardingUpgradeTargetHouseId
        {
            get { return onboardingUpgradeTargetHouseId; }
        }

        /// <summary>The grid-coordinate tile map (#109), seeded with just
        /// the starting FourWay intersection until zones are unlocked (#56).</summary>
        public TileMap Map { get; }

        /// <summary>#295: the full authored target neighborhood
        /// (<c>docs/tools/map-data.json</c>, loaded via #383's
        /// <see cref="MapLoader"/>) against which the player-choice frontier is
        /// derived. Null until wired (a fresh <see cref="CreateNew"/> game has
        /// none until the Unity layer supplies it), in which case there is no
        /// frontier to unlock. Not itself persisted — it is fixed design data,
        /// re-supplied on every launch.</summary>
        public TileMap TargetMap { get; private set; }

        /// <summary>#295: supplies the authored target map used to compute the
        /// unlock frontier. The thin Unity layer loads the authored map data
        /// and calls this once at bootstrap.</summary>
        public void SetTargetMap(TileMap targetMap)
        {
            TargetMap = targetMap;
        }

        // #398: the live sidewalk+crosswalk+front-walkway network, derived
        // from the whole unlocked Map (not the starting-tile-only
        // NeighborhoodLayout cache). Lazily built and cached; invalidated
        // whenever the map or the built houses change so it grows onto newly
        // unlocked tiles and picks up freshly built houses' walkways.
        private WalkNetwork walkNetwork;

        /// <summary>
        /// The walkable graph (#106) spanning every unlocked tile (#398):
        /// sidewalks and crosswalks for all of <see cref="Map"/>'s
        /// road-bearing tiles, plus a front walkway for every built house.
        /// Rebuilds after a successful <see cref="TryUnlockNextZone"/> or
        /// <see cref="TryBuildHouse"/> (and on save-restore); a failed action
        /// leaves the cached instance untouched. Dogs wander this graph, so
        /// unlocking a zone lets them explore its sidewalks.
        /// </summary>
        public WalkNetwork WalkNetwork
        {
            get { return walkNetwork ?? (walkNetwork = BuildWalkNetwork()); }
        }

        private WalkNetwork BuildWalkNetwork()
        {
            // A built house grows a front walkway (#430) when it has a resolved
            // model to derive the door from: either an authored starter style
            // (#64) OR a zone-built house's rolled variant (#299, #414). A zone
            // lot with no house yet has no entry in `houses`, so it stays off
            // the graph exactly as before — the tile's sidewalks/crosswalks
            // still span it, since those derive from the Map's roads, not its
            // houses. The zone house's walkway is a real graph edge; the
            // resident-only wander gate (WanderBehavior) keeps every OTHER dog
            // off it.
            var builtLots = houses
                .Where(house => Art.HouseStyleTable.HasStyle(house.Id) || house.Variant.HasValue)
                .Select(house => GetHouseLot(house.Id))
                .ToList();
            return MapWalkNetwork.BuildFrom(Map, builtLots);
        }

        // Drops the cached network so the next read rebuilds it from the
        // current Map + houses. Called only after a change actually lands.
        private void InvalidateWalkNetwork()
        {
            walkNetwork = null;
        }

        /// <summary>Zones unlocked so far, in unlock order (#56). Empty for
        /// a new game — the starting intersection isn't itself a zone.</summary>
        public IReadOnlyList<Zone> UnlockedZones
        {
            get { return unlockedZones; }
        }

        /// <summary>#295: the coordinates the player has unlocked one-at-a-time
        /// via <see cref="TryUnlockTile"/>, in unlock order. Persisted as a set
        /// (SaveCodec) so player-chosen unlock order round-trips.</summary>
        public IReadOnlyList<TileCoordinate> UnlockedTiles
        {
            get { return unlockedTiles; }
        }

        /// <summary>#434: the pre-assigned <see cref="HouseVariant"/> for each
        /// zone lot, keyed by house id — rolled and locked in at unlock so an
        /// empty lot reads as the plot of its predetermined house. Persisted
        /// (SaveCodec) for unbuilt lots; a built lot's variant lives on its
        /// <see cref="House"/> instead. Empty for a game with no zone
        /// unlocked.</summary>
        public IReadOnlyDictionary<int, HouseVariant> AssignedLotVariants
        {
            get { return assignedLotVariants; }
        }

        /// <summary>Permanent world changes from completed quests (#27).</summary>
        public IReadOnlyList<PlacedItem> PlacedItems
        {
            get { return placedItems; }
        }

        /// <summary>Yard decorations, permanent once delivered (#27, #46).</summary>
        public IReadOnlyList<Decorations.Decoration> Decorations
        {
            get { return decorations; }
        }

        private GameState(IReadOnlyList<House> houses, IReadOnlyList<Dog> startingDogs)
        {
            this.houses = new List<House>(houses);
            dogs = new List<Dog>(startingDogs);
            Wallet = new Economy.Wallet();
            Quests = new Quests.QuestManager(this);
            Map = new TileMap(StartingIntersectionCoordinate, TileType.FourWay);
        }

        public static GameState CreateNew()
        {
            // The 4 starting houses (#38) already have the 8 roster dogs
            // living in them (#63) — never vacant, unlike a house #57 will
            // eventually build (#58).
            var houses = NeighborhoodLayout.HouseLots
                .Select(lot => new House(lot.HouseId, lot.Quadrant, isVacant: false))
                .ToList();

            return new GameState(houses, DogRoster.CreateStartingDogs());
        }

        public void AddPlacedItem(int houseId, string itemName)
        {
            placedItems.Add(new PlacedItem(houseId, itemName));
        }

        /// <summary>Appends a decoration with no capacity check (#27, #46).
        /// This is the uncapped path used by save-load and by the
        /// grandfathered MVP data (#59) — it never removes or rejects, so a
        /// yard that already holds more decorations than its level's cap
        /// keeps every one. New, capacity-respecting placements go through
        /// <see cref="TryAddDecoration"/>.</summary>
        public void AddDecoration(Decorations.Decoration decoration)
        {
            decorations.Add(decoration);
        }

        /// <summary>How many decorations currently sit in
        /// <paramref name="houseId"/>'s yard (#59).</summary>
        public int DecorationCountForHouse(int houseId)
        {
            return decorations.Count(decoration => decoration.HouseId == houseId);
        }

        /// <summary>How many decorations <paramref name="houseId"/>'s yard
        /// can hold (#59): exactly the house's level (1/2/3/4). An unknown
        /// house has no capacity (0).</summary>
        public int DecorationCapacityForHouse(int houseId)
        {
            var house = houses.FirstOrDefault(candidate => candidate.Id == houseId);
            return house == null ? 0 : house.Level;
        }

        /// <summary>
        /// Places a decoration in a house's yard (#59), respecting the
        /// capacity cap (decorations = level). Returns false with no state
        /// change when the yard is already at (or over) capacity, or the
        /// house is unknown. Grandfathered decorations placed before the cap
        /// existed are never removed — this only blocks NEW placements once
        /// the count has reached the level.
        /// </summary>
        public bool TryAddDecoration(Decorations.Decoration decoration)
        {
            if (DecorationCountForHouse(decoration.HouseId) >= DecorationCapacityForHouse(decoration.HouseId))
            {
                return false;
            }

            decorations.Add(decoration);
            return true;
        }

        /// <summary>
        /// #295: the frontier coordinates the player may currently unlock. The
        /// full geometric frontier (<see cref="Expansion.TileFrontier"/> over
        /// the live <see cref="Map"/> and <see cref="TargetMap"/>) is offered
        /// once the onboarding "expand the map" step has completed; before that,
        /// only the single scripted <see cref="OnboardingExpansionTile"/> is
        /// offered (Derek's "the lock icons don't appear on the remaining open
        /// ended roads until the onboarding quests have been completed").
        /// Empty when no <see cref="TargetMap"/> has been supplied.
        /// </summary>
        public IReadOnlyCollection<TileCoordinate> UnlockableFrontier()
        {
            if (TargetMap == null)
            {
                return Array.Empty<TileCoordinate>();
            }

            var frontier = Expansion.TileFrontier.Compute(Map, TargetMap);
            if (RewardChain.CurrentStep > OnboardingRewardStep.ExpandMap)
            {
                return frontier;
            }

            // Onboarding gate: only the scripted expand tile is unlockable until
            // the "expand the map" step completes.
            var gated = new List<TileCoordinate>();
            if (frontier.Contains(OnboardingExpansionTile))
            {
                gated.Add(OnboardingExpansionTile);
            }

            return gated;
        }

        /// <summary>
        /// #295: the single player-choice unlock entry point. Places the chosen
        /// frontier tile (its type read from <see cref="TargetMap"/>, validated
        /// through <see cref="TileMap.Place"/>) after charging the flat
        /// <see cref="Expansion.TileUnlock.Cost"/>. Returns false with no state
        /// change (no deduction, no tile placed) when the coordinate isn't a
        /// currently-unlockable frontier tile (see <see cref="UnlockableFrontier"/>)
        /// or the balance can't afford the cost. Advances the onboarding
        /// reward chain's "expand the map" step on success (a no-op once that
        /// step is already past), mirroring <see cref="TryUnlockNextZone"/>.
        /// </summary>
        public bool TryUnlockTile(TileCoordinate coordinate)
        {
            if (!UnlockableFrontier().Contains(coordinate))
            {
                return false;
            }

            if (!Wallet.TrySpend(Expansion.TileUnlock.Cost(Map.Tiles.Count)))
            {
                return false;
            }

            Map.Place(coordinate, TargetMap.GetTileAt(coordinate));
            unlockedTiles.Add(coordinate);
            InvalidateWalkNetwork();
            AdvanceRewardChain(OnboardingRewardStep.ExpandMap);
            return true;
        }

        /// <summary>
        /// #295: restores a persisted player-unlocked tile on load — places it
        /// onto <see cref="Map"/> with its persisted type and records it in
        /// <see cref="UnlockedTiles"/>, WITHOUT charging the wallet or advancing
        /// the reward chain (both persist separately). The parallel of
        /// <see cref="RestoreUnlockedZoneCount"/>. Defensively a no-op if the
        /// coordinate is already placed or would fail #109 adjacency.
        /// </summary>
        public void RestoreUnlockedTile(TileCoordinate coordinate, TileType type)
        {
            if (Map.HasTileAt(coordinate) || !Map.CanPlace(coordinate, type))
            {
                return;
            }

            Map.Place(coordinate, type);
            unlockedTiles.Add(coordinate);
            InvalidateWalkNetwork();
        }

        /// <summary>A newly moved-in dog (#54) joins the live roster
        /// immediately — eligible for the very next daily quest rotation,
        /// exactly like any other quest-free dog.</summary>
        public void AddDog(Dog dog)
        {
            dogs.Add(dog);
        }

        /// <summary>The #54/#58 move-in hook: called once per completed
        /// quest (QuestManager.Complete). Rolls the shared pity counter
        /// against whichever houses currently report vacant, and on
        /// success fills exactly one — flipping its vacancy and adding
        /// its new dog(s) to the live roster immediately. Returns the
        /// newly moved-in household (empty when nothing happened).</summary>
        public IReadOnlyList<Dog> HandleQuestCompleted(Random rng)
        {
            var household = Expansion.HouseOccupancy.ApplyMoveIn(Houses, moveInSystem, Dogs, rng);
            foreach (var dog in household)
            {
                AddDog(dog);
            }

            return household;
        }

        /// <summary>#437: the shared move-in pity counter
        /// (quests-since-last-move-in) as it stands now — read on save so the
        /// accumulated move-in chance survives a relaunch instead of resetting
        /// to the 5% base.</summary>
        public int MoveInQuestsSinceLastMoveIn
        {
            get { return moveInSystem.QuestsSinceLastMoveIn; }
        }

        /// <summary>#437: the easter-egg names not yet consumed — read on save
        /// so a used easter-egg name never reappears after a relaunch.</summary>
        public IReadOnlyList<string> MoveInRemainingEasterEggNames
        {
            get { return moveInSystem.RemainingEasterEggNames; }
        }

        /// <summary>#437: the reserved breeds not yet introduced — read on save
        /// so the reserved-breed pair isn't handed out twice across a
        /// relaunch.</summary>
        public IReadOnlyList<Dogs.Breed> MoveInRemainingReservedBreeds
        {
            get { return moveInSystem.RemainingReservedBreeds; }
        }

        /// <summary>#437: restores the persisted move-in state on load —
        /// rebuilds <see cref="moveInSystem"/> from the saved pity counter and
        /// the remaining easter-egg/reserved-breed reserves, WITHOUT rolling
        /// any dice or firing a move-in (the parallel of
        /// <see cref="RestoreUnlockedZoneCount"/> / <see cref="RestoreRewardChainStep"/>).
        /// A legacy save with no move-in line simply keeps
        /// <see cref="CreateNew"/>'s default fresh system.</summary>
        public void RestoreMoveInState(
            int questsSinceLastMoveIn,
            IEnumerable<string> remainingEasterEggNames,
            IEnumerable<Dogs.Breed> remainingReservedBreeds)
        {
            moveInSystem = new Expansion.MoveInSystem(
                remainingEasterEggNames, remainingReservedBreeds, questsSinceLastMoveIn);
        }

        /// <summary>
        /// Unlocks the next authored <see cref="Zone"/> (#56,
        /// <see cref="ZoneCatalog.Zones"/>) in sequence: the nth zone costs
        /// <see cref="Expansion.ZoneUnlock.CostForZoneNumber"/>, deducted
        /// from <see cref="Wallet"/>. Returns false with no state change
        /// (no deduction, no tiles placed) when the balance can't afford
        /// it, or when every authored zone is already unlocked.
        /// </summary>
        public bool TryUnlockNextZone()
        {
            var zoneNumber = unlockedZones.Count + 1;
            if (zoneNumber > ZoneCatalog.Zones.Count)
            {
                return false;
            }

            var cost = Expansion.ZoneUnlock.CostForZoneNumber(zoneNumber);
            if (!Wallet.TrySpend(cost))
            {
                return false;
            }

            var zone = ZoneCatalog.Zones[zoneNumber - 1];
            zone.PlaceOnto(Map);
            unlockedZones.Add(zone);
            AssignZoneLotVariants(zone);
            InvalidateWalkNetwork();
            AdvanceRewardChain(OnboardingRewardStep.ExpandMap);
            return true;
        }

        /// <summary>#434: rolls and locks in each of a freshly unlocked zone's
        /// lot variants (ladder + tint) the moment the lots appear, so the empty
        /// lot already knows the house that will stand on it. The roll is the
        /// same deterministic <see cref="HouseVariantAssignment.ForHouse"/> the
        /// build path used to invoke lazily (#299) — moving it here (#434) only
        /// changes WHEN it happens, not the value. Every zone lot id is a zone
        /// house id (>= <see cref="HouseVariantAssignment.FirstZoneHouseId"/>),
        /// so none is rejected.</summary>
        private void AssignZoneLotVariants(Zone zone)
        {
            foreach (var lot in zone.Lots)
            {
                assignedLotVariants[lot.HouseId] = HouseVariantAssignment.ForHouse(lot.HouseId);
            }
        }

        /// <summary>#434: restores a persisted lot-variant assignment on load —
        /// records the <paramref name="variant"/> for an unbuilt zone lot so
        /// <see cref="TryBuildHouse"/> reads the SAVED value rather than
        /// re-rolling (future-proofing against an RNG or palette retune). The
        /// parallel of <see cref="RestoreBuiltHouse"/> for a lot that has an
        /// assignment but no house yet; a legacy save with no such line simply
        /// falls back to the deterministic roll at build.</summary>
        public void RestoreAssignedLotVariant(int houseId, HouseVariant variant)
        {
            assignedLotVariants[houseId] = variant;
        }

        /// <summary>Whether <paramref name="houseId"/> (a <see cref="HouseLot"/>
        /// id from a <see cref="Zone"/>, or the starting layout) has no
        /// <see cref="House"/> built on it yet (#56, #57) — a freshly
        /// unlocked zone reports every one of its lots buildable this way.</summary>
        public bool IsLotBuildable(int houseId)
        {
            return Houses.All(house => house.Id != houseId);
        }

        /// <summary>
        /// Builds a house on <paramref name="houseId"/>'s lot (#57): charges
        /// <see cref="Expansion.HouseBuildNumbers.Cost"/> from <see cref="Wallet"/>
        /// and adds a new <see cref="House"/> at <see cref="House.InitialLevel"/>,
        /// vacant (#58). Returns false with no state change (no deduction,
        /// no house added) when the lot already has a house
        /// (<see cref="IsLotBuildable"/> false), the lot's zone hasn't been
        /// unlocked yet, or the balance can't afford the cost.
        /// </summary>
        public bool TryBuildHouse(int houseId)
        {
            if (!IsLotBuildable(houseId))
            {
                return false;
            }

            var lot = FindLotInUnlockedZones(houseId);
            if (lot == null)
            {
                return false;
            }

            if (!Wallet.TrySpend(Expansion.HouseBuildNumbers.Cost))
            {
                return false;
            }

            // #434: the art variant (ladder + tint) was already rolled and
            // persisted when the zone unlocked (AssignedLotVariants), so the
            // build reads that pre-assignment rather than re-rolling. A legacy
            // save whose zone predates #434 has no persisted entry; the
            // deterministic #299 roll is the fallback — bit-identical, since it
            // is a pure function of the house id.
            var variant = assignedLotVariants.TryGetValue(houseId, out var assigned)
                ? assigned
                : HouseVariantAssignment.ForHouse(houseId);
            houses.Add(new House(houseId, lot.Quadrant, variant: variant));
            InvalidateWalkNetwork();
            AdvanceRewardChain(OnboardingRewardStep.BuildHouse);
            return true;
        }

        /// <summary>
        /// Upgrades the house on <paramref name="houseId"/> one level (#59):
        /// charges <see cref="Expansion.HouseUpgradeNumbers.CostToReach"/>
        /// (100 / 200 / 400, doubling per step) from <see cref="Wallet"/>
        /// and raises the house's level. Returns false with no state change
        /// (no deduction, level unchanged) when no house has that id, the
        /// house is already at <see cref="Expansion.HouseUpgradeNumbers.MaxLevel"/>,
        /// or the balance can't afford the step — the same charge-then-mutate
        /// contract as <see cref="TryBuildHouse"/>. Raising the level also
        /// raises the yard's decoration capacity (see
        /// <see cref="DecorationCapacityForHouse"/>).
        /// </summary>
        public bool TryUpgradeHouse(int houseId)
        {
            // #469: while the onboarding chain is on its "upgrade a house" step,
            // only the first-quest dog's house may be upgraded — any other house
            // is a no-op (no charge, no level change), the same contract as an
            // unknown/max/unaffordable house. This keeps the self-funding ladder
            // from being soft-locked by spending the sole 100 coins on a house
            // that doesn't advance the chain. The restriction lifts the moment
            // the chain moves past UpgradeHouse.
            if (!IsHouseUpgradeEligible(houseId))
            {
                return false;
            }

            var house = houses.FirstOrDefault(candidate => candidate.Id == houseId);
            if (house == null)
            {
                return false;
            }

            if (house.Level >= Expansion.HouseUpgradeNumbers.MaxLevel)
            {
                return false;
            }

            var cost = Expansion.HouseUpgradeNumbers.CostToReach(house.Level + 1);
            if (!Wallet.TrySpend(cost))
            {
                return false;
            }

            house.RaiseLevel();
            AdvanceRewardChain(OnboardingRewardStep.UpgradeHouse);
            return true;
        }

        /// <summary>#316: the onboarding-completion bonus — step 1 of the reward
        /// chain. Fired once by <see cref="Onboarding.OnboardingSequence"/> when
        /// the guided first quest completes (the same "first quest completed"
        /// event, scoped to the genuine onboarding run so it pays exactly once
        /// and never perturbs ordinary quest completions). Reuses the quest
        /// reward-payout path (a wallet deposit), not the random rotation.</summary>
        public void GrantOnboardingCompletionReward(int houseId)
        {
            // #469: remember the first-quest dog's house so the following
            // "upgrade a house" step is scoped to it. Recorded here — where the
            // house is still known — because TargetDog goes stale the instant
            // its quest resolves and across a reload.
            onboardingUpgradeTargetHouseId = houseId;
            AdvanceRewardChain(OnboardingRewardStep.FirstQuest);
        }

        /// <summary>#469: whether <paramref name="houseId"/> may be upgraded
        /// right now. Everything is eligible except a house other than the
        /// stored onboarding target while the reward chain is waiting on its
        /// <see cref="OnboardingRewardStep.UpgradeHouse"/> step. With no stored
        /// target (before the handoff, or a legacy save) nothing is restricted.
        /// The thin Unity layer consults this to fold "not the eligible house
        /// right now" into the existing disabled-Upgrade-button state, so the
        /// gate is queried in exactly one place.</summary>
        public bool IsHouseUpgradeEligible(int houseId)
        {
            return RewardChain.CurrentStep != OnboardingRewardStep.UpgradeHouse
                || !onboardingUpgradeTargetHouseId.HasValue
                || houseId == onboardingUpgradeTargetHouseId.Value;
        }

        /// <summary>#469: restores the persisted onboarding upgrade-target house
        /// id on load (see <see cref="SaveCodec"/>), so the target-house
        /// restriction survives a save/reload mid-chain — the parallel of
        /// <see cref="RestoreRewardChainStep"/>. A legacy save with no such line
        /// simply leaves the target null (no restriction).</summary>
        public void RestoreOnboardingUpgradeTargetHouseId(int houseId)
        {
            onboardingUpgradeTargetHouseId = houseId;
        }

        /// <summary>#316: notifies the reward chain that a tracked action just
        /// succeeded. When it is the step the chain is waiting on, the chain
        /// pays the flat reward into <see cref="Wallet"/> and advances; the
        /// moment that advance completes the chain (step 4, build), the normal
        /// quest rotation is released — the #312 -> #310 handoff.</summary>
        private void AdvanceRewardChain(OnboardingRewardStep action)
        {
            if (RewardChain.TryAdvance(action, Wallet) && RewardChain.IsComplete)
            {
                Quests.ReleaseInitialRotation();
            }
        }

        /// <summary>#316: restores the persisted reward-chain step on load
        /// without paying — round-tripping progress so the one-time chain is
        /// never restarted or re-paid.</summary>
        public void RestoreRewardChainStep(OnboardingRewardStep step)
        {
            RewardChain.RestoreStep(step);
        }

        /// <summary>
        /// #343: restores <paramref name="count"/> already-unlocked zones on
        /// load — replays the first <paramref name="count"/> authored
        /// <see cref="ZoneCatalog.Zones"/> onto <see cref="Map"/> and
        /// <see cref="UnlockedZones"/> in sequence, WITHOUT charging the
        /// wallet or advancing the reward chain (both persist separately).
        /// The parallel of <see cref="RestoreRewardChainStep"/>: round-trips
        /// progress so a persisted zone is never re-paid on reload. A no-op
        /// for 0; ignores a count beyond the authored zones (a save can only
        /// legitimately hold what was unlockable when it was written).
        /// </summary>
        public void RestoreUnlockedZoneCount(int count)
        {
            var target = Math.Min(count, ZoneCatalog.Zones.Count);
            while (unlockedZones.Count < target)
            {
                var zone = ZoneCatalog.Zones[unlockedZones.Count];
                zone.PlaceOnto(Map);
                unlockedZones.Add(zone);
            }

            InvalidateWalkNetwork();
        }

        /// <summary>
        /// #299: restores a zone-built house on load — recreates the
        /// <see cref="House"/> at its persisted level/vacancy carrying its
        /// persisted <see cref="HouseVariant"/> (ladder + tint), WITHOUT
        /// charging the wallet or advancing the reward chain (the parallel of
        /// <see cref="RestoreUnlockedZoneCount"/> / <see cref="RestoreRewardChainStep"/>).
        /// The house's quadrant comes from its lot in the already-restored
        /// unlocked zones, so <see cref="RestoreUnlockedZoneCount"/> must run
        /// first (SaveCodec emits zones before houses). Defensively a no-op if
        /// the lot already has a house or its zone isn't unlocked.
        /// </summary>
        public void RestoreBuiltHouse(int houseId, int level, bool isVacant, HouseVariant variant)
        {
            if (!IsLotBuildable(houseId))
            {
                return;
            }

            var lot = FindLotInUnlockedZones(houseId);
            if (lot == null)
            {
                return;
            }

            houses.Add(new House(houseId, lot.Quadrant, isVacant, level, variant));
            InvalidateWalkNetwork();
        }

        /// <summary>
        /// Resolves the <see cref="HouseLot"/> for any known house id — the
        /// starting layout's lots (#38) or an unlocked zone's lots (#56) —
        /// for callers (Unity's WorldBuilder) that need a built house's
        /// position/quadrant. Throws if the id isn't part of the starting
        /// layout or any unlocked zone.
        /// </summary>
        public HouseLot GetHouseLot(int houseId)
        {
            var startingLot = NeighborhoodLayout.HouseLots.FirstOrDefault(lot => lot.HouseId == houseId);
            if (startingLot != null)
            {
                return startingLot;
            }

            var zoneLot = FindLotInUnlockedZones(houseId);
            if (zoneLot != null)
            {
                return zoneLot;
            }

            throw new ArgumentException(
                $"No house lot with id {houseId} in the starting layout or any unlocked zone.", nameof(houseId));
        }

        /// <summary>
        /// The current upgrade level of the built house with
        /// <paramref name="houseId"/> (#460), or <see cref="House.InitialLevel"/>
        /// (level 1) when no house with that id is built yet. Level-aware fence
        /// geometry (<see cref="LotFence.GeometryFor(HouseLot, GameState)"/>)
        /// reads it so a lot's backyard connectors track the house's actual
        /// mesh as it upgrades; the InitialLevel default keeps a not-yet-built
        /// lot's queryable geometry byte-identical to the level-blind form.
        /// </summary>
        public int GetHouseLevel(int houseId)
        {
            var house = houses.FirstOrDefault(candidate => candidate.Id == houseId);
            return house == null ? House.InitialLevel : house.Level;
        }

        private HouseLot FindLotInUnlockedZones(int houseId)
        {
            foreach (var zone in unlockedZones)
            {
                var lot = zone.Lots.FirstOrDefault(candidate => candidate.HouseId == houseId);
                if (lot != null)
                {
                    return lot;
                }
            }

            return null;
        }

        /// <summary>First-launch tutorial flag (#44); persists in the save.</summary>
        public bool OnboardingComplete { get; private set; }

        public void MarkOnboardingComplete()
        {
            OnboardingComplete = true;
        }

        /// <summary>#310: UTC instant of the most recent quest-rotation
        /// refresh, or null until the first refresh runs. Persists in the save
        /// (like <see cref="OnboardingComplete"/>) so the 8h refresh cadence
        /// (<see cref="Quests.QuestPacingPolicy.ShouldRefresh"/>) holds across
        /// sessions. Stored as UTC so it is unaffected by device-timezone
        /// changes.</summary>
        public DateTime? LastRotationUtc { get; private set; }

        /// <summary>#310: records that a rotation refresh happened at
        /// <paramref name="nowUtc"/>. Caller passes a UTC instant
        /// (<c>DateTime.UtcNow</c> in production) — never a local time.</summary>
        public void RecordRotationUtc(DateTime nowUtc)
        {
            LastRotationUtc = nowUtc;
        }
    }
}
