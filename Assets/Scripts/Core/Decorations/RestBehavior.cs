using System;
using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Decorations
{
    /// <summary>
    /// Autonomous comfort use (#52): a wandering dog whose house has a
    /// comfort decoration periodically settles onto it by itself — no
    /// player trigger. Dogs without one never enter Rest.
    /// </summary>
    public static class RestBehavior
    {
        /// <summary>Chance per tick that an eligible dog lies down.</summary>
        public const double RestChancePerTick = 0.05;

        public static void Tick(Dog dog, GameState state, Random rng)
        {
            if (dog.State != DogState.IdleWander)
            {
                return;
            }

            var hasComfort = state.Decorations.Any(d =>
                d.HouseId == dog.HouseId && ComfortDecorations.ItemNames.Contains(d.ItemName));

            if (!hasComfort)
            {
                return;
            }

            if (rng.NextDouble() < RestChancePerTick)
            {
                dog.TryRest(comfortDecorationSelected: true);
            }
        }
    }
}
