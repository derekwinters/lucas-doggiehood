using System;

namespace Doggiehood.Core.Ui
{
    /// <summary>
    /// #616: engine-free rounded-rectangle contour geometry behind the shared UI
    /// chrome's <b>constant-width outline band</b>. The fill is a true rounded rect
    /// (an anti-aliased signed-distance bake); the outline must be a band of
    /// constant width <c>W</c> around that same contour — corners included —
    /// rather than Unity's <c>Outline</c> mesh effect, whose union of four
    /// diagonally-offset copies bulges on-axis and dips off-axis around curves.
    ///
    /// <para>This class turns "looks uneven" into a checkable spec:
    /// <see cref="FillRadius"/> gives the fill contour radius <c>R(theta)</c> per
    /// ray, <see cref="PerpendicularBandWidth"/> proves the inflate-by-W underlay
    /// yields a constant band analytically, and <see cref="MeasureBandWidths"/> is
    /// the generic ray-march checker the Unity EditMode pixel test reuses against
    /// the actually-baked chrome alpha. All plain <see cref="Math"/> — no engine
    /// dependency (rule #2). See docs/specs/ui/shared-components.md.</para>
    /// </summary>
    public static class RoundedRectContour
    {
        // Coverage crosses this alpha at the anti-aliased contour edge.
        private const double AlphaThreshold = 0.5;

        // Bisection depth: 60 halvings resolve any bracket to far below a pixel.
        private const int BisectionSteps = 60;

        // Finite-difference step (px) for estimating the fill-alpha gradient
        // (the local contour normal) in the grid checker.
        private const double GradientStepPx = 0.5;

        /// <summary>Signed distance from <c>(px, py)</c> to a rounded rectangle
        /// centred at the origin with outer half-extents
        /// <c>(halfWidth, halfHeight)</c> and corner radius <paramref name="radius"/>.
        /// Negative inside, zero on the contour, positive outside — the standard
        /// rounded-box SDF, a true Euclidean distance field outside the shape.</summary>
        public static double SignedDistance(double px, double py, double halfWidth, double halfHeight, double radius)
        {
            var qx = Math.Abs(px) - (halfWidth - radius);
            var qy = Math.Abs(py) - (halfHeight - radius);
            var outside = Math.Sqrt(Math.Max(qx, 0.0) * Math.Max(qx, 0.0) + Math.Max(qy, 0.0) * Math.Max(qy, 0.0));
            var inside = Math.Min(Math.Max(qx, qy), 0.0);
            return outside + inside - radius;
        }

        /// <summary>Anti-aliased coverage of a texel centred at <c>(px, py)</c>
        /// (shape-centre coordinates): the linear ramp
        /// <c>clamp01(0.5 - SignedDistance)</c> — exactly what the chrome bake
        /// writes per texel. Shared analytic expectation for the Unity per-texel
        /// bake-fidelity test, which ties the actually-baked chrome alpha to this
        /// geometry so the constant-band proof transfers to the bake.</summary>
        public static double Coverage(double px, double py, double halfWidth, double halfHeight, double radius)
        {
            var c = AlphaThreshold - SignedDistance(px, py, halfWidth, halfHeight, radius);
            if (c < 0.0)
            {
                return 0.0;
            }

            return c > 1.0 ? 1.0 : c;
        }

        /// <summary>Radial distance from the shape centre along
        /// <paramref name="angleRadians"/> to the fill contour
        /// (<see cref="SignedDistance"/> == 0), found by bisection. The shape is
        /// convex and contains the centre, so the ray crosses the contour exactly
        /// once.</summary>
        public static double FillRadius(double angleRadians, double halfWidth, double halfHeight, double radius)
        {
            var ux = Math.Cos(angleRadians);
            var uy = Math.Sin(angleRadians);
            var hi = (halfWidth + halfHeight + radius) * 2.0 + 1.0;
            return BisectDistanceToLevel(0.0, 0.0, ux, uy, 0.0, hi,
                s => SignedDistance(s * ux, s * uy, halfWidth, halfHeight, radius));
        }

