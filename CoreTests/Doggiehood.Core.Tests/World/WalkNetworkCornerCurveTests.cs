using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #581: plain <c>Turn*</c> road bends and cul-de-sac bulb turnarounds
    /// curve the walk network to follow the rounded road art, instead of the
    /// straight box-corner chord the crossing/crosswalk logic used to emit at a
    /// bend. Box-corner intersection turns (<c>FourWay</c>/<c>Tee</c>) stay hard
    /// 90 (Derek's decision); <c>OpposingTurns</c> are out of scope (#583).
    /// </summary>
    public class WalkNetworkCornerCurveTests
    {
        private static readonly HouseLot[] NoLots = new HouseLot[0];

        private const float Tolerance = 0.01f;

        /// <summary>The sidewalk centerline's perpendicular offset from the road
        /// centerline — the same magnitude the sidewalk arms sit at.</summary>
        private static float SidewalkOffset()
        {
            return WorldDimensions.RoadWidth / 2f + WorldDimensions.GrassVergeWidth
                + WorldDimensions.SidewalkWidth / 2f;
        }

        private static WalkNetwork BuildTile(TileType type)
        {
            return MapWalkNetwork.BuildFrom(new TileMap(new TileCoordinate(0, 0), type), NoLots);
        }

        private static float Distance(GridPoint a, GridPoint b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        [Test]
        public void Bend_HasNoCrosswalkBox_DistinguishingItFromARealCrossing()
        {
            // A TurnNE is two perpendicular stub roads meeting only at their
            // shared tile-centre endpoint. That is NOT a real crossing, so it
            // must not get the crosswalk box a FourWay/Tee crossing gets.
            var bend = BuildTile(TileType.TurnNE);
            Assert.That(bend.Edges.Any(e => e.Kind == WalkEdgeKind.Crosswalk), Is.False,
                "a plain Turn* bend must not emit crosswalk edges");

            // Contrast: a real crossing still does.
            var fourWay = BuildTile(TileType.FourWay);
            Assert.That(fourWay.Edges.Count(e => e.Kind == WalkEdgeKind.Crosswalk), Is.EqualTo(4),
                "a real FourWay crossing still gets its 4-edge crosswalk box");
        }

        [Test]
        public void Bend_InsertsSidewalkWaypointsAlongTheCornerArc_NotAStraightChord()
        {
            // TurnNE at the origin: the road curves N<->E about a corner arc of
            // radius RoadBendCornerRadius, centred at (R, R) from the tile centre
            // (into the +X/+Z quadrant the two arms point into). The two
            // sidewalks curve concentrically at R +/- the sidewalk offset.
            var bend = BuildTile(TileType.TurnNE);
            var r = WorldDimensions.RoadBendCornerRadius;
            var offset = SidewalkOffset();
            var center = new GridPoint(r, r); // tile centre is the origin

            // Inserted waypoints exist that lie OFF both straight arm centrelines
            // (X = +/-offset, Z = +/-offset) — i.e. the corner actually curves.
            var arcNodes = bend.Nodes.Where(n =>
                Math.Abs(Math.Abs(n.X) - offset) > Tolerance &&
                Math.Abs(Math.Abs(n.Z) - offset) > Tolerance).ToList();
            Assert.That(arcNodes, Is.Not.Empty,
                "the bend must insert curved waypoints off the straight arm lines");

            // Every such curved waypoint sits on one of the two concentric
            // corner arcs (inner R-offset or outer R+offset about the arc centre).
            foreach (var node in arcNodes)
            {
                var d = Distance(node, center);
                var onInner = Math.Abs(d - (r - offset)) < Tolerance;
                var onOuter = Math.Abs(d - (r + offset)) < Tolerance;
                Assert.That(onInner || onOuter, Is.True,
                    $"curved waypoint {node} must lie on a concentric corner arc (d={d})");
            }

            // And at least one waypoint is on the OUTER arc — the visible curve
            // the playtest reported cutting straight across.
            Assert.That(arcNodes.Any(n => Math.Abs(Distance(n, center) - (r + offset)) < Tolerance),
                Is.True, "expected outer-arc waypoints bulging around the corner");
        }

        [Test]
        public void FindPath_AcrossABend_RoutesThroughTheCurvedWaypoints_NotThroughTheTileCentre()
        {
            // A dog rounding a TurnNE on the outer sidewalk must trace the curve,
            // not cut a straight chord through the tile centre.
            var bend = BuildTile(TileType.TurnNE);
            var r = WorldDimensions.RoadBendCornerRadius;
            var offset = SidewalkOffset();
            var center = new GridPoint(r, r);
            var half = WorldDimensions.TileSize / 2f;

            // Outer arm ends: north arm's west sidewalk, east arm's south sidewalk.
            var start = new GridPoint(-offset, half);   // north outer arm, at the tile edge
            var goal = new GridPoint(half, -offset);    // east outer arm, at the tile edge
            var path = bend.FindPath(start, goal);

            Assert.That(path.Count, Is.GreaterThan(2), "the route must have intermediate waypoints");

            // The route bulges along the outer corner arc: interior path nodes
            // sit on that arc (not on either straight arm line), and the route is
            // longer than the straight chord it used to cut.
            var interior = path.Skip(1).Take(path.Count - 2).ToList();
            Assert.That(interior.Any(n => Math.Abs(Distance(n, center) - (r + offset)) < Tolerance),
                Is.True, "the route must pass through the outer corner-arc waypoints");

            // The old box-corner behaviour connected the arms with straight
            // crosswalk chords; the curved route is strictly longer than a single
            // chord from start to goal.
            var routeLength = 0f;
            for (var i = 0; i + 1 < path.Count; i++)
            {
                routeLength += Distance(path[i], path[i + 1]);
            }
            Assert.That(routeLength, Is.GreaterThan(Distance(start, goal) + Tolerance),
                "the curved route must be longer than a straight chord across the bend");
        }

        [Test]
        public void CulDeSac_CurvesItsBulbTurnaround_UsingTheBulbRadius()
        {
            // The starting FourWay plus the scripted north cul-de-sac (a
            // CulDeSacSouth at (0,1), bulb at that tile's centre). Its two
            // sidewalk arms used to dead-end separately at the bulb; now they're
            // joined by a curved turnaround of radius CulDeSacBulbRadius that
            // bulges around the closed (north) bulb.
            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            map.Place(new TileCoordinate(0, 1), TileType.CulDeSacSouth);
            var network = MapWalkNetwork.BuildFrom(map, NoLots);

            var offset = SidewalkOffset();
            var rBulb = WorldDimensions.CulDeSacBulbRadius;
            var bulb = TileGeometry.CenterOf(new TileCoordinate(0, 1)); // (0, 60)
            var back = (float)Math.Sqrt(rBulb * rBulb - offset * offset);
            var arcCenter = new GridPoint(bulb.X, bulb.Z - back); // bulges toward +Z (closed side)

            // Both bulb-end sidewalk nodes exist (the arm ends the arc welds to).
            Assert.That(network.Nodes.Any(n => Math.Abs(n.X - offset) < Tolerance && Math.Abs(n.Z - bulb.Z) < Tolerance),
                Is.True, "the +side bulb arm end must exist");
            Assert.That(network.Nodes.Any(n => Math.Abs(n.X + offset) < Tolerance && Math.Abs(n.Z - bulb.Z) < Tolerance),
                Is.True, "the -side bulb arm end must exist");

            // Inserted turnaround waypoints ride the bulb-radius circle and bulge
            // PAST the bulb line into the closed side — nothing lived north of it
            // before (the arms stopped at the bulb).
            var turnaroundWaypoints = network.Nodes.Where(n =>
                Math.Abs(Distance(n, arcCenter) - rBulb) < Tolerance &&
                Math.Abs(Math.Abs(n.X) - offset) > Tolerance).ToList();
            Assert.That(turnaroundWaypoints, Is.Not.Empty,
                "the cul-de-sac must insert turnaround waypoints on the bulb-radius arc");
            Assert.That(turnaroundWaypoints.Any(n => n.Z > bulb.Z + Tolerance), Is.True,
                "the turnaround must bulge past the bulb into the closed side");

            // The two arm ends are joined by that curved turnaround: a path
            // between them exists and rides the bulb arc.
            var path = network.FindPath(new GridPoint(offset, bulb.Z), new GridPoint(-offset, bulb.Z));
            Assert.That(path.Skip(1).Take(path.Count - 2)
                    .Any(n => Math.Abs(Distance(n, arcCenter) - rBulb) < Tolerance),
                Is.True, "the turnaround route must ride the bulb-radius arc");
        }

        [Test]
        public void FourWayAndTee_BoxCorners_StayStraight_OnlyBendsAndCulDeSacsCurve()
        {
            // Regression (Derek's decision): FourWay/Tee box-corner turns stay
            // hard 90 — their crosswalk box is unchanged, and no curved arc
            // waypoints are inserted at a real crossing.
            foreach (var type in new[] { TileType.FourWay, TileType.TeeNorth })
            {
                var network = BuildTile(type);
                Assert.That(network.Edges.Count(e => e.Kind == WalkEdgeKind.Crosswalk), Is.EqualTo(4),
                    $"{type} keeps its 4-edge crosswalk box");

                // Every sidewalk node sits on an axis-aligned arm line
                // (X or Z at the sidewalk offset) — no off-axis curved waypoints.
                var offset = SidewalkOffset();
                foreach (var node in network.Nodes)
                {
                    var onArmLine = Math.Abs(Math.Abs(node.X) - offset) < Tolerance
                        || Math.Abs(Math.Abs(node.Z) - offset) < Tolerance;
                    Assert.That(onArmLine, Is.True,
                        $"{type} node {node} must stay on a straight arm line (no curve at a box corner)");
                }
            }

            // A straight run is likewise untouched.
            var straight = BuildTile(TileType.StraightNS);
            var straightOffset = SidewalkOffset();
            foreach (var node in straight.Nodes)
            {
                Assert.That(Math.Abs(Math.Abs(node.X) - straightOffset) < Tolerance, Is.True,
                    $"StraightNS node {node} must stay on its straight sidewalk line");
            }
        }

        [Test]
        public void IsFullyConnected_HoldsWithACurvedBendAndACurvedCulDeSac()
        {
            // A connected map carrying both a curved bend and a curved cul-de-sac
            // must stay one graph — no node orphaned by the inserted arc
            // waypoints. Origin FourWay, a CulDeSacSouth north of it (its bulb
            // turnaround curves), and a TurnNE west of the origin whose east arm
            // knits into the origin's crosswalk box (its bend curves).
            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            map.Place(new TileCoordinate(0, 1), TileType.CulDeSacSouth);
            map.Place(new TileCoordinate(-1, 0), TileType.TurnNE);
            var network = MapWalkNetwork.BuildFrom(map, NoLots);

            Assert.That(network.IsFullyConnected(), Is.True,
                "the curved bend + cul-de-sac network must stay fully connected");
        }
    }
}
