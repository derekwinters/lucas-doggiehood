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
    /// #295: unlocked tiles round-trip through <see cref="SaveCodec"/> as the
    /// actual set of unlocked coordinates (superseding the sequential
    /// <c>zones=N</c> count) — unlock order is player-chosen, so the persisted
    /// form is a set, not a count.
    /// </summary>
    public class TileUnlockPersistenceTests
    {
        [Test]
        public void UnlockedTiles_RoundTripAsASetOfCoordinates_RebuildingTheMap()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(LoadAuthoredTargetMap());
            state.RestoreRewardChainStep(OnboardingRewardStep.Done);
            // Two unlocks: the first at the base, the second scaled by +10 (#540).
            state.Wallet.Deposit(TileUnlock.Cost(1) + TileUnlock.Cost(2));
            Assert.That(state.TryUnlockTile(new TileCoordinate(0, 1)), Is.True);
            Assert.That(state.TryUnlockTile(new TileCoordinate(1, 0)), Is.True);
            var mapTileCountBefore = state.Map.Tiles.Count;

            var reloaded = SaveCodec.Load(SaveCodec.Save(state));

            Assert.That(reloaded.UnlockedTiles, Is.EquivalentTo(new[]
            {
                new TileCoordinate(0, 1),
                new TileCoordinate(1, 0),
            }), "the set of unlocked coordinates round-trips");
            Assert.That(reloaded.Map.Tiles.Count, Is.EqualTo(mapTileCountBefore),
                "the unlocked tiles are replaced onto the map on load");
            Assert.That(reloaded.Map.HasTileAt(new TileCoordinate(0, 1)), Is.True);
            Assert.That(reloaded.Map.HasTileAt(new TileCoordinate(1, 0)), Is.True);
        }

        [Test]
        public void RestoringUnlockedTiles_DoesNotRechargeTheWallet()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(LoadAuthoredTargetMap());
            state.RestoreRewardChainStep(OnboardingRewardStep.Done);
            state.Wallet.Deposit(TileUnlock.Cost(1));
            state.TryUnlockTile(new TileCoordinate(0, 1));
            var coinsAfterUnlock = state.Wallet.Coins; // 0

            var reloaded = SaveCodec.Load(SaveCodec.Save(state));

            Assert.That(reloaded.Wallet.Coins, Is.EqualTo(coinsAfterUnlock),
                "restoring persisted tiles must not re-charge the wallet");
        }

        [Test]
        public void FreshGame_RoundTrips_WithNoUnlockedTiles()
        {
            var reloaded = SaveCodec.Load(SaveCodec.Save(GameState.CreateNew()));

            Assert.That(reloaded.UnlockedTiles, Is.Empty);
            Assert.That(reloaded.Map.Tiles.Count, Is.EqualTo(1),
                "a fresh game's map has only the starting intersection tile");
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
