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
        /// The house's front walkway edge (#128): A is the front-door node
        /// on the lot, B is the sidewalk attach point. The lot's ONLY
        /// connection to the rest of the network.
        /// </summary>
        public bool TryGetFrontWalkway(int houseId, out WalkEdge walkway)
        {
            return frontWalkways.TryGetValue(houseId, out walkway);
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
            var edges = new List<WalkEdge>();

            foreach (var road in roads)
            {
                var crossings = FindCrossings(road, roads);
                BuildSidewalkArms(road, crossings, edges);
                BuildCrosswalks(road, crossings, edges);
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

        private readonly struct Crossing
        {
            public readonly float Along;
            public readonly Road Other;

            public Crossing(float along, Road other)
            {
                Along = along;
                Other = other;
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
                    crossings.Add(new Crossing(along, other));
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
                    var mag = SidewalkOffsetMagnitude(crossing.Other);
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

        /// <summary>
        /// The lot's front walkway (#128, replacing the old lot-center
        /// driveway stub): find the sidewalk edge nearest the LOT CENTER
        /// (that still decides which street the house faces, exactly as
        /// the stub did), derive the house's front-setback position and
        /// its catalog door from it, then run the walkway from the DOOR
        /// perpendicular onto that sidewalk — splitting the sidewalk edge
        /// at the attach point if needed. The door becomes the lot-side
        /// node: dogs path to actual front doors now.
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
            var housePosition = HousePlacement.PositionFor(lot, HousePlacement.HouseTargetFootprint, bestPoint);
            var model = HouseModelCatalog.ForHouse(lot.HouseId);
            var scale = HousePlacement.HouseTargetFootprint / model.MaxFootprint;
            var door = model.FrontDoorWorldPosition(
                housePosition, HousePlacement.ModelYawDegrees(facing), scale);

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
