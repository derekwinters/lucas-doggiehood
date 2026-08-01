using System;
using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #430: a zone-built house joins the live <see cref="GameState.WalkNetwork"/>
    /// for rendering + pathing — it grows a real front-walkway edge (so it can
    /// render and face the street), while an empty zone lot (no house built
    /// yet) stays off the graph exactly as before.
    /// </summary>
    public class ZoneHouseWalkwayTests
    {
        private static GameState UnlockedZoneState()
        {
            return FrontierTestWorld.WithFirstTileUnlocked(10_000);
        }

        private static System.Collections.Generic.IReadOnlyList<HouseLot> Lots(GameState state)
        {
            return state.LotsForUnlockedTile(FrontierTestWorld.FirstTile);
        }

        [Test]
        public void BuiltZoneHouse_JoinsTheWalkNetwork_WithAFrontWalkway()
        {
            // #430 item 1: once a zone lot has a built house (rolled variant /
            // resolved model), its lot joins the network with a front walkway.
            var state = UnlockedZoneState();
            var lot = Lots(state)[0];

            Assert.That(state.WalkNetwork.TryGetFrontWalkway(lot.HouseId, out _), Is.False,
                "an empty zone lot must not have a front walkway before it is built");

            Assert.That(state.TryBuildHouse(lot.HouseId), Is.True);

            Assert.That(state.WalkNetwork.TryGetFrontWalkway(lot.HouseId, out _), Is.True,
                "a built zone house's lot must join the network with a front walkway");
        }

        [Test]
        public void EmptyZoneLot_StaysOffTheGraph_WhenAnotherLotInTheZoneIsBuilt()
        {
            // #430 item 1: widening the filter admits only *built* zone houses;
            // an unlocked-but-unbuilt sibling lot must stay excluded exactly as
            // today (a bare empty-lot marker never gets a walkway).
            var state = UnlockedZoneState();
            var built = Lots(state)[0];
            var stillEmpty = Lots(state)[1];

            Assert.That(state.TryBuildHouse(built.HouseId), Is.True);

            Assert.That(state.WalkNetwork.TryGetFrontWalkway(stillEmpty.HouseId, out _), Is.False,
                "an unbuilt zone lot must not join the network just because a sibling was built");
        }

        [Test]
        public void FrontFacing_ResolvesWalkwayDerivedFacing_ForABuiltZoneHouse()
        {
            // #430 item 2: the #429 network-aware overload, now actually
            // exercised against a zone lot, returns the street-ward facing
            // derived from the walkway (door -> sidewalk) rather than the crude
            // single-arg Z-sign fallback.
            var state = UnlockedZoneState();
            var lot = Lots(state)[0];
            Assert.That(state.TryBuildHouse(lot.HouseId), Is.True);

            Assert.That(state.WalkNetwork.TryGetFrontWalkway(lot.HouseId, out var walkway), Is.True);

            var facing = HousePlacement.FrontFacing(lot, state.WalkNetwork);

            Assert.That(facing, Is.EqualTo(HousePlacement.FacingToward(walkway.A, walkway.B)),
                "facing must be derived from the resolved front walkway, not the fallback");
        }

        [Test]
        public void Position_ResolvesTheFrontSetbackPosition_ForABuiltZoneHouse()
        {
            // #430 item 5 (render alignment): the network-aware Position
            // overload sets a built zone house back from its street exactly like
            // a starting house, so the rendered mesh lines up with the walkway's
            // door node. The single-arg overload (starting-tile network only)
            // would return the un-set-back lot centre for a zone lot.
            var state = UnlockedZoneState();
            var lot = Lots(state)[0];
            Assert.That(state.TryBuildHouse(lot.HouseId), Is.True);

            Assert.That(state.WalkNetwork.TryGetFrontWalkway(lot.HouseId, out var walkway), Is.True);

            var position = HousePlacement.Position(lot, HousePlacement.KitScale, state.WalkNetwork);

            Assert.That(position, Is.EqualTo(HousePlacement.PositionFor(lot, HousePlacement.KitScale, walkway.B)),
                "position must be derived from the resolved front walkway's sidewalk attach");
            Assert.That(position, Is.Not.EqualTo(lot.Position),
                "a built zone house must be set back from its street, not left at the lot centre");
        }

        [Test]
        public void Position_FallsBackToLotCentre_WhenTheNetworkHasNoWalkwayForTheLot()
        {
            // #430 item 5: a lot with no front-walkway edge in the given network
            // (e.g. an unbuilt zone lot) keeps its lot-centre position — the same
            // contract the single-arg overload holds.
            var state = UnlockedZoneState();
            var unbuilt = Lots(state)[0];

            var position = HousePlacement.Position(unbuilt, HousePlacement.KitScale, state.WalkNetwork);

            Assert.That(position, Is.EqualTo(unbuilt.Position));
        }
    }
}
