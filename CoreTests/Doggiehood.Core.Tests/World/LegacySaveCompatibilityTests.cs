using System;
using System.Globalization;
using System.Linq;
using Doggiehood.Core.Onboarding;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #704: Derek and Lucas have saves on disk written by the shipped build,
    /// and the new lines (dog=, quest=, questTimerUtc=) are purely additive —
    /// there is no schema version bump and no rewrite step, because an unknown
    /// key was already skipped and every new field has a defined absent-value
    /// behavior. This pins that: a literal pre-#704 save loads with all of its
    /// progress intact.
    /// </summary>
    public class LegacySaveCompatibilityTests
    {
        private static readonly DateTime LastRotation =
            new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>A save exactly as the pre-#704 codec wrote one: every key it
        /// emitted, and none of the ones this change adds.</summary>
        private static string LegacySave()
        {
            return string.Join("\n", new[]
            {
                "version=1",
                "coins=250",
                "onboarded=1",
                "tile=0|1|CulDeSacSouth",
                "lotvariant=" + FrontierTestWorld.SecondLotId.ToString(CultureInfo.InvariantCulture) + "|3|7",
                "house=" + FrontierTestWorld.FirstLotId.ToString(CultureInfo.InvariantCulture) + "|2|0|1|4",
                "rewardChain=Done",
                "upgradeTarget=1",
                "moveIn=3|Ziggy|FrenchBulldog",
                "rotatedUtc=" + LastRotation.ToString("O", CultureInfo.InvariantCulture),
                "questPacingAcc=0.25",
                "placed=1|toy",
                "deco=2|pool|3.5|-2.25",
                string.Empty,
            });
        }

        [Test]
        public void ALegacySave_LoadsWithoutThrowing()
        {
            Assert.DoesNotThrow(() => SaveCodec.Load(LegacySave()));
        }

        [Test]
        public void ALegacySave_KeepsEveryPieceOfProgressItAlreadyHad()
        {
            var state = SaveCodec.Load(LegacySave());

            Assert.That(state.Wallet.Coins, Is.EqualTo(250), "coins");
            Assert.That(state.OnboardingComplete, Is.True, "onboarding");
            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.Done), "reward chain");
            Assert.That(state.UnlockedTiles, Does.Contain(FrontierTestWorld.FirstTile), "unlocked tile");
            Assert.That(state.OnboardingUpgradeTargetHouseId, Is.EqualTo(1), "onboarding upgrade target");
            Assert.That(state.MoveInQuestsSinceLastMoveIn, Is.EqualTo(3), "move-in pity counter");
            Assert.That(state.MoveInRemainingEasterEggNames, Does.Contain("Ziggy"), "easter-egg reserve");
            Assert.That(state.LastRotationUtc, Is.EqualTo(LastRotation), "rotation stamp");
            Assert.That(state.QuestPacingAccumulator, Is.EqualTo(0.25).Within(1e-9), "pacing accumulator");
            Assert.That(state.PlacedItems.Count, Is.EqualTo(1), "placed item");
            Assert.That(state.Decorations.Count, Is.EqualTo(1), "decoration");

            var house = state.Houses.FirstOrDefault(h => h.Id == FrontierTestWorld.FirstLotId);
            Assert.That(house, Is.Not.Null, "the built house");
            Assert.That(house.Level, Is.EqualTo(2), "its upgrade level");
            Assert.That(house.Variant.Value.LadderId, Is.EqualTo(1), "its rolled ladder");
            Assert.That(house.Variant.Value.TintIndex, Is.EqualTo(4), "its rolled tint");
        }

        [Test]
        public void ALegacySave_LoadsWithNoQuestsAndTheStartingRoster()
        {
            // Nothing to migrate here — the old format simply held neither, and
            // the absent lines mean "none", not "corrupt".
            var state = SaveCodec.Load(LegacySave());

            Assert.That(state.Quests.ActiveQuests, Is.Empty);
            Assert.That(state.Dogs.Count, Is.EqualTo(GameState.CreateNew().Dogs.Count));
        }

        [Test]
        public void ALegacySave_ReVacatesTheHouseItRecordedAsOccupiedWithNoResidents()
        {
            // The one real migration: the legacy house= line says occupied, but
            // the household that filled it was never persisted. Re-vacating puts
            // it back in the move-in pool instead of leaving it permanently dead.
            var state = SaveCodec.Load(LegacySave());

            var house = state.Houses.First(h => h.Id == FrontierTestWorld.FirstLotId);
            Assert.That(house.IsVacant, Is.True);
        }

        [Test]
        public void ALegacySave_ReSavesInTheNewFormat_AndRoundTrips()
        {
            var reSaved = SaveCodec.Save(SaveCodec.Load(LegacySave()));

            var again = SaveCodec.Load(reSaved);
            Assert.That(again.Wallet.Coins, Is.EqualTo(250), "an upgrade-then-save keeps the balance");
            Assert.That(again.UnlockedTiles, Does.Contain(FrontierTestWorld.FirstTile), "and the map");
            Assert.That(again.PlacedItems.Count, Is.EqualTo(1), "and the placed items");
        }

        [Test]
        public void AnUnknownFutureKey_IsSkipped_RatherThanFailingTheLoad()
        {
            // Forward compatibility is the same mechanism: the loader only acts
            // on keys it knows, so a save written by a newer build still opens.
            var saved = SaveCodec.Save(GameState.CreateNew()) + "somethingNew=42|whatever\n";

            Assert.That(SaveCodec.Load(saved).Dogs.Count, Is.EqualTo(GameState.CreateNew().Dogs.Count));
        }
    }
}
