using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Art;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// Automated low-poly style gate (#6): every mesh under the art asset
    /// folders must stay within ArtBudget.MaxTrianglesPerMesh, and every
    /// material must use an allowlisted low-poly-friendly shader. Runs over
    /// whatever assets exist, so it starts enforcing the moment the first
    /// real art lands.
    /// </summary>
    public class LowPolyBudgetTests
    {
        private static readonly string[] ArtFolders =
        {
            "Assets/Art",
        };

        private static readonly string[] AllowedShaderPrefixes =
        {
            "Standard",
            "Unlit/",
            "Mobile/",
            "UI/",
            "Sprites/",
        };

        private static IEnumerable<string> AssetPathsUnderArtFolders(string filter)
        {
            return ArtFolders
                .Where(AssetDatabase.IsValidFolder)
                .SelectMany(folder => AssetDatabase.FindAssets(filter, new[] { folder }))
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct();
        }

        [Test]
        public void EveryMeshInArtFolders_StaysWithinTheTriangleBudget()
        {
            var offenders = new List<string>();

            foreach (var path in AssetPathsUnderArtFolders("t:Mesh t:Model t:GameObject"))
            {
                foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
                {
                    var triangles = mesh.triangles.Length / 3;
                    if (!ArtBudget.IsWithinBudget(triangles))
                    {
                        offenders.Add($"{path} ({mesh.name}): {triangles} tris > {ArtBudget.MaxTrianglesPerMesh}");
                    }
                }
            }

            Assert.That(offenders, Is.Empty,
                "Meshes exceed the low-poly budget:\n" + string.Join("\n", offenders));
        }

        [Test]
        public void EveryMaterialInArtFolders_UsesAnAllowlistedShader()
        {
            var offenders = new List<string>();

            foreach (var path in AssetPathsUnderArtFolders("t:Material"))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null)
                {
                    continue;
                }

                if (!AllowedShaderPrefixes.Any(prefix => material.shader.name.StartsWith(prefix)))
                {
                    offenders.Add($"{path}: shader '{material.shader.name}' is not in the low-poly allowlist");
                }
            }

            Assert.That(offenders, Is.Empty,
                "Materials use non-allowlisted shaders:\n" + string.Join("\n", offenders));
        }
    }
}
