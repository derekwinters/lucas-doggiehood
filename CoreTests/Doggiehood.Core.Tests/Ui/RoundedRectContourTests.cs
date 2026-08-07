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

        // ---------------------------------------------------------------------
        // #616 follow-up (PR #642): the measurement itself must be robust to how
        // Unity samples the baked chrome textures. CI showed the ray-march
        // checker over-reading a provably-constant band (7.7384px vs W=6 at
        // 114 deg, radius 12) because (a) Texture2D.GetPixelBilinear places
        // texel centres on the integer grid — no half-texel offset — so the
        // sampled field is displaced by half a pixel, and (b) the
        // finite-difference normal collapses against the clamped texture border
        // near a small sprite's strip-meet, so the march crosses the band
        // obliquely. These tests replicate that exact sampling model engine-free
        // and require the geometry-aware measurement to stay flat where the
        // alpha-gradient measurement is corrupted.
        // ---------------------------------------------------------------------

        // CandyChromeUgui's baked sprite: a small solid centre strip keeps the
        // 9-slice grid non-degenerate; straight edges reach the texture border.
        private const int BakeCenterStripPx = 4;

        // The EditMode pixel test's exclusion margin: rays closer than this to
        // an axis run along the straight 9-slice strips / border-clamped texels.
        private const double CornerOffAxisMarginDeg = 24.0;

        [Test]
        public void Coverage_IsTheAntiAliasedRampOfTheSignedDistance()
        {
            // The bake writes clamp01(0.5 - SDF) per texel; Coverage is that
            // exact analytic expectation, shared with the Unity per-texel test.
            Assert.That(RoundedRectContour.Coverage(0.0, 0.0, PipRadius, PipRadius, PipRadius),
                Is.EqualTo(1.0), "deep inside is fully covered");
            Assert.That(RoundedRectContour.Coverage(PipRadius, 0.0, PipRadius, PipRadius, PipRadius),
                Is.EqualTo(0.5).Within(1e-9), "a texel centred on the contour is half covered");
            Assert.That(RoundedRectContour.Coverage(PipRadius + 0.5, 0.0, PipRadius, PipRadius, PipRadius),
                Is.EqualTo(0.0), "half a pixel outside the contour the ramp reaches zero");
            Assert.That(RoundedRectContour.Coverage(PipRadius - 0.25, 0.0, PipRadius, PipRadius, PipRadius),
                Is.EqualTo(0.75).Within(1e-9), "the ramp is linear in the signed distance");
        }

        [Test]
        public void MeasureBandWidths_WithKnownGeometry_IsFlatUnderTheUnityBilinearSampler()
        {
            // The fixed measurement — texel-centre-compensated sampling plus the
            // analytic contour normal from the known geometry — must read a flat
            // band across every tested corner angle for both chrome radii, with
            // real headroom inside the +/-1px AA tolerance.
            AssertGeometryMeasurementIsFlat(12);
            AssertGeometryMeasurementIsFlat(40);
        }

        [Test]
        public void MeasureBandWidths_AlphaGradientNormals_OverReadNearTheStripMeet_TheArtifactPr642Hit()
        {
            // Regression documentation: under the faithful Unity sampling model
            // (integer-grid texel centres, clamped border), the alpha-gradient
            // measurement over-reads the radius-12 band at 114 deg — reproducing
            // the 7.7384px CI failure — even though the band is analytically
            // constant. This is why the EditMode test now measures along the
            // analytic contour normal instead.
            var fill = BakeChromeAlpha(12);
            var ink = BakeChromeAlpha(12 + (int)BandWidth);
            var fillAlpha = UncompensatedSampler(fill);
            var inkAlpha = UncompensatedSampler(ink);

            const int angleCount = 720;
            var maxMarch = 12 * 2.0 + BandWidth + 8.0;
            var widths = RoundedRectContour.MeasureBandWidths(
                fillAlpha, inkAlpha, 0.0, 0.0, maxMarch, angleCount);

            var at114 = widths[angleCount * 114 / 360];
            Assert.That(at114, Is.EqualTo(7.7384).Within(0.05),
                "the sampling-artifact over-read PR #642's CI observed has changed — " +
                "re-verify the faithful-sampler model against a real Unity run");
        }

        private static void AssertGeometryMeasurementIsFlat(int radius)
        {
            var fill = BakeChromeAlpha(radius);
            var ink = BakeChromeAlpha(radius + (int)BandWidth);
            var fillAlpha = CompensatedSampler(fill);
            var inkAlpha = CompensatedSampler(ink);

            const int angleCount = 720;
            var half = (radius * 2 + BakeCenterStripPx) / 2.0;
            var maxMarch = radius * 2.0 + BandWidth + 8.0;
            var widths = RoundedRectContour.MeasureBandWidths(
                fillAlpha, inkAlpha, 0.0, 0.0, maxMarch, angleCount,
                half, half, radius);

            for (var k = 0; k < angleCount; k++)
            {
                var deg = 360.0 * k / angleCount;
                var mod = deg % 90.0;
                var offAxis = Math.Min(mod, 90.0 - mod);
                if (offAxis < CornerOffAxisMarginDeg)
                {
                    continue; // straight strip / border-clamped texels — covered per-texel instead
                }

                Assert.That(widths[k], Is.EqualTo(BandWidth).Within(0.5),
                    "geometry-normal band width drifts at " + deg + " deg (radius " + radius + ")");
            }
        }

        /// <summary>Replicates <c>CandyChromeUgui.RoundedRectCoverage</c>'s baked
        /// texture, including the Color32 byte quantisation: corners are quarter
        /// circles of <paramref name="radius"/>, straight edges reach the texture
        /// border (what lets a 9-slice sprite stretch).</summary>
        private static double[,] BakeChromeAlpha(int radius)
        {
            var side = radius * 2 + BakeCenterStripPx;
            var tex = new double[side, side];
            for (var y = 0; y < side; y++)
            {
                for (var x = 0; x < side; x++)
                {
                    var half = side / 2.0;
                    var coverage = RoundedRectContour.Coverage(
                        x + 0.5 - half, y + 0.5 - half, half, half, radius);
                    tex[y, x] = Math.Round(coverage * 255.0) / 255.0;
                }
            }

            return tex;
        }

        /// <summary>Faithful model of <c>Texture2D.GetPixelBilinear</c> on a
        /// clamped texture: texel centres sit on the <b>integer</b> coordinate
        /// grid (<c>fx = u * width</c>, no half-texel offset) — verified by the
        /// exact reproduction of PR #642's observed 7.7384px over-read.</summary>
        private static double UnityBilinear(double[,] tex, double u, double v)
        {
            var side = tex.GetLength(0);
            var fx = u * side;
            var fy = v * side;
            var x0 = (int)Math.Floor(fx);
            var y0 = (int)Math.Floor(fy);
            var tx = fx - x0;
            var ty = fy - y0;

            double At(int ix, int iy)
            {
                ix = Math.Min(Math.Max(ix, 0), side - 1);
                iy = Math.Min(Math.Max(iy, 0), side - 1);
                return tex[iy, ix];
            }

            var a = At(x0, y0) * (1.0 - tx) + At(x0 + 1, y0) * tx;
            var b = At(x0, y0 + 1) * (1.0 - tx) + At(x0 + 1, y0 + 1) * tx;
            return a * (1.0 - ty) + b * ty;
        }

        /// <summary>The EditMode test's original world→UV mapping — un-compensated
        /// for the integer-grid texel centres, so the sampled shape is displaced
        /// by half a texel.</summary>
        private static Func<double, double, double> UncompensatedSampler(double[,] tex)
        {
            var side = tex.GetLength(0);
            return (x, y) => UnityBilinear(tex, x / side + 0.5, y / side + 0.5);
        }

        /// <summary>The corrected mapping: aligns world (0,0) with the baked
        /// shape's centre under integer-grid texel centres.</summary>
        private static Func<double, double, double> CompensatedSampler(double[,] tex)
        {
            var side = tex.GetLength(0);
            return (x, y) => UnityBilinear(tex, (x - 0.5) / side + 0.5, (y - 0.5) / side + 0.5);
        }
    }
}