        /// <summary>Perpendicular width of the ink band at the fill-contour point
        /// for <paramref name="angleRadians"/>: the ink underlay is the fill shape
        /// inflated by <paramref name="bandWidth"/> (a Minkowski expansion — extents
        /// and radius each grow by W), so its outer contour is the fill SDF's
        /// <c>bandWidth</c> level set. Marching from the fill contour point along the
        /// outward normal (unit SDF gradient) to that level set returns exactly W
        /// for every angle, corners included — the invariant the fix guarantees.</summary>
        public static double PerpendicularBandWidth(double angleRadians, double halfWidth, double halfHeight, double radius, double bandWidth)
        {
            var r = FillRadius(angleRadians, halfWidth, halfHeight, radius);
            var px = r * Math.Cos(angleRadians);
            var py = r * Math.Sin(angleRadians);

            // Outward normal = normalized SDF gradient (points away from the shape).
            var gx = SignedDistance(px + GradientStepPx, py, halfWidth, halfHeight, radius)
                     - SignedDistance(px - GradientStepPx, py, halfWidth, halfHeight, radius);
            var gy = SignedDistance(px, py + GradientStepPx, halfWidth, halfHeight, radius)
                     - SignedDistance(px, py - GradientStepPx, halfWidth, halfHeight, radius);
            var glen = Math.Sqrt(gx * gx + gy * gy);
            if (glen < 1e-9)
            {
                return 0.0;
            }

            var nx = gx / glen;
            var ny = gy / glen;
            var hi = bandWidth * 2.0 + 2.0;
            return BisectDistanceToLevel(px, py, nx, ny, 0.0, hi,
                s => SignedDistance(px + s * nx, py + s * ny, halfWidth, halfHeight, radius) - bandWidth);
        }

        /// <summary>Generic ray-march band checker used by the Unity EditMode pixel
        /// test. Given a fill coverage field and an ink coverage field (alpha in
        /// [0,1], sampled from the actually-baked chrome textures), for each of
        /// <paramref name="angleCount"/> rays from <c>(centerX, centerY)</c> it
        /// finds the fill edge (fill alpha crosses <see cref="AlphaThreshold"/>),
        /// then measures the perpendicular distance from there to the ink outer edge
        /// (ink alpha crosses the threshold) along the local fill-alpha normal.
        /// Returns the band width per angle; a correct constant-width outline yields
        /// a flat array within the anti-aliasing tolerance.</summary>
        public static double[] MeasureBandWidths(
            Func<double, double, double> fillAlpha,
            Func<double, double, double> inkAlpha,
            double centerX, double centerY, double maxMarchRadius, int angleCount)
        {
            if (fillAlpha == null) throw new ArgumentNullException(nameof(fillAlpha));

            // Local outward normal from the fill-alpha gradient (alpha decreases
            // outward, so the outward normal is -grad).
            return MeasureBandWidthsAlongNormals(fillAlpha, inkAlpha, centerX, centerY, maxMarchRadius, angleCount,
                (double px, double py, double ux, double uy, out double nx, out double ny) =>
                {
                    var gx = fillAlpha(px + GradientStepPx, py) - fillAlpha(px - GradientStepPx, py);
                    var gy = fillAlpha(px, py + GradientStepPx) - fillAlpha(px, py - GradientStepPx);
                    var glen = Math.Sqrt(gx * gx + gy * gy);
                    if (glen < 1e-9)
                    {
                        nx = ux;
                        ny = uy;
                    }
                    else
                    {
                        nx = -gx / glen;
                        ny = -gy / glen;
                    }
                });
        }

