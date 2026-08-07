using System;

namespace Doggiehood.Core.Tuning
{
    /// <summary>
    /// #622: one tunable's descriptive model — the label, unit, group and
    /// min/max/step range the debug tuning menu needs in order to render a
    /// slider for a <see cref="TuningConfig"/> field, plus the typed read/write
    /// accessors that move a value in and out of that field.
    ///
    /// <para>This is deliberately <b>Core, not Unity</b>. The approved
    /// wireframe (docs/specs/ui/debug-tuning-menu.md) states that "labels,
    /// ranges, defaults, and current values all come from that config" and that
    /// "each slider's min/max/step come from that field's declared range in
    /// <c>TuningConfig</c>, not from this layout" — so the whole descriptive
    /// model lives here, engine-free and unit-tested, and the Unity layer is a
    /// pure view over <see cref="TuningCatalog"/>. A hand-maintained list of
    /// sliders in the overlay could silently drift from Core; a reflection test
    /// over this catalog cannot.</para>
    ///
    /// <para>Every value crosses the boundary as a <see cref="double"/> — one
    /// slider shape for both the <c>int</c> and <c>double</c> fields — with
    /// <see cref="IsInteger"/> telling the view how to format it and
    /// <see cref="Clamp"/> snapping to the declared grid on the way in.</para>
    /// </summary>
    public sealed class TuningField
    {
        /// <summary>Decimal places a snapped value is rounded to, so repeated
        /// step-snapping cannot accumulate binary floating-point dust (which
        /// would show up as "0.30000000000000004" in the live value readout).
        /// Well below the finest step any tunable declares.</summary>
        private const int SnapDecimalPlaces = 6;

        private readonly Func<TuningConfig, double> read;
        private readonly Action<TuningConfig, double> write;

        public TuningField(
            string fieldName,
            string label,
            string unit,
            TuningGroup group,
            double min,
            double max,
            double step,
            bool isInteger,
            Func<TuningConfig, double> read,
            Action<TuningConfig, double> write)
        {
            FieldName = fieldName;
            Label = label;
            Unit = unit;
            Group = group;
            Min = min;
            Max = max;
            Step = step;
            IsInteger = isInteger;
            this.read = read;
            this.write = write;
        }

        /// <summary>The <see cref="TuningConfig"/> instance-field name this
        /// describes. The catalog's coverage test matches on it, so a renamed
        /// or newly added field fails the suite instead of quietly vanishing
        /// from the panel.</summary>
        public string FieldName { get; }

        /// <summary>Human-readable slider label, per the wireframe's control rows.</summary>
        public string Label { get; }

        /// <summary>Unit suffix shown after the live value ("coins", "h", "×",
        /// "dogs"…). Empty — never null — for a bare number.</summary>
        public string Unit { get; }

        /// <summary>Which of the wireframe's four groups this row sits in.</summary>
        public TuningGroup Group { get; }

        /// <summary>Slider minimum.</summary>
        public double Min { get; }

        /// <summary>Slider maximum.</summary>
        public double Max { get; }

        /// <summary>Slider increment (1 for whole-number fields).</summary>
        public double Step { get; }

        /// <summary>True when the underlying field is an <c>int</c>, so the
        /// view shows a whole number and the slider snaps to whole steps.</summary>
        public bool IsInteger { get; }

        /// <summary>Reads this field's current value out of
        /// <paramref name="config"/>.</summary>
        public double Read(TuningConfig config)
        {
            return read(config);
        }

        /// <summary>Writes <paramref name="value"/> into this field on
        /// <paramref name="config"/>, snapped to <see cref="Step"/> and clamped
        /// into <see cref="Min"/>..<see cref="Max"/> — so a slider drag can
        /// never push a balance value outside its declared range.</summary>
        public void Write(TuningConfig config, double value)
        {
            write(config, Clamp(value));
        }

        /// <summary>Snaps <paramref name="value"/> onto this field's step grid
        /// and clamps it into range. Idempotent, so a value that is already
        /// legal survives a round-trip unchanged.</summary>
        public double Clamp(double value)
        {
            var snapped = IsInteger
                ? Math.Round(value, MidpointRounding.AwayFromZero)
                : Math.Round(Min + Math.Round((value - Min) / Step) * Step, SnapDecimalPlaces);

            if (snapped < Min)
            {
                return Min;
            }

            return snapped > Max ? Max : snapped;
        }
    }
}
