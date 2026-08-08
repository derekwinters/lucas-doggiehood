using Doggiehood.Core.Expansion;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Expansion
{
    /// <summary>
    /// #540, rebalanced #674: the per-tile frontier unlock cost is a single
    /// named, tunable balance function — a 200-coin base that rises by
    /// <see cref="TileUnlockNumbers.PerExistingTileStep"/> (10) for every tile
    /// the player has already unlocked. The origin FourWay the map is seeded
    /// with is excluded (<see cref="TileUnlockNumbers.OriginTileCount"/>), so
    /// the FIRST unlock — with only the origin placed — is at the base cost.
    /// No upper cap. Callers thread <c>Map.Tiles.Count</c> (total placed,
    /// origin included); the function subtracts the origin so the scaling counts
    /// only player-unlocked tiles.
    /// </summary>
    public class TileUnlockCostTests
    {
        [Test]
        public void BaseCost_IsTwoHundredCoins_MatchingTheCostOfFillingAFourLotTile()
        {
            // #674: expanding should cost about what filling the tile it opens
            // costs (4 lots x a 50-coin house = 200), so the player builds out a
            // tile before buying the next one instead of spreading out with
            // empty lots everywhere.
            Assert.That(TileUnlockNumbers.BaseCost, Is.EqualTo(200));
        }

        [Test]
        public void PerExistingTileStep_IsTenCoins()
        {
            Assert.That(TileUnlockNumbers.PerExistingTileStep, Is.EqualTo(10));
        }

        [Test]
        public void FirstUnlock_WithOnlyTheOriginPlaced_CostsTheBase()
        {
            // Map.Tiles.Count == 1 (only the seeded origin FourWay) at the first
            // unlock — the origin is excluded, so no scaling applies yet.
            Assert.That(TileUnlock.Cost(placedTileCount: 1), Is.EqualTo(TileUnlockNumbers.BaseCost));
        }

        [Test]
        public void Cost_RisesByTenPerPlayerUnlockedTile()
        {
            // placedTileCount = origin + N player-unlocked tiles.
            Assert.That(TileUnlock.Cost(placedTileCount: 1), Is.EqualTo(200), "0 player tiles -> base");
            Assert.That(TileUnlock.Cost(placedTileCount: 2), Is.EqualTo(210), "1 player tile -> +10");
            Assert.That(TileUnlock.Cost(placedTileCount: 3), Is.EqualTo(220), "2 player tiles -> +20");
            Assert.That(TileUnlock.Cost(placedTileCount: 6), Is.EqualTo(250), "5 player tiles -> +50");
        }

        [Test]
        public void Cost_HasNoUpperCap()
        {
            Assert.That(TileUnlock.Cost(placedTileCount: 101),
                Is.EqualTo(TileUnlockNumbers.BaseCost + TileUnlockNumbers.PerExistingTileStep * 100),
                "the linear curve keeps rising with no ceiling");
        }
    }
}
