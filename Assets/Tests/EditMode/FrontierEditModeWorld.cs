using System.Collections.Generic;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using UnityEditor;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #453 EditMode test support: builds a <see cref="GameState"/> on the
    /// player-choice frontier model (target map supplied via the real
    /// <see cref="MapDataLoader"/> Resources asset + a first tile unlocked) — the
    /// frontier replacement for the retired <c>TryUnlockNextZone</c> setup many
    /// EditMode tests used to obtain a buildable non-starter lot.
    /// </summary>
    public static class FrontierEditModeWorld
    {
        /// <summary>The staged authored-map TextAsset path (force-imported so a
        /// cold CI Library still resolves it before Resources.Load).</summary>
        public const string MapDataPath = "Assets/Resources/Data/map-data.json";

        /// <summary>The scripted first cul-de-sac north of the origin (0,1).</summary>
        public static readonly TileCoordinate FirstTile = new TileCoordinate(0, 1);

        /// <summary>The tile type at <see cref="FirstTile"/> — the north
        /// cul-de-sac.</summary>
        public const TileType FirstTileType = TileType.CulDeSacSouth;

        /// <summary>The first buildable lot id of <see cref="FirstTile"/>
        /// (its SE quadrant), via the #453 <see cref="FrontierHouseId.For"/>
        /// scheme.</summary>
        public static int FirstLotId => FrontierHouseId.For(FirstTile, Quadrant.SouthEast);

        /// <summary>The authored target map, loaded through the real Unity
        /// <see cref="MapDataLoader"/> Resources path (force-imported first).</summary>
        public static TileMap LoadTargetMap()
        {
            AssetDatabase.ImportAsset(MapDataPath, ImportAssetOptions.ForceSynchronousImport);
            return MapDataLoader.Load().Map;
        }

        /// <summary>A fresh game with the target map supplied (no tile unlocked
        /// yet). The reward chain is left fresh.</summary>
        public static GameState WithTargetMap()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(LoadTargetMap());
            return state;
        }

        /// <summary>A game with the target map supplied and <see cref="FirstTile"/>
        /// unlocked and charged — so its lots are buildable. The reward chain is
        /// left fresh (the scripted first tile unlocks during onboarding), so this
        /// mirrors the retired <c>TryUnlockNextZone</c> setup.</summary>
        public static GameState WithFirstTileUnlocked(int extraCoins = 0)
        {
            var state = WithTargetMap();
            state.Wallet.Deposit(TileUnlock.Cost(state.Map.Tiles.Count) + extraCoins);
            state.TryUnlockTile(FirstTile);
            return state;
        }

        /// <summary>The lots of <see cref="FirstTile"/> once unlocked, from live
        /// state.</summary>
        public static IReadOnlyList<HouseLot> FirstTileLots(GameState state)
        {
            return state.LotsForUnlockedTile(FirstTile);
        }

        /// <summary>The lots of <see cref="FirstTile"/> computed standalone
        /// (tile catalog + <see cref="FrontierHouseId.For"/>), for geometry tests
        /// that don't unlock the tile on a live <see cref="GameState"/>.</summary>
        public static IReadOnlyList<HouseLot> FirstTileLots()
        {
            var lots = new List<HouseLot>();
            var center = TileGeometry.CenterOf(FirstTile);
            foreach (var pair in TileLotCatalog.LotsFor(FirstTileType))
            {
                var position = new GridPoint(center.X + pair.Value.X, center.Z + pair.Value.Z);
                lots.Add(new HouseLot(FrontierHouseId.For(FirstTile, pair.Key), pair.Key, position));
            }

            return lots;
        }
    }
}
