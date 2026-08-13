using System;
using System.Collections.Generic;

namespace Doggiehood.Core.Debugging
{
    /// <summary>
    /// The on-device debug-toggle registry (#219): a name -> bool map of
    /// debug affordances surfaced in the settings Debug tab. This establishes
    /// the standard — new debug switches <see cref="Register"/> here and are
    /// read/flipped through the tab rather than being wired as temporary code
    /// edits. Unknown toggle names are handled safely (never throw, never
    /// silently created), so a stale UI reference can't corrupt state.
    /// Engine-free Core logic; the Unity layer binds each toggle's effect.
    /// </summary>
    public sealed class DebugToggleRegistry
    {
        private readonly Dictionary<string, bool> toggles = new Dictionary<string, bool>();

        /// <summary>Raised when a registered toggle's value changes, with the
        /// toggle name and its new value — the Unity layer subscribes to
        /// apply each toggle's effect (e.g. driving a build seam).</summary>
        public event Action<string, bool> Changed;

        /// <summary>Registers a toggle with an initial value (idempotent —
        /// re-registering resets it to <paramref name="initialValue"/>).</summary>
        public void Register(string name, bool initialValue = false)
        {
            toggles[name] = initialValue;
        }

        /// <summary>#692: every registered toggle name, in a stable ordinal
        /// order. A <see cref="Dictionary{TKey,TValue}"/> has no guaranteed
        /// enumeration order, so this sorts — a bug-report snapshot
        /// (<see cref="Doggiehood.Core.Diagnostics.DiagnosticReport"/>) must
        /// render the same bytes twice from the same state.</summary>
        public IReadOnlyList<string> Names
        {
            get
            {
                var names = new List<string>(toggles.Keys);
                names.Sort(StringComparer.Ordinal);
                return names;
            }
        }

        /// <summary>True if the named toggle is registered.</summary>
        public bool Contains(string name)
        {
            return toggles.ContainsKey(name);
        }

        /// <summary>The toggle's current value; false for an unknown name.</summary>
        public bool IsOn(string name)
        {
            return toggles.TryGetValue(name, out var value) && value;
        }

        /// <summary>Sets a registered toggle's value, raising
        /// <see cref="Changed"/> when it actually changes. A no-op for an
        /// unknown name — an unregistered toggle is never created here.</summary>
        public void Set(string name, bool value)
        {
            if (!toggles.TryGetValue(name, out var current))
            {
                return;
            }

            if (current == value)
            {
                return;
            }

            toggles[name] = value;
            Changed?.Invoke(name, value);
        }

        /// <summary>Flips a registered toggle and returns its new value.
        /// A safe no-op returning false for an unknown name.</summary>
        public bool Toggle(string name)
        {
            if (!toggles.TryGetValue(name, out var current))
            {
                return false;
            }

            var next = !current;
            toggles[name] = next;
            Changed?.Invoke(name, next);
            return next;
        }
    }
}
