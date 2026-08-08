using System.Collections.Generic;
using Doggiehood.Core.Art;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.Onboarding;
using Doggiehood.Core.Tests.World;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Onboarding
{
    /// <summary>
    /// #571: the onboarding "fix up a home" (upgrade a house) reward-chain step
    /// marks the target house with the existing red ground-ring highlight (#535)
    /// so it's obvious which house to tap and upgrade. These cover the
    /// Unity-independent decision — "should the highlight show, and on which
    /// house?" — mirroring <see cref="LostItemGlow.ShouldShow"/>'s shape, so the
    /// Unity view stays a thin apply-seam. No new Core state: the predicate reads
    /// only <see cref="OnboardingRewardChain.CurrentStep"/> and the already-stored
    /// <see cref="GameState.OnboardingUpgradeTargetHouseId"/> (#469).
    ///
    /// <para>#668: the same decision now also covers the FINAL reward-chain step,
    /// "build a new house" — the one step that had no world-space "this one" cue
    /// at all. Its target is not a stored id but a derivation from live map state:
    /// the easternmost buildable empty lot (tie-broken north), which keeps the
    /// #469 no-new-persisted-state property.</para>
    /// </summary>
    public class OnboardingHouseHighlightTests
    {
        [Test]
        public void ShouldShow_TrueOnlyDuringTheUpgradeStepWithARecordedTarget()
        {
            Assert.That(OnboardingHouseHighlight.ShouldShow(OnboardingRewardStep.UpgradeHouse, 5), Is.True,
                "the highlight is active while the chain waits on UpgradeHouse and a target house is recorded");
        }

        [Test]
        public void ShouldShow_FalseDuringTheUpgradeStepWithNoRecordedTarget()
        {
            // A legacy save mid-chain on UpgradeHouse carries no target (#469):
            // with nothing to point at, no highlight shows.
            Assert.That(OnboardingHouseHighlight.ShouldShow(OnboardingRewardStep.UpgradeHouse, null), Is.False,
                "no recorded target house means nothing to highlight");
        }

        [Test]
        public void ShouldShow_TrueOnTheBuildStepWithAResolvedTarget()
        {
            // #668: the "build a new house" step gets the same red ring on the lot
            // it wants tapped — the one reward-chain step that had no world-space
            // cue, and the one whose flow opens a centered dialog over the coach bar.
            Assert.That(OnboardingHouseHighlight.ShouldShow(OnboardingRewardStep.BuildHouse, 9), Is.True,
                "the highlight is active while the chain waits on BuildHouse and a target lot resolves");
        }

        [Test]
        public void ShouldShow_FalseOnTheBuildStepWithNoResolvedTarget()
        {
            // No buildable empty lot on the map (nothing to point at) — no ring,
            // no throw.
            Assert.That(OnboardingHouseHighlight.ShouldShow(OnboardingRewardStep.BuildHouse, null), Is.False,
                "no resolved target lot means nothing to highlight");
        }

        [Test]
        public void ShouldShow_FalseForEveryOtherStep_IncludingOncePastTheBuildStep()
        {
            Assert.That(OnboardingHouseHighlight.ShouldShow(OnboardingRewardStep.FirstQuest, 5), Is.False,
                "before the upgrade step there is no highlight");
            Assert.That(OnboardingHouseHighlight.ShouldShow(OnboardingRewardStep.ExpandMap, 5), Is.False,
                "the highlight clears the instant the chain advances past UpgradeHouse");
            Assert.That(OnboardingHouseHighlight.ShouldShow(OnboardingRewardStep.Done, 5), Is.False,
                "a returning player past onboarding never gets the highlight");
        }

        [Test]
        public void TargetHouseId_IsExactlyTheStoredTarget_DuringTheUpgradeStep_NeverAnotherHouse()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            var target = state.Houses[0].Id;
            var other = state.Houses[1].Id;

            // Step 1 completes: records the target house and advances to UpgradeHouse.
            state.GrantOnboardingCompletionReward(target);
            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.UpgradeHouse));

            Assert.That(OnboardingHouseHighlight.TargetHouseId(state), Is.EqualTo(target),
                "the highlight targets exactly the recorded first-quest dog's house");
            Assert.That(OnboardingHouseHighlight.TargetHouseId(state), Is.Not.EqualTo(other),
                "never another house, even if another is upgrade-eligible for unrelated reasons");
        }

        [Test]
        public void TargetHouseId_IsNullBeforeTheUpgradeStep()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());

            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.FirstQuest));
            Assert.That(OnboardingHouseHighlight.TargetHouseId(state), Is.Null,
                "no highlight before the upgrade step is reached");
        }

        [Test]
        public void TargetHouseId_IsNullOncePastTheUpgradeStep_EvenThoughTheTargetIdIsStillStored()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            var target = state.Houses[0].Id;

            state.GrantOnboardingCompletionReward(target);
            state.TryUpgradeHouse(target); // advances past UpgradeHouse -> ExpandMap

            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.ExpandMap));
            Assert.That(state.OnboardingUpgradeTargetHouseId, Is.EqualTo(target),
                "the stored target id persists past the step");
            Assert.That(OnboardingHouseHighlight.TargetHouseId(state), Is.Null,
                "but the highlight clears the moment the chain advances past UpgradeHouse");
        }

        // ---------------------------------------------------------------
        // #668 — the "build a new house" step's target lot.
        // ---------------------------------------------------------------

        [TestCase(TileType.FourWay, Quadrant.NorthEast,
            TestName = "BuildTargetLot_FourWay_KeepsAllFourQuadrants_TakesTheNorthEast")]
        [TestCase(TileType.StraightNS, Quadrant.NorthEast,
            TestName = "BuildTargetLot_Straight_KeepsAllFourQuadrants_TakesTheNorthEast")]
        [TestCase(TileType.TurnNE, Quadrant.SouthEast,
            TestName = "BuildTargetLot_TurnNE_KeepsNorthWestAndSouthEast_TakesTheSouthEast")]
        [TestCase(TileType.CulDeSacEast, Quadrant.NorthEast,
            TestName = "BuildTargetLot_CulDeSacEast_BothKeptLotsAreEast_TieBreaksNorth")]
        public void BuildTargetLot_IsTheEasternmostLot_TieBrokenNorth(TileType type, Quadrant expected)
        {
            // Derek's rule (2026-08-07): the build step rings the EAST lot. Two
            // quadrants can be equally east on the same tile, so the rule is
            // completed as "easternmost, and on a tie take the northern one" —
            // NorthEast > SouthEast > NorthWest > SouthWest.
            var coordinate = new TileCoordinate(0, 1);
            var lots = LotsOf(type, coordinate);

            var target = OnboardingHouseHighlight.BuildTargetLot(lots);

            Assert.That(target, Is.Not.Null);
            Assert.That(target.Quadrant, Is.EqualTo(expected));
            Assert.That(target.HouseId, Is.EqualTo(FrontierHouseId.For(coordinate, expected)));
        }

        [Test]
        public void BuildTargetLot_ComparesAcrossTheWholeMap_NotJustWithinOneTile()
        {
            // "The easternmost buildable empty lot on the map" — a lot on a tile
            // further east beats the northeast lot of a tile further west.
            var west = LotsOf(TileType.FourWay, new TileCoordinate(0, 1));
            var east = LotsOf(TileType.FourWay, new TileCoordinate(1, 1));
            var all = new List<HouseLot>(west);
            all.AddRange(east);

            var target = OnboardingHouseHighlight.BuildTargetLot(all);

            Assert.That(target.HouseId,
                Is.EqualTo(FrontierHouseId.For(new TileCoordinate(1, 1), Quadrant.NorthEast)),
                "the easternmost lot on the map wins, whichever tile it sits on");
        }

        [Test]
        public void BuildTargetLot_IsNullWhenThereIsNoBuildableLot()
        {
            Assert.That(OnboardingHouseHighlight.BuildTargetLot(new List<HouseLot>()), Is.Null,
                "no buildable lot means no target — and no throw");
            Assert.That(OnboardingHouseHighlight.BuildTargetLot(null), Is.Null, "null-safe");
        }

        [Test]
        public void TargetHouseId_OnTheBuildStep_IsTheEasternmostBuildableLot()
        {
            var state = AtBuildStep();

            // The scripted first tile is a CulDeSacSouth: it keeps its SE + SW
            // quadrants, so the east lot is the SE one.
            Assert.That(OnboardingHouseHighlight.TargetHouseId(state),
                Is.EqualTo(FrontierHouseId.For(FrontierTestWorld.FirstTile, Quadrant.SouthEast)));
            Assert.That(OnboardingHouseHighlight.TargetHouseId(state),
                Is.Not.EqualTo(FrontierHouseId.For(FrontierTestWorld.FirstTile, Quadrant.SouthWest)),
                "never the west lot");
        }

        [Test]
        public void TargetHouseId_OnTheBuildStep_SkipsALotThatAlreadyHasAHouse()
        {
            var state = AtBuildStep();
            var eastLot = FrontierHouseId.For(FrontierTestWorld.FirstTile, Quadrant.SouthEast);
            var westLot = FrontierHouseId.For(FrontierTestWorld.FirstTile, Quadrant.SouthWest);

            // A house already standing on the east lot (restored from a save) is
            // not buildable, so the target falls through to the next lot east.
            state.RestoreBuiltHouse(eastLot, level: 1, isVacant: true, variant: HouseVariantAssignment.ForHouse(eastLot));

            Assert.That(OnboardingHouseHighlight.TargetHouseId(state), Is.EqualTo(westLot),
                "only BUILDABLE empty lots are candidates");
        }

        [Test]
        public void TargetHouseId_IsNullOncePastTheBuildStep_EvenThoughBuildableLotsRemain()
        {
            var state = AtBuildStep();
            var eastLot = FrontierHouseId.For(FrontierTestWorld.FirstTile, Quadrant.SouthEast);

            state.Wallet.Deposit(HouseBuildNumbers.Cost(state.PlayerBuiltHouseCount));
            Assert.That(state.TryBuildHouse(eastLot), Is.True, "the target lot builds");
            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.Done));

            Assert.That(OnboardingHouseHighlight.TargetHouseId(state), Is.Null,
                "the highlight clears the instant the chain advances past BuildHouse, "
                + "even though the tile's west lot is still buildable");
        }

        [Test]
        public void TargetHouseId_OnTheBuildStep_NeedsNoPersistedState_SoASaveRoundTripResolvesTheSameLot()
        {
            // #469's property: the highlight reads only RewardChain.CurrentStep
            // plus state that already exists. A save/load round trip must resolve
            // the same target with no new save line to carry it.
            var state = AtBuildStep();
            var before = OnboardingHouseHighlight.TargetHouseId(state);

            var reloaded = SaveCodec.Load(SaveCodec.Save(state));

            Assert.That(reloaded.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.BuildHouse));
            Assert.That(OnboardingHouseHighlight.TargetHouseId(reloaded), Is.EqualTo(before),
                "the build-step target is derived from live map state, not persisted");
        }

        /// <summary>A game whose reward chain is waiting on the final
        /// <see cref="OnboardingRewardStep.BuildHouse"/> step, with the scripted
        /// first frontier tile unlocked so its lots are buildable.</summary>
        private static GameState AtBuildStep()
        {
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());

            var house = state.Houses[0].Id;
            state.GrantOnboardingCompletionReward(house); // -> UpgradeHouse
            state.TryUpgradeHouse(house);                 // -> ExpandMap
            state.Wallet.Deposit(TileUnlock.Cost(state.Map.Tiles.Count));
            state.TryUnlockTile(FrontierTestWorld.FirstTile); // -> BuildHouse

            Assert.That(state.RewardChain.CurrentStep, Is.EqualTo(OnboardingRewardStep.BuildHouse),
                "precondition: the chain is waiting on BuildHouse");
            return state;
        }

        /// <summary>The <see cref="HouseLot"/>s a tile of <paramref name="type"/>
        /// at <paramref name="coordinate"/> carries, built the same way
        /// <see cref="GameState.LotsForUnlockedTile"/> does (tile catalog +
        /// <see cref="FrontierHouseId.For"/>) but standalone, so the east-lot rule
        /// can be exercised against tile types the authored map's first tile
        /// isn't.</summary>
        private static IReadOnlyList<HouseLot> LotsOf(TileType type, TileCoordinate coordinate)
        {
            var lots = new List<HouseLot>();
            var center = TileGeometry.CenterOf(coordinate);
            foreach (var pair in TileLotCatalog.LotsFor(type))
            {
                lots.Add(new HouseLot(
                    FrontierHouseId.For(coordinate, pair.Key),
                    pair.Key,
                    new GridPoint(center.X + pair.Value.X, center.Z + pair.Value.Z)));
            }

            return lots;
        }
    }
}
