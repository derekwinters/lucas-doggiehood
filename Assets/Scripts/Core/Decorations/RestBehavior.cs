using System;
using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Decorations
{
    /// <summary>
    /// Autonomous comfort use (#52, #112): a wandering dog whose house has a
    /// comfort decoration periodically decides — on its own, no player
    /// trigger — to walk over to it and settle down. The decision produces a
    /// <see cref="RestApproach"/> the dog follows over the walk network; it
    /// enters the Rest pose only on arrival, never by teleport. Dogs without
    /// a comfort decoration never approach or rest.
    /// </summary>
    public static class RestBehavior
    {
        /// <summary>Chance per tick that an eligible dog decides to walk over
        /// to its comfort item (#52). The roll gates when an APPROACH starts,
        /// not when the dog is instantly placed into the Rest pose (#112).</summary>
        public const double RestChancePerTick = 0.05;

        /// <summary>
        /// #112: on a successful <see cref="RestChancePerTick"/> roll, begins a
        /// real walk-over from <paramref name="dogPosition"/> to a comfort
        /// decoration in the dog's yard via the walk network, returning the
        /// <see cref="RestApproach"/> to advance frame by frame. Returns null
        /// when the dog isn't eligible (not wandering, or its house has no
        /// comfort decoration) or the roll fails — no state change, no
        /// teleport. The dog enters the Rest pose only once the returned
        /// approach reports <see cref="RestApproach.HasArrived"/>.
        /// </summary>
        public static RestApproach TryBeginApproach(
            Dog dog, GameState state, GridPoint dogPosition, WalkNetwork network, Random rng)
        {
            if (dog.State != DogState.IdleWander)
            {
                return null;
            }

            var comfort = state.Decorations.FirstOrDefault(d =>
                d.HouseId == dog.HouseId && ComfortDecorations.ItemNames.Contains(d.ItemName));

            if (comfort == null)
            {
                return null;
            }

            if (rng.NextDouble() >= RestChancePerTick)
            {
                return null;
            }

            return RestApproach.Begin(dogPosition, comfort, network);
        }
    }
}
