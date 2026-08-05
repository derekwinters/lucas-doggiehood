using System;
using System.Collections.Generic;
using System.Linq;

namespace Doggiehood.Core.World
{
    /// <summary>What kind of walkable connection a <see cref="WalkEdge"/> is
    /// (#106; FrontWalkway replaced DrivewayStub in #128 — Derek's decision:
    /// the neighborhood has no driveways).</summary>
    public enum WalkEdgeKind
    {
        Sidewalk,
        Crosswalk,
        FrontWalkway,
    }

    /// <summary>
    /// One walkable connection in the <see cref="WalkNetwork"/> graph
    /// (#106): a straight hop between two nodes, tagged with what kind of
    /// surface it represents and how wide that surface is.
    /// </summary>
    public readonly struct WalkEdge
    {
        public GridPoint A { get; }
        public GridPoint B { get; }
        public WalkEdgeKind Kind { get; }

        /// <summary>Surface width: SidewalkWidth for Sidewalk/FrontWalkway
        /// edges, CrosswalkWidth for Crosswalk edges (#105).</summary>
        public float Width { get; }

        /// <summary>Straight-line distance between A and B; also this
        /// edge's pathfinding weight.</summary>
        public float Length
        {
            get
            {
                var dx = A.X - B.X;
                var dz = A.Z - B.Z;
                return (float)Math.Sqrt(dx * dx + dz * dz);
            }
        }

        public WalkEdge(GridPoint a, GridPoint b, WalkEdgeKind kind, float width)
        {
            A = a;
            B = b;
            Kind = kind;
            Width = width;
        }

        /// <summary>The other endpoint, given one of them.</summary>
        public GridPoint Other(GridPoint node)
        {
            return node.Equals(A) ? B : A;
        }
    }

    /// <summary>
    /// A cul-de-sac's bulb-side turnaround to curve (#581): the dead-end
    /// <see cref="Stub"/> road and which of its ends carries the bulb. The
    /// walk network joins the stub's two sidewalk arms around the bulb with a
    /// curved turnaround of radius <see cref="WorldDimensions.CulDeSacBulbRadius"/>
    /// instead of leaving them as two dead-ends. Supplied by
    /// <see cref="MapWalkNetwork"/>, which knows a tile is a cul-de-sac — a
    /// dead-end bulb can't be told from a road running to the map frontier by
    /// road geometry alone.
    /// </summary>
    public readonly struct CulDeSacTurnaround
    {
        public Road Stub { get; }

        /// <summary>True when the bulb (dead-end) is at the stub's positive-along
        /// end (<see cref="Road.HalfLength"/>), false when it's at the negative
        /// end — the connecting road edge is the opposite end.</summary>
        public bool BulbAtPositiveEnd { get; }

        public CulDeSacTurnaround(Road stub, bool bulbAtPositiveEnd)
        {
            Stub = stub;
            BulbAtPositiveEnd = bulbAtPositiveEnd;
        }
    }

    /// <summary>
    /// The walkable graph of the neighborhood (#106): sidewalks on both
    /// sides of every road, crosswalks connecting those sidewalks wherever
    /// a road needs to be crossed, and front walkways (#128) connecting
    /// each house's FRONT DOOR to the nearest point on its street-facing
    /// sidewalk. Generic and data-driven — built from whatever
    /// <see cref="Road"/>s and <see cref="HouseLot"/>s are passed in, not
    /// hardcoded to today's single intersection. Supports real
    /// shortest-path queries (Dijkstra; the graph is tiny, so a priority
    /// queue would be overkill).
    /// </summary>
    public sealed class WalkNetwork
    {
        private const float Epsilon = 0.001f;

        private readonly List<WalkEdge> edges;
        private readonly List<GridPoint> nodeOrder;
        private readonly Dictionary<GridPoint, List<WalkEdge>> adjacency;
        private readonly Dictionary<int, WalkEdge> frontWalkways;

        public IReadOnlyList<WalkEdge> Edges
        {
            get { return edges; }
        }

        public IReadOnlyList<GridPoint> Nodes
        {
            get { return nodeOrder; }
        }

        private WalkNetwork(List<WalkEdge> edges, Dictionary<int, WalkEdge> frontWalkways)
        {
            this.edges = edges;
            this.frontWalkways = frontWalkways;
            nodeOrder = new List<GridPoint>();
            adjacency = new Dictionary<GridPoint, List<WalkEdge>>();

            foreach (var edge in edges)
            {
                AddAdjacency(edge.A, edge);
                AddAdjacency(edge.B, edge);
            }
        }

