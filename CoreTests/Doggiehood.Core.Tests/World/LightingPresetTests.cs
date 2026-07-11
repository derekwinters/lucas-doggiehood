using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    public class LightingPresetTests
    {
        [Test]
        public void Daytime_DefinesTheSingleFixedLightingSetup()
        {
            // #39: always sunny mid-day. The EditMode suite asserts the scene
            // light matches these same constants.
            Assert.That(LightingPreset.SunPitchDegrees, Is.EqualTo(50f));
            Assert.That(LightingPreset.SunYawDegrees, Is.EqualTo(30f));
            Assert.That(LightingPreset.SunIntensity, Is.EqualTo(1.1f));
            Assert.That(LightingPreset.SunColorHex, Is.EqualTo("#FFF4E5"));
            Assert.That(LightingPreset.AmbientColorHex, Is.EqualTo("#C7DDF5"));
        }

        [Test]
        public void CoreContainsNoTimeOfDayOrWeatherTypes()
        {
            // Guard (#39): day/night + weather are explicitly future ideas
            // (#87), not MVP. Fails the moment such state sneaks into Core.
            // (Invariant guard — no red phase.)
            var forbidden = new[] { "timeofday", "daynight", "weather" };

            var offenders = typeof(LightingPreset).Assembly.GetTypes()
                .Where(t => t.Namespace != null
                    && t.Namespace.StartsWith("Doggiehood.Core")
                    && !t.Namespace.Contains("Tests"))
                .Where(t => forbidden.Any(f => t.Name.ToLowerInvariant().Contains(f)))
                .Select(t => t.FullName)
                .ToList();

            Assert.That(offenders, Is.Empty,
                "Core must not model time-of-day/weather for MVP (#39): " + string.Join(", ", offenders));
        }
    }
}
