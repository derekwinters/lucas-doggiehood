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

        /// <summary>#597: how far along its own walkway (0 at the sidewalk attach,
        /// 1 at the door) a resident dog must be before it counts as "on the lot
        /// side" and retraces the walkway. 0.5 is the walkway's midpoint, so the
        /// door-side half triggers the retrace while the sidewalk-side half falls
        /// through to ordinary wander.</summary>
        private const float LotSideEntryFraction = 0.5f;

        /// <summary>#597: how far PAST the door (in walkway-length units) still
        /// counts as at the door — a dog stops a hair short of or just past the
        /// door node, so a small overshoot onto the lot must still retrace rather
        /// than beeline across the yard.</summary>
        private const float LotSideOvershootFraction = 1.5f;

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
            var node = ResolveCurrentNode(current, network);
            var candidates = network.EdgesFrom(node)
                .Where(e => IsWalkable(e, network))
                .ToList();

            var next = candidates.Count == 0
                ? node
                : ChooseNext(node, candidates, continueWeight, deviateWeight);

            previousNode = node;
            return next;
        }

        /// <summary>
        /// #517/#597: resolve the dog's current network node for this hop. A
        /// resident dog on the LOT SIDE of its OWN walkway — at, near, or a
        /// hair past its front door (the walkway's lot-side endpoint
        /// <c>mine.A</c>, a walkway-only node the general walkable set
        /// deliberately excludes, #430) — must resolve to that door node, so
        /// its sole candidate edge is the walkway back down to the sidewalk
        /// attach point and it returns the way it came. Every other position
        /// (and every non-resident dog, which is barred from the walkway at
        /// all) falls through to the unchanged
        /// <see cref="WalkNetwork.NearestWalkableNode"/>, which would otherwise
        /// snap a dog near its door to the straight-line-nearest sidewalk node
        /// across the yard and beeline it off-network.
        ///
        /// #517 only matched the door node EXACTLY; a dog stops a hair short of
        /// or just past the node, so #597 saw the exact match miss and the dog
        /// cut diagonally across the lawn. This now fires for the whole
        /// door-side half of the dog's own walkway (plus a small overshoot),
        /// detected geometrically against the walkway segment so no other
        /// sidewalk node can trigger it.
        /// </summary>
        private GridPoint ResolveCurrentNode(GridPoint current, WalkNetwork network)
        {
            if (residentHouseId != NoResidentHouseId
                && network.TryGetFrontWalkway(residentHouseId, out var mine)
                && IsOnLotSideOfOwnWalkway(current, mine))
            {
                return mine.A;
            }

            return network.NearestWalkableNode(current);
        }

        /// <summary>#597: true when <paramref name="current"/> lies on the lot
        /// (door) side of the dog's own <paramref name="walkway"/> — its
        /// perpendicular projection onto the attach→door line is on the
        /// door-side half (past <see cref="LotSideEntryFraction"/>, up to a
        /// small overshoot past the door) AND it sits within the walkway's own
        /// lateral footprint. The lateral (perpendicular) bound is what keeps a
        /// distant sidewalk node — which can be straight-line closer to a door
        /// than that door's own attach point — from ever triggering the
        /// retrace: such a node is far to the SIDE of the narrow walkway
        /// segment.</summary>
        private static bool IsOnLotSideOfOwnWalkway(GridPoint current, WalkEdge walkway)
        {
            var alongX = walkway.A.X - walkway.B.X; // attach (B) -> door (A)
            var alongZ = walkway.A.Z - walkway.B.Z;
            var lengthSquared = alongX * alongX + alongZ * alongZ;
            if (lengthSquared <= float.Epsilon)
            {
                return current.Equals(walkway.A);
            }

            // Parameter along the walkway line: 0 at the attach, 1 at the door.
            var t = ((current.X - walkway.B.X) * alongX + (current.Z - walkway.B.Z) * alongZ) / lengthSquared;
            if (t < LotSideEntryFraction || t > LotSideOvershootFraction)
            {
                return false;
            }

            // Perpendicular distance from the walkway line, in the same
            // length-squared units, must be within half the walkway's width.
            var footX = walkway.B.X + t * alongX;
            var footZ = walkway.B.Z + t * alongZ;
            var lateralSquared = (current.X - footX) * (current.X - footX)
                + (current.Z - footZ) * (current.Z - footZ);
            var halfWidth = walkway.Width * 0.5f;
            return lateralSquared <= halfWidth * halfWidth;
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
