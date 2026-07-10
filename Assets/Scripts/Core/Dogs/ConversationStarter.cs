namespace Doggiehood.Core.Dogs
{
    /// <summary>
    /// Opens conversations (#11): only reachable for a dog with an active
    /// quest — otherwise a null no-op. Real template-generated dialogue
    /// arrives with the quest system (#69, milestone 04); until then the
    /// lines are neutral placeholders.
    /// </summary>
    public static class ConversationStarter
    {
        public static Conversation TryOpen(Dog dog)
        {
            if (!dog.HasActiveQuest)
            {
                return null;
            }

            return new Conversation(
                new[]
                {
                    $"{dog.Name} has something to ask you.",
                    "(Quest dialogue arrives with the quest template system.)",
                },
                ConversationEnding.Accept);
        }
    }
}
