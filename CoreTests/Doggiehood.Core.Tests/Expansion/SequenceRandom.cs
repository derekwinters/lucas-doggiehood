using System;

namespace Doggiehood.Core.Tests.Expansion
{
    /// <summary>
    /// Deterministic Random test double for #54. .NET's Random honors an
    /// overridden Sample() as a back-compat shim — every NextDouble()/
    /// Next(n) call routes through it — so scripting Sample() lets tests
    /// pin exact outcomes instead of depending on statistical luck.
    /// Once the scripted values are exhausted, the last one repeats.
    /// </summary>
    internal sealed class SequenceRandom : Random
    {
        private readonly double[] samples;
        private int index;

        public SequenceRandom(params double[] samples)
        {
            if (samples.Length == 0)
            {
                throw new ArgumentException("Need at least one sample.", nameof(samples));
            }

            this.samples = samples;
        }

        protected override double Sample()
        {
            var value = samples[Math.Min(index, samples.Length - 1)];
            index++;
            return value;
        }
    }
}
