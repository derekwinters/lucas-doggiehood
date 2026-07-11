using System.Collections.Generic;

namespace Doggiehood.Core.Dogs
{
    public enum ConversationEnding
    {
        Accept,
        Complete,
    }

    /// <summary>
    /// A conversation with a dog (#33): a linear sequence of plain text
    /// lines closed by exactly one action. Deliberately nothing more — lines
    /// are raw strings and the ending is a single enum value, so the schema
    /// has no place to represent branching choices. Do not add choice/node
    /// members; a guard test enforces this shape.
    /// </summary>
    public sealed class Conversation
    {
        public IReadOnlyList<string> Lines { get; }
        public ConversationEnding Ending { get; }

        public Conversation(IReadOnlyList<string> lines, ConversationEnding ending)
        {
            Lines = lines;
            Ending = ending;
        }
    }
}
