using System.IO;
using System.Runtime.CompilerServices;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.Onboarding;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #453 test support: builds a <see cref="GameState"/> on the player-choice
    /// frontier model (target map supplied + a first tile unlocked), the
    /// frontier replacement for the retired <c>TryUnlockNextZone</c> setup many
    /// tests used to obtain a buildable non-starter lot.
    /// </summary>
    public static class FrontierTestWorld
    {
        /// <summary>The scripted first cul-de-sac directly north of the starting
        /// intersection (docs/specs/expansion.md "Map shape") — the tile
        /// onboarding gates to and the one these helpers unlock.</summary>
        public static readonly TileCoordinate FirstTile = new TileCoordinate(0, 1);

        /// <summary>The two buildable lot ids of <see cref="FirstTile"/>
        /// (a <c>CulDeSacSouth</c> keeps its SE + SW quadrants), keyed via the
        /// #453 <see cref="FrontierHouseId.For"/> scheme.</summary>
        public static int FirstLotId => FrontierHouseId.For(FirstTile, Quadrant.SouthEast);

        public static int SecondLotId => FrontierHouseId.For(FirstTile, Quadrant.SouthWest);

        /// <summary>The tile type at <see cref="FirstTile"/> in the authored
        /// map — the north cul-de-sac (#360).</summary>
        public const TileType FirstTileType = TileType.CulDeSacSouth;

        /// <summary>The <see cref="HouseLot"/>s <see cref="FirstTile"/> carries,
        /// computed the same way <see cref="GameState.LotsForUnlockedTile"/> does
        /// (tile catalog + <see cref="FrontierHouseId.For"/>) but standalone, for
        /// geometry tests that operate on a bare map/lot rather than a
        /// <see cref="GameState"/>.</summary>
        public static System.Collections.Generic.IReadOnlyList<HouseLot> FirstTileLots()
        {
            var lots = new System.Collections.Generic.List<HouseLot>();
            var center = TileGeometry.CenterOf(FirstTile);
            foreach (var pair in TileLotCatalog.LotsFor(FirstTileType))
            {
                var position = new GridPoint(center.X + pair.Value.X, center.Z + pair.Value.Z);
                lots.Add(new HouseLot(FrontierHouseId.For(FirstTile, pair.Key), pair.Key, position));
            }

            return lots;
        }

        /// <summary>The full authored target neighborhood
        /// (<c>docs/tools/map-data.json</c>) as a validated Core map.</summary>
        public static TileMap LoadAuthoredTargetMap()
        {
            var definition = MapDefinition.Parse(File.ReadAllText(AuthoredMapPath()));
            return MapLoader.Load(definition).Map;
        }

        /// <summary>A fresh game with the target map supplied and onboarding
        /// complete, so the whole frontier is open (no tile unlocked yet).</summary>
        public static GameState AfterOnboarding()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(LoadAuthoredTargetMap());
            state.RestoreRewardChainStep(OnboardingRewardStep.Done);
            return state;
        }

        /// <summary>A game with the target map supplied and <see cref="FirstTile"/>
        /// unlocked and charged — so its lots are buildable. The reward chain is
        /// left fresh (untouched): the scripted first tile is unlockable during
        /// onboarding, so this mirrors the retired <c>TryUnlockNextZone</c> setup
        /// (which also left the chain where it was). <paramref name="extraCoins"/>
        /// tops up the wallet for a follow-on build/upgrade.</summary>
        public static GameState WithFirstTileUnlocked(int extraCoins = 0)
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(LoadAuthoredTargetMap());
            state.Wallet.Deposit(TileUnlock.Cost(state.Map.Tiles.Count) + extraCoins);
            if (!state.TryUnlockTile(FirstTile))
            {
                throw new System.InvalidOperationException("precondition: the first frontier tile must unlock");
            }

            return state;
        }

        private static string AuthoredMapPath([CallerFilePath] string thisFilePath = null)
        {
            var testFileDirectory = Path.GetDirectoryName(thisFilePath);
            var repoRoot = Path.GetFullPath(Path.Combine(testFileDirectory, "..", "..", ".."));
            return Path.Combine(repoRoot, "docs", "tools", "map-data.json");
        }
    }
}
