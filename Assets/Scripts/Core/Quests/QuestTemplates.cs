using System;
using System.Collections.Generic;
using Doggiehood.Core.Dogs;

namespace Doggiehood.Core.Quests
{
    /// <summary>
    /// The template instances for every quest type (#69): the 3 MVP types
    /// (#12, #13, #53) plus the decoration request (#50). First-draft line
    /// text — Derek and Lucas own the actual writing; the structure (slots +
    /// personality flavor) is the contract.
    /// </summary>
    public static class QuestTemplates
    {
        private static readonly QuestTemplate LostItem = new QuestTemplate(
            "{dog} sniffs around anxiously. \"I lost my {item} somewhere in the neighborhood...\"",
            new Dictionary<Personality, string>
            {
                { Personality.Grumpy, "{dog} grumbles. \"Someone moved my {item}. Typical. Find it, would you?\"" },
                { Personality.Excited, "{dog} bounces in circles! \"My {item}! It's GONE! Ooh ooh, can you find it?!\"" },
                { Personality.Shy, "{dog} whispers from behind a bush. \"Um... I lost my {item}. Could you maybe... look for it?\"" },
                { Personality.Brave, "{dog} stands tall. \"My {item} has gone missing. I'd search myself, but I'm guarding the street.\"" },
                { Personality.AdventurousExploring, "{dog} trots up. \"I explored a bit too far and dropped my {item} somewhere out there!\"" },
                { Personality.Athletic, "{dog} skids to a stop. \"Dropped my {item} mid-zoomies! Help me track it down?\"" },
            },
            new[]
            {
                "\"Keep your eyes peeled while you look around — it's out there somewhere!\"",
            });

        private static readonly QuestTemplate BuyGift = new QuestTemplate(
            "{dog} looks up hopefully. \"Could you get me a {item}? It would mean a lot.\"",
            new Dictionary<Personality, string>
            {
                { Personality.Grumpy, "{dog} huffs. \"If you MUST do something nice, a {item} wouldn't be the worst thing.\"" },
                { Personality.Excited, "{dog} wags at top speed! \"A {item}! A {item}! Can I have a {item}? Please please please!\"" },
                { Personality.Shy, "{dog} paws the ground. \"I've... always wanted a {item}. If it's not too much trouble...\"" },
                { Personality.Brave, "{dog} nods firmly. \"A {item} would serve this household well. Can you arrange it?\"" },
                { Personality.AdventurousExploring, "{dog} grins. \"You know what my next adventure needs? A {item}!\"" },
                { Personality.Athletic, "{dog} stretches. \"Training's better with gear. How about a {item}?\"" },
            },
            new[]
            {
                "\"The delivery truck will bring it right to my door — I'll head home and wait!\"",
            });

        private static readonly QuestTemplate PestControl = new QuestTemplate(
            "{dog} scratches nervously. \"My house has bugs! Could you spray them away?\"",
            new Dictionary<Personality, string>
            {
                { Personality.Grumpy, "{dog} glares at the house. \"Bugs. In MY house. Deal with them.\"" },
                { Personality.Excited, "{dog} spins around! \"There are bugs EVERYWHERE! It's awful! Spray them! Quick!\"" },
                { Personality.Shy, "{dog} shudders. \"There are... creepy crawlies in my house. I can't go in...\"" },
                { Personality.Brave, "{dog} stands guard. \"Bugs have invaded my home. I need backup — bring the spray.\"" },
                { Personality.AdventurousExploring, "{dog} reports back. \"Scouted the house. Bug infestation confirmed. Over to you!\"" },
                { Personality.Athletic, "{dog} paces. \"Can't do my morning laps with bugs in the house! Spray 'em out?\"" },
            },
            new[]
            {
                "\"Just give the house a good spray and they'll clear right out!\"",
            });

        private static readonly QuestTemplate DecorationRequest = new QuestTemplate(
            "{dog} gestures at the yard. \"Something comfy out here would be lovely... maybe a {item}?\"",
            new Dictionary<Personality, string>
            {
                { Personality.Grumpy, "{dog} sighs. \"This yard is unacceptably uncomfortable. A {item} would fix that.\"" },
                { Personality.Excited, "{dog} zooms across the yard! \"Imagine a {item} RIGHT HERE! Wouldn't that be amazing?!\"" },
                { Personality.Shy, "{dog} looks at the ground. \"The yard feels a bit bare... a {item} might be nice...\"" },
                { Personality.Brave, "{dog} surveys the yard. \"Every good post needs a {item}. Can you supply one?\"" },
                { Personality.AdventurousExploring, "{dog} flops down. \"After a long trek, a {item} to rest on would be perfect.\"" },
                { Personality.Athletic, "{dog} finishes a lap. \"Recovery matters! A {item} for the yard, coach?\"" },
            },
            new[]
            {
                "\"Anything comfy works — you pick!\"",
            });

        public static QuestTemplate For(QuestType type)
        {
            switch (type)
            {
                case QuestType.LostItem: return LostItem;
                case QuestType.BuyGift: return BuyGift;
                case QuestType.PestControl: return PestControl;
                case QuestType.DecorationRequest: return DecorationRequest;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }
    }
}
