using System;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Quests
{
    /// <summary>
    /// #521/#535: the red "finder glow" on a lost quest item. Derek's revised
    /// design (#535) is a flat red GROUND RING only — the item keeps its own
    /// mesh, size and colour, with just a ring on the surface beneath it. These
    /// cover the Unity-independent pieces: the "should the glow show?" predicate
    /// over quest state and the ground-ring tuning, so the tuning lives in Core
    /// with plain NUnit and the Unity view is a thin apply-seam. The earlier
    /// engulfing halo, the size pulse and the orbiting sparkle (which read as
    /// the item itself ballooning and turning red) are gone.
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
        public void GroundRingTuningConstants_ArePositive()
        {
            // #161/#535: the glow is now a single flat ground ring — its
            // dimensions are named, sane constants.
            Assert.That(LostItemGlow.GroundRingScale, Is.GreaterThan(0f));
            Assert.That(LostItemGlow.GroundRingHeight, Is.GreaterThan(0f));
            Assert.That(LostItemGlow.GroundRingThickness, Is.GreaterThan(0f));
        }

        [Test]
        public void GroundRingInnerScale_IsPositive_AndOpensAHoleInsideTheOuterEdge()
        {
            // #602: the highlight is a hollow RING, not a filled disc — the hole
            // is a named constant (#161), the inner diameter of the annulus.
            // Positive so there is a real ring band, and strictly less than the
            // outer GroundRingScale so the ring is genuinely hollow.
            Assert.That(LostItemGlow.GroundRingInnerScale, Is.GreaterThan(0f),
                "the ring has a real, positive inner radius (a hole, not a point)");
            Assert.That(LostItemGlow.GroundRingInnerScale, Is.LessThan(LostItemGlow.GroundRingScale),
                "the hole is inside the outer edge, so the shape is an annulus, not a disc");
        }

        // ---- #535: no more halo / size pulse / sparkle -------------------
        // The revised design preserves the item's own mesh, size and colour,
        // so Core no longer exposes any size-pulse or halo/sparkle tuning. If
        // any of these come back, the item can balloon/recolour again — pin
        // their absence.

        [Test]
        public void NoSizePulseApi_SoTheItemNeverScales()
        {
            Assert.That(typeof(LostItemGlow).GetMethod("PulseScaleAt"), Is.Null,
                "the item must not pulse in size any more (#535)");
            Assert.That(typeof(LostItemGlow).GetField("PulseScaleMin"), Is.Null);
            Assert.That(typeof(LostItemGlow).GetField("PulseScaleMax"), Is.Null);
            Assert.That(typeof(LostItemGlow).GetField("PulsePeriodSeconds"), Is.Null);
        }

        [Test]
        public void NoHaloTuning_SoNothingEngulfsTheItem()
        {
            Assert.That(typeof(LostItemGlow).GetField("HaloScale"), Is.Null,
                "the engulfing red halo is dropped (#535)");
            Assert.That(typeof(LostItemGlow).GetField("HaloHeight"), Is.Null);
        }

        [Test]
        public void NoSparkleTuning_SoNothingOrbitsTheItem()
        {
            Assert.That(typeof(LostItemGlow).GetMethod("SparkleAngleAt"), Is.Null,
                "the orbiting sparkle is dropped (#535)");
            Assert.That(typeof(LostItemGlow).GetField("SparkleScale"), Is.Null);
            Assert.That(typeof(LostItemGlow).GetField("SparkleOrbitRadius"), Is.Null);
            Assert.That(typeof(LostItemGlow).GetField("SparkleHeight"), Is.Null);
            Assert.That(typeof(LostItemGlow).GetField("SparkleOrbitDegreesPerSecond"), Is.Null);
        }
    }
}
