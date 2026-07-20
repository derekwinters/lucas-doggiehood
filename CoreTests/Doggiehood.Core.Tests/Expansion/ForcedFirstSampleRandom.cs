using System;

namespace Doggiehood.Core.Tests.Expansion
{
    /// <summary>
    /// Deterministic Random test double for #54: forces exactly the first
    /// Sample() draw (the move-in system's pity-counter roll) to a chosen
    /// value, e.g. 0.0 to guarantee a success, while every later draw
    /// (composition, breed, name, personality) delegates to a real seeded
    /// Random so those stay genuinely, variably random across trials.
    /// </summary>
    internal sealed class ForcedFirstSampleRandom : Random
    {
        private readonly double firstSample;
        private readonly Random inner;
        private bool consumedFirst;

        public ForcedFirstSampleRandom(double firstSample, Random inner)
        {
            this.firstSample = firstSample;
            this.inner = inner;
        }

        protected override double Sample()
        {
            if (!consumedFirst)
            {
                consumedFirst = true;
                return firstSample;
            }

            return inner.NextDouble();
        }
    }
}
