using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #539: the <see cref="TileType.GreenSpace"/> tile — a full grid tile
    /// authored like the other 15 but carrying NO road and NO buildable lots.
    /// It auto-activates (free, no lock icon) once 2+ of its 4 edges border an
    /// already-activated tile; those higher-level behaviors live in
    /// <see cref="Doggiehood.Core.Expansion.GreenSpaceActivation"/> and
    /// <see cref="GameState"/>. These tests pin the catalog data the rest
    /// builds on: empty road edges and zero lots.
    /// </summary>
    public class GreenSpaceTileTests
    {
        [Test]
        public void Catalog_GreenSpace_HasNoRoadEdges()
        {
            var definition = TileCatalog.Get(TileType.GreenSpace);

            Assert.That(definition.RoadEdges, Is.Empty,
                "a green-space tile carries no road on any edge");
        }

        [Test]
        public void LotCatalog_GreenSpace_HasZeroLots()
        {
            var lots = TileLotCatalog.LotsFor(TileType.GreenSpace);

            Assert.That(lots, Is.Empty,
                "a green-space tile never holds a house — no lot slots");
        }

        [Test]
        public void LotCatalog_GreenSpace_HasNoTreeQuadrants()
        {
            var trees = TileLotCatalog.TreeQuadrantsFor(TileType.GreenSpace);

            Assert.That(trees, Is.Empty,
                "a green-space tile has no cul-de-sac bulb, so no open-space trees");
        }

        [Test]
        public void MapDataAuthoring_AcceptsAGreenSpaceEntry_LikeAnyOtherTileType()
        {
            // #539: a green space is authored in docs/tools/map-data.json exactly
            // like the other 17 types ({x, y, type: "GreenSpace"}). The name-keyed
            // MapDefinition.Parse + MapLoader.Load path needs no change to accept
            // it — its empty road edges satisfy CanPlace against no-road neighbors.
            const string json = "{\"name\":\"green fixture\",\"tiles\":["
                + "{\"x\":0,\"y\":0,\"type\":\"FourWay\"},"
                + "{\"x\":0,\"y\":1,\"type\":\"StraightNS\"},"
                + "{\"x\":1,\"y\":0,\"type\":\"StraightEW\"},"
                + "{\"x\":1,\"y\":1,\"type\":\"GreenSpace\"}]}";

            var definition = MapDefinition.Parse(json);
            var result = MapLoader.Load(definition);

            Assert.That(result.RejectedCoordinates, Is.Empty,
                "the green space places validly against its no-road neighbors");
            Assert.That(result.Map.HasTileAt(new TileCoordinate(1, 1)), Is.True);
            Assert.That(result.Map.GetTileAt(new TileCoordinate(1, 1)), Is.EqualTo(TileType.GreenSpace));
        }
    }
}
