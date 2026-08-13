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

        /// <summary>#295/#453: the single scripted tile the onboarding "expand
        /// the map" step unlocks. Before that step completes it is the only
        /// unlockable frontier coordinate (Derek's "this only works after the
        /// first onboarding expansion happens"). A plain constant — the confirmed
        /// first cul-de-sac directly north of the starting intersection, (0,1)
        /// (docs/specs/expansion.md "Map shape"). Was sourced from
        /// <c>ZoneCatalog.FirstZone</c> until #453 retired the legacy zone path;
        /// the coordinate is unchanged.</summary>
        private static readonly TileCoordinate OnboardingExpansionTile = new TileCoordinate(0, 1);

        private readonly List<House> houses;
        private readonly List<PlacedItem> placedItems = new List<PlacedItem>();
        private readonly List<Decorations.Decoration> decorations = new List<Decorations.Decoration>();
        private readonly List<Dog> dogs;

        /// <summary>#434/#453: the <see cref="HouseVariant"/> rolled and locked in
        /// for each frontier lot the moment its tile unlocks (its lots first
        /// appear), keyed by house id — so an empty lot already knows the house
        /// that will stand on it (its footprint, its trees) before it is built.
        /// Populated by <see cref="TryUnlockTile"/>, persisted through
        /// <see cref="SaveCodec"/>, and read (with a deterministic fallback) by
        /// <see cref="TryBuildHouse"/>. A built lot keeps its entry — the built
        /// <see cref="House"/> is the source of truth from then on, so the save
        /// emits it as a house= line rather than an unbuilt lotvariant= line.</summary>
        private readonly Dictionary<int, HouseVariant> assignedLotVariants = new Dictionary<int, HouseVariant>();

        /// <summary>#295: the coordinates unlocked one-at-a-time through the
        /// player-choice frontier (<see cref="TryUnlockTile"/>), in unlock
        /// order. Distinct from the retired zone group
        /// model this superseded; both place onto <see cref="Map"/>.</summary>
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

        /// <summary>#539: the count of non-green-space tiles on <see cref="Map"/>
        /// (the origin FourWay plus every player-unlocked road tile). The
        /// road-unlock cost curve (<see cref="Expansion.TileUnlock.Cost"/>) scales
        /// on this rather than raw <c>Map.Tiles.Count</c> so the free auto-placed
        /// green spaces — which also live in <c>Map.Tiles</c> — never inflate the
        /// price of the next road unlock. With no green space placed this equals
        /// <c>Map.Tiles.Count</c>, so pre-#539 behavior is unchanged.</summary>
        private int RoadTileCount
        {
            get
            {
                var count = 0;
                foreach (var entry in Map.Tiles)
                {
                    if (entry.Value != TileType.GreenSpace)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

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
        /// and calls this once at bootstrap. #539: this is also the single
        /// "after target-map/save restore" hook where the green-space activation
        /// pass first runs — a loaded game's replayed <c>tile=</c> road lines are
        /// already on <see cref="Map"/> by now, so re-running the pass here
        /// reproduces every green space they qualify (pure replay, no save
        /// line).</summary>
        public void SetTargetMap(TileMap targetMap)
        {
            TargetMap = targetMap;
            ActivateGreenSpaces();
        }

        /// <summary>#539: runs the green-space auto-activation pass to a fixpoint
        /// — repeatedly placing every target <see cref="TileType.GreenSpace"/>
        /// coordinate that <see cref="Expansion.GreenSpaceActivation.Compute"/>
        /// now finds eligible (2+ edges bordering a placed tile) until no more
        /// qualify. Looping (not single-hop) is what lets one freshly activated
        /// green space make an adjacent one newly eligible in the same call. Free
        /// — no <see cref="Wallet"/> interaction — and green spaces are placed
        /// straight onto <see cref="Map"/> WITHOUT being recorded in
        /// <see cref="UnlockedTiles"/>: they are not player unlocks, carry no
        /// lots, are re-derived on load rather than persisted, and must not
        /// inflate the road-unlock cost curve. A no-op when no target map is
        /// supplied.</summary>
        private void ActivateGreenSpaces()
        {
            if (TargetMap == null)
            {
                return;
            }

            var placedAny = false;
            bool placedThisPass;
            do
            {
                placedThisPass = false;
                foreach (var coordinate in Expansion.GreenSpaceActivation.Compute(Map, TargetMap))
                {
                    // Compute keys purely on the 2-edge rule; guard with CanPlace
                    // so a hypothetical road/no-road authoring mismatch is skipped
                    // rather than thrown (a valid authored map never trips this).
                    if (Map.CanPlace(coordinate, TileType.GreenSpace))
                    {
                        Map.Place(coordinate, TileType.GreenSpace);
                        placedThisPass = true;
                        placedAny = true;
                    }
                }
            }
            while (placedThisPass);

            if (placedAny)
            {
                InvalidateWalkNetwork();
            }
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
        /// Rebuilds after a successful <see cref="TryUnlockTile"/> or
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

        /// <summary>#295: the coordinates the player has unlocked one-at-a-time
        /// via <see cref="TryUnlockTile"/>, in unlock order. Persisted as a set
        /// (SaveCodec) so player-chosen unlock order round-trips.</summary>
        public IReadOnlyList<TileCoordinate> UnlockedTiles
        {
            get { return unlockedTiles; }
        }

        /// <summary>#434/#453: the pre-assigned <see cref="HouseVariant"/> for
        /// each frontier lot, keyed by house id — rolled and locked in at unlock
        /// so an empty lot reads as the plot of its predetermined house. Persisted
        /// (SaveCodec) for unbuilt lots; a built lot's variant lives on its
        /// <see cref="House"/> instead. Empty for a game with no tile
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

        /// <summary>#704: how many of <see cref="Dogs"/> came from
        /// <see cref="DogRoster.CreateStartingDogs"/>. Every dog past this
        /// index moved in during play and so must be persisted — the starters
        /// are recreated by <see cref="CreateNew"/> on every load, exactly like
        /// the four starting houses.</summary>
        private readonly int startingDogCount;

        private GameState(IReadOnlyList<House> houses, IReadOnlyList<Dog> startingDogs)
        {
            this.houses = new List<House>(houses);
            dogs = new List<Dog>(startingDogs);
            startingDogCount = startingDogs.Count;
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
        /// step is already past).
        /// </summary>
        public bool TryUnlockTile(TileCoordinate coordinate)
        {
            if (!UnlockableFrontier().Contains(coordinate))
            {
                return false;
            }

            if (!Wallet.TrySpend(Expansion.TileUnlock.Cost(RoadTileCount)))
            {
                return false;
            }

            Map.Place(coordinate, TargetMap.GetTileAt(coordinate));
            unlockedTiles.Add(coordinate);
            AssignFrontierLotVariants(coordinate);
            InvalidateWalkNetwork();
            AdvanceRewardChain(OnboardingRewardStep.ExpandMap);
            // #539: a road placement can newly qualify green-space neighbors —
            // run the free activation pass to its fixpoint after the unlock.
            ActivateGreenSpaces();
            return true;
        }

        /// <summary>#453: rolls and locks in each of a freshly unlocked frontier
        /// tile's lot variants (ladder + tint) the moment the lots appear, so the
        /// empty lot already reads as the plot of its predetermined house — the
        /// frontier equivalent of the retired zone path's <c>AssignZoneLotVariants</c>.
        /// The roll is the same deterministic
        /// <see cref="HouseVariantAssignment.ForHouse"/> the build path falls back
        /// to, so assigning it here only changes WHEN it happens, not the value.
        /// Every frontier lot id is >= <see cref="FrontierHouseId.BaseId"/>, well
        /// above <see cref="HouseVariantAssignment.FirstZoneHouseId"/>, so none is
        /// rejected.</summary>
        private void AssignFrontierLotVariants(TileCoordinate coordinate)
        {
            foreach (var lot in LotsForUnlockedTile(coordinate))
            {
                assignedLotVariants[lot.HouseId] = HouseVariantAssignment.ForHouse(lot.HouseId);
            }
        }

        /// <summary>
        /// #295: restores a persisted player-unlocked tile on load — places it
        /// onto <see cref="Map"/> with its persisted type and records it in
        /// <see cref="UnlockedTiles"/>, WITHOUT charging the wallet or advancing
        /// the reward chain (both persist separately). Defensively a no-op if the
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
            // #453: variants are NOT re-assigned here — a persisted unbuilt lot's
            // variant is restored from its lotvariant= line (RestoreAssignedLotVariant),
            // and an unpersisted one falls back to the deterministic roll at build
            // (TryBuildHouse), exactly as the retired zone-restore path did.
            InvalidateWalkNetwork();
        }

        /// <summary>A newly moved-in dog (#54) joins the live roster
        /// immediately — eligible for the very next daily quest rotation,
        /// exactly like any other quest-free dog.</summary>
        public void AddDog(Dog dog)
        {
            dogs.Add(dog);
        }

        /// <summary>#704: the dogs that moved in during play — everyone past
        /// the starting roster <see cref="CreateNew"/> seeds. These are the
        /// dogs <see cref="SaveCodec"/> persists (the starters are recreated on
        /// every load, like the four starting houses), so the neighborhood
        /// keeps the population it grew to instead of resetting to 8.</summary>
        public IReadOnlyList<Dog> MovedInDogs
        {
            get { return dogs.Skip(startingDogCount).ToList(); }
        }

        /// <summary>#704: restores a persisted moved-in dog on load — adds it
        /// to the live roster WITHOUT rolling a move-in, filling a house, or
        /// paying the move-in reward (the parallel of
        /// <see cref="RestoreBuiltHouse"/>). Defensively ignores a name already
        /// on the roster, since dog names are unique among active dogs and a
        /// replayed line must never double a household. Also ignores a dog
        /// whose house is not in the save at all: the scene spawns a view per
        /// dog by looking its house up, so a resident of a house that never
        /// loaded would take the whole launch down — a save should never
        /// contain one, but a truncated or hand-edited file must still
        /// open.</summary>
        public void RestoreDog(Dog dog)
        {
            if (dogs.Any(existing => existing.Name == dog.Name)
                || houses.All(house => house.Id != dog.HouseId))
            {
                return;
            }

            dogs.Add(dog);
        }

        /// <summary>#704: the load-time repair for a legacy save's
        /// occupied-but-empty house. Before moved-in dogs were persisted, a
        /// filled house round-tripped as occupied while its residents did not —
        /// leaving a house with no dogs in it that could never take another
        /// move-in either (<see cref="Expansion.HouseOccupancy.ApplyMoveIn"/>
        /// only ever considers vacant houses). Any house with no resident on
        /// the roster is marked vacant again so it re-enters the move-in pool.
        /// Run once at the end of <see cref="SaveCodec.Load"/>, after every
        /// house= and dog= line has been replayed; a post-#704 save has a
        /// resident for every occupied house, so it is a no-op there.</summary>
        public void VacateHousesWithNoResidents()
        {
            foreach (var house in houses)
            {
                if (house.IsVacant || dogs.Any(dog => dog.HouseId == house.Id))
                {
                    continue;
                }

                house.MarkVacant();
            }
        }

        /// <summary>The #54/#58 move-in hook: called once per completed
        /// quest (QuestManager.Complete). Rolls the shared pity counter
        /// against whichever houses currently report vacant, and on
        /// success fills exactly one — flipping its vacancy and adding
        /// its new dog(s) to the live roster immediately. Returns the
        /// newly moved-in household (empty when nothing happened).
        ///
        /// <para>#675: a successful move-in also pays the player
        /// <see cref="Economy.EconomyNumbers.MoveInReward"/> — once per
        /// household, never once per dog, since one move-in fills exactly one
        /// house. The deposit hangs off the move-in state change itself (here,
        /// beside the household placement) rather than off the welcome pop-up:
        /// the pop-up is presentation and can be dismissed or missed, and a
        /// payout wired to it would silently cost the player the coins. It is an
        /// ordinary <see cref="Economy.Wallet.Deposit"/> — the same path quest
        /// and onboarding payouts use, not a second money-granting
        /// mechanism.</para>
        ///
        /// <para><b>Invariant — move-in income is rate-limited by quest
        /// completions, never by the number of vacant houses.</b> This runs once
        /// per completed quest and <see cref="Expansion.HouseOccupancy.ApplyMoveIn"/>
        /// fills at most one house per call, so however many houses stand empty
        /// the player collects at most one reward per completion. Building more
        /// houses stockpiles vacancies; it never raises the move-in rate, so
        /// there is no build-to-earn loop.</para></summary>
        public IReadOnlyList<Dog> HandleQuestCompleted(Random rng)
        {
            var household = Expansion.HouseOccupancy.ApplyMoveIn(Houses, moveInSystem, Dogs, rng);
            foreach (var dog in household)
            {
                AddDog(dog);
            }

            if (household.Count > 0)
            {
                Wallet.Deposit(Economy.EconomyNumbers.MoveInReward);
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
        /// <see cref="RestoreRewardChainStep"/>).
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

        /// <summary>#434: restores a persisted lot-variant assignment on load —
        /// records the <paramref name="variant"/> for an unbuilt frontier lot so
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
        /// id from an unlocked frontier tile, or the starting layout) has no
        /// <see cref="House"/> built on it yet (#56, #57) — a freshly
        /// unlocked tile reports every one of its lots buildable this way.</summary>
        public bool IsLotBuildable(int houseId)
        {
            return Houses.All(house => house.Id != houseId);
        }

        /// <summary>#540: how many houses the PLAYER has built — the total
        /// <see cref="Houses"/> minus the 4 starting houses seeded at
        /// <see cref="CreateNew"/> (<see cref="NeighborhoodLayout.HouseLots"/>).
        /// This is the count the house-build cost curve
        /// (<see cref="Expansion.HouseBuildNumbers.Cost"/>) scales on, so the
        /// first player build is at the base and the 4 starting houses never
        /// inflate it.</summary>
        public int PlayerBuiltHouseCount
        {
            get { return houses.Count - NeighborhoodLayout.HouseLots.Count; }
        }

        /// <summary>
        /// Builds a house on <paramref name="houseId"/>'s lot (#57): charges
        /// <see cref="Expansion.HouseBuildNumbers.Cost"/> from <see cref="Wallet"/>
        /// and adds a new <see cref="House"/> at <see cref="House.InitialLevel"/>,
        /// vacant (#58). Returns false with no state change (no deduction,
        /// no house added) when the lot already has a house
        /// (<see cref="IsLotBuildable"/> false), the lot's tile hasn't been
        /// unlocked yet, or the balance can't afford the cost.
        /// </summary>
        public bool TryBuildHouse(int houseId)
        {
            if (!IsLotBuildable(houseId))
            {
                return false;
            }

            var lot = FindFrontierLot(houseId);
            if (lot == null)
            {
                return false;
            }

            if (!Wallet.TrySpend(Expansion.HouseBuildNumbers.Cost(PlayerBuiltHouseCount)))
            {
                return false;
            }

            // #434: the art variant (ladder + tint) was already rolled and
            // persisted when the tile unlocked (AssignedLotVariants), so the
            // build reads that pre-assignment rather than re-rolling. If no entry
            // is present the deterministic #299 roll is the fallback —
            // bit-identical, since it is a pure function of the house id.
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
        /// #299/#453: restores a frontier-built house on load — recreates the
        /// <see cref="House"/> at its persisted level/vacancy carrying its
        /// persisted <see cref="HouseVariant"/> (ladder + tint), WITHOUT
        /// charging the wallet or advancing the reward chain (the parallel of
        /// <see cref="RestoreRewardChainStep"/>). The house's quadrant comes from
        /// its lot on the already-restored unlocked tiles, so the tile= lines must
        /// be replayed first (SaveCodec emits tiles before houses). Defensively a
        /// no-op if the lot already has a house or its tile isn't unlocked.
        /// </summary>
        public void RestoreBuiltHouse(int houseId, int level, bool isVacant, HouseVariant variant)
        {
            if (!IsLotBuildable(houseId))
            {
                return;
            }

            var lot = FindFrontierLot(houseId);
            if (lot == null)
            {
                return;
            }

            houses.Add(new House(houseId, lot.Quadrant, isVacant, level, variant));
            InvalidateWalkNetwork();
        }

        /// <summary>
        /// Resolves the <see cref="HouseLot"/> for any known house id — the
        /// starting layout's lots (#38) or an unlocked frontier tile's lots
        /// (#295/#453) — for callers (Unity's WorldBuilder) that need a built
        /// house's position/quadrant. Throws if the id isn't part of the starting
        /// layout or any unlocked frontier tile.
        /// </summary>
        public HouseLot GetHouseLot(int houseId)
        {
            var startingLot = NeighborhoodLayout.HouseLots.FirstOrDefault(lot => lot.HouseId == houseId);
            if (startingLot != null)
            {
                return startingLot;
            }

            var frontierLot = FindFrontierLot(houseId);
            if (frontierLot != null)
            {
                return frontierLot;
            }

            throw new ArgumentException(
                $"No house lot with id {houseId} in the starting layout or any unlocked frontier tile.",
                nameof(houseId));
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

        /// <summary>#453: the <see cref="HouseLot"/> for a frontier lot id
        /// (id from <see cref="FrontierHouseId.For"/>) across every currently
        /// unlocked tile, or null if no unlocked tile carries that lot — the
        /// frontier replacement for the retired zone-lot lookup. A pure derivation
        /// from <see cref="UnlockedTiles"/> + the tile catalog, so no per-lot
        /// state is stored.</summary>
        private HouseLot FindFrontierLot(int houseId)
        {
            foreach (var coordinate in unlockedTiles)
            {
                foreach (var lot in LotsForUnlockedTile(coordinate))
                {
                    if (lot.HouseId == houseId)
                    {
                        return lot;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// #453: the <see cref="HouseLot"/> slots an unlocked frontier tile
        /// carries — one per quadrant lot the tile's type defines
        /// (<see cref="TileLotCatalog.LotsFor"/>), positioned from the tile centre
        /// (<see cref="TileGeometry.CenterOf"/>) and given the stable
        /// <see cref="FrontierHouseId.For"/> id. The tile type is read from the
        /// live <see cref="Map"/> (the coordinate is already placed), so this
        /// needs no <see cref="TargetMap"/> on hand. A coordinate that isn't
        /// placed yields none, as does the ORIGIN <see cref="TileType.FourWay"/>
        /// (whose four lots belong to the starting layout, not the per-type
        /// catalog — #607); a non-origin FourWay is a full intersection and
        /// yields its four quadrant lots like every other lotted tile. The Unity
        /// world-build reads this to render a frontier tile's empty lots, its
        /// roads, and its pre-baked trees — the frontier replacement for the
        /// retired per-zone <c>Zone.Lots</c>.
        /// </summary>
        public IReadOnlyList<HouseLot> LotsForUnlockedTile(TileCoordinate coordinate)
        {
            var lots = new List<HouseLot>();
            if (!Map.HasTileAt(coordinate))
            {
                return lots;
            }

            var type = Map.GetTileAt(coordinate);
            // #607: the ORIGIN FourWay's four lots are seeded from
            // NeighborhoodLayout (ids 1-4), so it must not also emit
            // catalog lots — that would collide with the seeded origin
            // houses. Every OTHER FourWay is a full intersection whose four
            // quadrants are buildable, served generically below like every
            // other lotted tile.
            if (type == TileType.FourWay && coordinate.Equals(StartingIntersectionCoordinate))
            {
                return lots;
            }

            var center = TileGeometry.CenterOf(coordinate);
            foreach (var pair in TileLotCatalog.LotsFor(type))
            {
                var quadrant = pair.Key;
                var offset = pair.Value;
                var position = new GridPoint(center.X + offset.X, center.Z + offset.Z);
                lots.Add(new HouseLot(FrontierHouseId.For(coordinate, quadrant), quadrant, position));
            }

            return lots;
        }

        /// <summary>First-launch tutorial flag (#44); persists in the save.</summary>
        public bool OnboardingComplete { get; private set; }

        public void MarkOnboardingComplete()
        {
            OnboardingComplete = true;
        }

        /// <summary>#310: UTC instant of the most recent quest-rotation
        /// refresh, or null until the first refresh runs. Persists in the save
        /// (like <see cref="OnboardingComplete"/>) so the hourly refresh cadence
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

        /// <summary>#704: the UTC instant the quest board dropped below its
        /// population-scaled target — the start of the wait for the next
        /// trickle top-up — or null while the board is full (nothing is being
        /// waited for, so no clock runs). Persisted through
        /// <see cref="SaveCodec"/> so the wait is measured in elapsed time and
        /// never restarted by a relaunch. Maintained in one place,
        /// <see cref="Quests.QuestManager.TickPacing"/>, which the app polls
        /// while it is open as well as at launch.</summary>
        public DateTime? QuestRefreshTimerStartedUtc { get; private set; }

        /// <summary>#704: records (or clears, with null) the start of the wait
        /// for the next top-up. Caller passes a UTC instant — never a local
        /// time. Also the restore path used by <see cref="SaveCodec"/>.</summary>
        public void RecordQuestRefreshTimerStart(DateTime? startedUtc)
        {
            QuestRefreshTimerStartedUtc = startedUtc;
        }

        /// <summary>#543: the persisted fractional quest-pacing accumulator — the
        /// leftover fraction of a quest carried between hourly refreshes by the
        /// error-diffusion trickle
        /// (<see cref="Quests.QuestPacingPolicy.AdvanceAccumulator"/>). Persisted
        /// through <see cref="SaveCodec"/> (like the move-in pity counter) so the
        /// trickle cadence survives a relaunch instead of snapping back to a
        /// whole-hour boundary; always in <c>[0, 1)</c>. A legacy save with no
        /// accumulator line loads this at its <see cref="CreateNew"/> default of
        /// 0.0, so no migration is needed.</summary>
        public double QuestPacingAccumulator { get; private set; }

        /// <summary>#543: records the leftover fraction to carry to the next
        /// hourly refresh. Called by <see cref="Quests.QuestManager.StartNewDay"/>
        /// every boundary — regardless of downstream headroom/free-dog clamping —
        /// so fractional progress is never lost.</summary>
        public void RecordQuestPacingAccumulator(double remainder)
        {
            QuestPacingAccumulator = remainder;
        }

        /// <summary>#543: restores the persisted trickle accumulator on load (the
        /// parallel of <see cref="RestoreMoveInState"/>); a legacy save with no
        /// accumulator line simply keeps the 0.0 default.</summary>
        public void RestoreQuestPacingAccumulator(double accumulator)
        {
            QuestPacingAccumulator = accumulator;
        }
    }
}