        private void AddAdjacency(GridPoint node, WalkEdge edge)
        {
            if (!adjacency.TryGetValue(node, out var list))
            {
                list = new List<WalkEdge>();
                adjacency[node] = list;
                nodeOrder.Add(node);
            }

            list.Add(edge);
        }

        /// <summary>Edges touching <paramref name="node"/> (both directions).</summary>
        public IReadOnlyList<WalkEdge> EdgesFrom(GridPoint node)
        {
            return adjacency.TryGetValue(node, out var list) ? list : Array.Empty<WalkEdge>();
        }

        /// <summary>
        /// The ground surface height (world Y) a dog's feet should rest at
        /// while moving directly between two adjacent network nodes
        /// (#151): <see cref="WorldDimensions.SidewalkSurfaceHeight"/> for
        /// a Sidewalk or FrontWalkway edge, since both are the Kenney kit's
        /// raised paved band; <see cref="WorldDimensions.RoadSurfaceHeight"/>
        /// for a Crosswalk edge, since crosswalks are painted flat onto the
        /// road itself. Resolved from the specific edge connecting the two
        /// points — not just whichever edges touch the destination node —
        /// because a box-corner node (where a crosswalk meets its
        /// sidewalks) carries edges of both kinds, so only the edge
        /// actually being crossed disambiguates road from sidewalk there.
        /// Falls back to <see cref="WorldDimensions.RoadSurfaceHeight"/> if
        /// the two points aren't directly joined by a real edge (defensive;
        /// every legitimate hop over this network is a real edge).
        /// </summary>
        public float GroundHeight(GridPoint from, GridPoint to)
        {
            foreach (var edge in EdgesFrom(from))
            {
                if (edge.Other(from).Equals(to))
                {
                    return edge.Kind == WalkEdgeKind.Crosswalk
                        ? WorldDimensions.RoadSurfaceHeight
                        : WorldDimensions.SidewalkSurfaceHeight;
                }
            }

            return WorldDimensions.RoadSurfaceHeight;
        }

        /// <summary>
        /// The ground surface height (world Y) directly beneath an arbitrary
        /// POINT (#580) — the single-position companion to
        /// <see cref="GroundHeight(GridPoint, GridPoint)"/>, which only answers
        /// for a hop between two adjacent nodes. Used to sit the lost-item
        /// finder ring on the surface the hidden item actually rests on, the
        /// same tile-aware treatment dogs already get, instead of a flat
        /// ground-plane assumption that buried the ring under the raised road
        /// asset. Returns <see cref="WorldDimensions.SidewalkSurfaceHeight"/>
        /// when the point lies on the raised paved band of a Sidewalk or
        /// FrontWalkway edge (the Kenney kit's curb+sidewalk band, which reads
        /// in-game as "the road" since the kit paves it with no grass verge),
        /// and <see cref="WorldDimensions.RoadSurfaceHeight"/> everywhere else —
        /// the flat road lane, a Crosswalk (painted flat onto the road), and
        /// grass/open lot all sit on that base plane. A point is "on" a band
        /// when it falls within that edge's rectangular footprint: its clamped
        /// perpendicular distance to the edge segment is within the edge's
        /// half <see cref="WalkEdge.Width"/>.
        /// </summary>
        public float SurfaceHeightAt(GridPoint point)
        {
            foreach (var edge in edges)
            {
                // Crosswalks are painted flat onto the road (road level), which
                // is the default anyway — only the raised Sidewalk/FrontWalkway
                // bands lift the surface, so only they need testing.
                if (edge.Kind == WalkEdgeKind.Crosswalk)
                {
                    continue;
                }

                var foot = ProjectOntoSegment(point, edge.A, edge.B);
                var dx = point.X - foot.X;
                var dz = point.Z - foot.Z;
                var halfWidth = edge.Width / 2f;
                if (dx * dx + dz * dz <= halfWidth * halfWidth)
                {
                    return WorldDimensions.SidewalkSurfaceHeight;
                }
            }

            return WorldDimensions.RoadSurfaceHeight;
        }

        /// <summary>
        /// The house's front walkway edge (#128): A is the front-door node
        /// on the lot, B is the sidewalk attach point. The lot's ONLY
        /// connection to the rest of the network.
        /// </summary>
        public bool TryGetFrontWalkway(int houseId, out WalkEdge walkway)
        {
            return frontWalkways.TryGetValue(houseId, out walkway);
        }

