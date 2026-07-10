using System.Collections.Generic;

namespace Doggiehood.Core.Dogs
{
    /// <summary>
    /// The registry of interactable character kinds (#37). For v1 this is
    /// dogs, full stop — no cats, people, or other animals. Houses remain
    /// tappable scenery (#20) but are not characters.
    /// </summary>
    public static class InteractableCharacters
    {
        public static IReadOnlyList<string> Kinds { get; } = new[] { "Dog" };
    }
}
