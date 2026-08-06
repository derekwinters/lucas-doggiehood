using System.Collections.Generic;
using Doggiehood.Core.Art;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Art
{
    /// <summary>
    /// #601: a delivery truck picks one of the curated standard car colors
    /// (<see cref="Palette.CarColorHex"/>) at spawn. Unlike a house's persisted
    /// per-id tint (<see cref="HouseVariantAssignment"/>), a truck is transient
    /// and unsaved, so the pick is a pure function of a spawn seed the Unity
    /// layer supplies — deterministic (hence testable) but keyed on nothing
    /// stable — with an optional "distinct from the colors currently on the
    /// road" rule so two concurrent trucks don't share a color.
    /// </summary>
    public class CarColorAssignmentTests
    {
        [Test]
        public void IndexFor_IsDeterministic_SameSeedSameIndexEveryCall()
        {
            for (var seed = 0; seed < 200; seed++)
            {
                Assert.That(CarColorAssignment.IndexFor(seed), Is.EqualTo(CarColorAssignment.IndexFor(seed)),
                    $"seed {seed} yields a stable color index");
            }
        }

        [Test]
        public void IndexFor_StaysInRange_ForEverySeed()
        {
            for (var seed = 0; seed < 500; seed++)
            {
                Assert.That(CarColorAssignment.IndexFor(seed),
                    Is.InRange(0, CarColorAssignment.CarColorCount - 1),
                    $"seed {seed} color index in 0..{CarColorAssignment.CarColorCount - 1}");
            }
        }

        [Test]
        public void IndexFor_SpreadsAcrossEveryCarColor()
        {
            var indices = new HashSet<int>();
            for (var seed = 0; seed < 500; seed++)
            {
                indices.Add(CarColorAssignment.IndexFor(seed));
            }

            Assert.That(indices.Count, Is.EqualTo(CarColorAssignment.CarColorCount),
                "consecutive spawn seeds use every one of the car colors");
        }

        [Test]
        public void IndexForActive_ReturnsTheSeededPick_WhenNoColorsAreActive()
        {
            for (var seed = 0; seed < 50; seed++)
            {
                Assert.That(CarColorAssignment.IndexFor(seed, new HashSet<int>()),
                    Is.EqualTo(CarColorAssignment.IndexFor(seed)),
                    $"seed {seed} with an empty active set matches the plain seeded pick");
            }
        }

        [Test]
        public void IndexForActive_ReturnsAColorNotInUse_WhenOneIsFree()
        {
            // Occupy every color EXCEPT the seeded pick's own value minus one, so
            // the seeded pick collides and the helper must step to a free color.
            for (var seed = 0; seed < 50; seed++)
            {
                var seeded = CarColorAssignment.IndexFor(seed);
                var active = new HashSet<int> { seeded };

                var chosen = CarColorAssignment.IndexFor(seed, active);

                Assert.That(active.Contains(chosen), Is.False,
                    $"seed {seed}: chose a color not already on the road");
                Assert.That(chosen, Is.InRange(0, CarColorAssignment.CarColorCount - 1));
            }
        }

        [Test]
        public void IndexForActive_ChoosesTheOneFreeColor_WhenAllButOneAreInUse()
        {
            var free = 3;
            var active = new HashSet<int>();
            for (var i = 0; i < CarColorAssignment.CarColorCount; i++)
            {
                if (i != free)
                {
                    active.Add(i);
                }
            }

            for (var seed = 0; seed < 50; seed++)
            {
                Assert.That(CarColorAssignment.IndexFor(seed, active), Is.EqualTo(free),
                    $"seed {seed}: with only color {free} free, it must be chosen");
            }
        }

        [Test]
        public void IndexForActive_FallsBackToTheSeededPick_WhenEveryColorIsInUse()
        {
            var active = new HashSet<int>();
            for (var i = 0; i < CarColorAssignment.CarColorCount; i++)
            {
                active.Add(i);
            }

            for (var seed = 0; seed < 50; seed++)
            {
                Assert.That(CarColorAssignment.IndexFor(seed, active),
                    Is.EqualTo(CarColorAssignment.IndexFor(seed)),
                    $"seed {seed}: all colors taken, fall back to the seeded pick");
            }
        }
    }
}
