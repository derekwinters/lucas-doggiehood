using Doggiehood.Core.Quests;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #602: shared builder for the flat hollow-ring (annulus) mesh behind the
    /// red target highlight — used by both the lost-item finder glow
    /// (<see cref="LostItemView"/>) and the onboarding target-house highlight
    /// (<see cref="OnboardingHouseHighlightView"/>). Before #602 both drew the
    /// highlight from a paper-thin <c>Cylinder</c> primitive, which renders as a
    /// FILLED disc from above and paints over whatever sits beneath it. A Unity
    /// primitive can't be hollow, so a true ring outline needs a generated mesh:
    /// a band of quads between an inner and outer radius, lying flat in the XZ
    /// plane. Factoring it here keeps the two highlights visually identical —
    /// same segment count, same hole ratio — so they can't drift apart (the
    /// issue's "both indicators stay visually consistent" requirement).
    ///
    /// <para>The mesh is a UNIT-diameter ring (outer radius
    /// <see cref="OuterRadius"/> = 0.5, matching the default Cylinder primitive's
    /// radius) so callers keep the exact same diameter-valued localScale they
    /// used for the old disc: scaling the object by the ring diameter gives an
    /// outer edge of that diameter, so the ring's footprint is unchanged and
    /// only the middle opens up. The hole is sized from the shared
    /// <see cref="LostItemGlow"/> inner/outer ratio (#161), so every ring gets
    /// the same proportional opening regardless of its overall size.</para>
    /// </summary>
    public static class GroundRingMesh
    {
        /// <summary>Outer radius of the generated unit ring — 0.5 so the mesh
        /// spans a diameter of 1, matching the default Unity <c>Cylinder</c>
        /// primitive the pre-#602 disc scaled. Callers therefore keep their
        /// existing diameter-valued localScale unchanged. Named per #161.</summary>
        public const float OuterRadius = 0.5f;

        /// <summary>How many segments approximate the ring's circle — enough to
        /// read as a smooth outline in graybox. Named per #161.</summary>
        private const int SegmentCount = 48;

        /// <summary>Number of vertices per segment (one on the outer edge, one on
        /// the inner edge).</summary>
        private const int VerticesPerSegment = 2;

        /// <summary>Triangle indices per segment: two triangles for the top face
        /// and two for the bottom face, three indices each.</summary>
        private const int IndicesPerSegment = 12;

        /// <summary>Builds a flat annulus mesh in the XZ plane, centered at the
        /// origin. It is double-sided (top and bottom faces) so it reads under
        /// the game's fixed overhead camera pitch regardless of face culling.
        /// The hole radius is the shared <see cref="LostItemGlow"/> inner/outer
        /// ratio, so it matches across every highlight that shares this mesh.</summary>
        public static Mesh BuildAnnulus()
        {
            // #669: the hole ratio is read from the ONE shared Core constant
            // rather than recomputed here, because the sizing rule that has to
            // clear a target's footprint (TargetRingGeometry) reads the same
            // number — otherwise a future change to the hole would silently
            // break containment with no test failing.
            var innerRadius = OuterRadius * LostItemGlow.GroundRingInnerFraction;

            var vertices = new Vector3[SegmentCount * VerticesPerSegment];
            var normals = new Vector3[SegmentCount * VerticesPerSegment];
            var uv = new Vector2[SegmentCount * VerticesPerSegment];
            for (var i = 0; i < SegmentCount; i++)
            {
                var angle = (2f * Mathf.PI * i) / SegmentCount;
                var cos = Mathf.Cos(angle);
                var sin = Mathf.Sin(angle);

                vertices[(2 * i) + 0] = new Vector3(OuterRadius * cos, 0f, OuterRadius * sin);
                vertices[(2 * i) + 1] = new Vector3(innerRadius * cos, 0f, innerRadius * sin);
                normals[(2 * i) + 0] = Vector3.up;
                normals[(2 * i) + 1] = Vector3.up;
                uv[(2 * i) + 0] = new Vector2(i / (float)SegmentCount, 1f);
                uv[(2 * i) + 1] = new Vector2(i / (float)SegmentCount, 0f);
            }

            var triangles = new int[SegmentCount * IndicesPerSegment];
            var t = 0;
            for (var i = 0; i < SegmentCount; i++)
            {
                var next = (i + 1) % SegmentCount;
                var outerA = 2 * i;
                var innerA = (2 * i) + 1;
                var outerB = 2 * next;
                var innerB = (2 * next) + 1;

                // Top face.
                triangles[t++] = outerA;
                triangles[t++] = outerB;
                triangles[t++] = innerA;
                triangles[t++] = innerA;
                triangles[t++] = outerB;
                triangles[t++] = innerB;

                // Bottom face (reversed winding) so the flat ring is visible from
                // either side.
                triangles[t++] = outerA;
                triangles[t++] = innerA;
                triangles[t++] = outerB;
                triangles[t++] = outerB;
                triangles[t++] = innerA;
                triangles[t++] = innerB;
            }

            var mesh = new Mesh { name = "GroundRingAnnulus" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
