using System;
using Doggiehood.Core.World;
using UnityEditor;
using UnityEngine;

namespace Doggiehood.Unity.Editor
{
    /// <summary>
    /// #126 bonus: door-position overlays for the real neighborhood houses,
    /// drawn in the Editor scene view only. A static [DrawGizmo] drawer
    /// rather than OnDrawGizmos on HouseView, so no gizmo code ships in the
    /// player assembly at all and HouseView stays logic-free. The position
    /// comes from the same Core API the game path and gallery use
    /// (HouseModel.FrontDoorWorldPosition) fed with the house's actual
    /// placed transform — never re-derived math.
    /// </summary>
    public static class HouseViewGizmos
    {
        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected, typeof(HouseView))]
        private static void DrawDoorOverlay(HouseView view, GizmoType gizmoType)
        {
            if (!TryGetDoorWorldPosition(view, out var door))
            {
                return;
            }

            Gizmos.color = new Color(0.9f, 0.15f, 0.15f);
            Gizmos.DrawSphere(door, 0.3f);
            Gizmos.DrawLine(door, door + Vector3.up * 2f);
        }

        /// <summary>
        /// Where the catalog says this placed house's front door is, read
        /// from the house's actual "Model" child transform (position, yaw,
        /// uniform scale) and Core's FrontDoorWorldPosition. False for
        /// graybox-fallback houses (no kit model transform to read) and for
        /// uninitialized views.
        /// </summary>
        public static bool TryGetDoorWorldPosition(HouseView view, out Vector3 door)
        {
            door = default;

            var visual = view.transform.Find("Model");
            if (visual == null)
            {
                return false;
            }

            HouseModel model;
            try
            {
                model = HouseModelCatalog.ForHouse(view.HouseId);
            }
            catch (ArgumentException)
            {
                return false;
            }

            var position = view.transform.position;
            var point = model.FrontDoorWorldPosition(
                new GridPoint(position.x, position.z),
                visual.eulerAngles.y,
                visual.lossyScale.x);

            door = new Vector3(point.X, 0f, point.Z);
            return true;
        }
    }
}
