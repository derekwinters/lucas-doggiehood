using Doggiehood.Core.Tuning;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Tuning
{
    /// <summary>
    /// #620: the central, runtime-overridable home for Core balance values.
    /// A default-constructed <see cref="TuningConfig"/> must reproduce every
    /// shipping constant EXACTLY — this locks "no behavior change" for the
    /// plumbing-only refactor (default values move to #623–#626).
    /// </summary>
    public class TuningConfigTests
    {
        [Test]
        public void Default_ReproducesEconomyConstants()
        {
            var t = new TuningConfig();

            Assert.That(t.QuestPayout, Is.EqualTo(20));
            Assert.That(t.RefreshIntervalHours, Is.EqualTo(1));
            Assert.That(t.PacingWindowHours, Is.EqualTo(4));
            Assert.That(t.TargetActiveDivisor, Is.EqualTo(3));
            Assert.That(t.TargetActiveFloor, Is.EqualTo(5));
            Assert.That(t.TargetActiveCeiling, Is.EqualTo(12));
        }

        [Test]
        public void Default_ReproducesTileUnlockConstants()
        {
            var t = new TuningConfig();

            Assert.That(t.TileUnlockBaseCost, Is.EqualTo(50));
            Assert.That(t.TileUnlockPerExistingTileStep, Is.EqualTo(10));
            Assert.That(t.TileUnlockOriginTileCount, Is.EqualTo(1));
        }

        [Test]
        public void Default_ReproducesHouseBuildConstants()
        {
            var t = new TuningConfig();

            Assert.That(t.HouseBuildBaseCost, Is.EqualTo(50));
            Assert.That(t.HouseBuildPerBatchStep, Is.EqualTo(5));
            Assert.That(t.HouseBuildHousesPerStep, Is.EqualTo(4));
        }

        [Test]
        public void Default_ReproducesHouseUpgradeConstants()
        {
            var t = new TuningConfig();

            Assert.That(t.HouseMaxLevel, Is.EqualTo(4));
            Assert.That(t.HouseUpgradeCostToLevel2, Is.EqualTo(100));
            Assert.That(t.HouseUpgradeCostToLevel3, Is.EqualTo(200));
            Assert.That(t.HouseUpgradeCostToLevel4, Is.EqualTo(400));
        }

        [Test]
        public void Default_ReproducesMoveInConstants()
        {
            var t = new TuningConfig();

            Assert.That(t.BaseMoveInChance, Is.EqualTo(0.05));
            Assert.That(t.MoveInChanceIncrementPerQuest, Is.EqualTo(0.05));
            Assert.That(t.MoveInSingleWeight, Is.EqualTo(70));
            Assert.That(t.MoveInParentAndPuppyWeight, Is.EqualTo(25));
            Assert.That(t.MoveInThreeDogWeight, Is.EqualTo(5));
            Assert.That(t.EasterEggChance, Is.EqualTo(0.05));
            Assert.That(t.BreedWeightSmoothing, Is.EqualTo(1.0));
        }

        [Test]
        public void Default_ReproducesQuestCostTierConstants()
        {
            var t = new TuningConfig();

            Assert.That(t.StarterMinCost, Is.EqualTo(30));
            Assert.That(t.StarterMaxCost, Is.EqualTo(50));
            Assert.That(t.MidMinCost, Is.EqualTo(60));
            Assert.That(t.MidMaxCost, Is.EqualTo(90));
            Assert.That(t.PremiumMinCost, Is.EqualTo(100));
            Assert.That(t.StarterPopulationGate, Is.EqualTo(1));
            Assert.That(t.MidPopulationGate, Is.EqualTo(5));
            Assert.That(t.PremiumPopulationGate, Is.EqualTo(10));
        }

        [Test]
        public void Default_ReproducesOnboardingConstant()
        {
            var t = new TuningConfig();

            Assert.That(t.OnboardingRewardPerStep, Is.EqualTo(100));
        }

        [Test]
        public void ResetToDefaults_RestoresAFreshDefaultConfig()
        {
            // A fresh TuningConfig == shipping defaults; after mutating fields,
            // ResetToDefaults must return the Active config bit-identical to a
            // brand-new one — the debug menu's "reset" hook (#620).
            var original = TuningConfig.Active;
            try
            {
                TuningConfig.Active.QuestPayout = 999;
                TuningConfig.Active.TileUnlockBaseCost = 777;
                TuningConfig.Active.BaseMoveInChance = 0.99;

                TuningConfig.ResetToDefaults();

                var fresh = new TuningConfig();
                Assert.That(TuningConfig.Active.QuestPayout, Is.EqualTo(fresh.QuestPayout));
                Assert.That(TuningConfig.Active.TileUnlockBaseCost, Is.EqualTo(fresh.TileUnlockBaseCost));
                Assert.That(TuningConfig.Active.BaseMoveInChance, Is.EqualTo(fresh.BaseMoveInChance));
                Assert.That(TuningConfig.Active.QuestPayout, Is.EqualTo(20));
            }
            finally
            {
                TuningConfig.Active = original;
                TuningConfig.ResetToDefaults();
            }
        }
    }
}
