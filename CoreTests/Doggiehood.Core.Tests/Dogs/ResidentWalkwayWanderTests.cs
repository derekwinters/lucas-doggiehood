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
