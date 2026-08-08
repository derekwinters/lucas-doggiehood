namespace Doggiehood.Core.Interaction
{
    /// <summary>#670: the single outcome a house tap can produce.</summary>
    public enum HouseTapOutcome
    {
        /// <summary>The house has bugs on it: the tap is a spray.</summary>
        Spray,

        /// <summary>The house is clear: the tap opens its profile.</summary>
        OpenProfile,
    }

    /// <summary>
    /// #670 (absorbing #667): arbitrates a house tap into exactly one outcome.
    ///
    /// <c>HouseView.Tapped</c> used to fan out to two independent subscribers —
    /// QuestDirector's spray handler and WorldBootstrap's open-profile handler
    /// — and <em>both</em> fired on every tap, with nothing deciding between
    /// them. Tapping a bugged house therefore sprayed it and opened its profile
    /// panel on top of the result. Under the input authority's R3 (exclusive
    /// delivery) that becomes one consumer resolving one outcome, and this is
    /// the engine-free predicate it resolves with.
    ///
    /// Derek's call (2026-08-07) is the "whole house" reading: while a house has
    /// bugs, tapping anywhere on it sprays, and the profile is unreachable for
    /// that house until it's clear. That keeps the pest-control spec's "the
    /// house itself is the tap target… no separate spray tool or aiming" rule
    /// literally true, and it is why the bug swarm needs no tap zone of its own
    /// (it stays collider-free feedback).
    /// </summary>
    public static class HouseTapArbiter
    {
        /// <summary>Which single thing a tap on this house does.</summary>
        /// <param name="hasPendingSpray">Whether the house currently holds an
        /// accepted, not-yet-sprayed pest-control quest
        /// (<c>QuestManager.IsAwaitingSpray</c>).</param>
        public static HouseTapOutcome Resolve(bool hasPendingSpray)
            => hasPendingSpray ? HouseTapOutcome.Spray : HouseTapOutcome.OpenProfile;
    }
}
