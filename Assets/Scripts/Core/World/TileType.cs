namespace Doggiehood.Core.World
{
    /// <summary>
    /// The 17 tile types of the design catalog
    /// (docs/specs/world/tile-catalog.md, #105/#109): a 60m x 60m tile with
    /// roads entering/exiting along some subset of its N/S/E/W edges.
    /// <see cref="FourWay"/> is the existing starting tile; the other 16
    /// are for the multi-tile grid built by #109.
    /// </summary>
    public enum TileType
    {
        FourWay,
        StraightNS,
        StraightEW,
        TurnNE,
        TurnNW,
        TurnSE,
        TurnSW,
        TeeNorth,
        TeeSouth,
        TeeEast,
        TeeWest,
        CulDeSacNorth,
        CulDeSacSouth,
        CulDeSacEast,
        CulDeSacWest,
        OpposingTurnsNS,
        OpposingTurnsEW,
    }
}
