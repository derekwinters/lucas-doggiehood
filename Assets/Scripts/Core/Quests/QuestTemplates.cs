using System;
using System.Collections.Generic;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Economy;

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
                { Personality.Adventurous, new[] { "{dog} trots up. \"I explored a bit too far and dropped my {item} somewhere out there!\"" } },
                { Personality.Athletic, new[] { "{dog} skids to a stop. \"Dropped my {item} mid-zoomies! Help me track it down?\"" } },
            },
            new[]
            {
                "\"Keep your eyes peeled while you look around — it's out there somewhere!\"",
                "\"It's gotta be around here somewhere. Thanks for looking!\"",
            },
            new Dictionary<Personality, IReadOnlyList<string>>(),
            new[]
            {
                "{dog} looks up expectantly. \"Any sign of my {item} yet?\"",
                "{dog} sniffs the air. \"Still haven't found my {item}? It's out there somewhere.\"",
                "{dog} tilts its head. \"Have you tracked down my {item}?\"",
            },
            new Dictionary<Personality, IReadOnlyList<string>>
            {
                { Personality.Grumpy, new[] { "{dog} grumbles. \"Well? Is my {item} found or not?\"" } },
                { Personality.Excited, new[] { "{dog} bounces! \"Ooh, did you find my {item}?! Did you?!\"" } },
                { Personality.Shy, new[] { "{dog} peeks out from behind a bush. \"Um... any luck with my {item}?\"" } },
                { Personality.Brave, new[] { "{dog} stands tall. \"Report: has my {item} been recovered?\"" } },
                { Personality.Adventurous, new[] { "{dog} trots over. \"Found my {item} on your travels yet?\"" } },
                { Personality.Athletic, new[] { "{dog} jogs in place. \"Still chasing down my {item}? Keep at it!\"" } },
            });

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
                { Personality.Adventurous, new[] { "{dog} grins. \"You know what my next adventure needs? A {item}!\"" } },
                { Personality.Athletic, new[] { "{dog} stretches. \"Training's better with gear. How about a {item}?\"" } },
            },
            new[]
            {
                "\"The delivery truck will bring it right to my door — I'll head home and wait!\"",
                "\"I'll head home and keep an eye out for the delivery truck!\"",
            },
            new Dictionary<Personality, IReadOnlyList<string>>(),
            // #708: a Gift quest is paid for at accept and the truck is
            // dispatched right then, so its reminder pool speaks in the
            // already-bought voice — never "have you got it yet?".
            new[]
            {
                "{dog} watches the road. \"My {item} is on its way — thanks for ordering it!\"",
                "{dog} wags its tail. \"Just waiting on the delivery truck to bring my {item} now!\"",
                "{dog} peeks down the street. \"That {item} is paid for and coming. I can hardly wait!\"",
            },
            new Dictionary<Personality, IReadOnlyList<string>>
            {
                { Personality.Grumpy, new[] { "{dog} huffs. \"That {item}'s bought and on its way. About time, too.\"" } },
                { Personality.Excited, new[] { "{dog} spins in a circle! \"My {item} is coming! It's actually COMING!\"" } },
                { Personality.Shy, new[] { "{dog} paws the ground. \"Um... I heard my {item} is on its way. I'll wait right here.\"" } },
                { Personality.Brave, new[] { "{dog} nods firmly. \"The {item} is ordered and en route. I'll hold the post until it lands.\"" } },
                { Personality.Adventurous, new[] { "{dog} grins. \"My {item}'s already on its way — the adventure starts the moment it lands!\"" } },
                { Personality.Athletic, new[] { "{dog} stretches. \"Delivery's rolling in with my {item}. I'll warm up in the meantime!\"" } },
            },
            PendingActionOwner.Game);

        /// <summary>#701: the fence's own "Buy something" pools. The fence is
        /// the one Gift subject with no delivery leg (#318) — accepting installs
        /// it on the lot right away — so it must not inherit the generic
        /// <see cref="BuyGift"/> pools, whose closers promise a delivery truck
        /// and a walk home and whose openers frame the subject as a portable
        /// handed-over gift. First-draft placeholder text like every other pool
        /// here; the writing pass is #100.
        ///
        /// <para>#708: it keeps the default player-owed reminder voice rather
        /// than the generic Gift pool's "your delivery is on its way" — there is
        /// no delivery to acknowledge. In practice the fence completes at accept,
        /// so it never sits Accepted and never reaches a reminder at all; the
        /// pool exists only so the template has the same shape as every
        /// other.</para></summary>
        private static readonly QuestTemplate BuyFence = new QuestTemplate(
            new[]
            {
                "{dog} looks out across the yard. \"Any chance we could get a {item} put in around my yard?\"",
                "{dog} paces the edge of the lawn. \"A {item} right along here would be just the thing.\"",
                "{dog} eyes the wide-open backyard. \"This yard could really use a {item} around it.\"",
            },
            new Dictionary<Personality, IReadOnlyList<string>>
            {
                { Personality.Grumpy, new[] { "{dog} huffs at the open yard. \"Anyone can just wander through here. Put up a {item}.\"" } },
                { Personality.Excited, new[] { "{dog} zooms along the yard's edge! \"A {item}! All the way around my yard! Can we?! Can we?!\"" } },
                { Personality.Shy, new[] { "{dog} peeks out from the porch. \"I'd feel safer with a {item} around the yard... if that's okay.\"" } },
                { Personality.Brave, new[] { "{dog} paces the property line. \"A {item} would secure this yard properly. Can you have one put up?\"" } },
                { Personality.Adventurous, new[] { "{dog} trots the perimeter. \"I've mapped my whole yard — a {item} would mark the edge of it perfectly!\"" } },
                { Personality.Athletic, new[] { "{dog} pulls up from a lap. \"With a {item} around the yard I could run my laps loose. What do you say?\"" } },
            },
            new[]
            {
                "\"It goes straight up around the yard — nothing to wait on!\"",
                "\"I'll be right here in the yard, watching it go up!\"",
            },
            new Dictionary<Personality, IReadOnlyList<string>>(),
            new[]
            {
                "{dog} looks along the edge of the lawn. \"Still thinking about that {item} for my yard?\"",
                "{dog} paces the property line. \"Any word on getting my {item} put up?\"",
                "{dog} gazes at the wide-open yard. \"It's still awfully open out here — how's that {item} coming?\"",
            },
            new Dictionary<Personality, IReadOnlyList<string>>
            {
                { Personality.Grumpy, new[] { "{dog} huffs at the open yard. \"Still no {item}. Still anyone's shortcut.\"" } },
                { Personality.Excited, new[] { "{dog} zooms along the yard's edge! \"Is my {item} going up yet?! Is it?!\"" } },
                { Personality.Shy, new[] { "{dog} peeks out from the porch. \"Um... any news on the {item} for the yard?\"" } },
                { Personality.Brave, new[] { "{dog} paces the property line. \"Status on the {item}? The yard is still unsecured.\"" } },
                { Personality.Adventurous, new[] { "{dog} trots the perimeter. \"The edge of my yard is still unmarked — how's the {item}?\"" } },
                { Personality.Athletic, new[] { "{dog} pulls up from a lap. \"Still running my laps on-leash — any progress on the {item}?\"" } },
            });

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
                { Personality.Adventurous, new[] { "{dog} reports back. \"Scouted the house. Bug infestation confirmed. Over to you!\"" } },
                { Personality.Athletic, new[] { "{dog} paces. \"Can't do my morning laps with bugs in the house! Spray 'em out?\"" } },
            },
            new[]
            {
                "\"Just give the house a good spray and they'll clear right out!\"",
                "\"A quick spray should do the trick. Thanks for handling it!\"",
            },
            new Dictionary<Personality, IReadOnlyList<string>>(),
            new[]
            {
                "{dog} scratches nervously. \"Are those bugs still crawling around my house?\"",
                "{dog} shudders. \"Any chance you've cleared the bugs out yet?\"",
                "{dog} eyes the house. \"Still waiting on that bug spray, if you don't mind.\"",
            },
            new Dictionary<Personality, IReadOnlyList<string>>
            {
                { Personality.Grumpy, new[] { "{dog} glares at the house. \"Bugs. Still there. Still my problem. And yours.\"" } },
                { Personality.Excited, new[] { "{dog} spins around! \"The bugs! Are they gone yet?! Please say yes!\"" } },
                { Personality.Shy, new[] { "{dog} hides behind you. \"I... still can't go inside. Are the bugs gone?\"" } },
                { Personality.Brave, new[] { "{dog} stands guard. \"The invasion holds. Have you cleared my house?\"" } },
                { Personality.Adventurous, new[] { "{dog} reports back. \"Re-scouted — bugs still present. Any progress?\"" } },
                { Personality.Athletic, new[] { "{dog} paces. \"Can't do my laps yet — bugs still in the house?\"" } },
            });

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
                { Personality.Adventurous, new[] { "{dog} flops down. \"After a long trek, a {item} to rest on would be perfect.\"" } },
                { Personality.Athletic, new[] { "{dog} finishes a lap. \"Recovery matters! A {item} for the yard, coach?\"" } },
            },
            new[]
            {
                "\"Anything comfy works — you pick!\"",
                "\"Whatever you find comfy is fine by me!\"",
            },
            new Dictionary<Personality, IReadOnlyList<string>>(),
            // #708: the player picks the option and pays for it at accept, and
            // the truck leaves with that choice — so the reminder acknowledges
            // the chosen item is coming rather than asking for it again.
            new[]
            {
                "{dog} glances at the yard. \"The {item} is on its way over — this spot's all ready for it.\"",
                "{dog} flops in the grass. \"Just waiting on the truck to drop off my {item}. Won't be long!\"",
                "{dog} looks around the yard. \"My {item} is paid for and coming. The yard's about to get so comfy.\"",
            },
            new Dictionary<Personality, IReadOnlyList<string>>
            {
                { Personality.Grumpy, new[] { "{dog} sighs. \"The {item} is on its way. This yard had better feel better with it here.\"" } },
                { Personality.Excited, new[] { "{dog} zooms across the yard! \"The {item} is COMING! I'm trying it out the second it lands!\"" } },
                { Personality.Shy, new[] { "{dog} looks at the ground. \"I heard my {item} is on its way... I'll just wait quietly over here.\"" } },
                { Personality.Brave, new[] { "{dog} surveys the yard. \"The {item} is ordered and en route. The post will be properly equipped.\"" } },
                { Personality.Adventurous, new[] { "{dog} flops down. \"My {item}'s on its way — a proper rest spot after the next trek!\"" } },
                { Personality.Athletic, new[] { "{dog} finishes a lap. \"Recovery {item} is on the truck, coach. Right on schedule!\"" } },
            },
            PendingActionOwner.Game);

        /// <summary>#701: the subject-aware template lookup — the seam that
        /// keeps a quest's dialogue matching the mechanic that quest actually
        /// runs (docs/specs/quests/quest-content.md). Only a subject whose
        /// mechanic differs from its type's default gets its own pools (today:
        /// the fence, which installs in place instead of being delivered);
        /// every other subject falls through to <see cref="For(QuestType)"/>.
        /// </summary>
        public static QuestTemplate For(QuestType type, string itemName)
        {
            if (type == QuestType.BuyGift && itemName == ItemCatalog.FenceItemName)
            {
                return BuyFence;
            }

            return For(type);
        }

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
