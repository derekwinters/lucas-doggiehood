using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Dogs
{
    /// <summary>
    /// Produces wander movement targets as a node-to-node random walk over
    /// the sidewalk+crosswalk walk network (#8, #106). Front walkways (#128,
    /// replacing the old driveway stubs) are never entered — general
    /// wander stays off house lots/yards. At each
    /// node the choice between continuing straight and deviating/turning
    /// is weighted; the parameterless overload derives that weighting from
    /// the dog's own <see cref="MovementProfile.TurnProbability"/> (#89) —
    /// a low TurnProbability (Excited) means a high continue-weight, i.e.
    /// long straight stretches — while the explicit-weight overload is
    /// still available for callers that want to override it outright.
    /// Deterministic for a seed, matching the existing seeded-
    /// <see cref="Random"/> convention.
    /// </summary>
    public sealed class WanderBehavior
    {
        /// <summary>#430: the resident-house id meaning "this dog belongs to no
        /// house on the network" — the default, under which EVERY front walkway
        /// stays excluded exactly as the pre-#430 single-line filter did. Real
        /// house ids are always &gt;= 1 (starting ids 1-4, zone ids &gt;= 5), so
        /// 0 can never match a walkway's owning house.</summary>
        public const int NoResidentHouseId = 0;

        private readonly Random random;
        private readonly MovementProfile profile;
        private readonly Func<WalkNetwork> networkProvider;

        // #430: the id of the house this dog lives in. A FrontWalkway edge is a
        // wander candidate only when it is THIS house's own walkway; every other
        // front walkway stays excluded, so dogs never detour onto a neighbor's
        // lot. NoResidentHouseId excludes all front walkways.
        private readonly int residentHouseId;

        // The node the dog was at before its most recent hop — null until
        // the first call, since there's no arrival direction yet.
        private GridPoint? previousNode;

        public WanderBehavior(int seed, MovementProfile profile, int residentHouseId = NoResidentHouseId)
            : this(seed, profile, () => NeighborhoodLayout.WalkNetwork, residentHouseId)
        {
        }

        public WanderBehavior(int seed, MovementProfile profile, WalkNetwork network,
            int residentHouseId = NoResidentHouseId)
            : this(seed, profile, () => network, residentHouseId)
        {
        }

        /// <summary>
        /// Binds wander to a LIVE network the caller resolves on each hop
        /// (#398): DogView passes <c>() =&gt; state.WalkNetwork</c>, so an
        /// already-spawned dog automatically wanders onto newly unlocked
        /// tiles the moment the map-derived network grows — no re-spawn, no
        /// rebinding. <paramref name="residentHouseId"/> (#430) is the dog's own
        /// house, gating which front walkway (if any) it may step onto.
        /// </summary>
        public WanderBehavior(int seed, MovementProfile profile, Func<WalkNetwork> networkProvider,
            int residentHouseId = NoResidentHouseId)
        {
            random = new Random(seed);
            this.profile = profile;
            this.networkProvider = networkProvider ?? throw new ArgumentNullException(nameof(networkProvider));
            this.residentHouseId = residentHouseId;
        }

        /// <summary>Next node, weighting continue-straight-vs-deviate/turn
        /// by this dog's own MovementProfile.TurnProbability (#89): a
        /// lower TurnProbability means a higher chance of continuing
        /// straight, i.e. longer stretches before a turn.</summary>
        public GridPoint NextTarget(GridPoint current)
        {
            return NextTarget(current, continueWeight: 1f - profile.TurnProbability, deviateWeight: profile.TurnProbability);
        }

        /// <summary>
        /// Next node, weighting the continue-straight-vs-deviate/turn
        /// decision by the given weights (relative, need not sum to 1).
        /// </summary>
        public GridPoint NextTarget(GridPoint current, float continueWeight, float deviateWeight)
        {
            var network = networkProvider();
            var node = network.NearestWalkableNode(current);
            var candidates = network.EdgesFrom(node)
                .Where(e => IsWalkable(e, network))
                .ToList();

            var next = candidates.Count == 0
                ? node
                : ChooseNext(node, candidates, continueWeight, deviateWeight);

            previousNode = node;
            return next;
        }

        /// <summary>#430: which edges general wander may take from a node.
        /// Sidewalks and crosswalks are always fair game. A FrontWalkway is a
        /// candidate ONLY when it is this dog's own house's walkway — resolved
        /// by looking that house's walkway up on the network and matching its
        /// endpoints. Every other front walkway (and all of them, for a dog with
        /// <see cref="NoResidentHouseId"/>) stays excluded, so no dog wanders
        /// onto a lot that isn't its own.</summary>
        private bool IsWalkable(WalkEdge edge, WalkNetwork network)
        {
            if (edge.Kind != WalkEdgeKind.FrontWalkway)
            {
                return true;
            }

            return residentHouseId != NoResidentHouseId
                && network.TryGetFrontWalkway(residentHouseId, out var mine)
                && SameEdge(edge, mine);
        }

        private static bool SameEdge(WalkEdge a, WalkEdge b)
        {
            return (a.A.Equals(b.A) && a.B.Equals(b.B)) || (a.A.Equals(b.B) && a.B.Equals(b.A));
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
