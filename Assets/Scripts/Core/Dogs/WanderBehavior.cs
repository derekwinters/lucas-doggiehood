using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Dogs
{
    /// <summary>
    /// Produces wander movement targets as a node-to-node random walk over
    /// the sidewalk+crosswalk walk network (#8, #106). Driveway stubs are
    /// never entered — general wander stays off house lots/yards. At each
    /// node the choice between continuing straight and deviating/turning
    /// is weighted; callers that pass no weights get even/uniform
    /// randomness. Deterministic for a seed, matching the existing
    /// seeded-<see cref="Random"/> convention.
    /// </summary>
    public sealed class WanderBehavior
    {
        private readonly Random random;
        private readonly MovementProfile profile;
        private readonly WalkNetwork network;

        // The node the dog was at before its most recent hop — null until
        // the first call, since there's no arrival direction yet.
        private GridPoint? previousNode;

        public WanderBehavior(int seed, MovementProfile profile)
            : this(seed, profile, NeighborhoodLayout.WalkNetwork)
        {
        }

        public WanderBehavior(int seed, MovementProfile profile, WalkNetwork network)
        {
            random = new Random(seed);
            this.profile = profile;
            this.network = network;
        }

        /// <summary>Next node, choosing evenly between continuing straight
        /// and deviating/turning.</summary>
        public GridPoint NextTarget(GridPoint current)
        {
            return NextTarget(current, continueWeight: 1f, deviateWeight: 1f);
        }

        /// <summary>
        /// Next node, weighting the continue-straight-vs-deviate/turn
        /// decision by the given weights (relative, need not sum to 1).
        /// </summary>
        public GridPoint NextTarget(GridPoint current, float continueWeight, float deviateWeight)
        {
            var node = network.NearestWalkableNode(current);
            var candidates = network.EdgesFrom(node)
                .Where(e => e.Kind != WalkEdgeKind.DrivewayStub)
                .ToList();

            var next = candidates.Count == 0
                ? node
                : ChooseNext(node, candidates, continueWeight, deviateWeight);

            previousNode = node;
            return next;
        }

        private GridPoint ChooseNext(GridPoint node, List<WalkEdge> candidates, float continueWeight, float deviateWeight)
        {
            if (!previousNode.HasValue)
            {
                // No arrival direction yet (first call) — nothing to weigh.
                return Pick(candidates).Other(node);
            }

            var arrivalDirection = Direction(previousNode.Value, node);
            var continueEdges = candidates.Where(e => Direction(node, e.Other(node)).Equals(arrivalDirection)).ToList();
            var deviateEdges = candidates.Where(e => !Direction(node, e.Other(node)).Equals(arrivalDirection)).ToList();

            List<WalkEdge> bucket;
            if (continueEdges.Count == 0)
            {
                bucket = deviateEdges;
            }
            else if (deviateEdges.Count == 0)
            {
                bucket = continueEdges;
            }
            else
            {
                var totalWeight = continueWeight + deviateWeight;
                var roll = random.NextDouble() * totalWeight;
                bucket = roll < continueWeight ? continueEdges : deviateEdges;
            }

            return Pick(bucket).Other(node);
        }

        private WalkEdge Pick(IReadOnlyList<WalkEdge> edges)
        {
            return edges[random.Next(edges.Count)];
        }

        private static GridPoint Direction(GridPoint from, GridPoint to)
        {
            return new GridPoint(Math.Sign(to.X - from.X), Math.Sign(to.Z - from.Z));
        }
    }
}
