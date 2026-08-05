using System.Globalization;
using Doggiehood.Core.Onboarding;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #541: the Unity-side copy assembly for the two toast triggers
    /// (docs/specs/ui/toast.md → "Per-toast copy"). Copy stays OUT of engine-free
    /// Core (rule #2): Core's completion events name only the quest/step + the
    /// flat amount, and this turns that into the exact approved one-line message
    /// the toast shows — the accomplishment sentence followed by the payout as
    /// "+N coins", joined by a single space.
    ///
    /// <para>The four onboarding accomplishment lines are reused verbatim from the
    /// retired <see cref="OnboardingRewardCopy"/> (approved #374); only the
    /// surface changed, not the words, with each step's "+N coins" now folded into
    /// the single toast line.</para>
    /// </summary>
    public static class ToastCopy
    {
        // The fixed lead-in for the general quest-completion toast — the dynamic
        // part is only the payout (toast.md: "Quest complete! +N coins").
        private const string QuestCompletePrefix = "Quest complete!";

        /// <summary>The quest-completion toast line — "Quest complete! +N coins",
        /// N being the flat quest payout Core just deposited.</summary>
        public static string QuestComplete(int amount)
        {
            return QuestCompletePrefix + " " + PayoutSuffix(amount);
        }

        /// <summary>The onboarding reward-chain step toast line — the step's
        /// approved accomplishment sentence + "+N coins" (e.g. "You finished your
        /// first quest! +100 coins"). Delegates the accomplishment sentence to the
        /// shared <see cref="OnboardingRewardCopy"/> table so the four lines stay
        /// single-sourced.</summary>
        public static string OnboardingStep(OnboardingRewardStep step, int amount)
        {
            return OnboardingRewardCopy.MessageFor(step) + " " + PayoutSuffix(amount);
        }

        private static string PayoutSuffix(int amount)
        {
            return "+" + amount.ToString(CultureInfo.InvariantCulture) + " coins";
        }
    }
}
