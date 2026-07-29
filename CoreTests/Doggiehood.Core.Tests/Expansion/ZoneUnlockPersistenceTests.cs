using System.Linq;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Expansion
{
    /// <summary>
    /// #343: unlocked zones (and therefore the placed <see cref="TileMap"/>)
    /// survive an app relaunch. Zones are authored and deterministic
    /// (<see cref="ZoneCatalog"/>), so persisting the unlocked COUNT is
    /// enough to rebuild both <see cref="GameState.Map"/> and
    /// <see cref="GameState.UnlockedZones"/> on load — replaying each
    /// authored zone's placement without re-charging the wallet. Before
    /// this, Map/UnlockedZones reset every session
    /// (docs/specs/expansion.md #56 implementation note).
    /// </summary>
    public class ZoneUnlockPersistenceTests
    {
        [Test]
        public void UnlockedZone_RoundTripsThroughSaveCodec_RebuildingMapAndLots()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(ZoneUnlockNumbers.BaseCost);
            Assert.That(state.TryUnlockNextZone(), Is.True, "precondition: the zone unlocks");

            var zoneLotId = ZoneCatalog.FirstZone.Lots.First().HouseId;
            var mapTileCountBefore = state.Map.Tiles.Count;

            var reloaded = SaveCodec.Load(SaveCodec.Save(state));

            Assert.That(reloaded.UnlockedZones.Count, Is.EqualTo(1),
                "the unlocked zone survives the save round-trip");
            Assert.That(reloaded.Map.Tiles.Count, Is.EqualTo(mapTileCountBefore),
                "the zone's tiles are replaced onto the map on load");
            foreach (var placement in ZoneCatalog.FirstZone.TilePlacements)
            {
                Assert.That(reloaded.Map.HasTileAt(placement.Coordinate), Is.True,
                    $"tile at {placement.Coordinate} should be restored");
            }

            Assert.That(reloaded.IsLotBuildable(zoneLotId), Is.True,
                "the restored zone's empty lots are still buildable after reload");
        }

        [Test]
        public void RestoringZones_DoesNotChargeTheWallet_NorReUnlockOnReload()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(ZoneUnlockNumbers.BaseCost);
            state.TryUnlockNextZone();
            var coinsAfterUnlock = state.Wallet.Coins; // 0 — spent on the unlock

            var reloaded = SaveCodec.Load(SaveCodec.Save(state));

            Assert.That(reloaded.Wallet.Coins, Is.EqualTo(coinsAfterUnlock),
                "restoring a persisted zone must not re-charge the wallet");
        }

        [Test]
        public void FreshGame_RoundTrips_WithNoUnlockedZones_AndOnlyTheStartingTile()
        {
            var reloaded = SaveCodec.Load(SaveCodec.Save(GameState.CreateNew()));

            Assert.That(reloaded.UnlockedZones.Count, Is.EqualTo(0));
            Assert.That(reloaded.Map.Tiles.Count, Is.EqualTo(1),
                "a fresh game's map has only the starting intersection tile");
        }

        [Test]
        public void LegacySave_WithNoZonesLine_LoadsWithNoUnlockedZones()
        {
            // A pre-#343 save carries no zones field — it must load as a game
            // with nothing unlocked yet, never throw.
            var reloaded = SaveCodec.Load("version=1\ncoins=250\nonboarded=1\n");

            Assert.That(reloaded.UnlockedZones.Count, Is.EqualTo(0));
        }
    }
}
