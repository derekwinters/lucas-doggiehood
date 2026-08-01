namespace Doggiehood.Core.Dogs
{
    /// <summary>
    /// Opens conversations (#11): only reachable for a dog with an active
    /// quest — otherwise a null no-op. Real template-generated dialogue
    /// arrives with the quest system (#69, milestone 04); these lines are
    /// neutral placeholders. Since #472, <see cref="Doggiehood.Unity"/>'s
    /// ConversationPresenter renders real Core dialogue for both Available
    /// (opener/closer offer) and Accepted (contextual reminder) quests, so
    /// this placeholder is now only a defensive fallback for the
    /// no-GameState path and is never shown for a dog with a real Core quest.
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
