namespace Doggiehood.Core.World
{
    /// <summary>A house in the neighborhood, keyed to its lot (#38).</summary>
    public sealed class House
    {
        /// <summary>The level every newly built house starts at (#57).
        /// The full upgrade path (levels 2-4, upgrade costs, decoration-slot
        /// caps) is #59's job — this only pins the starting value so a
        /// house built via GameState.TryBuildHouse can commit to "level 1"
        /// now, per docs/specs/expansion.md#house-leveling.</summary>
        public const int InitialLevel = 1;

        public int Id { get; }
        public Quadrant Quadrant { get; }

        /// <summary>
        /// Whether this house has no dog living in it yet (#58). A freshly
        /// built house starts vacant and stays that way until #54's
        /// move-in system fills it (<see cref="MarkOccupied"/>) — the only
        /// place this flips. Defaults to true so "a newly built house" is
        /// vacant with no extra ceremony; GameState.CreateNew's 4 starting
        /// houses pass isVacant: false since their dogs (#63) already live
        /// there.
        /// </summary>
        public bool IsVacant { get; private set; }

        /// <summary>This house's current level (#59's full upgrade system
        /// isn't built yet — every house stays at whatever level it was
        /// constructed with).</summary>
        public int Level { get; }

        public House(int id, Quadrant quadrant, bool isVacant = true, int level = InitialLevel)
        {
            Id = id;
            Quadrant = quadrant;
            IsVacant = isVacant;
            Level = level;
        }

        /// <summary>A household has moved in (#54) — vacancy never
        /// reverses from here (there is no "move out").</summary>
        public void MarkOccupied()
        {
            IsVacant = false;
        }
    }
}
