using System;
using System.Collections.Generic;
using Doggiehood.Core.Dogs;

namespace Doggiehood.Core.Quests
{
    /// <summary>
    /// The template instances for every quest type (#69): the 3 MVP types
    /// (#12, #13, #53) plus the decoration request (#50). Pooled openers
    /// and closers (#189, "Model 2"): a default pool that carries the
    /// voice for the type plus a small per-personality pool for seasoning.
    /// First-draft line text — Derek and Lucas own the actual writing pass
    /// (#100); the structure (slots + pooled personality flavor) is the
    /// contract here, with just enough placeholder lines per pool to prove
    /// the mechanism works.
    /// </summary>
    public static class QuestTemplates
    {
        private static readonly QuestTemplate LostItem = new QuestTemplate(
            new[]
            {
                "{dog} sniffs around anxiously. \"I lost my {item} somewhere in the neighborhood...\"",
                "{dog} paces back and forth. \"Have you seen my {item}? I can't find it anywhere!\"",
                "{dog} tilts its head. \"My {item} is missing. I've looked everywhere I can think of.\"",
            },
            new Dictionary<Personality, IReadOnlyList<string>>
            {
                { Personality.Grumpy, new[] { "{dog} grumbles. \"Someone moved my {item}. Typical. Find it, would you?\"" } },
                { Personality.Excited, new[] { "{dog} bounces in circles! \"My {item}! It's GONE! Ooh ooh, can you find it?!\"" } },
                { Personality.Shy, new[] { "{dog} whispers from behind a bush. \"Um... I lost my {item}. Could you maybe... look for it?\"" } },
                { Personality.Brave, new[] { "{dog} stands tall. \"My {item} has gone missing. I'd search myself, but I'm guarding the street.\"" } },
                { Personality.AdventurousExploring, new[] { "{dog} trots up. \"I explored a bit too far and dropped my {item} somewhere out there!\"" } },
                { Personality.Athletic, new[] { "{dog} skids to a stop. \"Dropped my {item} mid-zoomies! Help me track it down?\"" } },
            },
            new[]
            {
                "\"Keep your eyes peeled while you look around — it's out there somewhere!\"",
                "\"It's gotta be around here somewhere. Thanks for looking!\"",
            },
            new Dictionary<Personality, IReadOnlyList<string>>());

        private static readonly QuestTemplate BuyGift = new QuestTemplate(
            new[]
            {
                "{dog} looks up hopefully. \"Could you get me a {item}? It would mean a lot.\"",
                "{dog} wags its tail. \"I've been thinking about a {item} lately. Any chance you could grab one?\"",
                "{dog} nudges your hand. \"A {item} would really make my day.\"",
            },
            new Dictionary<Personality, IReadOnlyList<string>>
            {
                { Personality.Grumpy, new[] { "{dog} huffs. \"If you MUST do something nice, a {item} wouldn't be the worst thing.\"" } },
                { Personality.Excited, new[] { "{dog} wags at top speed! \"A {item}! A {item}! Can I have a {item}? Please please please!\"" } },
                { Personality.Shy, new[] { "{dog} paws the ground. \"I've... always wanted a {item}. If it's not too much trouble...\"" } },
                { Personality.Brave, new[] { "{dog} nods firmly. \"A {item} would serve this household well. Can you arrange it?\"" } },
                { Personality.AdventurousExploring, new[] { "{dog} grins. \"You know what my next adventure needs? A {item}!\"" } },
                { Personality.Athletic, new[] { "{dog} stretches. \"Training's better with gear. How about a {item}?\"" } },
            },
            new[]
            {
                "\"The delivery truck will bring it right to my door — I'll head home and wait!\"",
                "\"I'll head home and keep an eye out for the delivery truck!\"",
            },
            new Dictionary<Personality, IReadOnlyList<string>>());

        private static readonly QuestTemplate PestControl = new QuestTemplate(
            new[]
            {
                "{dog} scratches nervously. \"My house has bugs! Could you spray them away?\"",
                "{dog} shakes its coat. \"Something's crawling around my house. Bugs, I think. Help?\"",
                "{dog} sighs. \"My house needs a good bug spraying. Would you take care of it?\"",
            },
            new Dictionary<Personality, IReadOnlyList<string>>
            {
                { Personality.Grumpy, new[] { "{dog} glares at the house. \"Bugs. In MY house. Deal with them.\"" } },
                { Personality.Excited, new[] { "{dog} spins around! \"There are bugs EVERYWHERE! It's awful! Spray them! Quick!\"" } },
                { Personality.Shy, new[] { "{dog} shudders. \"There are... creepy crawlies in my house. I can't go in...\"" } },
                { Personality.Brave, new[] { "{dog} stands guard. \"Bugs have invaded my home. I need backup — bring the spray.\"" } },
                { Personality.AdventurousExploring, new[] { "{dog} reports back. \"Scouted the house. Bug infestation confirmed. Over to you!\"" } },
                { Personality.Athletic, new[] { "{dog} paces. \"Can't do my morning laps with bugs in the house! Spray 'em out?\"" } },
            },
            new[]
            {
                "\"Just give the house a good spray and they'll clear right out!\"",
                "\"A quick spray should do the trick. Thanks for handling it!\"",
            },
            new Dictionary<Personality, IReadOnlyList<string>>());

        private static readonly QuestTemplate DecorationRequest = new QuestTemplate(
            new[]
            {
                "{dog} gestures at the yard. \"Something comfy out here would be lovely... maybe a {item}?\"",
                "{dog} looks around the yard. \"This place could use a little something. A {item}, perhaps?\"",
                "{dog} flops in the grass. \"A {item} out here would really tie the yard together.\"",
            },
            new Dictionary<Personality, IReadOnlyList<string>>
            {
                { Personality.Grumpy, new[] { "{dog} sighs. \"This yard is unacceptably uncomfortable. A {item} would fix that.\"" } },
                { Personality.Excited, new[] { "{dog} zooms across the yard! \"Imagine a {item} RIGHT HERE! Wouldn't that be amazing?!\"" } },
                { Personality.Shy, new[] { "{dog} looks at the ground. \"The yard feels a bit bare... a {item} might be nice...\"" } },
                { Personality.Brave, new[] { "{dog} surveys the yard. \"Every good post needs a {item}. Can you supply one?\"" } },
                { Personality.AdventurousExploring, new[] { "{dog} flops down. \"After a long trek, a {item} to rest on would be perfect.\"" } },
                { Personality.Athletic, new[] { "{dog} finishes a lap. \"Recovery matters! A {item} for the yard, coach?\"" } },
            },
            new[]
            {
                "\"Anything comfy works — you pick!\"",
                "\"Whatever you find comfy is fine by me!\"",
            },
            new Dictionary<Personality, IReadOnlyList<string>>());

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
