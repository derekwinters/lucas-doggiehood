using Doggiehood.Core.Art;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #740: the graybox backyard pool a delivered "pool" gift leaves in a
    /// dog's yard — a gray open-topped shell (<see cref="PoolShellMesh"/>)
    /// with a blue water surface inset within it and sitting slightly below
    /// the rim, at the position Core assigned
    /// (<see cref="PoolPlacement.TryPositionFor"/>).
    ///
    /// <para>Thin wiring only: every dimension is a Core constant and the two
    /// colours are named <see cref="Palette"/> entries, the same way
    /// <see cref="DecorationView"/> picks its graybox colours. A stand-in for
    /// a real low-poly model later, like the graybox decorations and the
    /// lost-item sphere.</para>
    ///
    /// <para>Purely decorative: no collider, so it is neither a tap target nor
    /// an obstacle — and it can't quietly swallow taps meant for what is
    /// underneath it (the #703 delivered-package lesson). Dogs are not blocked
    /// by it and there is no swim/enter behavior.</para>
    /// </summary>
    public sealed class PoolView : MonoBehaviour
    {
        /// <summary>World-object name prefix, one container per house:
        /// "Pool - N". Mirrors <c>WorldBuilder.FenceNamePrefix</c> so the
        /// build/refresh paths and tests can find a house's pool by
        /// name.</summary>
        public const string NamePrefix = "Pool - ";

        /// <summary>Child name of the gray outer shell.</summary>
        public const string ShellName = "Shell";

        /// <summary>Child name of the blue interior ("water") surface.</summary>
        public const string WaterName = "Water";

        /// <summary>Unity's <c>Cylinder</c> primitive is TWO units tall (and
        /// one unit in diameter), so a height-valued localScale.y has to be
        /// halved. Named per #161.</summary>
        private const float UnityCylinderPrimitiveHeight = 2f;

        /// <summary>Unity's Standard shader, used for the graybox
        /// materials.</summary>
        private const string StandardShaderName = "Standard";

        /// <summary>Which house's yard this pool stands in, so a re-sync can
        /// tell which yards already have theirs (mirrors
        /// <see cref="DecorationView.Decoration"/>).</summary>
        public int HouseId { get; private set; }

        public static PoolView Spawn(int houseId, GridPoint position, Transform parent)
        {
            var go = new GameObject(NamePrefix + houseId);
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(position.X, 0f, position.Z);

            BuildShell(go.transform);
            BuildWater(go.transform);

            var view = go.AddComponent<PoolView>();
            view.HouseId = houseId;
            return view;
        }

        /// <summary>The gray outer surface: the generated open-topped ring
        /// wall scaled to the pool's outer diameter and height. Open at the
        /// top so the blue interior beneath the rim actually shows.</summary>
        private static void BuildShell(Transform parent)
        {
            var shell = new GameObject(ShellName);
            shell.AddComponent<MeshFilter>().sharedMesh = PoolShellMesh.BuildOpenShell();
            shell.AddComponent<MeshRenderer>();
            shell.transform.SetParent(parent, worldPositionStays: false);
            shell.transform.localPosition = Vector3.zero;
            shell.transform.localScale = new Vector3(
                PoolPlacement.PoolOuterDiameter,
                PoolPlacement.PoolHeight,
                PoolPlacement.PoolOuterDiameter);
            Paint(shell, CoreColors.FromHex(Palette.PoolShellHex));
        }

        /// <summary>The blue interior: a plain cylinder inset within the shell
        /// wall, its surface sitting <see cref="PoolPlacement.PoolWaterDropBelowRim"/>
        /// below the rim so the pool reads as an open container from the
        /// game's fixed camera angle.</summary>
        private static void BuildWater(Transform parent)
        {
            var water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            water.name = WaterName;
            water.transform.SetParent(parent, worldPositionStays: false);
            water.transform.localScale = new Vector3(
                PoolPlacement.PoolInnerDiameter,
                PoolPlacement.PoolWaterSurfaceHeight / UnityCylinderPrimitiveHeight,
                PoolPlacement.PoolInnerDiameter);
            water.transform.localPosition = new Vector3(
                0f, PoolPlacement.PoolWaterSurfaceHeight / 2f, 0f);
            StripCollider(water);
            Paint(water, CoreColors.FromHex(Palette.PoolWaterHex));
        }

        /// <summary>CreatePrimitive hands out a collider for free; the pool is
        /// decoration, never a tap target or an obstacle, so it gives it
        /// straight back. Mode-aware teardown (#157): Destroy is deferred in
        /// edit mode, so edit-time callers need DestroyImmediate.</summary>
        private static void StripCollider(GameObject part)
        {
            var collider = part.GetComponent<Collider>();
            if (collider == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(collider);
            }
            else
            {
                DestroyImmediate(collider);
            }
        }

        private static void Paint(GameObject part, Color color)
        {
            var renderer = part.GetComponent<Renderer>();
            var material = new Material(Shader.Find(StandardShaderName));
            material.color = color;
            renderer.sharedMaterial = material;
        }
    }
}
