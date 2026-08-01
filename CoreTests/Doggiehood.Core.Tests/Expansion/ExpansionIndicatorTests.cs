using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.Onboarding;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Expansion
{
    /// <summary>
    /// #453: the multi-lock indicator resolver — one indicator state per
    /// coordinate the player may currently unlock
    /// (<see cref="GameState.UnlockableFrontier"/>), priced/tinted via the #295
    /// <see cref="TileUnlock.Cost"/> pricing path against the live wallet (not
    /// the retired <c>ZoneUnlock</c>). Two-plus simultaneous frontier tiles →
    /// two-plus states, so the Unity layer can render one lock per open
    /// connection point.
    /// </summary>
    public class ExpansionIndicatorTests
    {
        [Test]
        public void ResolveAll_OnAFreshGameStillOnboarding_ReturnsOnlyTheScriptedTile_Unaffordable()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(LoadAuthoredTargetMap());

            var indicators = ExpansionIndicator.ResolveAll(state);

            Assert.That(indicators.Select(i => i.Coordinate),
                Is.EquivalentTo(new[] { new TileCoordinate(0, 1) }),
                "onboarding gate: only the scripted tile is offered");
            Assert.That(indicators.Single().State.IsAffordable, Is.False,
                "a fresh wallet cannot afford the flat tile cost");
        }

        [Test]
        public void ResolveAll_AfterOnboarding_ReturnsOneStatePerFrontierCoordinate()
        {
            var state = AfterOnboarding();

            var indicators = ExpansionIndicator.ResolveAll(state);

            Assert.That(indicators.Select(i => i.Coordinate),
                Is.EquivalentTo(state.UnlockableFrontier()),
                "one indicator per currently-unlockable frontier coordinate");
            Assert.That(indicators.Count, Is.GreaterThanOrEqualTo(2),
                "post-onboarding the origin borders multiple open frontier tiles at once");
        }

        [Test]
        public void ResolveAll_PricesAffordability_FromTileUnlockCostAgainstTheLiveWallet()
        {
            var state = AfterOnboarding();
            Assert.That(ExpansionIndicator.ResolveAll(state).All(i => !i.State.IsAffordable), Is.True,
                "precondition: nothing affordable with an empty wallet");

            state.Wallet.Deposit(TileUnlock.Cost(state.Map.Tiles.Count));

            Assert.That(ExpansionIndicator.ResolveAll(state).All(i => i.State.IsAffordable), Is.True,
                "every lock turns affordable the moment the wallet covers the flat tile cost");
        }

        [Test]
        public void ResolveAll_DropsACoordinate_OnceItIsUnlocked()
        {
            var state = AfterOnboarding();
            state.Wallet.Deposit(TileUnlock.Cost(state.Map.Tiles.Count));
            var target = new TileCoordinate(0, 1);
            Assert.That(ExpansionIndicator.ResolveAll(state).Select(i => i.Coordinate),
                Does.Contain(target), "precondition");

            Assert.That(state.TryUnlockTile(target), Is.True);

            Assert.That(ExpansionIndicator.ResolveAll(state).Select(i => i.Coordinate),
                Does.Not.Contain(target), "a placed coordinate leaves the indicator set");
        }

        [Test]
        public void ResolveAll_PositionsEachIndicator_PastItsOwnFrontierEdge()
        {
            var state = AfterOnboarding();

            foreach (var indicator in ExpansionIndicator.ResolveAll(state))
            {
                var expected = ExpansionIndicatorPlacement.Resolve(state.Map, indicator.Coordinate);
                Assert.That(indicator.State.Position.X, Is.EqualTo(expected.X));
                Assert.That(indicator.State.Position.Z, Is.EqualTo(expected.Z));
            }
        }

        private static GameState AfterOnboarding()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(LoadAuthoredTargetMap());
            state.RestoreRewardChainStep(OnboardingRewardStep.Done);
            return state;
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
