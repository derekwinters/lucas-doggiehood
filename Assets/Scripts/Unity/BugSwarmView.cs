using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// The visible bug problem on a house (#53/#157): a small graybox swarm
    /// of dark cubes hovering over the affected house so the player can see
    /// which house needs spraying. Purely feedback — the house itself is the
    /// tap target (its tap routes to Core's SprayHouse); this view carries no
    /// collider and the director destroys it once the quest completes.
    /// Restyled into real pest art alongside the #65 UI pass.
    /// </summary>
    public sealed class BugSwarmView : MonoBehaviour
    {
        private const int BugCount = 3;
        private const float BugSize = 0.35f;
        private const float HoverHeight = 3f;
        private const float SpreadRadius = 0.8f;
        private static readonly Color BugColor = new Color(0.15f, 0.12f, 0.1f);

        public int HouseId { get; private set; }

        public static BugSwarmView Spawn(int houseId, Transform houseTransform, Transform parent)
        {
            var root = new GameObject("BugSwarm - house " + houseId);
            root.transform.SetParent(parent);
            root.transform.position = houseTransform.position + Vector3.up * HoverHeight;

            for (var i = 0; i < BugCount; i++)
            {
                var angle = (360f / BugCount) * i * Mathf.Deg2Rad;
                var bug = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bug.name = "Bug " + i;
                bug.transform.SetParent(root.transform);
                bug.transform.localScale = Vector3.one * BugSize;
                bug.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * SpreadRadius, 0f, Mathf.Sin(angle) * SpreadRadius);

                // Feedback only: the swarm must never intercept the tap meant
                // for the house beneath it.
                var collider = bug.GetComponent<Collider>();
                if (collider != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(collider);
                    }
                    else
                    {
                        DestroyImmediate(collider);
                    }
                }

                var renderer = bug.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = new Material(renderer.sharedMaterial) { color = BugColor };
            }

            var view = root.AddComponent<BugSwarmView>();
            view.HouseId = houseId;
            return view;
        }
    }
}
