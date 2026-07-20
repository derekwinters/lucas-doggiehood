namespace Doggiehood.Core.World
{
    /// <summary>A house in the neighborhood, keyed to its lot (#38).</summary>
    public sealed class House
    {
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

        public House(int id, Quadrant quadrant, bool isVacant = true)
        {
            Id = id;
            Quadrant = quadrant;
            IsVacant = isVacant;
        }

        /// <summary>A household has moved in (#54) — vacancy never
        /// reverses from here (there is no "move out").</summary>
        public void MarkOccupied()
        {
            IsVacant = false;
        }
    }
}