        /// <summary>Geometry-aware variant of
        /// <see cref="MeasureBandWidths(Func{double,double,double},Func{double,double,double},double,double,double,int)"/>:
        /// marches from each measured fill-edge point along the <b>analytic</b>
        /// outward normal of the known rounded-rect contour
        /// (<paramref name="halfWidth"/>, <paramref name="halfHeight"/>,
        /// <paramref name="radius"/>, centred at <c>(centerX, centerY)</c>)
        /// instead of a finite-difference alpha gradient. The checker verifies a
        /// shape whose intended geometry is known, so the march direction need
        /// not be estimated from the sampled alpha — which goes wrong where a
        /// small 9-slice bake's corner arc hugs the clamped texture border and
        /// bilinear samples straddle it (the PR #642 over-read).</summary>
        public static double[] MeasureBandWidths(
            Func<double, double, double> fillAlpha,
            Func<double, double, double> inkAlpha,
            double centerX, double centerY, double maxMarchRadius, int angleCount,
            double halfWidth, double halfHeight, double radius)
        {
            return MeasureBandWidthsAlongNormals(fillAlpha, inkAlpha, centerX, centerY, maxMarchRadius, angleCount,
                (double px, double py, double ux, double uy, out double nx, out double ny) =>
                {
                    // Outward normal = normalized SDF gradient of the intended
                    // contour, evaluated at the measured fill-edge point (the
                    // normal field is smooth around the contour, so the sub-pixel
                    // gap between measured and analytic edge is immaterial).
                    var gx = SignedDistance(px - centerX + GradientStepPx, py - centerY, halfWidth, halfHeight, radius)
                             - SignedDistance(px - centerX - GradientStepPx, py - centerY, halfWidth, halfHeight, radius);
                    var gy = SignedDistance(px - centerX, py - centerY + GradientStepPx, halfWidth, halfHeight, radius)
                             - SignedDistance(px - centerX, py - centerY - GradientStepPx, halfWidth, halfHeight, radius);
                    var glen = Math.Sqrt(gx * gx + gy * gy);
                    if (glen < 1e-9)
                    {
                        nx = ux;
                        ny = uy;
                    }
                    else
                    {
                        nx = gx / glen;
                        ny = gy / glen;
                    }
                });
        }

        // The outward march direction at a measured fill-edge point; (ux, uy) is
        // the ray direction to fall back on where a gradient degenerates.
        private delegate void OutwardNormal(double px, double py, double ux, double uy, out double nx, out double ny);

        private static double[] MeasureBandWidthsAlongNormals(
            Func<double, double, double> fillAlpha,
            Func<double, double, double> inkAlpha,
            double centerX, double centerY, double maxMarchRadius, int angleCount,
            OutwardNormal outwardNormal)
        {
            if (fillAlpha == null) throw new ArgumentNullException(nameof(fillAlpha));
            if (inkAlpha == null) throw new ArgumentNullException(nameof(inkAlpha));

            var widths = new double[angleCount];
            for (var k = 0; k < angleCount; k++)
            {
                var theta = 2.0 * Math.PI * k / angleCount;
                var ux = Math.Cos(theta);
                var uy = Math.Sin(theta);

                // Fill edge along the ray: alpha goes from ~1 at the centre to ~0
                // outside, so (threshold - alpha) crosses zero once.
                var rFill = BisectDistanceToLevel(centerX, centerY, ux, uy, 0.0, maxMarchRadius,
                    s => AlphaThreshold - fillAlpha(centerX + s * ux, centerY + s * uy));
                var pFillX = centerX + rFill * ux;
                var pFillY = centerY + rFill * uy;

                outwardNormal(pFillX, pFillY, ux, uy, out var nx, out var ny);

                // Perpendicular march from the fill edge to the ink outer edge.
                widths[k] = BisectDistanceToLevel(pFillX, pFillY, nx, ny, 0.0, maxMarchRadius,
                    s => AlphaThreshold - inkAlpha(pFillX + s * nx, pFillY + s * ny));
            }

            return widths;
        }

        /// <summary>Bisects along the ray <c>(ox,oy) + s*(dx,dy)</c> for the
        /// distance <c>s</c> in <c>[loInit, hiInit]</c> where <paramref name="f"/>
        /// crosses zero. Assumes <c>f(lo) &lt;= 0 &lt;= f(hi)</c> (the field increases
        /// outward).</summary>
        private static double BisectDistanceToLevel(
            double ox, double oy, double dx, double dy, double loInit, double hiInit, Func<double, double> f)
        {
            var lo = loInit;
            var hi = hiInit;
            for (var i = 0; i < BisectionSteps; i++)
            {
                var mid = (lo + hi) * 0.5;
                if (f(mid) <= 0.0)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            return (lo + hi) * 0.5;
        }
    }
}
