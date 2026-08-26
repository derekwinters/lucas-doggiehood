using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #740: the graybox backyard pool's gray outer shell — an OPEN-TOPPED
    /// ring wall (outer wall + top rim + inner wall), generated rather than
    /// built from a <c>Cylinder</c> primitive.
    ///
    /// <para>A Unity primitive can't be hollow: a capped gray cylinder would
    /// hide the blue interior beneath it entirely, and the pool would read as
    /// a solid gray drum instead of Derek's "gray outer surface, and blue
    /// interior that is slightly lower than the rest of the cylinder". Same
    /// reason <see cref="GroundRingMesh"/> was generated for the ring
    /// highlight (#602).</para>
    ///
    /// <para>The mesh is a UNIT-diameter, UNIT-height shell — outer radius
    /// <see cref="OuterRadius"/> = 0.5, spanning y in [0, 1] so it stands on
    /// the ground with no offset — so a caller scales it by
    /// <c>(diameter, height, diameter)</c>. The wall thickness comes from
    /// Core's own <see cref="PoolPlacement.PoolInnerDiameter"/> /
    /// <see cref="PoolPlacement.PoolOuterDiameter"/> ratio rather than a
    /// second figure here, so the blue interior always fits the opening
    /// exactly (the #669 one-shared-constant precedent).</para>
    /// </summary>
    public static class PoolShellMesh
    {
        /// <summary>Outer radius of the generated unit shell — 0.5 so the mesh
        /// spans a diameter of 1, matching the default Unity <c>Cylinder</c>
        /// primitive's radius, so callers pass a diameter-valued localScale.
        /// Named per #161.</summary>
        public const float OuterRadius = 0.5f;

        /// <summary>Bottom of the shell in mesh-local units — ground level, so
        /// a scaled shell stands on the lawn without a Y offset.</summary>
        public const float BaseY = 0f;

        /// <summary>Top (rim) of the shell in mesh-local units: 1, so the
        /// caller's Y scale IS the pool's height.</summary>
        public const float RimY = 1f;

        /// <summary>How many segments approximate the shell's circle — enough
        /// to read as a round pool in graybox, matching
        /// <see cref="GroundRingMesh"/>'s smoothness. Named per #161.</summary>
        private const int SegmentCount = 48;

        /// <summary>Vertical bands the shell is built from: the outer wall,
        /// the flat top rim, and the inner wall.</summary>
        private const int BandCount = 3;

        /// <summary>Each band contributes two vertices per segment (one on
        /// each of its two edges).</summary>
        private const int VerticesPerSegment = 2;

        /// <summary>Triangle indices per segment per band: two triangles for
        /// the band's front face and two for its back face, three indices
        /// each. Every band is double-sided — like
        /// <see cref="GroundRingMesh"/>'s annulus — so the shell reads under
        /// the game's fixed camera pitch regardless of face culling.</summary>
        private const int IndicesPerSegment = 12;

        /// <summary>Builds the open-topped shell, centred on the origin in XZ
        /// and standing from <see cref="BaseY"/> to <see cref="RimY"/>. The
        /// interior above the water is genuinely open — no geometry spans the
        /// middle — so the blue surface beneath the rim is visible.</summary>
        public static Mesh BuildOpenShell()
        {
            var innerRadius = OuterRadius
                * (PoolPlacement.PoolInnerDiameter / PoolPlacement.PoolOuterDiameter);

            var vertexCount = BandCount * SegmentCount * VerticesPerSegment;
            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uv = new Vector2[vertexCount];
            var triangles = new int[BandCount * SegmentCount * IndicesPerSegment];

            var v = 0;
            var t = 0;
            for (var band = 0; band < BandCount; band++)
            {
                var bandStart = v;
                for (var i = 0; i < SegmentCount; i++)
                {
                    var angle = (2f * Mathf.PI * i) / SegmentCount;
                    var cos = Mathf.Cos(angle);
                    var sin = Mathf.Sin(angle);
                    var u = i / (float)SegmentCount;

                    Vector3 near;
                    Vector3 far;
                    Vector3 normal;
                    switch (band)
                    {
                        case 0: // Outer wall: rim edge down to the ground.
                            near = new Vector3(OuterRadius * cos, RimY, OuterRadius * sin);
                            far = new Vector3(OuterRadius * cos, BaseY, OuterRadius * sin);
                            normal = new Vector3(cos, 0f, sin);
                            break;
                        case 1: // Flat top rim: outer edge in to the opening.
                            near = new Vector3(OuterRadius * cos, RimY, OuterRadius * sin);
                            far = new Vector3(innerRadius * cos, RimY, innerRadius * sin);
                            normal = Vector3.up;
                            break;
                        default: // Inner wall: opening edge down to the ground.
                            near = new Vector3(innerRadius * cos, RimY, innerRadius * sin);
                            far = new Vector3(innerRadius * cos, BaseY, innerRadius * sin);
                            normal = new Vector3(-cos, 0f, -sin);
                            break;
                    }

                    vertices[v] = near;
                    normals[v] = normal;
                    uv[v] = new Vector2(u, 1f);
                    v++;

                    vertices[v] = far;
                    normals[v] = normal;
                    uv[v] = new Vector2(u, 0f);
                    v++;
                }

                for (var i = 0; i < SegmentCount; i++)
                {
                    var next = (i + 1) % SegmentCount;
                    var nearA = bandStart + (VerticesPerSegment * i);
                    var farA = nearA + 1;
                    var nearB = bandStart + (VerticesPerSegment * next);
                    var farB = nearB + 1;

                    // Front face.
                    triangles[t++] = nearA;
                    triangles[t++] = nearB;
                    triangles[t++] = farA;
                    triangles[t++] = farA;
                    triangles[t++] = nearB;
                    triangles[t++] = farB;

                    // Back face (reversed winding), so the band is visible from
                    // either side under the fixed camera pitch.
                    triangles[t++] = nearA;
                    triangles[t++] = farA;
                    triangles[t++] = nearB;
                    triangles[t++] = nearB;
                    triangles[t++] = farA;
                    triangles[t++] = farB;
                }
            }

            var mesh = new Mesh { name = "PoolOpenShell" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
