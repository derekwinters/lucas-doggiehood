using System;
using System.Linq;
using System.Reflection;
using Doggiehood.Core.Tuning;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Tuning
{
    /// <summary>
    /// #622: the engine-free descriptor table the debug tuning menu renders
    /// from. The overlay must build its slider set from <b>Core</b> — never a
    /// hand-maintained UI list that can drift from <see cref="TuningConfig"/> —
    /// so these tests reflect over the real config type and assert the catalog
    /// covers it exactly, field for field, with a usable range around each
    /// shipping default.
    /// </summary>
    public class TuningCatalogTests
    {
        private static FieldInfo[] ConfigFields()
        {
            return typeof(TuningConfig)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .ToArray();
        }

        [Test]
        public void EveryTuningConfigField_HasExactlyOneDescriptor()
        {
            var described = TuningCatalog.Fields.Select(f => f.FieldName).ToList();
            var actual = ConfigFields().Select(f => f.Name).ToList();

            var undescribed = actual.Except(described).ToList();
            Assert.That(undescribed, Is.Empty,
                "TuningConfig fields with no TuningCatalog descriptor: " + string.Join(", ", undescribed));

            var orphaned = described.Except(actual).ToList();
            Assert.That(orphaned, Is.Empty,
                "TuningCatalog descriptors pointing at no TuningConfig field: " + string.Join(", ", orphaned));

            Assert.That(described.Distinct().Count(), Is.EqualTo(described.Count),
                "a TuningConfig field is described more than once");
        }

        [Test]
        public void EveryDescriptor_DeclaresItsFieldsNumericKind()
        {
            foreach (var field in ConfigFields())
            {
                var descriptor = TuningCatalog.Fields.Single(d => d.FieldName == field.Name);
                var isIntegerField = field.FieldType == typeof(int);

                Assert.That(descriptor.IsInteger, Is.EqualTo(isIntegerField),
                    field.Name + ": descriptor IsInteger must match the declared field type");
            }
        }

        [Test]
        public void EveryDescriptor_HasALabelAGroupAndAUsableRange()
        {
            foreach (var descriptor in TuningCatalog.Fields)
            {
                Assert.That(descriptor.Label, Is.Not.Null.And.Not.Empty, descriptor.FieldName + ": label");
                Assert.That(descriptor.Unit, Is.Not.Null, descriptor.FieldName + ": unit (may be empty, never null)");
                Assert.That(Enum.IsDefined(typeof(TuningGroup), descriptor.Group), Is.True, descriptor.FieldName + ": group");
                Assert.That(descriptor.Max, Is.GreaterThan(descriptor.Min), descriptor.FieldName + ": max > min");
                Assert.That(descriptor.Step, Is.GreaterThan(0d), descriptor.FieldName + ": step > 0");
            }
        }

        [Test]
        public void EveryShippingDefault_SitsInsideItsDeclaredRange()
        {
            // A default outside its own slider range would snap to a different
            // value the instant the panel is built — a silent balance change.
            var defaults = new TuningConfig();

            foreach (var descriptor in TuningCatalog.Fields)
            {
                var value = descriptor.Read(defaults);
                Assert.That(value, Is.GreaterThanOrEqualTo(descriptor.Min), descriptor.FieldName + ": default below min");
                Assert.That(value, Is.LessThanOrEqualTo(descriptor.Max), descriptor.FieldName + ": default above max");
            }
        }

        [Test]
        public void ReadAndWrite_RoundTripThroughTheRealField()
        {
            var config = new TuningConfig();

            foreach (var descriptor in TuningCatalog.Fields)
            {
                // Pick a target strictly inside the range, snapped to the step,
                // so the write is not swallowed by clamping.
                var midpoint = descriptor.Clamp((descriptor.Min + descriptor.Max) / 2d);
                descriptor.Write(config, midpoint);

                Assert.That(descriptor.Read(config), Is.EqualTo(midpoint).Within(1e-9),
                    descriptor.FieldName + ": write then read must round-trip");
            }
        }

        [Test]
        public void Write_ClampsToTheDeclaredRange()
        {
            var config = new TuningConfig();

            foreach (var descriptor in TuningCatalog.Fields)
            {
                descriptor.Write(config, descriptor.Max * 10d + 1000d);
                Assert.That(descriptor.Read(config), Is.EqualTo(descriptor.Max).Within(1e-9),
                    descriptor.FieldName + ": above-range write must clamp to max");

                descriptor.Write(config, descriptor.Min - 1000d);
                Assert.That(descriptor.Read(config), Is.EqualTo(descriptor.Min).Within(1e-9),
                    descriptor.FieldName + ": below-range write must clamp to min");
            }
        }

        [Test]
        public void Write_RoundsIntegerFieldsToWholeNumbers()
        {
            var config = new TuningConfig();
            var payout = TuningCatalog.Fields.Single(f => f.FieldName == nameof(TuningConfig.QuestPayout));

            payout.Write(config, 33.4d);

            Assert.That(config.QuestPayout, Is.EqualTo(33));
            Assert.That(payout.Read(config), Is.EqualTo(33d));
        }

        [Test]
        public void Groups_AreTheWireframesFourGroupsInDisplayOrder()
        {
            // docs/specs/ui/debug-tuning-menu.md: "the four groups in order:
            // Pacing, Economy, Expansion, Move-in".
            Assert.That(TuningCatalog.Groups, Is.EqualTo(new[]
            {
                TuningGroup.Pacing,
                TuningGroup.Economy,
                TuningGroup.Expansion,
                TuningGroup.MoveIn,
            }));
        }

        [Test]
        public void EveryGroup_HasAtLeastOneFieldAndADisplayName()
        {
            foreach (var group in TuningCatalog.Groups)
            {
                Assert.That(TuningCatalog.FieldsIn(group), Is.Not.Empty, group + ": no fields");
                Assert.That(TuningCatalog.DisplayName(group), Is.Not.Null.And.Not.Empty, group + ": display name");
            }

            Assert.That(TuningCatalog.DisplayName(TuningGroup.MoveIn), Is.EqualTo("Move-in"));
        }

        [Test]
        public void FieldsIn_PartitionsTheWholeCatalog()
        {
            var partitioned = TuningCatalog.Groups.SelectMany(g => TuningCatalog.FieldsIn(g)).ToList();

            Assert.That(partitioned.Count, Is.EqualTo(TuningCatalog.Fields.Count));
            CollectionAssert.AreEquivalent(TuningCatalog.Fields, partitioned);
        }
    }
}
