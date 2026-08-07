using System;
using Doggiehood.Core.Ui;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Ui
{
    /// <summary>
    /// #616: engine-free rounded-rectangle contour geometry behind the shared UI
    /// chrome's <b>constant-width outline band</b>. These tests encode the
    /// pixel-level invariant Derek asked for as a checkable spec: the outline is a
    /// band of constant width <c>W</c> around the true rounded-rect contour
    /// (corners included), replacing the offset-copy <c>Outline</c> mesh effect
    /// whose union of four shifted stamps bulges/dips around curves. The same
    /// <see cref="RoundedRectContour.MeasureBandWidths"/> ray-march checker is run
    /// by the Unity EditMode pixel test against the actually-baked chrome alpha.
    /// See docs/specs/ui/shared-components.md.
    /// </summary>
    public class RoundedRectContourTests
    {
        // Representative chrome shapes (docs/specs/ui/shared-components.md):
        // a level pip (circle), the `Lv N` pill (stadium), a panel corner.
        private const double PipRadius = 12.0;

        private const double PillHalfWidth = 120.0;
        private const double PillHalfHeight = 36.0; // == radius: fully-round caps
        private const double PillRadius = 36.0;

        private const double PanelHalf = 200.0;
        private const double PanelRadius = 40.0;

        // Shared outline band width (OutlineThicknessPx).
        private const double BandWidth = 6.0;

        private const double AntiAliasTolerancePx = 1.0;

        [Test]
        public void SignedDistance_IsZeroOnContour_NegativeInside_PositiveOutside()
        {
            // A circle of radius 12 centred at the origin.
            Assert.That(RoundedRectContour.SignedDistance(0.0, 0.0, PipRadius, PipRadius, PipRadius),
                Is.EqualTo(-PipRadius).Within(1e-6), "the centre is `radius` deep inside");
            Assert.That(RoundedRectContour.SignedDistance(PipRadius, 0.0, PipRadius, PipRadius, PipRadius),
                Is.EqualTo(0.0).Within(1e-6), "a point on the contour is at distance zero");
            Assert.That(RoundedRectContour.SignedDistance(PipRadius + 8.0, 0.0, PipRadius, PipRadius, PipRadius),
                Is.EqualTo(8.0).Within(1e-6), "8px outside the contour is +8");
        }

        [Test]
        public void FillRadius_OnACircle_IsConstantThroughEveryCornerArcAngle()
        {
            // A pip is all-corner: R(theta) must be the same at every angle — the
            // "constant R through the corner arc" invariant in its purest form.
            for (var i = 0; i < 360; i++)
            {
                var theta = i * Math.PI / 180.0;
                var r = RoundedRectContour.FillRadius(theta, PipRadius, PipRadius, PipRadius);
                Assert.That(r, Is.EqualTo(PipRadius).Within(1e-3),
                    "R(theta) drifts at " + i + " degrees — the pip contour is not a true circle");
            }
        }

        [Test]
        public void FillRadius_OnAPill_HasNoInwardDip_TracingASmoothContour()
        {
            // Sample R(theta) densely through a quadrant (long axis -> short axis,
            // across the whole corner arc). The defect being fixed is a local
            // *minimum* (the outline dipping inward). Assert no interior sample is
            // a strict local minimum.
            const int samples = 240;
            var radii = new double[samples];
            for (var i = 0; i < samples; i++)
            {
                var theta = (i / (double)(samples - 1)) * (Math.PI / 2.0);
                radii[i] = RoundedRectContour.FillRadius(theta, PillHalfWidth, PillHalfHeight, PillRadius);
            }

            for (var i = 1; i < samples - 1; i++)
            {
                var dip = radii[i] < radii[i - 1] - 1e-4 && radii[i] < radii[i + 1] - 1e-4;
                Assert.That(dip, Is.False,
                    "R(theta) dips inward at sample " + i + " — a flat/indented corner, the #616 defect");
            }
        }

        [Test]
        public void CornerArc_PointsAreEquidistantFromTheCornerCentre_Round_NotFlat()
        {
            // Through the top-right corner arc of a panel, every contour point must
            // sit exactly `radius` from the corner centre — a true rounded corner,
            // not the flattened/indented union of offset copies.
            var cornerCx = PanelHalf - PanelRadius;
            var cornerCy = PanelHalf - PanelRadius;

            // Angles that fall on the top-right corner arc (between the straight
            // edges' tangent points).
            var startAngle = Math.Atan2(PanelHalf, cornerCx); // where the right edge meets the arc
            var endAngle = Math.Atan2(cornerCy, PanelHalf);    // symmetric, top edge side
            var lo = Math.Min(startAngle, endAngle);
            var hi = Math.Max(startAngle, endAngle);

            const int samples = 80;
            for (var i = 0; i <= samples; i++)
            {
                var theta = lo + (hi - lo) * (i / (double)samples);
                var r = RoundedRectContour.FillRadius(theta, PanelHalf, PanelHalf, PanelRadius);
                var px = r * Math.Cos(theta);
                var py = r * Math.Sin(theta);
                var toCorner = Math.Sqrt((px - cornerCx) * (px - cornerCx) + (py - cornerCy) * (py - cornerCy));
                Assert.That(toCorner, Is.EqualTo(PanelRadius).Within(1e-2),
                    "corner point at " + theta + " rad is not `radius` from the corner centre");
            }
        }

        [Test]
        public void PerpendicularBandWidth_IsConstantW_AcrossAllAngles_IncludingCorners()
        {
            AssertConstantBand(PipRadius, PipRadius, PipRadius);
            AssertConstantBand(PillHalfWidth, PillHalfHeight, PillRadius);
            AssertConstantBand(PanelHalf, PanelHalf, PanelRadius);
        }

        [Test]
        public void MeasureBandWidths_OnASampledCoverageGrid_IsFlatAroundTheWholePerimeter()
        {
            // The generic checker the EditMode pixel test reuses: it only sees the
            // rendered alpha (a coverage field), not the analytic SDF. Feed it the
            // fill coverage and the inflated ink coverage and confirm it reports a
            // flat band around the whole perimeter, corners included.
            AssertGridBand(PipRadius, PipRadius, PipRadius);
            AssertGridBand(PillHalfWidth, PillHalfHeight, PillRadius);
            AssertGridBand(PanelHalf, PanelHalf, PanelRadius);
        }

        private static void AssertConstantBand(double hw, double hh, double r)
        {
            for (var i = 0; i < 720; i++)
            {
                var theta = i * Math.PI / 360.0;
                var w = RoundedRectContour.PerpendicularBandWidth(theta, hw, hh, r, BandWidth);
                Assert.That(w, Is.EqualTo(BandWidth).Within(1e-2),
                    "band width varies at " + (i * 0.5) + " degrees for shape (" + hw + "," + hh + "," + r + ")");
            }
        }

        private static void AssertGridBand(double hw, double hh, double r)
        {
            Func<double, double, double> fill = (x, y) =>
                Coverage(RoundedRectContour.SignedDistance(x, y, hw, hh, r));
            Func<double, double, double> ink = (x, y) =>
                Coverage(RoundedRectContour.SignedDistance(x, y, hw + BandWidth, hh + BandWidth, r + BandWidth));

            var maxMarch = hw + hh + r + BandWidth + 8.0;
            var widths = RoundedRectContour.MeasureBandWidths(fill, ink, 0.0, 0.0, maxMarch, 720);

            foreach (var w in widths)
            {
                Assert.That(w, Is.EqualTo(BandWidth).Within(AntiAliasTolerancePx),
                    "sampled band width is outside the +/-1px AA tolerance for shape (" + hw + "," + hh + "," + r + ")");
            }
        }

        private static double Coverage(double signedDistance)
        {
            var c = 0.5 - signedDistance;
            if (c < 0.0) return 0.0;
            if (c > 1.0) return 1.0;
            return c;
        }
    }
}
