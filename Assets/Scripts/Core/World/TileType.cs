namespace Doggiehood.Core.World
{
    /// <summary>
    /// The 16 tile types of the design catalog
    /// (docs/specs/world/tile-catalog.md, #105/#109): a 60m x 60m tile with
    /// roads entering/exiting along some subset of its N/S/E/W edges.
    /// <see cref="FourWay"/> is the existing starting tile; the next 14
    /// are the multi-tile road grid built by #109. <see cref="GreenSpace"/>
    /// (#539) is the odd one out: a full grid tile that carries NO road and NO
    /// buildable lot, added last so its enum value doesn't shift the others'
    /// (the save format and map-data.json both key on the name, but keeping
    /// values stable avoids any future by-index serialization surprise). It
    /// auto-activates for free when 2+ edges border an already-activated tile
    /// (<see cref="Doggiehood.Core.Expansion.GreenSpaceActivation"/>).
    ///
    /// #583 removed the two <c>OpposingTurns</c> "twin bend" types outright
    /// (Derek: "remove tile completely") — they had no City Kit render path,
    /// the Map Builder blocked placing them, and the live map carried none.
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
        GreenSpace,
    }
}
