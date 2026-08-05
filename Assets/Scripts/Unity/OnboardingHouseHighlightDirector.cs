using Doggiehood.Core.Onboarding;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #571: scene-side glue that keeps the red highlight ring on exactly the
    /// onboarding "fix up a home" target house while the reward chain waits on its
    /// <see cref="OnboardingRewardStep.UpgradeHouse"/> step, and tears it down the
    /// instant the chain advances past it (the house is upgraded, or a reload
    /// where the chain is already past it). Mirrors
    /// <see cref="QuestDirector.RefreshBugSwarms"/>'s idempotent
    /// re-sync-on-poll shape.
    ///
    /// <para>Every decision stays in Core: whether to show and on which house both
    /// come from <see cref="OnboardingHouseHighlight"/> (which reads only the
    /// reward-chain step and the recorded target house id). Unlike the #506 coach
    /// bar, this is NOT suppressed while a centered profile panel is open — filling
    /// that exact gap is the point — so it takes no panel observer.</para>
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
        /// target house while the UpgradeHouse step is active, and none otherwise.
        /// Idempotent, so polling every frame (or a one-off call from a test) just
        /// converges. Public so EditMode tests can drive it without a running
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

            var houseTransform = FindHouseTransform(target.Value);
            if (houseTransform != null)
            {
                OnboardingHouseHighlightView.Spawn(target.Value, houseTransform, worldRoot);
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

        private static Transform FindHouseTransform(int houseId)
        {
            foreach (var view in Object.FindObjectsByType<HouseView>(FindObjectsSortMode.None))
            {
                if (view.HouseId == houseId)
                {
                    return view.transform;
                }
            }

            return null;
        }
    }
}
