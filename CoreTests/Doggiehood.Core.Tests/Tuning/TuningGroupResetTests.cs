using System.Linq;
using Doggiehood.Core.Tuning;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Tuning
{
    /// <summary>
    /// #622: the debug tuning menu offers two reset scopes (per the approved
    /// wireframe, docs/specs/ui/debug-tuning-menu.md) — a global "Reset all"
    /// and a per-group "Reset". #620 landed only the global one, so this covers
    /// the group-scoped seam: it must restore exactly that group's fields and
    /// leave every other group's live override alone.
    /// </summary>
    public class TuningGroupResetTests
    {
        private TuningConfig originalActive;

        [SetUp]
        public void CaptureActive()
        {
            originalActive = TuningConfig.Active;
            TuningConfig.Active = new TuningConfig();
        }

        [TearDown]
        public void RestoreActive()
        {
            TuningConfig.Active = originalActive;
            TuningConfig.ResetToDefaults();
        }

        [Test]
        public void ResetGroupToDefaults_RestoresOnlyThatGroupsFields()
        {
            var defaults = new TuningConfig();

            // Push EVERY field off its default, so the assertion below can tell
            // "restored" from "never moved".
            foreach (var field in TuningCatalog.Fields)
            {
                field.Write(TuningConfig.Active, Offset(field, defaults));
            }

            TuningConfig.ResetGroupToDefaults(TuningGroup.Economy);

            foreach (var field in TuningCatalog.Fields)
            {
                var live = field.Read(TuningConfig.Active);
                if (field.Group == TuningGroup.Economy)
                {
                    Assert.That(live, Is.EqualTo(field.Read(defaults)).Within(1e-9),
                        field.FieldName + ": Economy field must be back at its shipping default");
                }
                else
                {
                    Assert.That(live, Is.EqualTo(Offset(field, defaults)).Within(1e-9),
                        field.FieldName + ": non-Economy field must keep its live override");
                }
            }
        }

        [Test]
        public void ResetGroupToDefaults_MutatesTheActiveInstanceInPlace()
        {
            // Core seams that captured the Active instance must see the reset;
            // the group reset must not swap Active for a different object (that
            // is the global reset's job).
            var active = TuningConfig.Active;
            TuningConfig.Active.QuestPayout = 999;

            TuningConfig.ResetGroupToDefaults(TuningGroup.Economy);

            Assert.That(TuningConfig.Active, Is.SameAs(active));
            Assert.That(active.QuestPayout, Is.EqualTo(new TuningConfig().QuestPayout));
        }

        [Test]
        public void ResetGroupToDefaults_AcrossEveryGroup_RestoresTheWholeConfig()
        {
            var defaults = new TuningConfig();

            foreach (var field in TuningCatalog.Fields)
            {
                field.Write(TuningConfig.Active, Offset(field, defaults));
            }

            foreach (var group in TuningCatalog.Groups)
            {
                TuningConfig.ResetGroupToDefaults(group);
            }

            foreach (var field in TuningCatalog.Fields)
            {
                Assert.That(field.Read(TuningConfig.Active), Is.EqualTo(field.Read(defaults)).Within(1e-9),
                    field.FieldName + ": resetting every group must restore the whole config");
            }
        }

        [Test]
        public void ResetGroupToDefaults_DoesNotTouchATuningConfigOtherThanActive()
        {
            var untouched = new TuningConfig();
            untouched.QuestPayout = 12345;

            TuningConfig.ResetGroupToDefaults(TuningGroup.Economy);

            Assert.That(untouched.QuestPayout, Is.EqualTo(12345));
        }

        /// <summary>A value guaranteed to differ from <paramref name="defaults"/>
        /// for this field and to sit inside its declared range.</summary>
        private static double Offset(TuningField field, TuningConfig defaults)
        {
            var current = field.Read(defaults);
            var shifted = current + field.Step;
            if (shifted > field.Max)
            {
                shifted = current - field.Step;
            }

            return field.Clamp(shifted);
        }

        [Test]
        public void Offset_ActuallyMovesEveryField()
        {
            // Guards the helper above: if a range were ever one step wide the
            // "non-group fields kept their override" assertion would pass
            // vacuously.
            var defaults = new TuningConfig();
            var stuck = TuningCatalog.Fields
                .Where(f => Offset(f, defaults) == f.Read(defaults))
                .Select(f => f.FieldName)
                .ToList();

            Assert.That(stuck, Is.Empty, "fields whose range is too narrow to move: " + string.Join(", ", stuck));
        }
    }
}
