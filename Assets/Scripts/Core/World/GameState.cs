using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Dogs;

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

        private readonly List<PlacedItem> placedItems = new List<PlacedItem>();
        private readonly List<Decorations.Decoration> decorations = new List<Decorations.Decoration>();
        private readonly List<Dog> dogs;
        private readonly List<Zone> unlockedZones = new List<Zone>();

        /// <summary>Owns the shared move-in pity counter and easter-egg
        /// reserve (#54). Not yet persisted through SaveCodec — see
        /// docs/specs/expansion.md's move-in system note.</summary>
        private readonly Expansion.MoveInSystem moveInSystem = new Expansion.MoveInSystem();

        public IReadOnlyList<House> Houses { get; }

        public IReadOnlyList<Dog> Dogs
        {
            get { return dogs; }
        }

        public Economy.Wallet Wallet { get; }
        public Quests.QuestManager Quests { get; }

        /// <summary>The grid-coordinate tile map (#109), seeded with just
        /// the starting FourWay intersection until zones are unlocked (#56).</summary>
        public TileMap Map { get; }

        /// <summary>Zones unlocked so far, in unlock order (#56). Empty for
        /// a new game — the starting intersection isn't itself a zone.</summary>
        public IReadOnlyList<Zone> UnlockedZones
        {
            get { return unlockedZones; }
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
            Houses = houses;
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

        public void AddDecoration(Decorations.Decoration decoration)
        {
            decorations.Add(decoration);
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
            return true;
        }

        /// <summary>Whether <paramref name="houseId"/> (a <see cref="HouseLot"/>
        /// id from a <see cref="Zone"/>, or the starting layout) has no
        /// <see cref="House"/> built on it yet (#56, #57) — a freshly
        /// unlocked zone reports every one of its lots buildable this way.</summary>
        public bool IsLotBuildable(int houseId)
        {
            return Houses.All(house => house.Id != houseId);
        }

        /// <summary>First-launch tutorial flag (#44); persists in the save.</summary>
        public bool OnboardingComplete { get; private set; }

        public void MarkOnboardingComplete()
        {
            OnboardingComplete = true;
        }
    }
}
