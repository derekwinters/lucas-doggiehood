using Doggiehood.Core.Onboarding;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #571/#668: scene-side glue that keeps the red highlight ring on exactly the
    /// thing the onboarding reward chain wants tapped next, and tears it down the
    /// instant the chain advances past that step. Mirrors
    /// <see cref="QuestDirector.RefreshBugSwarms"/>'s idempotent
    /// re-sync-on-poll shape.
    ///
    /// <para>Two steps carry a ring, and the director treats them identically —
    /// the "fix up a home" target house
    /// (<see cref="OnboardingRewardStep.UpgradeHouse"/>, #571) and the
    /// "build a new house" target lot
    /// (<see cref="OnboardingRewardStep.BuildHouse"/>, #668). The only Unity-side
    /// difference is which scene object carries the id: a built house is a
    /// <see cref="HouseView"/>, an unbuilt lot is an <see cref="EmptyLotView"/>
    /// foundation slab. Both are just a transform with renderer bounds as far as
    /// the ring is concerned, so there is one view, one mesh and one sizing rule
    /// rather than a second parallel highlight system.</para>
    ///
    /// <para>Every decision stays in Core: whether to show and on which target both
    /// come from <see cref="OnboardingHouseHighlight"/> (which reads only the
    /// reward-chain step, the recorded upgrade target id, and the live map — no
    /// persisted build-step state, #469). Unlike the #506 coach bar, this is NOT
    /// suppressed while a centered panel is open — filling that exact gap is the
    /// point, and the build step opens one every time (#406) — so it takes no
    /// panel observer.</para>
    /// </summary>
    public sealed class OnboardingHouseHighlightDirector : MonoBehaviour
    {
        public GameState State { get; private set; }

        private Transform worldRoot;

        public void Init(GameState state, Transform worldRoot)
        {
            State = state;
            this.worldRoot = worldRoot;
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        /// <summary>Re-syncs the highlight to Core: ensures exactly one ring on the
        /// target house/lot while a ringed step is active, and none otherwise.
        /// Idempotent, so polling every frame (or a one-off call from a test) just
        /// converges — which is also what picks the ring up on the build step, whose
        /// target lot only appears in the scene once its tile is unlocked and
        /// rendered. Public so EditMode tests can drive it without a running
        /// player loop, mirroring <see cref="OnboardingOverlay.Poll"/>.</summary>
        public void Refresh()
        {
            var target = OnboardingHouseHighlight.TargetHouseId(State);

            var existing = Object.FindObjectsByType<OnboardingHouseHighlightView>(FindObjectsSortMode.None);
            var alreadyCorrect = false;
            foreach (var view in existing)
            {
                if (target.HasValue && view.HouseId == target.Value && !alreadyCorrect)
                {
                    alreadyCorrect = true; // keep exactly one correct highlight
                }
                else
                {
                    DestroyView(view.gameObject);
                }
            }

            if (!target.HasValue || alreadyCorrect)
            {
                return;
            }

            var targetTransform = FindTargetTransform(target.Value);
            if (targetTransform != null)
            {
                OnboardingHouseHighlightView.Spawn(target.Value, targetTransform, worldRoot);
            }
        }

        /// <summary>Mode-aware destroy so EditMode tests see the highlight gone at
        /// once (DestroyImmediate), Destroy under Play — mirroring
        /// <see cref="QuestDirector.RefreshBugSwarms"/>.</summary>
        private static void DestroyView(GameObject go)
        {
            if (Application.isPlaying)
            {
                Destroy(go);
            }
            else
            {
                DestroyImmediate(go);
            }
        }

        /// <summary>The scene transform carrying <paramref name="houseId"/>: a
        /// built house's <see cref="HouseView"/> (the #571 upgrade target) or an
        /// unbuilt lot's <see cref="EmptyLotView"/> foundation slab (the #668 build
        /// target). House ids and frontier lot ids share one id space, and a lot
        /// never has both views at once — it is a marker until it is built, then a
        /// house — so the two lookups can't collide. Null when the target isn't in
        /// the scene yet, which just means the next Refresh tries again.</summary>
        private static Transform FindTargetTransform(int houseId)
        {
            foreach (var view in Object.FindObjectsByType<HouseView>(FindObjectsSortMode.None))
            {
                if (view.HouseId == houseId)
                {
                    return view.transform;
                }
            }

            foreach (var lot in Object.FindObjectsByType<EmptyLotView>(FindObjectsSortMode.None))
            {
                if (lot.HouseId == houseId)
                {
                    return lot.transform;
                }
            }

            return null;
        }
    }
}