        /// <summary>
        /// #461: the point on the lot's nearest sidewalk its front walkway WILL
        /// attach to once a house is built — resolved even before that walkway
        /// edge exists, by the SAME nearest-sidewalk projection
        /// <see cref="AttachFrontWalkway"/> uses at build time. A zone lot's
        /// trees are pre-baked at unlock (before any house/walkway), so
        /// <see cref="HousePlacement.PredeterminedFrontFacing"/> /
        /// <see cref="HousePlacement.PredeterminedPosition"/> call this to orient
        /// them to the house's real street-ward facing — which then equals the
        /// walkway-derived facing once the house is actually built (the map is
        /// axis-aligned, so projecting the door lands on the same sidewalk line).
        /// Returns false when the network carries no sidewalk edge at all (the
        /// caller then falls back to the crude Z-sign guess).
        /// </summary>
        public bool TryProjectFrontSidewalk(HouseLot lot, out GridPoint attach)
        {
            var bestDistance = float.MaxValue;
            var found = false;
            attach = lot.Position;

            foreach (var edge in edges)
            {
                if (edge.Kind != WalkEdgeKind.Sidewalk)
                {
                    continue;
                }

                var projected = ProjectOntoSegment(lot.Position, edge.A, edge.B);
                var dx = projected.X - lot.Position.X;
                var dz = projected.Z - lot.Position.Z;
                var distance = dx * dx + dz * dz;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    attach = projected;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>The graph node nearest an arbitrary point — used to
        /// snap loosely-known positions (e.g. a dog's current transform)
        /// onto the network.</summary>
        public GridPoint NearestNode(GridPoint from)
        {
            return NearestNodeAmong(nodeOrder, from);
        }

        /// <summary>The nearest node reachable by a Sidewalk or Crosswalk
        /// edge (#106) — excludes front-door nodes, which only ever have a
        /// FrontWalkway edge. General wander must never snap onto one.</summary>
        public GridPoint NearestWalkableNode(GridPoint from)
        {
            var walkable = nodeOrder.Where(n => adjacency[n].Any(e => e.Kind != WalkEdgeKind.FrontWalkway));
            return NearestNodeAmong(walkable, from);
        }

        private static GridPoint NearestNodeAmong(IEnumerable<GridPoint> candidates, GridPoint from)
        {
            var best = default(GridPoint);
            var bestDistance = float.MaxValue;
            var found = false;

            foreach (var node in candidates)
            {
                var dx = node.X - from.X;
                var dz = node.Z - from.Z;
                var distance = dx * dx + dz * dz;
                if (!found || distance < bestDistance)
                {
                    bestDistance = distance;
                    best = node;
                    found = true;
                }
            }

            return best;
        }

        /// <summary>True if every node can reach every other node (#106) —
        /// the starting tile's sidewalk+crosswalk+walkway network must
        /// form one connected graph.</summary>
        public bool IsFullyConnected()
        {
            if (nodeOrder.Count == 0)
            {
                return true;
            }

            var visited = new HashSet<GridPoint> { nodeOrder[0] };
            var queue = new Queue<GridPoint>();
            queue.Enqueue(nodeOrder[0]);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var edge in EdgesFrom(current))
                {
                    var neighbor = edge.Other(current);
                    if (visited.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return visited.Count == nodeOrder.Count;
        }

        /// <summary>
        /// Shortest path (Dijkstra) from the node nearest <paramref name="from"/>
        /// to the node nearest <paramref name="to"/>, as an ordered list of
        /// real network nodes. Every consecutive pair is a real edge.
        /// </summary>
        public IReadOnlyList<GridPoint> FindPath(GridPoint from, GridPoint to)
        {
            var start = NearestNode(from);
            var goal = NearestNode(to);

            var distances = new Dictionary<GridPoint, float> { [start] = 0f };
            var previous = new Dictionary<GridPoint, GridPoint>();
            var visited = new HashSet<GridPoint>();
            var frontier = new List<GridPoint> { start };

            while (frontier.Count > 0)
            {
                frontier.Sort((a, b) => distances[a].CompareTo(distances[b]));
                var current = frontier[0];
                frontier.RemoveAt(0);

                if (!visited.Add(current))
                {
                    continue;
                }

                if (current.Equals(goal))
                {
                    break;
                }

                foreach (var edge in EdgesFrom(current))
                {
                    var neighbor = edge.Other(current);
                    if (visited.Contains(neighbor))
                    {
                        continue;
                    }

                    var candidate = distances[current] + edge.Length;
                    if (!distances.TryGetValue(neighbor, out var known) || candidate < known)
                    {
                        distances[neighbor] = candidate;
                        previous[neighbor] = current;
                        frontier.Add(neighbor);
                    }
                }
            }

            if (!distances.ContainsKey(goal))
            {
                return Array.Empty<GridPoint>();
            }

            var path = new List<GridPoint> { goal };
            var node = goal;
            while (!node.Equals(start))
            {
                node = previous[node];
                path.Add(node);
            }

            path.Reverse();
            return path;
        }

        /// <summary>
        /// Builds the walk network from a set of roads and house lots
        /// (#106). Generic over any axis-aligned road layout: finds every
        /// crossing between roads of different orientation, splits each
        /// road's sidewalks into arms around those crossings, adds the
        /// 4-crosswalk box at each crossing, and runs a front walkway
        /// (#128) from every house's door to its street-facing sidewalk —
        /// consulting <see cref="HousePlacement"/>'s pure helpers and the
        /// #125 <see cref="HouseModelCatalog"/> for the door position, so
        /// dogs can path to actual front doors.
        /// </summary>
        public static WalkNetwork BuildFrom(IReadOnlyList<Road> roads, IReadOnlyList<HouseLot> houseLots)
        {
            return BuildFrom(roads, houseLots, Array.Empty<CulDeSacTurnaround>());
        }

        /// <summary>
        /// #581 overload: as <see cref="BuildFrom(IReadOnlyList{Road}, IReadOnlyList{HouseLot})"/>,
        /// plus a set of cul-de-sac bulb turnarounds to curve. Plain <c>Turn*</c>
        /// bends are detected geometrically from the roads themselves (two
        /// perpendicular stubs meeting endpoint-to-endpoint) and curved
        /// automatically; a cul-de-sac's dead-end can't be told apart from a
        /// road running to the map frontier by geometry alone, so the caller
        /// (<see cref="MapWalkNetwork"/>, which knows the tile is a cul-de-sac)
        /// passes those explicitly. <see cref="NeighborhoodLayout"/>'s starting
        /// intersection has neither, so it uses the two-argument overload.
        /// </summary>
        public static WalkNetwork BuildFrom(IReadOnlyList<Road> roads, IReadOnlyList<HouseLot> houseLots,
            IReadOnlyList<CulDeSacTurnaround> culDeSacTurnarounds)
        {
            var edges = new List<WalkEdge>();

            foreach (var road in roads)
            {
                var crossings = FindCrossings(road, roads);
                BuildSidewalkArms(road, crossings, edges);
                BuildCrosswalks(road, crossings, edges);
            }

            BuildBendArcs(roads, edges);

            foreach (var turnaround in culDeSacTurnarounds)
            {
                BuildCulDeSacTurnaround(turnaround, edges);
            }

            var frontWalkways = new Dictionary<int, WalkEdge>();
            foreach (var lot in houseLots)
            {
                var walkway = AttachFrontWalkway(lot, edges);
                if (walkway.HasValue)
                {
                    frontWalkways[lot.HouseId] = walkway.Value;
                }
            }

            return new WalkNetwork(edges, frontWalkways);
        }

        /// <summary>
        /// Re-aligns one lot's front-walkway edge to the house's current
        /// <paramref name="level"/> (#454). <see cref="BuildFrom"/> bakes each
        /// walkway once from the level-1 door and never recomputes it, so an
        /// upgraded house's walkway kept running to its stale level-1 door. This
        /// removes the lot's existing FrontWalkway edge and re-attaches it from
        /// the level-aware door position (<see cref="HouseModelCatalog.ForHouse(int, int)"/>
        /// + <see cref="HousePlacement.PositionFor(HouseLot, float, GridPoint, int)"/>),
        /// re-projecting the sidewalk attach point and re-splitting the sidewalk
        /// as needed, then rebuilds the adjacency index. Returns whether a
        /// walkway now exists for the lot (false only if no sidewalk was found).
        /// Called by <c>HouseUpgradeDirector.RefreshHouse</c> alongside the mesh
        /// rebuild so mesh, visible door, and walkway all agree at the new level.
        /// </summary>
        public bool RefreshFrontWalkway(HouseLot lot, int level)
        {
            if (!frontWalkways.TryGetValue(lot.HouseId, out var old))
            {
                // No baked walkway for this lot (e.g. a lot with no reachable
                // sidewalk) — nothing to re-align.
                return false;
            }

            // Reuse the baked walkway's known sidewalk placement rather than
            // re-running the nearest-sidewalk search over the now-fragmented
            // edge set: the facing and the sidewalk line the lot attaches to are
            // fixed by leveling (leveling never moves the lot center), so only
            // the door and its perpendicular attach point move.
            var facing = HousePlacement.FacingToward(lot.Position, old.B);
            var housePosition = HousePlacement.PositionFor(lot, HousePlacement.KitScale, old.B, level);
            var model = HouseModelCatalog.ForHouse(lot.HouseId, level);
            var door = model.FrontDoorWorldPosition(
                housePosition, HousePlacement.ModelYawDegrees(facing), HousePlacement.KitScale);

            // Re-project the new door perpendicular onto the sidewalk centerline
            // line the old walkway attached to. This map is axis-aligned, so the
            // sidewalk runs along the lateral axis at the fixed offset old.B
            // already carries; only the door's lateral coordinate can shift it.
            var attach = facing.X != 0f
                ? new GridPoint(old.B.X, door.Z)
                : new GridPoint(door.X, old.B.Z);

            edges.Remove(old);
            if (!PointsNearlyEqual(attach, old.B))
            {
                // The door shifted laterally (a level whose door sits off-centre),
                // so the attach slides along the sidewalk: move the split node the
                // old walkway created from old.B to the new attach, keeping the
                // two collinear sidewalk halves connected.
                MoveSidewalkNode(old.B, attach);
            }

            var walkway = new WalkEdge(door, attach, WalkEdgeKind.FrontWalkway, WorldDimensions.SidewalkWidth);
            edges.Add(walkway);
            frontWalkways[lot.HouseId] = walkway;

            RebuildAdjacency();
            return true;
        }

        /// <summary>Slides a sidewalk split node from <paramref name="from"/> to
        /// <paramref name="to"/> (#454): rewrites every Sidewalk edge endpoint at
        /// <paramref name="from"/> to <paramref name="to"/>. Used when a refreshed
        /// front walkway's attach point moves along the same sidewalk line, so the
        /// two collinear sidewalk halves that met at the old split stay joined at
        /// the new one.</summary>
        private void MoveSidewalkNode(GridPoint from, GridPoint to)
        {
            for (var i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (edge.Kind != WalkEdgeKind.Sidewalk)
                {
                    continue;
                }

                var a = PointsNearlyEqual(edge.A, from) ? to : edge.A;
                var b = PointsNearlyEqual(edge.B, from) ? to : edge.B;
                if (!a.Equals(edge.A) || !b.Equals(edge.B))
                {
                    edges[i] = new WalkEdge(a, b, edge.Kind, edge.Width);
                }
            }
        }

        /// <summary>Rebuilds the node/adjacency index from the current
        /// <see cref="edges"/> list — used after
        /// <see cref="RefreshFrontWalkway"/> mutates the edge set (#454).</summary>
        private void RebuildAdjacency()
        {
            adjacency.Clear();
            nodeOrder.Clear();
            foreach (var edge in edges)
            {
                AddAdjacency(edge.A, edge);
                AddAdjacency(edge.B, edge);
            }
        }

        private readonly struct Crossing
        {
            public readonly float Along;
            public readonly Road Other;

            /// <summary>#581: true when this is a plain <c>Turn*</c> bend — two
            /// perpendicular stub roads meeting only at their shared endpoint
            /// (the crossing sits at the END of BOTH roads) rather than a real
            /// intersection (where the crossing is interior to at least one
            /// road). A bend curves; a real crossing gets the crosswalk box.</summary>
            public readonly bool IsBend;

            public Crossing(float along, Road other, bool isBend)
            {
                Along = along;
                Other = other;
                IsBend = isBend;
            }
        }

        private static List<Crossing> FindCrossings(Road road, IReadOnlyList<Road> allRoads)
        {
            var crossings = new List<Crossing>();

            foreach (var other in allRoads)
            {
                if (ReferenceEquals(other, road) || other.Orientation == road.Orientation)
                {
                    continue;
                }

                var crossPoint = road.Orientation == StreetOrientation.NorthSouth
                    ? new GridPoint(road.Center.X, other.Center.Z)
                    : new GridPoint(other.Center.X, road.Center.Z);

                var along = road.Orientation == StreetOrientation.NorthSouth
                    ? crossPoint.Z - road.Center.Z
                    : crossPoint.X - road.Center.X;

                var alongOnOther = other.Orientation == StreetOrientation.NorthSouth
                    ? crossPoint.Z - other.Center.Z
                    : crossPoint.X - other.Center.X;

                if (Math.Abs(along) <= road.HalfLength + Epsilon && Math.Abs(alongOnOther) <= other.HalfLength + Epsilon)
                {
                    // A bend: the crossing is at the END of both roads (two
                    // perpendicular stubs touching endpoint-to-endpoint). A real
                    // crossing is interior to at least one of them.
                    var isBend = Math.Abs(Math.Abs(along) - road.HalfLength) < Epsilon
                        && Math.Abs(Math.Abs(alongOnOther) - other.HalfLength) < Epsilon;
                    crossings.Add(new Crossing(along, other, isBend));
                }
            }

            crossings.Sort((a, b) => a.Along.CompareTo(b.Along));
            return crossings;
        }

        private static float SidewalkOffsetMagnitude(Road road)
        {
            return Math.Abs(road.Sidewalks[0].CenterOffset);
        }

        private static void BuildSidewalkArms(Road road, List<Crossing> crossings, List<WalkEdge> edges)
        {
            foreach (var sidewalk in road.Sidewalks)
            {
                var boundaries = new List<float> { -road.HalfLength };
                foreach (var crossing in crossings)
                {
                    // A bend clips the arm back to the corner-arc tangent point
                    // (RoadBendCornerRadius from the shared endpoint), where the
                    // inserted arc takes over (#581); a real crossing clips back
                    // by the crossing road's sidewalk half-width, as before.
                    var mag = crossing.IsBend
                        ? WorldDimensions.RoadBendCornerRadius
                        : SidewalkOffsetMagnitude(crossing.Other);
                    boundaries.Add(crossing.Along - mag);
                    boundaries.Add(crossing.Along + mag);
                }

                boundaries.Add(road.HalfLength);

                for (var i = 0; i + 1 < boundaries.Count; i += 2)
                {
                    var t0 = boundaries[i];
                    var t1 = boundaries[i + 1];
                    if (t1 - t0 < Epsilon)
                    {
                        continue;
                    }

                    var a = road.PointAt(t0, sidewalk.CenterOffset);
                    var b = road.PointAt(t1, sidewalk.CenterOffset);
                    edges.Add(new WalkEdge(a, b, WalkEdgeKind.Sidewalk, WorldDimensions.SidewalkWidth));
                }
            }
        }

        private static void BuildCrosswalks(Road road, List<Crossing> crossings, List<WalkEdge> edges)
        {
            var positive = road.Sidewalks.First(s => s.Side == RoadSide.Positive);
            var negative = road.Sidewalks.First(s => s.Side == RoadSide.Negative);

            foreach (var crossing in crossings)
            {
                // A plain Turn* bend is not a real crossing — it curves instead
                // of getting a straight crosswalk box (#581).
                if (crossing.IsBend)
                {
                    continue;
                }

                var mag = SidewalkOffsetMagnitude(crossing.Other);

                foreach (var sign in new[] { 1f, -1f })
                {
                    var t = crossing.Along + sign * mag;
                    var a = road.PointAt(t, positive.CenterOffset);
                    var b = road.PointAt(t, negative.CenterOffset);
                    edges.Add(new WalkEdge(a, b, WalkEdgeKind.Crosswalk, WorldDimensions.CrosswalkWidth));
                }
            }
        }

        /// <summary>The largest angular step (radians) between two consecutive
        /// waypoints on an inserted corner/turnaround arc (#581): a smaller step
        /// makes the polyline hug the true arc more closely. 30 degrees.</summary>
        private static readonly float ArcMaxSegmentRadians = (float)(Math.PI / 6.0);

        /// <summary>Minimum straight hops any inserted arc is split into (#581),
        /// so even a very short arc still reads as a curve, not a single chord.</summary>
        private const int MinArcSegments = 2;

        /// <summary>
        /// Curves every plain <c>Turn*</c> bend in <paramref name="roads"/>
        /// (#581): two perpendicular stub roads meeting endpoint-to-endpoint.
        /// Each bend's two sidewalk arms (already clipped
        /// <see cref="WorldDimensions.RoadBendCornerRadius"/> back from the
        /// shared corner by <see cref="BuildSidewalkArms"/>) are joined by two
        /// quarter-circle arcs — inner and outer — concentric with the road's
        /// own corner arc, so <see cref="FindPath"/> traces the curve instead of
        /// the old straight box-corner chord. Each bend is processed once, from
        /// its north-south stub (a bend's two stubs always have opposite
        /// orientations).
        /// </summary>
        private static void BuildBendArcs(IReadOnlyList<Road> roads, List<WalkEdge> edges)
        {
            foreach (var road in roads)
            {
                if (road.Orientation != StreetOrientation.NorthSouth)
                {
                    continue;
                }

                foreach (var crossing in FindCrossings(road, roads))
                {
                    if (crossing.IsBend)
                    {
                        BuildBendArc(road, crossing.Along, crossing.Other, edges);
                    }
                }
            }
        }

        private static void BuildBendArc(Road nsRoad, float alongNs, Road ewRoad, List<WalkEdge> edges)
        {
            var r = WorldDimensions.RoadBendCornerRadius;
            var offset = SidewalkOffsetMagnitude(nsRoad);

            // The shared corner endpoint (the tile centre) and the interior
            // directions each stub runs from it.
            var p = nsRoad.PointAt(alongNs, 0f);
            var alongEw = ewRoad.AlongAxis(p);
            var dirNsZ = -Math.Sign(alongNs);
            var dirEwX = -Math.Sign(alongEw);

            // The corner arc's centre: R along each interior direction from the
            // corner (into the quadrant the two arms point into) — the same
            // centre the rendered road-bend arc has.
            var center = new GridPoint(p.X + r * dirEwX, p.Z + r * dirNsZ);

            // Where each arm was clipped: R from the corner, toward the interior.
            var clipNs = alongNs - Math.Sign(alongNs) * r;
            var clipEw = alongEw - Math.Sign(alongEw) * r;

            // The inner sidewalk of each stub is the one offset toward the arc
            // centre; the outer is the opposite. NS sidewalks offset along X,
            // EW along Z.
            var innerNsOffset = center.X > p.X ? offset : -offset;
            var innerEwOffset = center.Z > p.Z ? offset : -offset;

            AddArc(edges,
                nsRoad.PointAt(clipNs, innerNsOffset),
                ewRoad.PointAt(clipEw, innerEwOffset),
                center, r - offset);

            AddArc(edges,
                nsRoad.PointAt(clipNs, -innerNsOffset),
                ewRoad.PointAt(clipEw, -innerEwOffset),
                center, r + offset);
        }

        /// <summary>
        /// Curves a cul-de-sac's bulb-side turnaround (#581): joins the dead-end
        /// stub's two sidewalk arm ends with an arc of radius
        /// <see cref="WorldDimensions.CulDeSacBulbRadius"/> that bulges around
        /// the closed bulb, so a dog can loop from one side to the other instead
        /// of dead-ending. The arc's endpoints are the stub's existing bulb-end
        /// sidewalk nodes, so no node is left dangling.
        /// </summary>
        private static void BuildCulDeSacTurnaround(CulDeSacTurnaround turnaround, List<WalkEdge> edges)
        {
            var stub = turnaround.Stub;
            var rBulb = WorldDimensions.CulDeSacBulbRadius;
            var offset = SidewalkOffsetMagnitude(stub);

            var bulbAlong = turnaround.BulbAtPositiveEnd ? stub.HalfLength : -stub.HalfLength;
            var closedSign = turnaround.BulbAtPositiveEnd ? 1f : -1f;

            var endA = stub.PointAt(bulbAlong, offset);
            var endB = stub.PointAt(bulbAlong, -offset);

            // The bulb-end sidewalk nodes sit `offset` apart across the road; the
            // arc centre lies `back` down the OPEN side of that chord so the arc
            // of radius rBulb bulges toward the closed (dead) side.
            var pb = stub.PointAt(bulbAlong, 0f);
            var back = (float)Math.Sqrt(rBulb * rBulb - offset * offset);
            var center = stub.Orientation == StreetOrientation.NorthSouth
                ? new GridPoint(pb.X, pb.Z - closedSign * back)
                : new GridPoint(pb.X - closedSign * back, pb.Z);

            AddArc(edges, endA, endB, center, rBulb);
        }

        /// <summary>
        /// Adds the sidewalk waypoint hops of one inserted arc (#581): from
        /// <paramref name="endA"/> to <paramref name="endB"/> along the minor
        /// arc of the circle centred at <paramref name="center"/> with the given
        /// <paramref name="radius"/>. The two endpoints are used verbatim (they
        /// are existing arm nodes) so the arc stays welded to the arms; only the
        /// interior waypoints are generated on the circle.
        /// </summary>
        private static void AddArc(List<WalkEdge> edges, GridPoint endA, GridPoint endB, GridPoint center, float radius)
        {
            var angleA = (float)Math.Atan2(endA.Z - center.Z, endA.X - center.X);
            var angleB = (float)Math.Atan2(endB.Z - center.Z, endB.X - center.X);

            // Sweep the short way around — the minor arc, which by construction
            // bulges to the correct side for every #581 corner and turnaround.
            var sweep = angleB - angleA;
            var twoPi = (float)(2 * Math.PI);
            while (sweep > Math.PI) sweep -= twoPi;
            while (sweep < -Math.PI) sweep += twoPi;

            var segments = Math.Max(MinArcSegments,
                (int)Math.Ceiling(Math.Abs(sweep) / ArcMaxSegmentRadians));

            var previous = endA;
            for (var i = 1; i < segments; i++)
            {
                var angle = angleA + sweep * (i / (float)segments);
                var waypoint = new GridPoint(
                    center.X + radius * (float)Math.Cos(angle),
                    center.Z + radius * (float)Math.Sin(angle));
                edges.Add(new WalkEdge(previous, waypoint, WalkEdgeKind.Sidewalk, WorldDimensions.SidewalkWidth));
                previous = waypoint;
            }

            edges.Add(new WalkEdge(previous, endB, WalkEdgeKind.Sidewalk, WorldDimensions.SidewalkWidth));
        }

        /// <summary>
        /// The lot's front walkway (#128, replacing the old lot-center
        /// driveway stub): find the sidewalk edge nearest the LOT CENTER
        /// (that still decides which street the house faces, exactly as
        /// the stub did), derive the house's front-setback position and
        /// its catalog door from it, then run the walkway from the DOOR
        /// perpendicular onto that sidewalk — splitting the sidewalk edge
        /// at the attach point if needed. The door becomes the lot-side
        /// node: dogs path to actual front doors now. Baked once at
        /// world-build time from the house's as-built (level-1) mesh;
        /// <see cref="RefreshFrontWalkway"/> re-aligns a single lot's edge to
        /// an upgraded level afterward (#454).
        /// </summary>
        private static WalkEdge? AttachFrontWalkway(HouseLot lot, List<WalkEdge> edges)
        {
            var bestDistance = float.MaxValue;
            var bestIndex = -1;
            var bestPoint = lot.Position;

            for (var i = 0; i < edges.Count; i++)
            {
                if (edges[i].Kind != WalkEdgeKind.Sidewalk)
                {
                    continue;
                }

                var projected = ProjectOntoSegment(lot.Position, edges[i].A, edges[i].B);
                var dx = projected.X - lot.Position.X;
                var dz = projected.Z - lot.Position.Z;
                var distance = dx * dx + dz * dz;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                    bestPoint = projected;
                }
            }

            if (bestIndex < 0)
            {
                return null;
            }

            var facing = HousePlacement.FacingToward(lot.Position, bestPoint);
            var housePosition = HousePlacement.PositionFor(lot, HousePlacement.KitScale, bestPoint);
            var model = HouseModelCatalog.ForHouse(lot.HouseId);
            var door = model.FrontDoorWorldPosition(
                housePosition, HousePlacement.ModelYawDegrees(facing), HousePlacement.KitScale);

            var original = edges[bestIndex];
            var attach = ProjectOntoSegment(door, original.A, original.B);

            if (!PointsNearlyEqual(attach, original.A) && !PointsNearlyEqual(attach, original.B))
            {
                // Split the sidewalk edge at the attach point.
                edges.RemoveAt(bestIndex);
                edges.Add(new WalkEdge(original.A, attach, WalkEdgeKind.Sidewalk, original.Width));
                edges.Add(new WalkEdge(attach, original.B, WalkEdgeKind.Sidewalk, original.Width));
            }

            var walkway = new WalkEdge(door, attach, WalkEdgeKind.FrontWalkway, WorldDimensions.SidewalkWidth);
            edges.Add(walkway);
            return walkway;
        }

        private static GridPoint ProjectOntoSegment(GridPoint point, GridPoint a, GridPoint b)
        {
            var abx = b.X - a.X;
            var abz = b.Z - a.Z;
            var lengthSquared = abx * abx + abz * abz;

            if (lengthSquared < Epsilon)
            {
                return a;
            }

            var t = ((point.X - a.X) * abx + (point.Z - a.Z) * abz) / lengthSquared;
            t = Math.Max(0f, Math.Min(1f, t));

            return new GridPoint(a.X + t * abx, a.Z + t * abz);
        }

        private static bool PointsNearlyEqual(GridPoint a, GridPoint b)
        {
            return Math.Abs(a.X - b.X) < Epsilon && Math.Abs(a.Z - b.Z) < Epsilon;
        }
    }
}
