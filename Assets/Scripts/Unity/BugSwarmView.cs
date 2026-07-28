using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// The visible bug problem on a house (#53/#157/#331): a bright, tall
    /// graybox pest marker that hovers clear of the affected house so a
    /// first-time player can immediately tell which house needs spraying. It
    /// is a wide ring of chunky pest cubes riding the top of a tall beacon
    /// column, positioned above the house's ACTUAL roofline (measured from the
    /// house's renderers) rather than a blind fixed height — so a tall roof
    /// can neither swallow it nor occlude it under the 45-degree ortho camera.
    /// Purely feedback — the house itself is the tap target (its tap routes to
    /// Core's SprayHouse); this view carries no collider and the director
    /// destroys it once the quest completes.
    ///
    /// #331 made this placeholder actually USABLE on device (the prior version
    /// — three 0.35m near-black cubes a blind 3m over the roof — read as
    /// nothing); real low-poly pest art swaps in via #334.
    /// </summary>
    public sealed class BugSwarmView : MonoBehaviour
    {
        // Readable graybox sizing (#331). Deliberately bigger, brighter, and
        // taller than the old near-invisible indicator; guarded by the
        // legibility test in QuestDirectorTests.
        private const int BugCount = 5;
        private const float BugSize = 0.6f;
        private const float SpreadRadius = 1.1f;

        // A tall marker column, so part of the indicator always pokes into
        // open sky above the roofline — the fix for "a small overhead marker
        // the roof hides." The column rests RoofClearance above the measured
        // roof and rises BeaconHeight to the swarm ring.
        private const float RoofClearance = 0.6f;
        private const float BeaconHeight = 2.6f;
        private const float BeaconThickness = 0.28f;

        // Bright alert magenta — deliberately off the house/grass/sky palette
        // so it can't blend into the scene. Graybox; #334 restyles it.
        private static readonly Color PestAlertColor = new Color(1f, 0.25f, 0.7f);

        // Gentle runtime life so the swarm shimmers and draws the eye. Kept to
        // a vertical bob plus a spin about the column, so the marker never
        // drifts off its house in XZ (only runs while playing — EditMode tests
        // observe the spawned pose).
        private const float BobAmplitude = 0.18f;
        private const float BobSpeed = 2.2f;
        private const float SpinDegreesPerSecond = 45f;

        public int HouseId { get; private set; }

        private float restingY;
        private float bobPhase;

        public static BugSwarmView Spawn(int houseId, Transform houseTransform, Transform parent)
        {
            var root = new GameObject("BugSwarm - house " + houseId);
            root.transform.SetParent(parent);

            // Sit above the house's real roofline, not a blind fixed height, so
            // the marker is never sunk into or hidden by the roof mesh.
            var baseY = RoofTopOf(houseTransform) + RoofClearance;
            root.transform.position = new Vector3(
                houseTransform.position.x, baseY, houseTransform.position.z);

            // The beacon column: from just above the roof up to the swarm ring.
            var beacon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beacon.name = "Beacon";
            beacon.transform.SetParent(root.transform);
            beacon.transform.localScale = new Vector3(BeaconThickness, BeaconHeight, BeaconThickness);
            beacon.transform.localPosition = new Vector3(0f, BeaconHeight / 2f, 0f);
            MakeFeedbackOnly(beacon);
            Paint(beacon);

            // The pest swarm riding the top of the beacon.
            for (var i = 0; i < BugCount; i++)
            {
                var angle = (360f / BugCount) * i * Mathf.Deg2Rad;
                var bug = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bug.name = "Bug " + i;
                bug.transform.SetParent(root.transform);
                bug.transform.localScale = Vector3.one * BugSize;
                bug.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * SpreadRadius, BeaconHeight, Mathf.Sin(angle) * SpreadRadius);
                MakeFeedbackOnly(bug);
                Paint(bug);
            }

            var view = root.AddComponent<BugSwarmView>();
            view.HouseId = houseId;
            view.restingY = baseY;
            return view;
        }

        private void Update()
        {
            // Animation is presentation-only and stays out of tests, which
            // observe the deterministic spawned pose.
            if (!Application.isPlaying)
            {
                return;
            }

            bobPhase += Time.deltaTime;
            var position = transform.position;
            position.y = restingY + Mathf.Sin(bobPhase * BobSpeed) * BobAmplitude;
            transform.position = position;
            transform.Rotate(0f, SpinDegreesPerSecond * Time.deltaTime, 0f, Space.Self);
        }

        /// <summary>The world-space top of the house's mesh (its roofline), so
        /// the marker can be placed clear of it. Falls back to the house's own
        /// Y if the house has no renderers yet.</summary>
        private static float RoofTopOf(Transform houseTransform)
        {
            var renderers = houseTransform.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return houseTransform.position.y;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds.max.y;
        }

        /// <summary>Feedback only: the marker must never intercept the tap
        /// meant for the house beneath it (the house stays the tap target).</summary>
        private static void MakeFeedbackOnly(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
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

        private static void Paint(GameObject go)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(renderer.sharedMaterial) { color = PestAlertColor };
        }
    }
}
