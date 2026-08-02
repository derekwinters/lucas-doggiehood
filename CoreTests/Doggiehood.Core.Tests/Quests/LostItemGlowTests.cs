using System;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Quests
{
    /// <summary>
    /// #521: the red "finder glow" on a lost quest item. These cover the
    /// Unity-independent pieces — the "should the glow show?" predicate over
    /// quest state and the deterministic pulse curve — so the tuning lives in
    /// Core with plain NUnit and the Unity view is a thin apply-seam.
    /// </summary>
    public class LostItemGlowTests
    {
        private static Quest LostItemQuest(GridPoint? hidden)
        {
            return new Quest(
                1, QuestType.LostItem, "Zeus", "ball",
                Array.Empty<string>(), hidden, null, null);
        }

        [Test]
        public void ShouldShow_TrueForALostItemQuestWithAPlacedItem()
        {
            var quest = LostItemQuest(new GridPoint(3f, -4f));

            Assert.That(LostItemGlow.ShouldShow(quest), Is.True,
                "the finder glow is active while a lost-item quest's hidden item is placed");
        }

        [Test]
        public void ShouldShow_FalseWhenNoItemIsPlaced()
        {
            var quest = LostItemQuest(null);

            Assert.That(LostItemGlow.ShouldShow(quest), Is.False,
                "no placed item means nothing to glow");
        }

        [Test]
        public void ShouldShow_FalseForANonLostItemQuest()
        {
            var quest = new Quest(
                2, QuestType.BuyGift, "Zeus", "bone",
                Array.Empty<string>(), null, 10, null);

            Assert.That(LostItemGlow.ShouldShow(quest), Is.False,
                "only lost-item quests carry the finder glow");
        }

        [Test]
        public void ShouldShow_FalseForANullQuest()
        {
            Assert.That(LostItemGlow.ShouldShow(null), Is.False);
        }

        [Test]
        public void PulseScale_StartsAtTheMinimum()
        {
            Assert.That(LostItemGlow.PulseScaleAt(0f),
                Is.EqualTo(LostItemGlow.PulseScaleMin).Within(0.0001f));
        }

        [Test]
        public void PulseScale_PeaksAtTheMaximumHalfwayThroughThePeriod()
        {
            Assert.That(LostItemGlow.PulseScaleAt(LostItemGlow.PulsePeriodSeconds / 2f),
                Is.EqualTo(LostItemGlow.PulseScaleMax).Within(0.0001f));
        }

        [Test]
        public void PulseScale_IsPeriodicOverThePulsePeriod()
        {
            const float sample = 0.37f;

            Assert.That(LostItemGlow.PulseScaleAt(sample),
                Is.EqualTo(LostItemGlow.PulseScaleAt(sample + LostItemGlow.PulsePeriodSeconds)).Within(0.0001f));
        }

        [Test]
        public void PulseScale_StaysWithinTheMinMaxBand()
        {
            for (var t = 0f; t <= LostItemGlow.PulsePeriodSeconds; t += 0.05f)
            {
                var scale = LostItemGlow.PulseScaleAt(t);
                Assert.That(scale, Is.GreaterThanOrEqualTo(LostItemGlow.PulseScaleMin - 0.0001f));
                Assert.That(scale, Is.LessThanOrEqualTo(LostItemGlow.PulseScaleMax + 0.0001f));
            }
        }

        [Test]
        public void SparkleAngle_AdvancesLinearlyWithTime()
        {
            Assert.That(LostItemGlow.SparkleAngleAt(0f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(LostItemGlow.SparkleAngleAt(1f),
                Is.EqualTo(LostItemGlow.SparkleOrbitDegreesPerSecond).Within(0.0001f));
        }

        [Test]
        public void TuningConstants_ArePositive()
        {
            // #161: every glow dimension/timing is a named, sane constant.
            Assert.That(LostItemGlow.HaloScale, Is.GreaterThan(0f));
            Assert.That(LostItemGlow.PulsePeriodSeconds, Is.GreaterThan(0f));
            Assert.That(LostItemGlow.PulseScaleMax, Is.GreaterThan(LostItemGlow.PulseScaleMin));
            Assert.That(LostItemGlow.GroundRingScale, Is.GreaterThan(0f));
            Assert.That(LostItemGlow.GroundRingHeight, Is.GreaterThan(0f));
            Assert.That(LostItemGlow.GroundRingThickness, Is.GreaterThan(0f));
            Assert.That(LostItemGlow.SparkleScale, Is.GreaterThan(0f));
            Assert.That(LostItemGlow.SparkleOrbitRadius, Is.GreaterThan(0f));
            Assert.That(LostItemGlow.SparkleOrbitDegreesPerSecond, Is.GreaterThan(0f));
        }
    }
}
