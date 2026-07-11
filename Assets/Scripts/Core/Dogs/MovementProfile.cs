namespace Doggiehood.Core.Dogs
{
    /// <summary>
    /// Per-personality movement parameters (#89): mood is conveyed through
    /// walk speed and turn pattern. MVP implements the Excited pattern
    /// (fast, long straight stretches); every other personality —
    /// explicitly including Grumpy, whose shuffle is deferred — uses base.
    /// Parameters are looked up from Personality only, never per-dog.
    /// </summary>
    public readonly struct MovementProfile
    {
        /// <summary>Walk speed in meters per second.</summary>
        public float Speed { get; }

        /// <summary>Chance per step of changing direction.</summary>
        public float TurnProbability { get; }

        public MovementProfile(float speed, float turnProbability)
        {
            Speed = speed;
            TurnProbability = turnProbability;
        }

        public static MovementProfile Base
        {
            get { return new MovementProfile(1.2f, 0.35f); }
        }

        public static MovementProfile ForPersonality(Personality personality)
        {
            switch (personality)
            {
                case Personality.Excited:
                    return new MovementProfile(2.2f, 0.08f);
                default:
                    return Base;
            }
        }
    }
}
