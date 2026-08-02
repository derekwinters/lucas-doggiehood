using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Doggiehood.Core.Dogs;

namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// #518: the pure message composer behind the "Welcome to the
    /// neighborhood!" pop-up (docs/specs/ui/welcome-popup.md). Given the
    /// household a move-in produced, it composes the three dynamic pieces the
    /// panel shows — the <see cref="NameLine"/>, the one <see cref="MetaLine"/>,
    /// and whether the member-chip row appears (<see cref="ShowsMemberChips"/>,
    /// with the per-dog <see cref="MemberNames"/>) — per the wireframe's
    /// "Household variants" table:
    /// <list type="bullet">
    /// <item>Single — name, "&lt;Breed&gt; · moved in next door", chip row hidden.</item>
    /// <item>Parent + puppy — "A &amp; B", "&lt;Breed&gt; family of 2", two chips.</item>
    /// <item>Three-dog — "A, B &amp; C", "moved in — N dogs", N chips.</item>
    /// </list>
    /// Engine-free (rule #2): strings only, over the roster Core already holds.
    /// The heading and chrome are constant and owned by the Unity panel; only
    /// these dynamic lines are composed here.
    /// </summary>
    public readonly struct WelcomeMessage
    {
        // --- Copy formats from the approved wireframe (docs/specs/ui/welcome-popup.md,
        // "Household variants"). Named rather than inlined so the exact approved
        // wording lives in one place. ---
        private const string NameJoinComma = ", ";
        private const string NameJoinFinal = " & ";
        private const string SingleMetaFormat = "{0} · moved in next door";
        private const string PairMetaFormat = "{0} family of 2";
        private const string GroupMetaFormat = "moved in — {0} dogs";

        private WelcomeMessage(string nameLine, string metaLine, bool showsMemberChips, IReadOnlyList<string> memberNames)
        {
            NameLine = nameLine;
            MetaLine = metaLine;
            ShowsMemberChips = showsMemberChips;
            MemberNames = memberNames;
        }

        /// <summary>The new dog's name, or the household's names for a multi-dog
        /// move-in (e.g. "Biscuit &amp; Pepper", "Mochi, Nori &amp; Yuzu").</summary>
        public string NameLine { get; }

        /// <summary>The one dynamic line: breed · household composition / how
        /// many dogs, depending on the household shape.</summary>
        public string MetaLine { get; }

        /// <summary>Whether the member-chip row renders — true only when the
        /// household has more than one dog. Hidden entirely for a single-dog
        /// move-in (the wireframe's hidden-for-single rule).</summary>
        public bool ShowsMemberChips { get; }

        /// <summary>One entry per dog in the household, for the member-chip row.
        /// Populated for every household; only shown when
        /// <see cref="ShowsMemberChips"/> is true.</summary>
        public IReadOnlyList<string> MemberNames { get; }

        /// <summary>Composes the pop-up copy for a moved-in
        /// <paramref name="household"/> (the household head first). One pop-up
        /// per household, never one per dog.</summary>
        public static WelcomeMessage ForHousehold(IReadOnlyList<Dog> household)
        {
            var names = household.Select(dog => dog.Name).ToList();
            var headBreed = DogProfile.For(household[0]).Breed;

            return new WelcomeMessage(
                ComposeNameLine(names),
                ComposeMetaLine(household.Count, headBreed),
                showsMemberChips: household.Count > 1,
                memberNames: names);
        }

        /// <summary>"A" / "A &amp; B" / "A, B &amp; C" — the last two joined by
        /// " &amp; ", any earlier ones comma-separated.</summary>
        private static string ComposeNameLine(IReadOnlyList<string> names)
        {
            if (names.Count == 1)
            {
                return names[0];
            }

            var leading = string.Join(NameJoinComma, names.Take(names.Count - 1));
            return leading + NameJoinFinal + names[names.Count - 1];
        }

        private static string ComposeMetaLine(int count, string breed)
        {
            if (count == 1)
            {
                return string.Format(CultureInfo.InvariantCulture, SingleMetaFormat, breed);
            }

            if (count == 2)
            {
                return string.Format(CultureInfo.InvariantCulture, PairMetaFormat, breed);
            }

            return string.Format(CultureInfo.InvariantCulture, GroupMetaFormat,
                count.ToString(CultureInfo.InvariantCulture));
        }
    }
}
