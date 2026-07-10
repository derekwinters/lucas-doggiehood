namespace Doggiehood.Core.World
{
    /// <summary>A house in the neighborhood, keyed to its lot (#38).</summary>
    public sealed class House
    {
        public int Id { get; }
        public Quadrant Quadrant { get; }

        public House(int id, Quadrant quadrant)
        {
            Id = id;
            Quadrant = quadrant;
        }
    }
}
