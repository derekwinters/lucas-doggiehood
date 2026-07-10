using System.Collections.Generic;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Quests
{
    public enum QuestType
    {
        LostItem,
        BuyGift,
        PestControl,
        DecorationRequest,
    }

    public enum QuestStatus
    {
        Available,
        Accepted,
        Completed,
    }

    public enum DeliveryPhase
    {
        None,
        HeadingHome,
        WaitingForDelivery,
        Delivered,
    }

    /// <summary>
    /// A quest instance generated from a template (#69). Deliberately has
    /// no expiry, timer, or fail fields — quests stay active until
    /// completed (#28); a guard test enforces the schema.
    /// </summary>
    public sealed class Quest
    {
        public int Id { get; }
        public QuestType Type { get; }
        public string DogName { get; }

        /// <summary>Null for a generic DecorationRequest until the player
        /// chooses from Options (#50).</summary>
        public string ItemName { get; internal set; }

        /// <summary>DecorationRequest only (#50): the 2+ selectable options.
        /// Empty for quests that name one specific item.</summary>
        public IReadOnlyList<string> Options { get; }

        public IReadOnlyList<string> DialogueLines { get; }

        /// <summary>LostItem only: where the item is hidden (#31).</summary>
        public GridPoint? HiddenItemPosition { get; }

        /// <summary>BuyGift/DecorationRequest only: catalog cost (#62).</summary>
        public int? Cost { get; internal set; }

        /// <summary>PestControl only: the afflicted house (#53).</summary>
        public int? TargetHouseId { get; }

        public QuestStatus Status { get; internal set; }
        public DeliveryPhase DeliveryPhase { get; internal set; }

        public Quest(int id, QuestType type, string dogName, string itemName,
            IReadOnlyList<string> dialogueLines, GridPoint? hiddenItemPosition,
            int? cost, int? targetHouseId, IReadOnlyList<string> options = null)
        {
            Id = id;
            Type = type;
            DogName = dogName;
            ItemName = itemName;
            Options = options ?? System.Array.Empty<string>();
            DialogueLines = dialogueLines;
            HiddenItemPosition = hiddenItemPosition;
            Cost = cost;
            TargetHouseId = targetHouseId;
            Status = QuestStatus.Available;
            DeliveryPhase = DeliveryPhase.None;
        }
    }
}
