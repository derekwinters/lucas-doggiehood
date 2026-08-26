using System;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Quests;
using Doggiehood.Core.Tuning;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Quests
{
    /// <summary>
    /// #743: the refresh interval is expressed in <b>minutes</b>, and the
    /// trickle rate is a <b>per-refresh</b> amount that actually contains the
    /// interval — <c>target × RefreshIntervalMinutes / (PacingWindowHours × 60)</c>.
    ///
    /// <para>The old <c>PerHourRate</c> returned <c>target / PacingWindowHours</c>
    /// and was added once per refresh boundary: correct only by coincidence at a
    /// 1h interval in a 4h window, where the two readings agree. Moving the
    /// interval off one hour makes that missing factor live, so the fix is a
    /// prerequisite of the 15-minute cadence, not a tidy-up.</para>
    /// </summary>
    public class QuestPacingRateTests
    {
        [TearDown]
        public void RestoreDefaults()
        {
            TuningConfig.ResetToDefaults();
        }

        private static GameState StateWithDogs(int dogCount)
        {
            var state = GameState.CreateNew();
            for (var i = state.Dogs.Count; i < dogCount; i++)
            {
                state.AddDog(new Dog($"extra-{i}", Breed.GermanShepherd, Personality.Brave, 1, false));
            }

            return state;
        }

        [Test]
        public void PerRefreshRate_ContainsTheRefreshInterval_NotJustTheWindow()
        {
            // The defect the 15-minute move exposes: a two-hour interval in a
            // four-hour window must pay HALF the target per refresh, not a
            // quarter of it. The old formula ignored the interval entirely.
            const int twoHours = 120;
            TuningConfig.Active.RefreshIntervalMinutes = twoHours;

            var policy = new QuestPacingPolicy();
            var state = StateWithDogs(8); // target 5 (floor)

            Assert.That(policy.PerRefreshRate(state), Is.EqualTo(2.5).Within(1e-9),
                "target 5 × 120min / (4h × 60) = 2.5 per refresh");
        }

        [Test]
        public void PerRefreshRate_AtTheOldHourlyInterval_ReproducesTheOldPerHourFormula()
        {
            // The behavior-preserving pin: at the pre-#743 1h/4h pair the new
            // formula is numerically identical to target / PacingWindowHours,
            // which is why the shipping build was correct by coincidence.
            const int oneHour = 60;
            TuningConfig.Active.RefreshIntervalMinutes = oneHour;

            var policy = new QuestPacingPolicy();

            foreach (var dogCount in new[] { 8, 12, 18, 100 })
            {
                var state = StateWithDogs(dogCount);
                Assert.That(policy.PerRefreshRate(state),
                    Is.EqualTo(policy.TargetActiveCount(state) / (double)EconomyNumbers.PacingWindowHours).Within(1e-9),
                    $"{dogCount} dogs: unchanged at the old hourly interval");
            }
        }

        [Test]
        public void RefreshInterval_IsBuiltFromMinutes()
        {
            const int quarterHour = 15;
            TuningConfig.Active.RefreshIntervalMinutes = quarterHour;

            Assert.That(EconomyNumbers.RefreshIntervalMinutes, Is.EqualTo(quarterHour));
            Assert.That(EconomyNumbers.RefreshInterval, Is.EqualTo(TimeSpan.FromMinutes(quarterHour)));
        }

        [Test]
        public void RefreshesPerPacingWindow_IsTheWindowDividedByTheInterval()
        {
            const int quarterHour = 15;
            TuningConfig.Active.RefreshIntervalMinutes = quarterHour;

            Assert.That(EconomyNumbers.RefreshesPerPacingWindow, Is.EqualTo(16),
                "16 quarter-hours in the 4h window");

            const int oneHour = 60;
            TuningConfig.Active.RefreshIntervalMinutes = oneHour;
            Assert.That(EconomyNumbers.RefreshesPerPacingWindow, Is.EqualTo(4),
                "and 4 hourly refreshes in the same window");
        }

        [Test]
        public void ZeroOrNegativeRefreshInterval_IsClampedAtTheConfigEdge()
        {
            // Guard 2: RefreshIntervalMinutes sits in a divisor and in a
            // TimeSpan. A 0 divides by zero (or makes ElapsedIntervals
            // infinite); a negative inverts the rate. Clamp where the config is
            // read, so no downstream seam has to defend itself.
            var policy = new QuestPacingPolicy();
            var state = StateWithDogs(8);

            foreach (var degenerate in new[] { 0, -15 })
            {
                TuningConfig.Active.RefreshIntervalMinutes = degenerate;

                Assert.That(EconomyNumbers.RefreshIntervalMinutes, Is.GreaterThan(0),
                    $"{degenerate} min clamps to a positive interval");
                Assert.That(EconomyNumbers.RefreshInterval, Is.GreaterThan(TimeSpan.Zero));
                Assert.That(policy.PerRefreshRate(state), Is.GreaterThan(0d));
                Assert.That(double.IsNaN(policy.PerRefreshRate(state)), Is.False);
                Assert.That(double.IsInfinity(policy.PerRefreshRate(state)), Is.False);
            }
        }

        [Test]
        public void ZeroOrNegativePacingWindow_IsClampedAtTheConfigEdge()
        {
            var policy = new QuestPacingPolicy();
            var state = StateWithDogs(8);

            foreach (var degenerate in new[] { 0, -4 })
            {
                TuningConfig.Active.PacingWindowHours = degenerate;

                Assert.That(EconomyNumbers.PacingWindowHours, Is.GreaterThan(0),
                    $"{degenerate}h clamps to a positive window");
                Assert.That(policy.PerRefreshRate(state), Is.GreaterThan(0d));
                Assert.That(double.IsNaN(policy.PerRefreshRate(state)), Is.False);
                Assert.That(double.IsInfinity(policy.PerRefreshRate(state)), Is.False);
            }
        }

        [Test]
        public void TheDebugSlider_BindsTheRefreshIntervalInMinutes_WithAMinimumAboveZero()
        {
            // Guard 2's other half: the Pacing group's "Refresh interval" row
            // moves to minutes, and its own slider minimum cannot reach the
            // degenerate zero. "Pacing window" stays in hours.
            var interval = FindRow(nameof(TuningConfig.RefreshIntervalMinutes));

            Assert.That(interval.Label, Is.EqualTo("Refresh interval"));
            Assert.That(interval.Group, Is.EqualTo(TuningGroup.Pacing));
            Assert.That(interval.Unit, Is.EqualTo("min"));
            Assert.That(interval.Min, Is.GreaterThan(0d), "the slider can never select a zero interval");
            Assert.That(interval.Max, Is.GreaterThanOrEqualTo((double)new TuningConfig().RefreshIntervalMinutes));

            var window = FindRow(nameof(TuningConfig.PacingWindowHours));
            Assert.That(window.Unit, Is.EqualTo("h"), "the pacing window stays in hours");
            Assert.That(window.Min, Is.GreaterThan(0d), "and it cannot reach zero either");
        }

        private static TuningField FindRow(string fieldName)
        {
            foreach (var field in TuningCatalog.Fields)
            {
                if (field.FieldName == fieldName)
                {
                    return field;
                }
            }

            Assert.Fail("no tuning row bound to " + fieldName);
            return null;
        }
    }
}
