using Doggiehood.Core.Expansion;
using Doggiehood.Core.Onboarding;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #539: <see cref="GameState"/>'s green-space activation pass. After a
    /// successful <see cref="GameState.TryUnlockTile"/> (and once after the
    /// target map is supplied on load), the pass loops
    /// <see cref="GreenSpaceActivation.Compute"/> + <see cref="TileMap.Place"/>
    /// to a fixpoint, so one activated green space can make an adjacent one
    /// newly eligible — all free, no wallet interaction.
    /// </summary>
    public class GreenSpaceGameStateTests
    {
        // A synthetic target map (not the shipped authored map, which has no
        // authored green space yet): an origin FourWay, an L of road tiles, and
        // two green spaces (1,1)+(2,1) arranged so green#1 activates off two
        // roads and green#2 activates only once green#1 is down — a genuine
        // two-hop cascade.
        private static TileMap BuildCascadeTarget()
        {
            var target = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            target.Place(new TileCoordinate(0, 1), TileType.StraightNS);
            target.Place(new TileCoordinate(1, 0), TileType.StraightEW);
            target.Place(new TileCoordinate(2, 0), TileType.StraightEW);
            target.Place(new TileCoordinate(3, 0), TileType.StraightEW);
            target.Place(new TileCoordinate(1, 1), TileType.GreenSpace);
            target.Place(new TileCoordinate(2, 1), TileType.GreenSpace);
            return target;
        }

        private static GameState AfterOnboarding(TileMap target)
        {
            var state = GameState.CreateNew();
            state.RestoreRewardChainStep(OnboardingRewardStep.Done);
            state.SetTargetMap(target);
            return state;
        }

        [Test]
        public void ActivationPass_PlacesAWholeCascadeInOneUnlock_NotJustOneHop()
        {
            var state = AfterOnboarding(BuildCascadeTarget());
            // Roads down first so green#1 sits at exactly one bordering edge and
            // green#2 at one — neither yet eligible.
            state.Wallet.Deposit(10000);
            Assert.That(state.TryUnlockTile(new TileCoordinate(1, 0)), Is.True, "unlock road (1,0)");
            Assert.That(state.TryUnlockTile(new TileCoordinate(2, 0)), Is.True, "unlock road (2,0)");
            Assert.That(state.Map.HasTileAt(new TileCoordinate(1, 1)), Is.False,
                "precondition: green#1 not yet activated (only its South road down)");

            // Unlocking (0,1) gives green#1 its second edge; the pass must then
            // cascade to green#2 in the SAME call.
            Assert.That(state.TryUnlockTile(new TileCoordinate(0, 1)), Is.True, "unlock road (0,1)");

            Assert.That(state.Map.HasTileAt(new TileCoordinate(1, 1)), Is.True,
                "green#1 auto-activated off two roads");
            Assert.That(state.Map.GetTileAt(new TileCoordinate(1, 1)), Is.EqualTo(TileType.GreenSpace));
            Assert.That(state.Map.HasTileAt(new TileCoordinate(2, 1)), Is.True,
                "green#2 cascaded off green#1 in the same unlock (fixpoint, not single-hop)");
            Assert.That(state.Map.GetTileAt(new TileCoordinate(2, 1)), Is.EqualTo(TileType.GreenSpace));
        }

        [Test]
        public void TryUnlockTile_AutoPlacesGreenNeighbors_WithoutChargingBeyondTheRoadCost()
        {
            var state = AfterOnboarding(BuildCascadeTarget());
            state.Wallet.Deposit(10000);
            Assert.That(state.TryUnlockTile(new TileCoordinate(1, 0)), Is.True);
            Assert.That(state.TryUnlockTile(new TileCoordinate(2, 0)), Is.True);

            var coinsBefore = state.Wallet.Coins;
            var roadCost = TileUnlock.Cost(state.Map.Tiles.Count);

            Assert.That(state.TryUnlockTile(new TileCoordinate(0, 1)), Is.True);

            Assert.That(state.Map.HasTileAt(new TileCoordinate(1, 1)), Is.True,
                "a green space auto-activated on this unlock");
            Assert.That(state.Wallet.Coins, Is.EqualTo(coinsBefore - roadCost),
                "only the road tile's own cost was charged — green activation is free");
        }

        [Test]
        public void UnlockableFrontier_NeverOffersAnActivatedOrActivatableGreenSpace()
        {
            var state = AfterOnboarding(BuildCascadeTarget());
            state.Wallet.Deposit(10000);
            state.TryUnlockTile(new TileCoordinate(1, 0));
            state.TryUnlockTile(new TileCoordinate(2, 0));
            state.TryUnlockTile(new TileCoordinate(0, 1)); // activates green spaces

            var frontier = state.UnlockableFrontier();

            Assert.That(frontier, Does.Not.Contain(new TileCoordinate(1, 1)),
                "an activated green space is never offered as a paid unlock");
            Assert.That(frontier, Does.Not.Contain(new TileCoordinate(2, 1)),
                "a green space never gets a lock icon");
        }

        [Test]
        public void ActivatedGreenSpaces_RoundTripByPureReplay_WithNoGreenSaveLine()
        {
            var target = BuildCascadeTarget();
            var state = AfterOnboarding(target);
            state.Wallet.Deposit(10000);
            state.TryUnlockTile(new TileCoordinate(1, 0));
            state.TryUnlockTile(new TileCoordinate(2, 0));
            state.TryUnlockTile(new TileCoordinate(0, 1));
            Assert.That(state.Map.HasTileAt(new TileCoordinate(1, 1)), Is.True, "precondition: green spaces activated");
            Assert.That(state.Map.HasTileAt(new TileCoordinate(2, 1)), Is.True);

            var saved = SaveCodec.Save(state);

            // No new save format: green spaces are re-derived, never persisted —
            // no tile= line records a GreenSpace coordinate or type.
            Assert.That(saved, Does.Not.Contain("GreenSpace"),
                "green spaces are not written to the save at all");
            Assert.That(saved, Does.Not.Contain("tile=1|1|"),
                "the (1,1) green space is not persisted as a tile= line");
            Assert.That(saved, Does.Not.Contain("tile=2|1|"),
                "the (2,1) green space is not persisted as a tile= line");

            // Replay: Load the road tile= lines, then re-supply the target map so
            // the activation pass re-derives the same green-space membership.
            var reloaded = SaveCodec.Load(saved);
            Assert.That(reloaded.Map.HasTileAt(new TileCoordinate(1, 1)), Is.False,
                "before the target map is supplied, only the replayed road tiles are down");
            reloaded.SetTargetMap(BuildCascadeTarget());

            Assert.That(reloaded.Map.HasTileAt(new TileCoordinate(1, 1)), Is.True,
                "green#1 re-derived by the post-load activation pass");
            Assert.That(reloaded.Map.HasTileAt(new TileCoordinate(2, 1)), Is.True,
                "green#2 re-derived by the same pass (cascade reproduced)");
            Assert.That(reloaded.Map.GetTileAt(new TileCoordinate(1, 1)), Is.EqualTo(TileType.GreenSpace));
        }

        [Test]
        public void GreenSpaceCount_DoesNotInflateTheNextUnlockCost()
        {
            var state = AfterOnboarding(BuildCascadeTarget());
            state.Wallet.Deposit(10000);
            state.TryUnlockTile(new TileCoordinate(1, 0));
            state.TryUnlockTile(new TileCoordinate(2, 0));
            state.TryUnlockTile(new TileCoordinate(0, 1)); // activates two green spaces

            Assert.That(state.Map.HasTileAt(new TileCoordinate(1, 1)), Is.True,
                "precondition: green spaces are now on the map");

            // Three player ROAD unlocks so far (origin excluded); the two free
            // green spaces also grew Map.Tiles. The next road unlock (3,0) must
            // be priced on the road tiles only — Base + 10*3 — not inflated to
            // Base + 10*5 by counting the green spaces.
            var expected = TileUnlockNumbers.BaseCost + TileUnlockNumbers.PerExistingTileStep * 3;
            var coinsBefore = state.Wallet.Coins;
            Assert.That(state.TryUnlockTile(new TileCoordinate(3, 0)), Is.True, "unlock the next road (3,0)");

            Assert.That(coinsBefore - state.Wallet.Coins, Is.EqualTo(expected),
                "green spaces must not inflate the road-unlock cost curve");
        }
    }
}
