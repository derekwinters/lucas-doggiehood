using System;
using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// A routable graph over the live map's road centerlines (#599). Nodes are
    /// every junction of the road set — each road's two endpoints plus every
    /// point where two roads cross (a four-way centre is the midpoint of both its
    /// roads, not an endpoint, so it must be found as a crossing) — and each road
    /// is split into graph edges between the consecutive junctions along it,
    /// weighted by length. A delivery truck's route is a shortest path here, so it
    /// always follows the roadway across however many tiles the path spans.
    ///
    /// Query endpoints (an off-map opening, or a stop projected onto a road) may
    /// fall mid-segment; <see cref="TryShortestPath"/> splices them in between the
    /// junctions that bracket them on their road, so the returned waypoints run
    /// cleanly along centerlines. This is the seam #600 (concurrent trucks /
    /// car-following) extends: it consumes routes produced here without
    /// re-deriving the graph.
    /// </summary>
    public sealed class RoadNetworkGraph
    {
        // Node identity snaps world points onto a fixed grid so junctions two
        // roads share resolve to the same node despite float representation.
        // 0.01m precision is far finer than any tile geometry.
        private const float SnapPrecision = 100f;
        private const float Epsilon = 0.01f;

        private readonly IReadOnlyList<Road> roads;
        private readonly Dictionary<long, GridPoint> nodes = new Dictionary<long, GridPoint>();
        private readonly Dictionary<long, List<Edge>> adjacency = new Dictionary<long, List<Edge>>();

        public RoadNetworkGraph(IReadOnlyList<Road> roads)
        {
            this.roads = roads ?? throw new ArgumentNullException(nameof(roads));

            var junctions = CollectJunctions(roads);
            foreach (var road in roads)
            {
                var onRoad = JunctionsOnRoad(road, junctions);
                onRoad.Sort((a, b) => a.Along.CompareTo(b.Along));
                for (var i = 0; i < onRoad.Count - 1; i++)
                {
                    Connect(onRoad[i].Point, onRoad[i + 1].Point);
                }
            }
        }

        /// <summary>
        /// The shortest sequence of waypoints from <paramref name="from"/> to
        /// <paramref name="to"/> along road centerlines, inclusive of both ends.
        /// Throws when they are on disconnected road clusters — callers that need
        /// to probe reachability first use <see cref="TryShortestPath"/>.
        /// </summary>
        public IReadOnlyList<GridPoint> ShortestPath(GridPoint from, GridPoint to)
        {
            if (!TryShortestPath(from, to, out var path))
            {
                throw new InvalidOperationException(
                    $"No road path connects {from} to {to} on the live map.");
            }

            return path;
        }

        /// <summary>
        /// Attempts a shortest path; returns false (and null) when the endpoints
        /// lie on road clusters with no connection between them.
        /// </summary>
        public bool TryShortestPath(GridPoint from, GridPoint to, out IReadOnlyList<GridPoint> path)
        {
            path = null;

            // Work on a scratch copy of the adjacency so injected temp nodes for
            // this query don't pollute the persistent graph.
            var links = CloneAdjacency();
            var points = new Dictionary<long, GridPoint>(nodes);

            var startKey = InjectPoint(from, links, points);
            var goalKey = InjectPoint(to, links, points);
            if (startKey == null || goalKey == null)
            {
                return false;
            }

            var route = Dijkstra(startKey.Value, goalKey.Value, links);
            if (route == null)
            {
                return false;
            }

            var waypoints = new List<GridPoint>(route.Count);
            foreach (var key in route)
            {
                waypoints.Add(points[key]);
            }

            path = waypoints;
            return true;
        }

        private static long Key(GridPoint point)
        {
            var x = (long)Math.Round(point.X * SnapPrecision);
            var z = (long)Math.Round(point.Z * SnapPrecision);
            return (x << 32) ^ (z & 0xffffffffL);
        }

        /// <summary>
        /// Every junction on the road set: each road's two endpoints, plus every
        /// perpendicular crossing point interior to two roads.
        /// </summary>
        private static Dictionary<long, GridPoint> CollectJunctions(IReadOnlyList<Road> roads)
        {
            var junctions = new Dictionary<long, GridPoint>();

            foreach (var road in roads)
            {
                Add(junctions, road.PointAt(road.HalfLength, 0f));
                Add(junctions, road.PointAt(-road.HalfLength, 0f));
            }

            for (var i = 0; i < roads.Count; i++)
            {
                for (var j = i + 1; j < roads.Count; j++)
                {
                    if (TryCrossing(roads[i], roads[j], out var crossing))
                    {
                        Add(junctions, crossing);
                    }
                }
            }

            return junctions;
        }

        /// <summary>The point where a north-south road and an east-west road
        /// cross, when that point is within both roads' extents.</summary>
        private static bool TryCrossing(Road a, Road b, out GridPoint crossing)
        {
            crossing = default;
            if (a.Orientation == b.Orientation)
            {
                return false;
            }

            var ns = a.Orientation == StreetOrientation.NorthSouth ? a : b;
            var ew = a.Orientation == StreetOrientation.NorthSouth ? b : a;

            var x = ns.Center.X;
            var z = ew.Center.Z;

            if (Math.Abs(z - ns.Center.Z) > ns.HalfLength + Epsilon)
            {
                return false;
            }

            if (Math.Abs(x - ew.Center.X) > ew.HalfLength + Epsilon)
            {
                return false;
            }

            crossing = new GridPoint(x, z);
            return true;
        }

        private static List<Junction> JunctionsOnRoad(Road road, Dictionary<long, GridPoint> junctions)
        {
            var result = new List<Junction>();
            foreach (var point in junctions.Values)
            {
                if (OnCenterline(road, point))
                {
                    result.Add(new Junction(road.AlongAxis(point), point));
                }
            }

            return result;
        }

        private static bool OnCenterline(Road road, GridPoint point)
        {
            var perpendicular = road.Orientation == StreetOrientation.NorthSouth
                ? point.X - road.Center.X
                : point.Z - road.Center.Z;
            if (Math.Abs(perpendicular) > Epsilon)
            {
                return false;
            }

            return Math.Abs(road.AlongAxis(point)) <= road.HalfLength + Epsilon;
        }

        private static void Add(Dictionary<long, GridPoint> junctions, GridPoint point)
        {
            junctions[Key(point)] = point;
        }

        private void Connect(GridPoint a, GridPoint b)
        {
            var ka = Key(a);
            var kb = Key(b);
            if (ka == kb)
            {
                return;
            }

            nodes[ka] = a;
            nodes[kb] = b;
            var weight = Distance(a, b);
            AddLink(adjacency, ka, kb, weight);
            AddLink(adjacency, kb, ka, weight);
        }

        private static void AddLink(Dictionary<long, List<Edge>> links, long from, long to, float weight)
        {
            if (!links.TryGetValue(from, out var list))
            {
                list = new List<Edge>();
                links[from] = list;
            }

            list.Add(new Edge(to, weight));
        }

        private Dictionary<long, List<Edge>> CloneAdjacency()
        {
            var clone = new Dictionary<long, List<Edge>>(adjacency.Count);
            foreach (var pair in adjacency)
            {
                clone[pair.Key] = new List<Edge>(pair.Value);
            }

            return clone;
        }

        /// <summary>
        /// Resolves <paramref name="point"/> to a node key. If it coincides with
        /// an existing node, that node is used; otherwise, if it lies on a road,
        /// a temp node is spliced between the two nodes that bracket it along that
        /// road. Returns null when the point is on no road at all.
        /// </summary>
        private long? InjectPoint(GridPoint point, Dictionary<long, List<Edge>> links, Dictionary<long, GridPoint> points)
        {
            var key = Key(point);
            if (points.ContainsKey(key))
            {
                return key;
            }

            foreach (var road in roads)
            {
                if (!OnCenterline(road, point))
                {
                    continue;
                }

                var along = road.AlongAxis(point);

                // Bracket the point with the nearest existing nodes on either
                // side along this road, so the splice preserves any junction
                // between the point and a far endpoint.
                long? lowerKey = null;
                var lowerAlong = float.NegativeInfinity;
                long? upperKey = null;
                var upperAlong = float.PositiveInfinity;

                foreach (var node in points)
                {
                    var candidate = node.Value;
                    if (!OnCenterline(road, candidate))
                    {
                        continue;
                    }

                    var candidateAlong = road.AlongAxis(candidate);
                    if (candidateAlong <= along && candidateAlong > lowerAlong)
                    {
                        lowerAlong = candidateAlong;
                        lowerKey = node.Key;
                    }

                    if (candidateAlong >= along && candidateAlong < upperAlong)
                    {
                        upperAlong = candidateAlong;
                        upperKey = node.Key;
                    }
                }

                if (lowerKey == null && upperKey == null)
                {
                    continue;
                }

                points[key] = point;
                LinkBoth(links, key, point, lowerKey, points);
                LinkBoth(links, key, point, upperKey, points);
                return key;
            }

            return null;
        }

        private static void LinkBoth(
            Dictionary<long, List<Edge>> links, long key, GridPoint point,
            long? neighborKey, Dictionary<long, GridPoint> points)
        {
            if (neighborKey == null || neighborKey.Value == key)
            {
                return;
            }

            var weight = Distance(point, points[neighborKey.Value]);
            AddLink(links, key, neighborKey.Value, weight);
            AddLink(links, neighborKey.Value, key, weight);
        }

        private static List<long> Dijkstra(long start, long goal, Dictionary<long, List<Edge>> links)
        {
            if (start == goal)
            {
                return new List<long> { start };
            }

            var best = new Dictionary<long, float> { [start] = 0f };
            var previous = new Dictionary<long, long>();
            var visited = new HashSet<long>();
            var frontier = new List<long> { start };

            while (frontier.Count > 0)
            {
                // Small graphs — a linear extract-min keeps this dependency-free.
                var currentIndex = 0;
                for (var i = 1; i < frontier.Count; i++)
                {
                    if (best[frontier[i]] < best[frontier[currentIndex]])
                    {
                        currentIndex = i;
                    }
                }

                var current = frontier[currentIndex];
                frontier.RemoveAt(currentIndex);
                if (!visited.Add(current))
                {
                    continue;
                }

                if (current == goal)
                {
                    break;
                }

                if (!links.TryGetValue(current, out var neighbors))
                {
                    continue;
                }

                foreach (var edge in neighbors)
                {
                    if (visited.Contains(edge.To))
                    {
                        continue;
                    }

                    var tentative = best[current] + edge.Weight;
                    if (!best.TryGetValue(edge.To, out var existing) || tentative < existing - 0.0001f)
                    {
                        best[edge.To] = tentative;
                        previous[edge.To] = current;
                        frontier.Add(edge.To);
                    }
                }
            }

            if (!previous.ContainsKey(goal))
            {
                return null;
            }

            var path = new List<long> { goal };
            var node = goal;
            while (node != start)
            {
                node = previous[node];
                path.Add(node);
            }

            path.Reverse();
            return path;
        }

        private static float Distance(GridPoint a, GridPoint b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return (float)Math.Sqrt((dx * dx) + (dz * dz));
        }

        private readonly struct Edge
        {
            public readonly long To;
            public readonly float Weight;

            public Edge(long to, float weight)
            {
                To = to;
                Weight = weight;
            }
        }

        private readonly struct Junction
        {
            public readonly float Along;
            public readonly GridPoint Point;

            public Junction(float along, GridPoint point)
            {
                Along = along;
                Point = point;
            }
        }
    }
}
