namespace Doggiehood.Core.World
{
    /// <summary>
    /// #703: the short visible beat a delivered package gets at the door before
    /// it is removed. The box the delivery truck drops is packaging with no
    /// state behind it — the gift itself is recorded permanently as a
    /// <see cref="PlacedItem"/> by <c>QuestManager.DeliverPackage</c> — so the
    /// cube must show up long enough for the drop to register and then go away.
    ///
    /// The beat is owned by the PACKAGE, not by quest state or by the truck:
    /// drop and receipt happen on the SAME frame (the drop invokes the delivered
    /// callback, which completes the quest), so keying removal off
    /// <c>DeliveryPhase.Delivered</c> would destroy the cube on the frame it was
    /// created; and tying it to the truck's exit drive would couple the box to an
    /// actor whose duration varies with route length and which is torn down
    /// independently. One instance of this accumulator per dropped package means
    /// concurrent deliveries (#600) each run their own beat.
    ///
    /// This type holds the duration and the "has it elapsed" decision; the Unity
    /// layer only feeds it frame deltas and performs the destruction.
    /// </summary>
    public sealed class DeliveredPackageLifetime
    {
        /// <summary>How long the dropped package stays at the door, in seconds
        /// (#161 — a named tunable, never an inline literal). Long enough that
        /// the drop reads on screen while the dog is still in the waiting pose,
        /// short enough that nothing is left standing in the doorway.</summary>
        public const float VisibleSeconds = 2.5f;

        /// <summary>Seconds accumulated since the package was dropped.</summary>
        public float ElapsedSeconds { get; private set; }

        /// <summary>True once the beat is over and the package must be removed
        /// — collider and all, not merely hidden.</summary>
        public bool HasElapsed => ElapsedSeconds >= VisibleSeconds;

        /// <summary>Advances the beat by one frame. Non-positive steps are
        /// ignored so the beat can never rewind.</summary>
        public void Advance(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            ElapsedSeconds += deltaTime;
        }
    }
}
