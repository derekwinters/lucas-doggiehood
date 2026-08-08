using System.Linq;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #540: the live cost-scaling seams thread the right COUNTS into the two
    /// balance functions. The tile curve counts only player-unlocked tiles (the
    /// seeded origin FourWay is excluded), and the house curve counts only
    /// player-built houses (the 4 starting houses are excluded) — so the first
    /// unlock and the first build are both at their base cost, and only later
    /// builds/unlocks scale up. (#674 moved the tile base to 200; the counting
    /// rules this class guards are unchanged, and the expectations read the
    /// live seams so they stay true through any further tuning.)
    /// </summary>
    public class ExpansionCostScalingTests
    {
        [Test]
        public void FirstUnlock_ChargesTheBase_SecondUnlock_ChargesBasePlusTen()
        {
            var state = FrontierTestWorld.AfterOnboarding();
            state.Wallet.Deposit(TileUnlockNumbers.BaseCost * 2 + TileUnlockNumbers.PerExistingTileStep);

            var before = state.Wallet.Coins;
            Assert.That(state.TryUnlockTile(new TileCoordinate(0, 1)), Is.True);
            var firstCharge = before - state.Wallet.Coins;
            Assert.That(firstCharge, Is.EqualTo(TileUnlockNumbers.BaseCost),
                "first unlock is at the base (origin excluded)");

            before = state.Wallet.Coins;
            Assert.That(state.TryUnlockTile(new TileCoordinate(1, 0)), Is.True);
            var secondCharge = before - state.Wallet.Coins;
            Assert.That(secondCharge,
                Is.EqualTo(TileUnlockNumbers.BaseCost + TileUnlockNumbers.PerExistingTileStep),
                "second unlock scales by one step for the one player-unlocked tile");
        }

        [Test]
        public void PlayerBuiltHouseCount_ExcludesTheFourStartingHouses()
        {
            var state = GameState.CreateNew();
            Assert.That(state.Houses.Count, Is.EqualTo(4), "the 4 starting houses exist");
            Assert.That(state.PlayerBuiltHouseCount, Is.EqualTo(0),
                "none of the starting houses count as player-built");
        }

        [Test]
        public void FirstBuild_CostsTheBase_EvenThoughFourStartingHousesExist()
        {
            var state = FrontierTestWorld.WithFirstTileUnlocked();
            var lotId = FrontierTestWorld.FirstLotId;

            var offer = HouseBuildOffer.Resolve(state, lotId);
            Assert.That(offer.Value.Cost, Is.EqualTo(HouseBuildNumbers.BaseCost),
                "the first player build is at the base — the 4 starting houses do not scale it");
        }

        [Test]
        public void SecondBuild_StaysAtTheBase_WhileUnderTheFourHouseBatch()
        {
            // Build the first frontier house; total Houses.Count becomes 5, but
            // player-built is only 1 (< HousesPerStep), so the NEXT build is still
            // at the base — proving the curve reads player-built houses, not the
            // total (which would already be at base+5).
            var state = FrontierTestWorld.WithFirstTileUnlocked(HouseBuildNumbers.BaseCost);
            Assert.That(state.TryBuildHouse(FrontierTestWorld.FirstLotId), Is.True);
            Assert.That(state.Houses.Count, Is.EqualTo(5));
            Assert.That(state.PlayerBuiltHouseCount, Is.EqualTo(1));

            var secondOffer = HouseBuildOffer.Resolve(state, FrontierTestWorld.SecondLotId);
            Assert.That(secondOffer.Value.Cost, Is.EqualTo(HouseBuildNumbers.BaseCost),
                "still under the 4-house batch, so the second build is at the base too");
        }
    }
}
