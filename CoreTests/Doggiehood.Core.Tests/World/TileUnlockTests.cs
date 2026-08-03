using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.Onboarding;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #295: <see cref="GameState.TryUnlockTile"/> — the single entry point for
    /// the player-choice frontier unlock. Charges the flat per-tile cost and
    /// places the chosen frontier tile; rejects (no state change) an
    /// unaffordable or non-frontier coordinate. Frontier order is the player's
    /// choice, gated behind the onboarding "expand the map" step.
    /// </summary>
    public class TileUnlockTests
    {
        private static GameState AfterOnboarding()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(LoadAuthoredTargetMap());
            // Past the onboarding "expand the map" step: the full frontier opens.
            state.RestoreRewardChainStep(OnboardingRewardStep.Done);
            return state;
        }

        [Test]
        public void TryUnlockTile_PlacesTheChosenFrontierTile_AndCharges()
        {
            var state = AfterOnboarding();
            state.Wallet.Deposit(TileUnlock.Cost(state.Map.Tiles.Count));
            var target = new TileCoordinate(1, 0);
            Assert.That(state.UnlockableFrontier(), Does.Contain(target), "precondition");

            var ok = state.TryUnlockTile(target);

            Assert.That(ok, Is.True);
            Assert.That(state.Map.HasTileAt(target), Is.True, "the tile is placed");
            Assert.That(state.Wallet.Coins, Is.EqualTo(0), "the flat cost was charged");
            Assert.That(state.UnlockedTiles, Does.Contain(target));
        }

        [Test]
        public void TryUnlockTile_RejectsNonFrontierCoordinate_NoStateChange()
        {
            var state = AfterOnboarding();
            state.Wallet.Deposit(TileUnlock.Cost(state.Map.Tiles.Count));
            // (1,1) exists in the target but does not yet border a placed tile.
            var nonFrontier = new TileCoordinate(1, 1);
            Assert.That(state.UnlockableFrontier(), Does.Not.Contain(nonFrontier), "precondition");

            var ok = state.TryUnlockTile(nonFrontier);

            Assert.That(ok, Is.False);
            Assert.That(state.Map.HasTileAt(nonFrontier), Is.False, "nothing placed");
            Assert.That(state.Wallet.Coins, Is.EqualTo(TileUnlock.Cost(1)), "no charge on rejection");
        }

        [Test]
        public void TryUnlockTile_RejectsAlreadyPlacedCoordinate_NoStateChange()
        {
            var state = AfterOnboarding();
            state.Wallet.Deposit(TileUnlock.Cost(1) * 2);
            Assert.That(state.TryUnlockTile(new TileCoordinate(0, 1)), Is.True, "precondition");
            var coinsAfterFirst = state.Wallet.Coins;

            var ok = state.TryUnlockTile(new TileCoordinate(0, 1));

            Assert.That(ok, Is.False, "a placed coordinate is no longer on the frontier");
            Assert.That(state.Wallet.Coins, Is.EqualTo(coinsAfterFirst), "no second charge");
        }

        [Test]
        public void TryUnlockTile_WhenUnaffordable_RejectsWithNoStateChange()
        {
            var state = AfterOnboarding();
            // No coins deposited.
            var target = new TileCoordinate(0, 1);
            Assert.That(state.UnlockableFrontier(), Does.Contain(target), "precondition");

            var ok = state.TryUnlockTile(target);

            Assert.That(ok, Is.False);
            Assert.That(state.Map.HasTileAt(target), Is.False, "nothing placed when unaffordable");
            Assert.That(state.Wallet.Coins, Is.EqualTo(0));
        }

        [Test]
        public void BeforeOnboardingExpandStep_OnlyTheScriptedTileIsOffered()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(LoadAuthoredTargetMap());
            // Fresh chain: the "expand the map" step has not completed yet.
            Assert.That(state.RewardChain.CurrentStep,
                Is.Not.EqualTo(OnboardingRewardStep.Done), "precondition: still onboarding");

            var frontier = state.UnlockableFrontier();

            Assert.That(frontier, Is.EquivalentTo(new[] { new TileCoordinate(0, 1) }),
                "during onboarding only the single scripted expand tile is unlockable");
        }

        [Test]
        public void BeforeOnboardingExpandStep_UnlockingANonScriptedFrontierTileIsRejected()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(LoadAuthoredTargetMap());
            state.Wallet.Deposit(TileUnlock.Cost(1));

            var ok = state.TryUnlockTile(new TileCoordinate(1, 0));

            Assert.That(ok, Is.False, "non-scripted tiles are locked until onboarding's expand step");
            Assert.That(state.Map.HasTileAt(new TileCoordinate(1, 0)), Is.False);
            Assert.That(state.Wallet.Coins, Is.EqualTo(TileUnlock.Cost(1)), "no charge");
        }

        [Test]
        public void UnlockingTheScriptedTile_AdvancesTheOnboardingExpandStep()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(LoadAuthoredTargetMap());
            // Onboarding reaches the "expand the map" step.
            state.RestoreRewardChainStep(OnboardingRewardStep.ExpandMap);
            state.Wallet.Deposit(TileUnlock.Cost(1));

            Assert.That(state.TryUnlockTile(new TileCoordinate(0, 1)), Is.True);

            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.BuildHouse),
                "unlocking the scripted tile completes the expand step");
        }

        [Test]
        public void AfterOnboarding_EveryFrontierTileIsSimultaneouslyUnlockable_InAnyOrder()
        {
            var state = AfterOnboarding();

            var frontier = state.UnlockableFrontier();
            Assert.That(frontier, Is.EquivalentTo(new[]
            {
                new TileCoordinate(1, 0),
                new TileCoordinate(-1, 0),
                new TileCoordinate(0, 1),
                new TileCoordinate(0, -1),
            }), "after onboarding the whole geometric frontier is open at once");

            // Player picks in an arbitrary order — not a fixed authored sequence.
            // Two unlocks: the first at the base, the second scaled by +10 (#540).
            state.Wallet.Deposit(TileUnlock.Cost(1) + TileUnlock.Cost(2));
            Assert.That(state.TryUnlockTile(new TileCoordinate(-1, 0)), Is.True);
            Assert.That(state.TryUnlockTile(new TileCoordinate(0, -1)), Is.True);
        }

        private static TileMap LoadAuthoredTargetMap()
        {
            var definition = MapDefinition.Parse(File.ReadAllText(AuthoredMapPath()));
            return MapLoader.Load(definition).Map;
        }

        private static string AuthoredMapPath([CallerFilePath] string thisFilePath = null)
        {
            var testFileDirectory = Path.GetDirectoryName(thisFilePath);
            var repoRoot = Path.GetFullPath(Path.Combine(testFileDirectory, "..", "..", ".."));
            return Path.Combine(repoRoot, "docs", "tools", "map-data.json");
        }
    }
}
