namespace Doggiehood.Core.World
{
    /// <summary>
    /// #600: 1-D car-following for delivery trucks queued single-file on one road
    /// segment. Two rules, both expressed purely in along-road coordinates so the
    /// thin Unity view only converts positions and drives:
    ///
    /// <list type="bullet">
    /// <item><b>Gap.</b> A follower never advances past one car length
    /// (<see cref="GapMeters"/>) behind the truck ahead — the crosswalk gate
    /// (<see cref="RoadCrossingGate"/>) only arbitrates the crosswalk claim, so
    /// this is what actually keeps a follower out of a stopped leader's body on
    /// the approach.</item>
    /// <item><b>Start-up delay.</b> When a stopped leader begins moving, the
    /// follower waits <see cref="StartUpDelaySeconds"/> before it starts to move,
    /// modelling a driver's reaction time.</item>
    /// </list>
    ///
    /// Single file only: this is not a traffic sim, so there is no lane model or
    /// passing. One instance per follower; it is ticked with the leader's current
    /// along-coordinate (or null when the road ahead is clear) and returns how far
    /// the follower may advance this tick. It composes UNDER the crosswalk gate —
    /// the caller applies both clamps and takes the more restrictive.
    /// </summary>
    public sealed class CarFollowing
    {
        /// <summary>
        /// The along-road length of a delivery vehicle in world meters — the
        /// following gap is exactly one of these (Derek, 2026-08-05: "1 car
        /// length"). A first-pass tuning value in the spirit of
        /// <see cref="DeliveryTruckLengthMeters"/>'s rendered counterpart
        /// (the Unity view's kit model scale), confirmable on-device; declared
        /// as a named constant per #161 rather than a bare literal in the clamp.
        /// </summary>
        public const float DeliveryTruckLengthMeters = 4f;

        /// <summary>One car length — the gap a follower holds behind the truck
        /// ahead (Derek, 2026-08-05).</summary>
        public const float GapMeters = DeliveryTruckLengthMeters;

        /// <summary>The reaction delay a follower waits after a stopped leader
        /// begins moving before it starts moving too (Derek, 2026-08-05).</summary>
        public const float StartUpDelaySeconds = 1f;

        // Below this along-road delta between ticks the leader counts as stopped —
        // guards the motion test against floating-point noise.
        private const float LeaderMotionEpsilon = 0.0001f;

        private readonly float travelSign;

        private float? previousLeaderAlong;
        private bool? leaderWasMoving;
        private float startUpTimerRemaining;

        /// <param name="travelSign">+1 when the follower drives toward increasing
        /// along-road coordinates, -1 toward decreasing — matching how
        /// <see cref="RoadCrossingTraversal"/> reads its entry/exit.</param>
        public CarFollowing(float travelSign)
        {
            this.travelSign = travelSign < 0f ? -1f : 1f;
        }

        /// <summary>
        /// Given the follower's current along-coordinate, the along-coordinate it
        /// intends to reach this tick, and its immediate leader's along-coordinate
        /// (or null when no truck is ahead on this segment), returns the
        /// along-coordinate the follower may actually advance to: the full target
        /// on an open road, else clamped to one car length behind the leader — and
        /// held in place entirely while the one-second start-up delay runs after a
        /// stopped leader begins to move.
        /// </summary>
        public float Advance(float currentAlong, float targetAlong, float? leaderAlong, float deltaTime)
        {
            if (!leaderAlong.HasValue)
            {
                // Open road ahead: no follow clamp, and the follow state resets so
                // a leader met later re-arms the start-up delay from a clean slate.
                previousLeaderAlong = null;
                leaderWasMoving = null;
                startUpTimerRemaining = 0f;
                return targetAlong;
            }

            var leader = leaderAlong.Value;

            // Movement of the leader since last tick, in the travel direction.
            // Null on the first observation (no baseline yet) so a leader that was
            // already cruising when first seen never arms a spurious start-up wait.
            bool? leaderMovingNow = previousLeaderAlong.HasValue
                ? (bool?)((leader - previousLeaderAlong.Value) * travelSign > LeaderMotionEpsilon)
                : null;

            // A stopped leader that begins moving arms the follower's reaction delay.
            if (leaderMovingNow == true && leaderWasMoving == false)
            {
                startUpTimerRemaining = StartUpDelaySeconds;
            }

            if (leaderMovingNow.HasValue)
            {
                leaderWasMoving = leaderMovingNow.Value;
            }

            previousLeaderAlong = leader;

            var cap = leader - travelSign * GapMeters;

            if (startUpTimerRemaining > 0f)
            {
                startUpTimerRemaining -= deltaTime;
                // Hold in place through the reaction delay (never past the gap).
                return ClampAhead(currentAlong, cap);
            }

            return ClampAhead(targetAlong, cap);
        }

        // Clamps a value so it never passes cap in the travel direction.
        private float ClampAhead(float value, float cap)
        {
            return travelSign > 0f
                ? (value < cap ? value : cap)
                : (value > cap ? value : cap);
        }
    }
}
