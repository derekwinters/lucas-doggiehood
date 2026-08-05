using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #546: a delivery truck (or any vehicle) and a dog must never occupy the
    /// same point on a crosswalk. Right-of-way is a first-come claim gate on the
    /// specific crosswalk <see cref="WalkEdge"/>: the first occupant to arrive
    /// claims it exclusively, the second is denied until the first exits. The
    /// gate is a generic Core abstraction shared by every vehicle and dog — it
    /// knows nothing about trucks or dogs, only occupant identity per crosswalk.
    /// </summary>
    public class RoadCrossingGateTests
    {
        private static WalkEdge Crosswalk(float ax, float az, float bx, float bz)
        {
            return new WalkEdge(
                new GridPoint(ax, az), new GridPoint(bx, bz),
                WalkEdgeKind.Crosswalk, WorldDimensions.CrosswalkWidth);
        }

        [Test]
        public void FirstCallerIsGranted_ASecondDifferentCallerIsDenied_WhileTheFirstHolds()
        {
            var gate = new RoadCrossingGate();
            var crosswalk = Crosswalk(4.75f, 4.75f, -4.75f, 4.75f);
            var truck = new object();
            var dog = new object();

            Assert.That(gate.TryEnter(crosswalk, truck), Is.True,
                "the first occupant to arrive claims the crosswalk");
            Assert.That(gate.TryEnter(crosswalk, dog), Is.False,
                "a second, different occupant is denied while the first still holds the claim");
        }

        [Test]
        public void AfterTheHolderExits_APreviouslyDeniedCallerIsGranted()
        {
            var gate = new RoadCrossingGate();
            var crosswalk = Crosswalk(4.75f, 4.75f, -4.75f, 4.75f);
            var truck = new object();
            var dog = new object();

            Assert.That(gate.TryEnter(crosswalk, truck), Is.True);
            Assert.That(gate.TryEnter(crosswalk, dog), Is.False, "denied while the truck holds it");

            gate.Exit(crosswalk, truck);

            Assert.That(gate.TryEnter(crosswalk, dog), Is.True,
                "once the holder exits, the previously-denied occupant may enter");
        }

        [Test]
        public void TheSameOccupantThatHoldsTheClaim_KeepsBeingGranted_IdempotentReChecks()
        {
            var gate = new RoadCrossingGate();
            var crosswalk = Crosswalk(4.75f, 4.75f, -4.75f, 4.75f);
            var truck = new object();

            Assert.That(gate.TryEnter(crosswalk, truck), Is.True);
            Assert.That(gate.TryEnter(crosswalk, truck), Is.True,
                "re-checking mid-crossing from the SAME occupant must keep succeeding, not self-lock");
            Assert.That(gate.TryEnter(crosswalk, truck), Is.True);
        }

        [Test]
        public void TwoDifferentCrosswalks_ClaimIndependently()
        {
            var gate = new RoadCrossingGate();
            var north = Crosswalk(4.75f, 4.75f, -4.75f, 4.75f);
            var south = Crosswalk(4.75f, -4.75f, -4.75f, -4.75f);
            var truck = new object();
            var dog = new object();

            Assert.That(gate.TryEnter(north, truck), Is.True);
            Assert.That(gate.TryEnter(south, dog), Is.True,
                "a claim on one crosswalk must never block entry on a different crosswalk");
        }

        [Test]
        public void ACrosswalkClaim_IsDirectionIndependent()
        {
            // The same physical crosswalk is one gate whether its edge is read
            // A->B or B->A — vehicle and dog resolve the edge from opposite ends.
            var gate = new RoadCrossingGate();
            var forward = Crosswalk(4.75f, 4.75f, -4.75f, 4.75f);
            var reversed = Crosswalk(-4.75f, 4.75f, 4.75f, 4.75f);
            var truck = new object();
            var dog = new object();

            Assert.That(gate.TryEnter(forward, truck), Is.True);
            Assert.That(gate.TryEnter(reversed, dog), Is.False,
                "the reversed edge is the same crosswalk, so a different occupant is still denied");
        }

        [Test]
        public void Clear_ReleasesEveryClaim()
        {
            var gate = new RoadCrossingGate();
            var north = Crosswalk(4.75f, 4.75f, -4.75f, 4.75f);
            var south = Crosswalk(4.75f, -4.75f, -4.75f, -4.75f);
            Assert.That(gate.TryEnter(north, new object()), Is.True);
            Assert.That(gate.TryEnter(south, new object()), Is.True);

            gate.Clear();

            Assert.That(gate.TryEnter(north, new object()), Is.True,
                "Clear drops all claims so any occupant may re-enter — used to reset the shared gate between tests");
            Assert.That(gate.TryEnter(south, new object()), Is.True);
        }

        [Test]
        public void Shared_IsASingleProcessWideInstance()
        {
            Assert.That(RoadCrossingGate.Shared, Is.Not.Null);
            Assert.That(RoadCrossingGate.Shared, Is.SameAs(RoadCrossingGate.Shared),
                "every vehicle and dog coordinates through one shared gate");
        }

        [Test]
        public void ExitByANonHolder_DoesNotReleaseTheClaim()
        {
            var gate = new RoadCrossingGate();
            var crosswalk = Crosswalk(4.75f, 4.75f, -4.75f, 4.75f);
            var truck = new object();
            var dog = new object();

            Assert.That(gate.TryEnter(crosswalk, truck), Is.True);
            gate.Exit(crosswalk, dog); // dog never held it

            Assert.That(gate.TryEnter(crosswalk, dog), Is.False,
                "a non-holder calling Exit must not release the real holder's claim");
        }
    }
}
