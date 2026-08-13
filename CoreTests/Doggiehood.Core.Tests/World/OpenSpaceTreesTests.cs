using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #700: an open-space quadrant is a whole 30x30m lot of grass, so a single
    /// tree at its corner offset read as bare. Every open-space quadrant is now
    /// planted with a small CLUSTER of trees, spaced by
    /// <see cref="YardLandscaping.MinSpacing"/>, sized with the #458 per-tree
    /// variance, and seeded deterministically per (tile coordinate, quadrant) so
    /// the same tile renders identically across sessions and saves.
    ///
    /// The other half of the same playtest report — the 3-way (<c>Tee*</c>) tiles
    /// looking treeless — is NOT an open-space case: a <c>Tee*</c> keeps all four
    /// quadrants as house lots, and an unlocked-but-unbuilt lot is already meant
    /// to render its predetermined house's yard trees (#434/#461). That rule is
    /// pinned here across EVERY lotted tile type so no type can quietly render a
    /// bare lot.
    /// </summary>
    public class OpenSpaceTreesTests
    {
        /// <summary>An arbitrary non-origin tile coordinate — far from the
        /// starting intersection's fixed streets, so only the tile's own road
        /// arms clip its quadrants.</summary>
        private static readonly TileCoordinate Coordinate = new TileCoordinate(2, 3);

        /// <summary>A tile with exactly one roaded edge is a dead end — a
        /// cul-de-sac, whose single arm ends in a paved bulb at the tile
        /// centre.</summary>
        private const int DeadEndRoadEdgeCount = 1;

        /// <summary>How far either side of the origin the pavement sweep runs, in
        /// tiles — wide enough that a lucky seed can't hide a tree standing on
        /// the bulb.</summary>
        private const int SweepRadiusInTiles = 4;

        private static IEnumerable<TileType> AllTypes
        {
            get { return (TileType[])Enum.GetValues(typeof(TileType)); }
        }

        private static IEnumerable<TileType> LottedTypes
        {
            get { return TileLotCatalog.Types; }
        }

        [TestCaseSource(nameof(AllTypes))]
        public void EveryOpenSpaceQuadrant_IsPlantedWithACluster_OfSpacedTreesOnCleanGrass(TileType type)
        {
            // The #700 invariant: never one lonely tree. Every quadrant the type
            // renders as open space carries at least OpenSpaceSelectMin trees,
            // no more than OpenSpaceSelectMax, all mutually spaced and all inside
            // the quadrant's road-cleared grass.
            var roads = LotBounds.RoadsFor(Coordinate, type);

            foreach (var quadrant in TileLotCatalog.TreeQuadrantsFor(type).Keys)
            {
                var trees = TileGeometry.OpenSpaceTreesFor(type, Coordinate, quadrant);

                Assert.That(trees.Count, Is.GreaterThanOrEqualTo(YardLandscaping.OpenSpaceSelectMin),
                    $"{type} {quadrant}: an open-space quadrant is never planted with fewer than "
                    + $"{YardLandscaping.OpenSpaceSelectMin} trees");
                Assert.That(trees.Count, Is.LessThanOrEqualTo(YardLandscaping.OpenSpaceSelectMax),
                    $"{type} {quadrant}: never more than {YardLandscaping.OpenSpaceSelectMax} trees");

                var grass = TileGeometry.OpenSpaceGrassFor(type, Coordinate, quadrant);
                foreach (var tree in trees)
                {
                    Assert.That(grass.Contains(tree.Position), Is.True,
                        $"{type} {quadrant}: {tree.Position} sits on the quadrant's clean grass");
                    foreach (var road in roads)
                    {
                        Assert.That(road.Contains(tree.Position), Is.False,
                            $"{type} {quadrant}: an open-space tree never lands in the road");
                    }
                }

                AssertMutuallySpaced(trees.Select(t => t.Position).ToList(), $"{type} {quadrant}");
            }
        }

        [Test]
        public void TreeWorldPositionsFor_IsTheUnionOfItsQuadrantClusters()
        {
            const TileType type = TileType.TurnNE;

            var all = TileGeometry.TreeWorldPositionsFor(type, Coordinate);

            var expected = TileLotCatalog.TreeQuadrantsFor(type).Keys
                .SelectMany(q => TileGeometry.OpenSpaceTreesFor(type, Coordinate, q))
                .ToList();
            Assert.That(all.Count, Is.EqualTo(expected.Count));
            foreach (var placement in expected)
            {
                Assert.That(all.Any(p => Approximately(p.Position, placement.Position)), Is.True,
                    $"the whole-tile list carries the quadrant cluster's tree at {placement.Position}");
            }
        }

        [Test]
        public void TreeWorldPositionsFor_IsEmpty_ForFullLotTypesAndTheGreenSpacePark()
        {
            // Unchanged by #700: a full-lot type (FourWay/Straight*/Tee*) has no
            // open-space quadrant at all — its four quadrants are house lots, and
            // an unbuilt one renders its PREDETERMINED house's yard trees
            // (pinned below), not open-space trees. The GreenSpace park (#539)
            // stays bare.
            Assert.That(TileGeometry.TreeWorldPositionsFor(TileType.StraightNS, Coordinate), Is.Empty);
            Assert.That(TileGeometry.TreeWorldPositionsFor(TileType.FourWay, Coordinate), Is.Empty);
            Assert.That(TileGeometry.TreeWorldPositionsFor(TileType.TeeNorth, Coordinate), Is.Empty);
            Assert.That(TileGeometry.TreeWorldPositionsFor(TileType.GreenSpace, Coordinate), Is.Empty);
        }

        [TestCaseSource(nameof(AllTypes))]
        public void NoOpenSpaceTree_EverStandsOnPavement_AcrossTheWholeMap(TileType type)
        {
            // The #700 invariant a cluster could otherwise break: scattering
            // several trees over a whole quadrant (instead of one at a fixed 14m
            // corner offset) puts them near the tile centre, where a cul-de-sac's
            // BULB is paved — pavement the per-edge road-corridor trim can't see,
            // because the stub's Road extent stops at the tile centre. Swept over
            // a block of coordinates so a rare seed can't hide it.
            for (var col = -SweepRadiusInTiles; col <= SweepRadiusInTiles; col++)
            {
                for (var row = -SweepRadiusInTiles; row <= SweepRadiusInTiles; row++)
                {
                    var coordinate = new TileCoordinate(col, row);
                    var center = TileGeometry.CenterOf(coordinate);
                    var isDeadEnd = TileCatalog.Get(type).RoadEdges.Count == DeadEndRoadEdgeCount;
                    var roads = LotBounds.RoadsFor(coordinate, type);

                    foreach (var quadrant in TileLotCatalog.TreeQuadrantsFor(type).Keys)
                    {
                        // Clearing the pavement must never starve a quadrant back
                        // to the one lonely tree #700 is fixing.
                        Assert.That(TileGeometry.OpenSpaceTreesFor(type, coordinate, quadrant).Count,
                            Is.GreaterThanOrEqualTo(YardLandscaping.OpenSpaceSelectMin),
                            $"{type} at {col},{row} {quadrant}: still planted with a cluster");
                    }

                    foreach (var tree in TileGeometry.TreeWorldPositionsFor(type, coordinate))
                    {
                        foreach (var road in roads)
                        {
                            Assert.That(road.Contains(tree.Position), Is.False,
                                $"{type} at {col},{row}: {tree.Position} stands in the road");
                        }

                        if (isDeadEnd)
                        {
                            Assert.That(Distance(center, tree.Position),
                                Is.GreaterThanOrEqualTo(WorldDimensions.CulDeSacBulbRadius),
                                $"{type} at {col},{row}: {tree.Position} stands on the cul-de-sac's bulb");
                        }
                    }
                }
            }
        }

        [Test]
        public void AQuadrantWithNoCleanGrass_PlantsNothing_RatherThanForcingATreeIntoTheRoad()
        {
            // #614's rule, carried into #700's cluster path: the plantable region
            // IS the quadrant minus its road corridors, so a quadrant the roads
            // leave no room in generates no candidates at all — a tree can never
            // be forced onto pavement. (This supersedes the old single-tree
            // "does this one corner offset still sit on grass?" gate.)
            var quadrant = new LotRect(0f, 5f, 0f, 30f);
            var roads = new[]
            {
                new Road(StreetOrientation.NorthSouth, new GridPoint(0f, 15f), 15f),
                new Road(StreetOrientation.NorthSouth, new GridPoint(5f, 15f), 15f),
            };

            var grass = LotBounds.ClearRoadCorridors(quadrant, roads);

            Assert.That(grass.Width, Is.EqualTo(0f), "both inset edges collapse the clean-grass rect");
            Assert.That(YardLandscaping.GenerateOpenSpaceCandidates(grass, 1), Is.Empty,
                "no candidate is generated where there is no grass");
            Assert.That(YardLandscaping.SelectOpenSpace(new List<YardTreeCandidate>(), 1), Is.Empty,
                "and an empty candidate pool selects nothing rather than inventing a pick");
        }

        [Test]
        public void OpenSpaceTrees_AreDeterministic_PerTileCoordinateAndQuadrant()
        {
            const TileType type = TileType.CulDeSacSouth;

            var first = TileGeometry.TreeWorldPositionsFor(type, Coordinate);
            var second = TileGeometry.TreeWorldPositionsFor(type, Coordinate);

            Assert.That(Describe(second), Is.EqualTo(Describe(first)),
                "the same tile renders the same trees every time it is asked");

            // A different tile of the same type is seeded separately, so the
            // cluster is not just the same pattern translated across the map.
            var elsewhere = TileGeometry.TreeWorldPositionsFor(type, new TileCoordinate(-4, 5));
            var elsewhereCenter = TileGeometry.CenterOf(new TileCoordinate(-4, 5));
            var center = TileGeometry.CenterOf(Coordinate);
            var sameLocalPattern = elsewhere.Count == first.Count
                && elsewhere.Select((p, i) => Approximately(
                        new GridPoint(p.Position.X - elsewhereCenter.X, p.Position.Z - elsewhereCenter.Z),
                        new GridPoint(first[i].Position.X - center.X, first[i].Position.Z - center.Z)))
                    .All(same => same);
            Assert.That(sameLocalPattern, Is.False,
                "each tile coordinate seeds its own cluster rather than repeating one stamped pattern");
        }

        [Test]
        public void OpenSpaceTrees_VaryInSize_WithinTheYardTreeScaleRange()
        {
            // #458's per-tree scale variance now applies to open-space trees too:
            // they used to be pinned at the baseline only because they had no
            // seed context, and #700's per-quadrant seed removes that reason.
            var placements = new List<YardTreePlacement>();
            foreach (var type in TileLotCatalog.Types)
            {
                placements.AddRange(TileGeometry.TreeWorldPositionsFor(type, Coordinate));
            }

            Assert.That(placements, Is.Not.Empty, "precondition: some type plants open-space trees");
            foreach (var placement in placements)
            {
                Assert.That(placement.Scale, Is.GreaterThanOrEqualTo(YardTreePlacement.BaselineScale),
                    "an open-space tree is never smaller than the baseline");
                Assert.That(placement.Scale, Is.LessThanOrEqualTo(YardLandscaping.MaxTreeScaleVariance),
                    "an open-space tree never exceeds the +25% cap");
            }

            Assert.That(placements.Any(p => p.Scale > YardTreePlacement.BaselineScale + 0.001f), Is.True,
                "at least one open-space tree renders larger than the baseline — sizes really vary");
        }

        [TestCaseSource(nameof(LottedTypes))]
        public void EveryUnbuiltLot_OnEveryTileType_RendersItsPredeterminedHousesYardTrees(TileType type)
        {
            // #434/#461, pinned across every lotted type by #700: an unlocked but
            // UNBUILT lot already renders the yard trees of the house that will
            // stand there — before and after it is built. A Tee*/Straight*/FourWay
            // tile has no open-space quadrant, so these predetermined yard trees
            // are the only thing keeping it from reading as an empty field.
            foreach (var lot in LotsFor(type, Coordinate))
            {
                var front = YardLandscaping.FrontTreesFor(lot, type);
                var back = YardLandscaping.BackTreesFor(lot, type);

                Assert.That(front, Is.Not.Empty,
                    $"{type} lot {lot.HouseId} ({lot.Quadrant}): the unbuilt lot shows front-yard trees");
                Assert.That(back, Is.Not.Empty,
                    $"{type} lot {lot.HouseId} ({lot.Quadrant}): the unbuilt lot shows back-yard trees");

                var quadrant = LotBounds.QuadrantBounds(lot);
                foreach (var tree in front.Concat(back))
                {
                    Assert.That(quadrant.Contains(tree.Position), Is.True,
                        $"{type} lot {lot.HouseId}: a yard tree never spills out of its own quadrant");
                }
            }
        }

        [Test]
        public void AThreeWayTilesLots_AllCarryPredeterminedYardTrees_ThroughTheLiveNetwork()
        {
            // The 3-way half of the #700 report, on the real unlock path: a
            // TeeSouth unlocked north of the origin has no open-space quadrant at
            // all, so every one of its four lots must carry the predetermined
            // yard trees the Unity layer renders (the network-aware overloads
            // WorldBuilder.BuildEmptyLots uses, #461) — and keep them once the
            // house is built, since the trees belong to the lot, not the house.
            const TileType type = TileType.TeeSouth;
            var coordinate = new TileCoordinate(0, 1);
            var target = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            target.Place(coordinate, type);

            var state = GameState.CreateNew();
            state.SetTargetMap(target);
            state.Wallet.Deposit(TileUnlock.Cost(state.Map.Tiles.Count)
                + HouseBuildNumbers.Cost(state.PlayerBuiltHouseCount));
            Assert.That(state.TryUnlockTile(coordinate), Is.True, "precondition: the 3-way unlocks");
            Assert.That(TileGeometry.TreeWorldPositionsFor(type, coordinate), Is.Empty,
                "precondition: a 3-way has no open-space quadrant — all four are house lots");

            var lots = state.LotsForUnlockedTile(coordinate);
            Assert.That(lots.Count, Is.EqualTo(4), "precondition: four buildable lots");
            foreach (var lot in lots)
            {
                Assert.That(YardLandscaping.FrontTreesFor(lot, type, state.WalkNetwork), Is.Not.Empty,
                    $"lot {lot.HouseId} ({lot.Quadrant}): front-yard trees");
                Assert.That(YardLandscaping.BackTreesFor(lot, type, state.WalkNetwork), Is.Not.Empty,
                    $"lot {lot.HouseId} ({lot.Quadrant}): back-yard trees");
            }

            var built = lots[0];
            var before = Describe(YardLandscaping.FrontTreesFor(built, type, state.WalkNetwork));
            Assert.That(state.TryBuildHouse(built.HouseId), Is.True, "the first lot builds");

            Assert.That(Describe(YardLandscaping.FrontTreesFor(built, type, state.WalkNetwork)),
                Is.EqualTo(before),
                "building the house leaves the lot's predetermined trees exactly where they were");
        }

        [Test]
        public void OpenSpaceTreesAndYardTrees_SurviveASaveReloadUnchanged()
        {
            var state = FrontierTestWorld.WithFirstTileUnlocked();
            var coordinate = FrontierTestWorld.FirstTile;
            var type = state.Map.GetTileAt(coordinate);
            var before = Describe(TileGeometry.TreeWorldPositionsFor(type, coordinate));
            var yardBefore = state.LotsForUnlockedTile(coordinate)
                .ToDictionary(lot => lot.HouseId, lot => Describe(YardTrees(lot, type)));

            var reloaded = SaveCodec.Load(SaveCodec.Save(state));

            var reloadedType = reloaded.Map.GetTileAt(coordinate);
            Assert.That(reloadedType, Is.EqualTo(type), "precondition: the tile round-trips");
            Assert.That(Describe(TileGeometry.TreeWorldPositionsFor(reloadedType, coordinate)), Is.EqualTo(before),
                "the tile's open-space trees are identical after a save/reload");

            foreach (var lot in reloaded.LotsForUnlockedTile(coordinate))
            {
                Assert.That(yardBefore.ContainsKey(lot.HouseId), Is.True, "the same lots come back");
                Assert.That(Describe(YardTrees(lot, reloadedType)), Is.EqualTo(yardBefore[lot.HouseId]),
                    $"lot {lot.HouseId}: its predetermined yard trees are identical after a save/reload");
            }
        }

        private static IReadOnlyList<YardTreePlacement> YardTrees(HouseLot lot, TileType type)
        {
            return YardLandscaping.FrontTreesFor(lot, type)
                .Concat(YardLandscaping.BackTreesFor(lot, type)).ToList();
        }

        private static IReadOnlyList<HouseLot> LotsFor(TileType type, TileCoordinate coordinate)
        {
            // The same construction GameState.LotsForUnlockedTile uses, standalone
            // so every tile type can be exercised without a placeable map.
            var center = TileGeometry.CenterOf(coordinate);
            return TileLotCatalog.LotsFor(type)
                .Select(pair => new HouseLot(
                    FrontierHouseId.For(coordinate, pair.Key),
                    pair.Key,
                    new GridPoint(center.X + pair.Value.X, center.Z + pair.Value.Z)))
                .ToList();
        }

        private static string Describe(IReadOnlyList<YardTreePlacement> placements)
        {
            return string.Join(
                " | ",
                placements.Select(p => $"{p.Position.X:F4},{p.Position.Z:F4},{p.Kind},{p.Scale:F4}"));
        }

        private static void AssertMutuallySpaced(IReadOnlyList<GridPoint> points, string label)
        {
            for (var i = 0; i < points.Count; i++)
            {
                for (var j = i + 1; j < points.Count; j++)
                {
                    Assert.That(Distance(points[i], points[j]),
                        Is.GreaterThanOrEqualTo(YardLandscaping.MinSpacing),
                        $"{label}: {points[i]} and {points[j]} must be at least "
                        + $"{YardLandscaping.MinSpacing}m apart");
                }
            }
        }

        private static float Distance(GridPoint a, GridPoint b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        private static bool Approximately(GridPoint a, GridPoint b)
        {
            return Math.Abs(a.X - b.X) < 0.001f && Math.Abs(a.Z - b.Z) < 0.001f;
        }
    }
}
