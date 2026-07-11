using Doggiehood.Core.Dogs;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Dogs
{
    public class MovementTests
    {
        [Test]
        public void ExcitedDogs_MoveFasterThanBase()
        {
            // #89: Excited = fast.
            Assert.That(MovementProfile.ForPersonality(Personality.Excited).Speed,
                Is.GreaterThan(MovementProfile.Base.Speed));
        }

        [Test]
        public void ExcitedDogs_TurnLessOften()
        {
            // #89: Excited = long straight stretches.
            Assert.That(MovementProfile.ForPersonality(Personality.Excited).TurnProbability,
                Is.LessThan(MovementProfile.Base.TurnProbability));
        }

        [Test]
        public void GrumpyDogs_UseBaseParametersForMvp()
        {
            // #89: Grumpy's distinct shuffle is explicitly deferred out of
            // MVP — parity with base is intentional, not an oversight.
            var grumpy = MovementProfile.ForPersonality(Personality.Grumpy);

            Assert.That(grumpy.Speed, Is.EqualTo(MovementProfile.Base.Speed));
            Assert.That(grumpy.TurnProbability, Is.EqualTo(MovementProfile.Base.TurnProbability));
        }

        [Test]
        public void ParametersComeFromThePersonality_ForEveryPersonality()
        {
            // #89: lookup by Personality never throws for any defined value.
            foreach (Personality personality in System.Enum.GetValues(typeof(Personality)))
            {
                Assert.That(MovementProfile.ForPersonality(personality).Speed, Is.GreaterThan(0f));
            }
        }

        [Test]
        public void WanderTargets_AlwaysStayOnTheStreets()
        {
            // #8: never inside a house lot, never off-map.
            var wander = new WanderBehavior(seed: 1234, MovementProfile.Base);
            var position = NeighborhoodLayout.Intersection;

            for (var step = 0; step < 500; step++)
            {
                position = wander.NextTarget(position);

                var halfWidth = NeighborhoodLayout.StreetWidth / 2f;
                var onNorthSouthStreet = System.Math.Abs(position.X) <= halfWidth;
                var onEastWestStreet = System.Math.Abs(position.Z) <= halfWidth;

                Assert.That(onNorthSouthStreet || onEastWestStreet, Is.True,
                    $"step {step}: {position} is off-street");
                Assert.That(System.Math.Abs(position.X), Is.LessThanOrEqualTo(WanderBehavior.StreetExtent));
                Assert.That(System.Math.Abs(position.Z), Is.LessThanOrEqualTo(WanderBehavior.StreetExtent));
            }
        }

        [Test]
        public void Wander_IsDeterministicForASeed()
        {
            var a = new WanderBehavior(seed: 7, MovementProfile.Base);
            var b = new WanderBehavior(seed: 7, MovementProfile.Base);
            var positionA = NeighborhoodLayout.Intersection;
            var positionB = NeighborhoodLayout.Intersection;

            for (var step = 0; step < 50; step++)
            {
                positionA = a.NextTarget(positionA);
                positionB = b.NextTarget(positionB);
                Assert.That(positionA, Is.EqualTo(positionB));
            }
        }

        [Test]
        public void ExcitedProfile_ProducesMeasurablyFewerTurns()
        {
            Assert.That(CountTurns(MovementProfile.ForPersonality(Personality.Excited)),
                Is.LessThan(CountTurns(MovementProfile.Base)));
        }

        private static int CountTurns(MovementProfile profile)
        {
            var wander = new WanderBehavior(seed: 99, profile);
            var position = NeighborhoodLayout.Intersection;
            var turns = 0;
            var lastDirection = default(GridPoint);
            var first = true;

            for (var step = 0; step < 400; step++)
            {
                var next = wander.NextTarget(position);
                var direction = new GridPoint(
                    System.Math.Sign(next.X - position.X),
                    System.Math.Sign(next.Z - position.Z));

                if (!first && !direction.Equals(lastDirection))
                {
                    turns++;
                }

                first = false;
                lastDirection = direction;
                position = next;
            }

            return turns;
        }

        [Test]
        public void TapResolution_HitsAWanderingDogAtItsCurrentPosition()
        {
            // #8: the interaction system can resolve a tap against wherever
            // the dog currently is along its path.
            var wander = new WanderBehavior(seed: 5, MovementProfile.Base);
            var dog = new Dog("Tappy", Breed.Beagle, Personality.Brave, 1, false);
            var position = NeighborhoodLayout.Intersection;

            for (var step = 0; step < 20; step++)
            {
                position = wander.NextTarget(position);

                Assert.That(TapResolver.IsHit(position, tap: position, radius: 1.5f), Is.True);
                Assert.That(TapResolver.IsHit(position, new GridPoint(position.X + 5f, position.Z), 1.5f), Is.False);
            }
        }
    }
}
