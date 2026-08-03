using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Dogs
{
    /// <summary>
    /// #430: a front walkway is a wander candidate only for its own resident
    /// dog. Every other dog keeps excluding all front walkways exactly as the
    /// single-line filter did before, so dogs never aimlessly detour onto a
    /// neighbor's lot.
    /// </summary>
    public class ResidentWalkwayWanderTests
    {
        // Enough seeds that a uniform first-hop pick from a handful of edges is
        // overwhelmingly likely to include the front walkway at least once.
        private const int SeedSweep = 100;

        private static (WalkNetwork network, WalkEdge walkway, int houseId) BuiltZoneHouse()
        {
            var state = Doggiehood.Core.Tests.World.FrontierTestWorld.WithFirstTileUnlocked(10_000);
            var lot = state.LotsForUnlockedTile(Doggiehood.Core.Tests.World.FrontierTestWorld.FirstTile)[0];
            state.TryBuildHouse(lot.HouseId);
            state.WalkNetwork.TryGetFrontWalkway(lot.HouseId, out var walkway);
            return (state.WalkNetwork, walkway, lot.HouseId);
        }

        [Test]
        public void ResidentDog_CanStepOntoItsOwnFrontWalkway()
        {
            // #430 item 3: standing at its own house's sidewalk-attach node,
            // the resident dog can pick the front-walkway edge onto the door.
            var (network, walkway, houseId) = BuiltZoneHouse();

            var steppedOntoWalkway = false;
            for (var seed = 0; seed < SeedSweep; seed++)
            {
                var wander = new WanderBehavior(seed, MovementProfile.Base, network, residentHouseId: houseId);
                if (wander.NextTarget(walkway.B).Equals(walkway.A))
                {
                    steppedOntoWalkway = true;
                    break;
                }
            }

            Assert.That(steppedOntoWalkway, Is.True,
                "the resident dog never once picked its own front walkway across the seed sweep");
        }

        [Test]
        public void NonResidentDog_NeverStepsOntoAnotherHousesFrontWalkway()
        {
            // #430 item 4: a dog resident of a DIFFERENT house, standing at the
            // zone house's attach node, must never receive that house's front
            // walkway as a candidate — even though it is a real graph edge from
            // that node.
            var (network, walkway, houseId) = BuiltZoneHouse();
            var otherHouseId = houseId + 1;

            for (var seed = 0; seed < SeedSweep; seed++)
            {
                var wander = new WanderBehavior(seed, MovementProfile.Base, network, residentHouseId: otherHouseId);
                Assert.That(wander.NextTarget(walkway.B), Is.Not.EqualTo(walkway.A),
                    $"seed {seed}: a non-resident stepped onto another house's front walkway");
            }
        }

        [Test]
        public void ResidentDog_AtItsOwnDoor_ReturnsDownTheWalkway_NotAcrossTheYard()
        {
            // #517: standing at its OWN front-door node (the walkway's lot-side
            // endpoint, whose only edge is the walkway back to the sidewalk), a
            // resident dog must return DOWN that walkway to the attach point —
            // not beeline to the nearest sidewalk node across the yard.
            var (network, walkway, houseId) = BuiltZoneHouse();

            // Precondition (the #517 trigger): the door node is a
            // front-walkway-only node, so the general walkable set deliberately
            // excludes it — NearestWalkableNode(door) can never be the door, and
            // instead snaps to a straight-line-nearest sidewalk/crosswalk node.
            // The fix must therefore NOT rely on NearestWalkableNode here.
            Assert.That(network.NearestWalkableNode(walkway.A), Is.Not.EqualTo(walkway.A),
                "sanity: the door node is excluded from the walkable set (the #517 trigger)");

            for (var seed = 0; seed < SeedSweep; seed++)
            {
                var wander = new WanderBehavior(seed, MovementProfile.Base, network, residentHouseId: houseId);
                Assert.That(wander.NextTarget(walkway.A), Is.EqualTo(walkway.B),
                    $"seed {seed}: a resident dog at its own door must walk back down the walkway "
                    + "to the sidewalk attach point (B), not across the yard");
            }
        }

        [Test]
        public void ResidentDog_OneHopPastItsDoor_ContinuesOnNetworkFromTheAttachPoint()
        {
            // #517 regression: after returning down the walkway to the attach
            // point, ordinary wander resumes — the very next hop is a real
            // network neighbor of the attach node, never an off-network beeline.
            var (network, walkway, houseId) = BuiltZoneHouse();
            var attachNeighbors = network.EdgesFrom(walkway.B)
                .Select(e => e.Other(walkway.B))
                .ToHashSet();

            for (var seed = 0; seed < SeedSweep; seed++)
            {
                var wander = new WanderBehavior(seed, MovementProfile.Base, network, residentHouseId: houseId);
                Assert.That(wander.NextTarget(walkway.A), Is.EqualTo(walkway.B),
                    $"seed {seed}: precondition — the dog first walks door -> attach");

                var next = wander.NextTarget(walkway.B);
                Assert.That(attachNeighbors, Does.Contain(next),
                    $"seed {seed}: from the attach point the dog must continue to a real network "
                    + "neighbor, staying on the sidewalk/crosswalk/walkway graph");
            }
        }

        [Test]
        public void DogWithNoResidentHouse_NeverStepsOntoAnyFrontWalkway()
        {
            // #430 item 4 (default): the sentinel "no resident house" preserves
            // the pre-#430 behavior — every front walkway stays excluded.
            var (network, walkway, _) = BuiltZoneHouse();

            for (var seed = 0; seed < SeedSweep; seed++)
            {
                var wander = new WanderBehavior(seed, MovementProfile.Base, network);
                Assert.That(wander.NextTarget(walkway.B), Is.Not.EqualTo(walkway.A),
                    $"seed {seed}: a dog with no resident house stepped onto a front walkway");
            }
        }
    }
}
