using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Expansion
{
    /// <summary>
    /// #539: the green-space auto-activation eligibility test. Given the placed
    /// <see cref="TileMap"/> and the authored target, a target
    /// <see cref="TileType.GreenSpace"/> coordinate is eligible only when 2+ of
    /// its 4 edges border a tile already in <c>placed</c> (a placed road tile OR
    /// an already-activated green space). 0 or 1 bordering edges excludes it.
    /// A pure function of its two inputs, same shape as
    /// <see cref="TileFrontier.Compute"/>.
    /// </summary>
    public class GreenSpaceActivationTests
    {
        [Test]
        public void Compute_TwoEdgesBorderPlacedTiles_ReturnsTheGreenSpace()
        {
            // placed: L-shape (0,0)+(1,0)+(0,1), so the green space at (1,1)
            // borders two placed tiles — its West (0,1) and South (1,0) edges.
            var placed = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            placed.Place(new TileCoordinate(1, 0), TileType.FourWay);
            placed.Place(new TileCoordinate(0, 1), TileType.FourWay);

            var target = new TileMap(new TileCoordinate(1, 1), TileType.GreenSpace);

            var activated = GreenSpaceActivation.Compute(placed, target);

            Assert.That(activated, Does.Contain(new TileCoordinate(1, 1)));
        }

        [Test]
        public void Compute_OnlyOneEdgeBordersAPlacedTile_ExcludesTheGreenSpace()
        {
            // placed: just (0,0)+(1,0), so the green space at (1,1) borders only
            // its South neighbor (1,0) — a single edge.
            var placed = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            placed.Place(new TileCoordinate(1, 0), TileType.FourWay);

            var target = new TileMap(new TileCoordinate(1, 1), TileType.GreenSpace);

            var activated = GreenSpaceActivation.Compute(placed, target);

            Assert.That(activated, Does.Not.Contain(new TileCoordinate(1, 1)),
                "one bordering edge is not enough to activate");
        }

        [Test]
        public void Compute_NoEdgeBordersAPlacedTile_ExcludesTheGreenSpace()
        {
            var placed = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);

            // Green space far from the placed map — no bordering edge at all.
            var target = new TileMap(new TileCoordinate(5, 5), TileType.GreenSpace);

            var activated = GreenSpaceActivation.Compute(placed, target);

            Assert.That(activated, Is.Empty);
        }

        [Test]
        public void Compute_OnlyReturnsGreenSpaceCoordinates_NeverRoadTiles()
        {
            // A road-tile coordinate in the target that borders 2+ placed tiles
            // must NOT be returned — activation is green-space-only.
            var placed = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            placed.Place(new TileCoordinate(1, 0), TileType.FourWay);
            placed.Place(new TileCoordinate(0, 1), TileType.FourWay);

            var target = new TileMap(new TileCoordinate(1, 1), TileType.FourWay);

            var activated = GreenSpaceActivation.Compute(placed, target);

            Assert.That(activated, Is.Empty,
                "only GreenSpace coordinates are ever auto-activated");
        }

        [Test]
        public void TileFrontier_NeverReturnsAGreenSpace_EvenBorderingTwoPlacedTiles()
        {
            // #539 no-lock-icon regression: a green space bordering 2+ placed
            // tiles is eligible for AUTO-activation but must never be a paid,
            // lock-iconed frontier candidate. TileFrontier keys on a real road
            // connection, which a roadless green space can never form.
            var placed = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            placed.Place(new TileCoordinate(1, 0), TileType.FourWay);
            placed.Place(new TileCoordinate(0, 1), TileType.FourWay);

            var target = new TileMap(new TileCoordinate(1, 1), TileType.GreenSpace);

            var frontier = TileFrontier.Compute(placed, target);

            Assert.That(frontier, Does.Not.Contain(new TileCoordinate(1, 1)),
                "a green space is never on the paid unlock frontier");
        }

        [Test]
        public void Compute_ExcludesAGreenSpaceAlreadyPlaced()
        {
            var placed = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            placed.Place(new TileCoordinate(1, 0), TileType.FourWay);
            placed.Place(new TileCoordinate(0, 1), TileType.FourWay);

            // The green space is already in placed too (seed it there directly).
            var placedWithGreen = new TileMap(new TileCoordinate(1, 1), TileType.GreenSpace);

            var target = new TileMap(new TileCoordinate(1, 1), TileType.GreenSpace);

            var activated = GreenSpaceActivation.Compute(placedWithGreen, target);

            Assert.That(activated, Does.Not.Contain(new TileCoordinate(1, 1)),
                "an already-placed green space is not re-activated");
        }
    }
}
