using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// A hand-authored group of tiles unlocked as one unit (#56,
    /// docs/specs/expansion.md "Map shape") — the player only unlocks
    /// zones in sequence, never places tiles individually. Each tile
    /// contributes the 4 quadrant lots <see cref="TileLotCatalog"/> defines
    /// for its type, converted to world-space positions via
    /// <see cref="TileGeometry"/> and given sequential house ids starting
    /// at the authored <c>firstHouseId</c>. A freshly unlocked zone's lots
    /// have no <see cref="House"/> yet — see GameState.IsLotBuildable.
    /// </summary>
    public sealed class Zone
    {
        /// <summary>Fixed order lots are enumerated in within a tile, so
        /// authored house ids are deterministic regardless of dictionary
        /// iteration order.</summary>
        private static readonly Quadrant[] QuadrantOrder =
        {
            Quadrant.NorthEast, Quadrant.NorthWest, Quadrant.SouthEast, Quadrant.SouthWest,
        };

        public IReadOnlyList<ZoneTilePlacement> TilePlacements { get; }
        public IReadOnlyList<HouseLot> Lots { get; }

        public Zone(IReadOnlyList<ZoneTilePlacement> tilePlacements, int firstHouseId)
        {
            TilePlacements = tilePlacements;
            Lots = BuildLots(tilePlacements, firstHouseId);
        }

        /// <summary>Adds every tile in <see cref="TilePlacements"/> to
        /// <paramref name="map"/>, in authored order — throws (via
        /// <see cref="TileMap.Place"/>) if any placement fails #109's
        /// adjacency validation.</summary>
        public void PlaceOnto(TileMap map)
        {
            foreach (var placement in TilePlacements)
            {
                map.Place(placement.Coordinate, placement.Type);
            }
        }

        private static IReadOnlyList<HouseLot> BuildLots(IReadOnlyList<ZoneTilePlacement> tilePlacements, int firstHouseId)
        {
            var lots = new List<HouseLot>();
            var nextHouseId = firstHouseId;

            foreach (var placement in tilePlacements)
            {
                var tileCenter = TileGeometry.CenterOf(placement.Coordinate);
                var offsetsByQuadrant = TileLotCatalog.LotsFor(placement.Type);

                foreach (var quadrant in QuadrantOrder)
                {
                    var offset = offsetsByQuadrant[quadrant];
                    var position = new GridPoint(tileCenter.X + offset.X, tileCenter.Z + offset.Z);
                    lots.Add(new HouseLot(nextHouseId, quadrant, position));
                    nextHouseId++;
                }
            }

            return lots;
        }
    }
}
