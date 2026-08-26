using Doggiehood.Core.Debugging;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Debugging
{
    /// <summary>
    /// #219: the on-device debug-toggle registry (name -> bool). Debug
    /// affordances register once and are read/flipped by the Debug tab; the
    /// registry must handle unknown toggle names safely (this is the standard
    /// all future debug affordances plug into).
    /// </summary>
    public class DebugToggleRegistryTests
    {
        [Test]
        public void RegisteredToggle_DefaultsToItsInitialValue()
        {
            var registry = new DebugToggleRegistry();
            registry.Register("fences", initialValue: false);

            Assert.That(registry.IsOn("fences"), Is.False);
        }

        [Test]
        public void Register_CanSeedAToggleOn()
        {
            var registry = new DebugToggleRegistry();
            registry.Register("fences", initialValue: true);

            Assert.That(registry.IsOn("fences"), Is.True);
        }

        [Test]
        public void Set_ChangesTheToggleState()
        {
            var registry = new DebugToggleRegistry();
            registry.Register("fences");

            registry.Set("fences", true);

            Assert.That(registry.IsOn("fences"), Is.True);
        }

        [Test]
        public void Toggle_FlipsAndReturnsTheNewState()
        {
            var registry = new DebugToggleRegistry();
            registry.Register("fences");

            Assert.That(registry.Toggle("fences"), Is.True);
            Assert.That(registry.IsOn("fences"), Is.True);
            Assert.That(registry.Toggle("fences"), Is.False);
            Assert.That(registry.IsOn("fences"), Is.False);
        }

        [Test]
        public void Changed_RaisesWithTheNameAndNewValueWhenAToggleFlips()
        {
            var registry = new DebugToggleRegistry();
            registry.Register("fences");

            string changedName = null;
            var changedValue = false;
            registry.Changed += (name, value) =>
            {
                changedName = name;
                changedValue = value;
            };

            registry.Toggle("fences");

            Assert.That(changedName, Is.EqualTo("fences"));
            Assert.That(changedValue, Is.True);
        }

        [Test]
        public void IsOn_ForAnUnknownToggle_IsFalse()
        {
            var registry = new DebugToggleRegistry();

            Assert.That(registry.IsOn("nope"), Is.False);
        }

        [Test]
        public void Set_OnAnUnknownToggle_IsANoOp_NotAnError()
        {
            var registry = new DebugToggleRegistry();

            Assert.DoesNotThrow(() => registry.Set("nope", true));
            Assert.That(registry.IsOn("nope"), Is.False,
                "an unregistered toggle stays absent (false), never silently created");
        }

        [Test]
        public void Toggle_OnAnUnknownToggle_IsASafeNoOp_ReturningFalse()
        {
            var registry = new DebugToggleRegistry();

            var result = false;
            Assert.DoesNotThrow(() => result = registry.Toggle("nope"));
            Assert.That(result, Is.False);
            Assert.That(registry.IsOn("nope"), Is.False);
        }

        [Test]
        public void Contains_ReportsWhetherAToggleIsRegistered()
        {
            var registry = new DebugToggleRegistry();
            registry.Register("fences");

            Assert.That(registry.Contains("fences"), Is.True);
            Assert.That(registry.Contains("nope"), Is.False);
        }

        [Test]
        public void Names_ListsEveryRegisteredToggle_InAStableOrder()
        {
            // #692: the bug-report snapshot renders one line per toggle, and it
            // must render the same bytes twice from the same state — a
            // Dictionary's enumeration order guarantees nothing.
            var registry = new DebugToggleRegistry();
            registry.Register("show-debug-element-colors", true);
            registry.Register("show-backyard-fences");

            Assert.That(registry.Names,
                Is.EqualTo(new[] { "show-backyard-fences", "show-debug-element-colors" }));
        }

        [Test]
        public void Names_IsEmptyForAFreshRegistry()
        {
            Assert.That(new DebugToggleRegistry().Names, Is.Empty);
        }
    }
}
