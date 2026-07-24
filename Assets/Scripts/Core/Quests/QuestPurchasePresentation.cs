using Doggiehood.Core.Dogs;
using Doggiehood.Core.Economy;

namespace Doggiehood.Core.Quests
{
    /// <summary>
    /// Pure presentation logic for the conversation panel's accept/option
    /// buttons (#186): what label to show for the accept pill or a
    /// decoration option, and whether it's currently affordable. Deliberately
    /// Unity-independent (no MonoBehaviour/UnityEngine types) so the
    /// interesting logic is covered by fast Core tests rather than living
    /// only in EditMode coverage — ConversationPresenter is just thin wiring
    /// on top of these queries.
    /// </summary>
    public static class QuestPurchasePresentation
    {
        private const string BuyLabelPrefix = "Buy";
        private const string AcceptLabelText = "Accept";
        private const string CompleteLabelText = "Complete";

        // "Buy · 40" per the approved conversation-panel wireframe (#175).
        private const string CostSeparator = " · ";

        /// <summary>The accept pill's label: "Buy · {cost}" for a buy-type
        /// quest, otherwise the existing Accept/Complete text.</summary>
        public static string AcceptLabel(Quest quest, ConversationEnding ending)
        {
            if (quest != null && quest.Cost.HasValue)
            {
                return FormatCost(BuyLabelPrefix, quest.Cost.Value);
            }

            return ending == ConversationEnding.Accept ? AcceptLabelText : CompleteLabelText;
        }

        /// <summary>Whether the wallet currently covers the quest's cost.
        /// A quest with no cost (or no wallet supplied) is always
        /// affordable.</summary>
        public static bool IsAcceptAffordable(Quest quest, Wallet wallet)
        {
            if (quest == null || !quest.Cost.HasValue || wallet == null)
            {
                return true;
            }

            return wallet.CanAfford(quest.Cost.Value);
        }

        /// <summary>A decoration-request option's label: "{name} · {cost}"
        /// for a purchasable catalog entry, otherwise the plain name.</summary>
        public static string OptionLabel(string optionName)
        {
            var cost = ItemCatalog.Get(optionName).Cost;
            return cost.HasValue ? FormatCost(optionName, cost.Value) : optionName;
        }

        /// <summary>Whether the wallet currently covers a decoration
        /// option's catalog cost.</summary>
        public static bool IsOptionAffordable(string optionName, Wallet wallet)
        {
            if (wallet == null)
            {
                return true;
            }

            var cost = ItemCatalog.Get(optionName).Cost;
            return !cost.HasValue || wallet.CanAfford(cost.Value);
        }

        private static string FormatCost(string label, int cost)
        {
            return label + CostSeparator + cost;
        }
    }
}
