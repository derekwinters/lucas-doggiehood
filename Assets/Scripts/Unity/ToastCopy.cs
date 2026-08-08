using System.Collections.Generic;
using System.Globalization;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.Onboarding;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #541/#675: the Unity-side copy assembly for the three toast triggers
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

        // The move-in arrival sentence (#675, Derek 2026-08-08). A named
        // household reads "<names> moved in!"; from MoveInFamilyThreshold dogs
        // up the names are dropped for MoveInFamilyLine, because three roster
        // names plus the payout overflows the pill's one-line text budget and
        // the toast clips rather than wraps (#578). The threshold and the line
        // are named constants (#161) and public so the fit guard can assert
        // against the real copy rather than a re-typed string.
        private const string MoveInSuffix = " moved in!";

        /// <summary>Households of this size or larger drop their names for
        /// <see cref="MoveInFamilyLine"/> — the width-driven copy branch.</summary>
        public const int MoveInFamilyThreshold = 3;

        /// <summary>The nameless arrival sentence for a household of
        /// <see cref="MoveInFamilyThreshold"/> dogs or more.</summary>
        public const string MoveInFamilyLine = "A new family moved in!";

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

        /// <summary>The move-in toast line (#675) — the arrival sentence + the
        /// flat move-in reward Core just deposited, e.g. "Biscuit moved in! +50
        /// coins". A one- or two-dog household is named using the
        /// <em>same</em> naming the welcome pop-up shows
        /// (<see cref="WelcomeMessage.NameLine"/>, so the two surfaces can never
        /// drift apart); a household of <see cref="MoveInFamilyThreshold"/> or
        /// more is announced with <see cref="MoveInFamilyLine"/> instead.</summary>
        public static string MoveIn(IReadOnlyList<Dog> household, int amount)
        {
            return MoveInAccomplishment(household) + " " + PayoutSuffix(amount);
        }

        /// <summary>The arrival sentence for a moved-in household: named for a
        /// small household, the nameless family line at
        /// <see cref="MoveInFamilyThreshold"/> dogs and up.</summary>
        private static string MoveInAccomplishment(IReadOnlyList<Dog> household)
        {
            if (household.Count >= MoveInFamilyThreshold)
            {
                return MoveInFamilyLine;
            }

            return WelcomeMessage.ForHousehold(household).NameLine + MoveInSuffix;
        }

        private static string PayoutSuffix(int amount)
        {
            return "+" + amount.ToString(CultureInfo.InvariantCulture) + " coins";
        }
    }
}
