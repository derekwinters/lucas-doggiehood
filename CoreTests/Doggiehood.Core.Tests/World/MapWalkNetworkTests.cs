using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #398: the walk network must derive from the live multi-tile
    /// <see cref="TileMap"/> (#109) rather than the hardcoded starting
    /// <see cref="NeighborhoodLayout"/> roads, so newly unlocked tiles
    /// contribute sidewalk/crosswalk nodes and edges the same way the
    /// starting intersection does. <see cref="MapWalkNetwork"/> turns a
    /// tile map's road-bearing tiles into <see cref="Road"/> segments and
    /// feeds them (plus the built houses' lots) into the existing
    /// <see cref="WalkNetwork.BuildFrom"/>.
    /// </summary>
    public class MapWalkNetworkTests
    {
        private const float StartingHalf = WorldDimensions.TileSize / 2f;

        private static TileMap StartingMapOnly()
        {
            return new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
        }

        private static TileMap StartingPlusFirstZone()
        {
            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            map.Place(FrontierTestWorld.FirstTile, FrontierTestWorld.FirstTileType);
            return map;
        }

        [Test]
        public void RoadsFrom_FourWayAlone_ReproducesTheStartingIntersectionsTwoFullRoads()
        {
            var roads = MapWalkNetwork.RoadsFrom(StartingMapOnly());

            Assert.That(roads.Count, Is.EqualTo(2));

            var ns = roads.Single(r => r.Orientation == StreetOrientation.NorthSouth);
            var ew = roads.Single(r => r.Orientation == StreetOrientation.EastWest);

            foreach (var road in new[] { ns, ew })
            {
                Assert.That(road.Center.X, Is.EqualTo(0f).Within(0.001f));
                Assert.That(road.Center.Z, Is.EqualTo(0f).Within(0.001f));
                Assert.That(road.HalfLength, Is.EqualTo(StartingHalf).Within(0.001f));
            }
        }

        [Test]
        public void BuildFrom_FourWayAlone_MatchesTheHardcodedStartingWalkNetworkNodes()
        {
            // Regression guard: the map-derived FourWay network must be the
            // same graph the hardcoded NeighborhoodLayout network already is.
            var mapNetwork = MapWalkNetwork.BuildFrom(StartingMapOnly(), NeighborhoodLayout.HouseLots);
            var expected = new HashSet<GridPoint>(NeighborhoodLayout.WalkNetwork.Nodes);
            var actual = new HashSet<GridPoint>(mapNetwork.Nodes);

            Assert.That(actual, Is.EquivalentTo(expected));
        }

        [Test]
        public void BuildFrom_MultiTile_AddsSidewalkNodesOnTheNewlyUnlockedZonesTiles()
        {
            // The starting FourWay's NS road tops out at the shared edge
            // z = TileSize/2 (30). The cul-de-sac tile directly north of it
            // must contribute sidewalk nodes beyond that edge — proving the
            // network now spans the unlocked tile, not just the start.
            var network = MapWalkNetwork.BuildFrom(StartingPlusFirstZone(), NeighborhoodLayout.HouseLots);

            Assert.That(network.Nodes.Any(n => n.Z > StartingHalf + 0.001f), Is.True,
                "no sidewalk node exists north of the starting tile's shared edge");
        }

        [Test]
        public void BuildFrom_MultiTile_IsFullyConnected_AcrossTheStartAndUnlockedTiles()
        {
            // docs/specs/world/sidewalks.md's reachability invariant must hold
            // across tiles: a dog on the starting tile can path to the
            // cul-de-sac's sidewalks.
            var network = MapWalkNetwork.BuildFrom(StartingPlusFirstZone(), NeighborhoodLayout.HouseLots);

            Assert.That(network.IsFullyConnected(), Is.True);
        }

        [Test]
        public void BuildFrom_OnlyLotsWithABuiltHouse_DoesNotThrow_WhenAnUnlockedZoneHasNoHousesYet()
        {
            // #398 crash guard: a freshly unlocked zone's lots have no House
            // (and thus no assigned art variant) until one is built, so
            // WalkNetwork.BuildFrom's front-walkway attach would throw if
            // passed them. The builder must only receive lots for houses
            // that already exist — here, just the four starting lots.
            var map = StartingPlusFirstZone();

            Assert.That(() => MapWalkNetwork.BuildFrom(map, NeighborhoodLayout.HouseLots),
                Throws.Nothing);
        }
    }
}
