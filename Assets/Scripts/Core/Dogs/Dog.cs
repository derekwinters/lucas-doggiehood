namespace Doggiehood.Core.Dogs
{
    /// <summary>
    /// A dog in the neighborhood (#35, #36, #63) with its pose state machine
    /// (#66). Transitions are guarded by the documented conditions: Rest
    /// needs a selected comfort decoration (#52), Sit needs an accepted
    /// "buy me X" quest and being home (#30), WindowWatch tracks placement
    /// inside a house (#9).
    /// </summary>
    public sealed class Dog
    {
        public string Name { get; }
        public Breed Breed { get; }
        public Personality Personality { get; }
        public int HouseId { get; }
        public bool IsPuppy { get; }
        public CoatColor Coat { get; }

        public DogLocation Location { get; private set; }
        public DogState State { get; private set; }
        public bool HasActiveQuest { get; private set; }

        /// <summary>#470: true while a buy-gift/decoration delivery is in
        /// flight (the quest's DeliveryPhase is HeadingHome or
        /// WaitingForDelivery). Set by <see cref="BeginDelivery"/> when the
        /// quest is accepted and cleared by <see cref="PlaceOnStreet"/> when
        /// the truck hands the dog back. While true the QuestDirector owns the
        /// dog's transform (walking it home), so <see cref="WantsToWander"/>
        /// must stay off — otherwise the wander branch and WalkDogHome fight
        /// over the same transform every frame.</summary>
        private bool deliveryInProgress;

        /// <summary>#470: a monotonic signal the Unity layer watches to know
        /// "pick a FRESH wander target now" rather than resuming a cached one.
        /// Bumped every time the dog is (re)placed on the street — most
        /// importantly when a delivery hands control back — so the resumed
        /// wander can't beeline off-network to the stale pre-quest target.</summary>
        public int WanderResetToken { get; private set; }

        /// <summary>Visual/flavor only (#47): read by animation and dialogue
        /// tone. Never gates quests, rewards, or any other logic — a guard
        /// test asserts quest behavior is identical across its range.</summary>
        public int Happiness { get; private set; }

        /// <summary>Only street dogs wander (#8, #9) — and not while a
        /// delivery is in flight (#470), so the scripted walk home and the
        /// wander branch never drive the same transform at once.</summary>
        public bool WantsToWander
        {
            get
            {
                return Location == DogLocation.Street
                    && State == DogState.IdleWander
                    && !deliveryInProgress;
            }
        }

        public Dog(string name, Breed breed, Personality personality, int houseId, bool isPuppy,
            CoatColor coat = CoatColor.Default)
        {
            Name = name;
            Breed = breed;
            Personality = personality;
            HouseId = houseId;
            IsPuppy = isPuppy;
            Coat = coat;
            Location = DogLocation.Street;
            State = DogState.IdleWander;
        }

        public void IncreaseHappiness(int amount)
        {
            Happiness += amount;
        }

        /// <summary>Test/flavor hook: happiness is informational, so setting
        /// it directly is safe by design.</summary>
        public void SetHappinessForFlavor(int value)
        {
            Happiness = value;
        }

        public void GiveQuest()
        {
            HasActiveQuest = true;
        }

        public void ClearQuest()
        {
            HasActiveQuest = false;
        }

        /// <summary>#470: marks the dog as mid-delivery so it stops wandering
        /// while the QuestDirector walks it home. Cleared by
        /// <see cref="PlaceOnStreet"/> when the package is delivered.</summary>
        public void BeginDelivery()
        {
            deliveryInProgress = true;
        }

        public void PlaceInsideAtWindow()
        {
            Location = DogLocation.InsideAtWindow;
            State = DogState.WindowWatch;
        }

        public void PlaceOnStreet()
        {
            Location = DogLocation.Street;
            State = DogState.IdleWander;
            // #470: the dog is back under wander control — end any in-flight
            // delivery and bump the reset token so the Unity layer picks a
            // fresh wander target instead of the stale pre-quest one.
            deliveryInProgress = false;
            WanderResetToken++;
        }

        public bool TryRest(bool comfortDecorationSelected)
        {
            if (!comfortDecorationSelected || Location != DogLocation.Street)
            {
                return false;
            }

            State = DogState.Rest;
            return true;
        }

        public bool TrySit(bool buyQuestAccepted, bool isAtHome)
        {
            if (!buyQuestAccepted || !isAtHome || Location != DogLocation.Street)
            {
                return false;
            }

            State = DogState.Sit;
            return true;
        }
    }
}
