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

        /// <summary>Only street dogs wander (#8, #9).</summary>
        public bool WantsToWander
        {
            get { return Location == DogLocation.Street && State == DogState.IdleWander; }
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

        public void GiveQuest()
        {
            HasActiveQuest = true;
        }

        public void ClearQuest()
        {
            HasActiveQuest = false;
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
