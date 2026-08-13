using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #702: a house built outside the starting intersection came back bare on
    /// the next launch — its yard trees were scene state pre-baked at unlock, and
    /// no render path rebuilt them for a lot that now carried a house. The render
    /// gap itself is fixed in <c>WorldBuilder</c>; what is pinned here is the
    /// Core half of the invariant it has to keep:
    ///
    /// <b>Invariant — every lot with a house has yard trees in EVERY session, not
    /// only the one it was built in.</b>
    ///
    /// Two ways that can silently break, both closed here. The picks could come
    /// back EMPTY (<c>SelectFront</c>/<c>SelectBack</c> cap at
    /// <c>Math.Min(desired, candidates.Count)</c>, so a yard whose candidate
    /// generation is fully clipped yields zero trees and nothing complains), or
    /// they could come back DIFFERENT once the house exists (building adds the
    /// front walkway and the backyard fence to the live network, which the yard's
    /// obstacle set reads) — a reload would then move a lot's trees. The sweep
    /// below runs every lotted <see cref="TileType"/> through a real unlock →
    /// build → re-derive cycle and rejects both.
    ///
    /// Complements <c>OpenSpaceTreesTests</c>, which pins the same
    /// never-bare rule for UNBUILT lots on the tile-only resolvers; this file is
    /// the BUILT-lot, network-aware case the reload path actually renders.
    /// </summary>
    public class YardTreeCoverageTests
    {
        /// <summary>The starting intersection every test tile is placed against,
        /// so the tile connects to the seeded FourWay's road network.</summary>
        private static readonly TileCoordinate Origin = new TileCoordinate(0, 0);

        private static IEnumerable<TileType> LottedTypes
        {
            get { return TileLotCatalog.Types; }
        }

        [Test]
        public void TreesFor_IsTheLotsFrontPicksThenItsBackPicks()
        {
            var lot = NeighborhoodLayout.HouseLots[0];

            var expected = YardLandscaping.FrontTreesFor(lot)
                .Concat(YardLandscaping.BackTreesFor(lot))
                .ToList();

            Assert.That(Describe(YardLandscaping.TreesFor(lot)), Is.EqualTo(Describe(expected)),
                "a lot's yard is its front picks followed by its back picks — one Core seam for "
                + "the whole yard, so the render layer never re-decides what a lot's trees are");
        }

        [Test]
        public void TreesFor_NetworkAware_IsTheLotsFrontPicksThenItsBackPicks()
        {
            var state = FrontierTestWorld.WithFirstTileUnlocked();
            var type = state.Map.GetTileAt(FrontierTestWorld.FirstTile);
            var lot = state.LotsForUnlockedTile(FrontierTestWorld.FirstTile)[0];

            var expected = YardLandscaping.FrontTreesFor(lot, type, state.WalkNetwork)
                .Concat(YardLandscaping.BackTreesFor(lot, type, state.WalkNetwork))
                .ToList();

            Assert.That(Describe(YardLandscaping.TreesFor(lot, type, state.WalkNetwork)),
                Is.EqualTo(Describe(expected)),
                "the network-aware seam composes the same two halves");
        }

        [TestCaseSource(nameof(LottedTypes))]
        public void EveryLotOnEveryLottedTileType_HasYardTrees_BeforeAndAfterItsHouseIsBuilt(TileType type)
        {
            // The #702 never-bare rule, swept across the whole tile catalog:
            // unlock a tile of this type, read each lot's yard, build the house,
            // then read the yard again the way a RELOAD would — from GameState
            // alone, with no memory of what was pre-baked at unlock. A built lot
            // is exactly the case the reported bug dropped, so it is asserted
            // here at both stages: the house appearing never empties the yard.
            var coordinate = PlacementFor(type);
            var state = WithTileUnlocked(coordinate, type);

            var lots = state.LotsForUnlockedTile(coordinate);
            Assert.That(lots, Is.Not.Empty, $"precondition: {type} carries lots");

            foreach (var lot in lots)
            {
                AssertNotBare(YardLandscaping.TreesFor(lot, type, state.WalkNetwork), type, lot, "unbuilt");
            }

            foreach (var lot in lots)
            {
                state.Wallet.Deposit(HouseBuildNumbers.Cost(state.PlayerBuiltHouseCount));
                Assert.That(state.TryBuildHouse(lot.HouseId), Is.True,
                    $"precondition: {type} lot {lot.HouseId} builds");
            }

            foreach (var lot in lots)
            {
                Assert.That(state.IsLotBuildable(lot.HouseId), Is.False,
                    $"precondition: {type} lot {lot.HouseId} now has a house on it");
                AssertNotBare(YardLandscaping.TreesFor(lot, type, state.WalkNetwork), type, lot, "built");
            }

            // NOT asserted here, deliberately: that the built yard is IDENTICAL to
            // the unbuilt pre-bake. It currently is not — building a house adds
            // its front walkway to the live network, and the front yard reads that
            // walkway as an obstacle only once it exists, so one front tree shifts
            // on the lot that was just built (reproducible on the NorthWest lot of
            // StraightEW/TeeNorth/TeeSouth). That is a separate defect from this
            // one — the trees are all still THERE, they move — and correcting it
            // means deciding which walkway a yard reserves (none / the
            // predetermined one / the live level-aware one), which is a design
            // call for Derek rather than something to settle inside a bug fix.
            // Filed as #719. What #702 needs — that a reload renders the built
            // lot's trees unchanged — is pinned in the reload sweep below.
        }

        [TestCaseSource(nameof(LottedTypes))]
        public void EveryLotOnEveryLottedTileType_KeepsItsYardTrees_AcrossASaveReload(TileType type)
        {
            // The reported symptom, end to end at the Core seam: build the houses,
            // round-trip the save, and re-derive every lot's yard from the loaded
            // state alone — the trees the next launch renders are the same trees.
            var coordinate = PlacementFor(type);
            var state = WithTileUnlocked(coordinate, type);
            foreach (var lot in state.LotsForUnlockedTile(coordinate))
            {
                state.Wallet.Deposit(HouseBuildNumbers.Cost(state.PlayerBuiltHouseCount));
                Assert.That(state.TryBuildHouse(lot.HouseId), Is.True,
                    $"precondition: {type} lot {lot.HouseId} builds");
            }

            var before = state.LotsForUnlockedTile(coordinate)
                .ToDictionary(lot => lot.HouseId,
                    lot => Describe(YardLandscaping.TreesFor(lot, type, state.WalkNetwork)));

            var reloaded = SaveCodec.Load(SaveCodec.Save(state));

            var reloadedType = reloaded.Map.GetTileAt(coordinate);
            Assert.That(reloadedType, Is.EqualTo(type), "precondition: the tile round-trips");
            var reloadedLots = reloaded.LotsForUnlockedTile(coordinate);
            Assert.That(reloadedLots.Count, Is.EqualTo(before.Count), "the same lots come back");

            foreach (var lot in reloadedLots)
            {
                Assert.That(reloaded.IsLotBuildable(lot.HouseId), Is.False,
                    $"precondition: {type} lot {lot.HouseId} still has its house after the reload");

                var trees = YardLandscaping.TreesFor(lot, reloadedType, reloaded.WalkNetwork);
                AssertNotBare(trees, type, lot, "reloaded");
                Assert.That(Describe(trees), Is.EqualTo(before[lot.HouseId]),
                    $"{type} lot {lot.HouseId} ({lot.Quadrant}): the reloaded yard is the built yard");
            }
        }

        [Test]
        public void EveryStartingLot_HasYardTrees_ThoughItAlwaysCarriesAHouse()
        {
            // The four seeded FourWay lots are built from the very first session,
            // so they are the one group whose yard is ONLY ever a built lot's
            // yard. They render through the starting-tile resolver.
            var state = GameState.CreateNew();

            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                Assert.That(state.IsLotBuildable(lot.HouseId), Is.False,
                    $"precondition: starting lot {lot.HouseId} is seeded with a house");
                Assert.That(YardLandscaping.TreesFor(lot), Is.Not.Empty,
                    $"starting lot {lot.HouseId} ({lot.Quadrant}): a built lot is never bare");
            }
        }

        [Test]
        public void EveryLotIsRenderedByExactlyOneSource_SoNoBuiltLotFallsBetweenThem()
        {
            // Why the starting lots need their own resolver at all: the origin
            // FourWay's four lots are seeded from NeighborhoodLayout and are
            // deliberately NOT emitted by LotsForUnlockedTile (#607), while every
            // frontier lot is emitted there and never in NeighborhoodLayout. The
            // two render loops in WorldBuilder therefore partition the lots
            // exactly — the gap #702 reported was a lot being dropped INSIDE the
            // frontier loop, not a lot belonging to neither.
            var state = FrontierTestWorld.WithFirstTileUnlocked();
            var startingIds = NeighborhoodLayout.HouseLots.Select(lot => lot.HouseId).ToList();

            var frontierIds = state.UnlockedTiles
                .SelectMany(coordinate => state.LotsForUnlockedTile(coordinate))
                .Select(lot => lot.HouseId)
                .ToList();

            Assert.That(frontierIds, Is.Not.Empty, "precondition: a frontier tile is unlocked");
            Assert.That(state.LotsForUnlockedTile(Origin), Is.Empty,
                "the origin FourWay's lots belong to the starting loop, never the frontier loop");
            Assert.That(frontierIds.Intersect(startingIds), Is.Empty,
                "no lot is claimed by both render loops, so no lot renders two yards");

            foreach (var house in state.Houses)
            {
                Assert.That(startingIds.Contains(house.Id) || frontierIds.Contains(house.Id), Is.True,
                    $"house {house.Id} sits on a lot one of the two render loops covers");
            }
        }

        /// <summary>A tile of <paramref name="type"/> placed against the starting
        /// intersection on one of its own roaded edges, so the edge-agreement
        /// placement rule accepts it whatever the type's road shape is.</summary>
        private static TileCoordinate PlacementFor(TileType type)
        {
            var roadEdge = TileCatalog.Get(type).RoadEdges.First();
            return Origin.Neighbor(roadEdge.Opposite());
        }

        /// <summary>A loaded-save-shaped state: the tile is placed and recorded as
        /// unlocked exactly as <see cref="SaveCodec"/> restores one, which is the
        /// path a relaunch takes.</summary>
        private static GameState WithTileUnlocked(TileCoordinate coordinate, TileType type)
        {
            var state = GameState.CreateNew();
            state.RestoreUnlockedTile(coordinate, type);
            Assert.That(state.Map.HasTileAt(coordinate), Is.True,
                $"precondition: {type} places at {coordinate.Col},{coordinate.Row}");
            return state;
        }

        private static void AssertNotBare(
            IReadOnlyList<YardTreePlacement> trees, TileType type, HouseLot lot, string stage)
        {
            Assert.That(trees, Is.Not.Empty,
                $"{type} lot {lot.HouseId} ({lot.Quadrant}), {stage}: a lot's yard is never bare — "
                + "an empty pick list renders zero trees and says nothing");
            Assert.That(trees.Count,
                Is.GreaterThanOrEqualTo(YardLandscaping.FrontSelectMin + YardLandscaping.BackSelectMin),
                $"{type} lot {lot.HouseId} ({lot.Quadrant}), {stage}: the yard carries at least its "
                + "guaranteed front and back minimums, not a partially starved selection");
        }

        private static string Describe(IReadOnlyList<YardTreePlacement> placements)
        {
            return string.Join("|", placements.Select(placement => string.Format(
                CultureInfo.InvariantCulture, "{0}@{1:F4},{2:F4}x{3:F4}",
                placement.Kind, placement.Position.X, placement.Position.Z, placement.Scale)));
        }
    }
}
