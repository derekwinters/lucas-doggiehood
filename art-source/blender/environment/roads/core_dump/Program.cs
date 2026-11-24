using System; using System.Linq; using System.Text; using System.Collections.Generic; using System.Globalization;
using Doggiehood.Core.World;
class P {
  static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
  static void Main() {
    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    var sb = new StringBuilder(); sb.Append("{\"tiles\":{");
    bool first = true;
    foreach (TileType t in Enum.GetValues(typeof(TileType))) {
      if (!first) sb.Append(","); first = false;
      var map = new TileMap(new TileCoordinate(0,0), t);
      var net = MapWalkNetwork.BuildFrom(map, new List<HouseLot>());
      sb.Append("\"" + t + "\":{\"edges\":[");
      sb.Append(string.Join(",", net.Edges.Select(e => $"[\"{e.Kind}\",{F(e.A.X)},{F(e.A.Z)},{F(e.B.X)},{F(e.B.Z)},{F(e.Width)}]")));
      sb.Append("],\"crosswalks\":[");
      sb.Append(string.Join(",", TileCrosswalkGeometry.RectanglesFor(new TileCoordinate(0,0), t).Select(r => $"[{F(r.Center.X)},{F(r.Center.Z)},{F(r.SpanX)},{F(r.SpanZ)}]")));
      sb.Append("],\"segments\":[");
      sb.Append(string.Join(",", TileRoadGeometry.SegmentsFor(new TileCoordinate(0,0), t).Select(s => $"[{F(s.Center.X)},{F(s.Center.Z)},\"{s.Orientation}\",{F(s.Length)},{F(s.Width)}]")));
      sb.Append("],\"trees\":[");
      var trees = new List<string>();
      for (int c = -3; c <= 3; c++) for (int r = -3; r <= 3; r++)
        foreach (var tp in TileGeometry.TreeWorldPositionsFor(t, new TileCoordinate(c, r)))
          trees.Add($"[{c},{r},{F(tp.Position.X - c * WorldDimensions.TileSize)},{F(tp.Position.Z - r * WorldDimensions.TileSize)}]");
      sb.Append(string.Join(",", trees));
      sb.Append("]}");
    }
    sb.Append("},\"block\":{");
    // validate the 3x3 block via TileMap.CanPlace, placing outward from the origin
    var block = new (int c,int r,TileType t)[]{ (0,1,TileType.TeeSouth),(-1,0,TileType.TeeEast),(1,0,TileType.TeeWest),(0,-1,TileType.StraightNS),
      (-1,1,TileType.TurnSE),(1,1,TileType.TurnSW),(-1,-1,TileType.CulDeSacNorth),(1,-1,TileType.TurnNE)};
    var m = new TileMap(new TileCoordinate(0,0), TileType.FourWay);
    sb.Append(string.Join(",", block.Select(b => { var ok = m.CanPlace(new TileCoordinate(b.c,b.r), b.t); if (ok) m.Place(new TileCoordinate(b.c,b.r), b.t); return $"\"{b.c},{b.r},{b.t}\":{(ok?"true":"false")}"; })));
    sb.Append("},\"StreetCorridorInset\":" + F(LotBounds.StreetCorridorInset) + "}");
    Console.WriteLine(sb.ToString());
  }
}
