using System;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #704: the save file is a line format with two separators — '|' between
    /// fields and ',' inside a list field — so any field carrying free text
    /// (authored quest dialogue, an item name) has to survive containing them.
    /// This is the one place that escaping lives.
    /// </summary>
    public class SaveFieldCodecTests
    {
        [TestCase("plain")]
        [TestCase("")]
        [TestCase("a|b")]
        [TestCase("a,b")]
        [TestCase("back\\slash")]
        [TestCase("all\\ of |them, at once")]
        [TestCase("a line\nbroken in two")]
        public void EscapedText_RoundTripsExactly(string text)
        {
            Assert.That(SaveFieldCodec.Unescape(SaveFieldCodec.Escape(text)), Is.EqualTo(text));
        }

        [Test]
        public void EscapedText_ContainsNoSeparatorOrNewline()
        {
            var escaped = SaveFieldCodec.Escape("a|b,c\nd");

            Assert.That(escaped, Does.Not.Contain("|"), "a field never splits its own record");
            Assert.That(escaped, Does.Not.Contain(","), "nor its own list");
            Assert.That(escaped, Does.Not.Contain("\n"), "nor the save's line separator");
        }

        [Test]
        public void ListsRoundTrip_IncludingSeparatorsInsideAnEntry()
        {
            var entries = new[] { "one, two", "three|four", "five" };

            var restored = SaveFieldCodec.SplitList(SaveFieldCodec.JoinList(entries));

            Assert.That(restored, Is.EqualTo(entries));
        }

        [Test]
        public void AnEmptyList_RoundTripsAsEmpty()
        {
            Assert.That(SaveFieldCodec.SplitList(SaveFieldCodec.JoinList(Array.Empty<string>())),
                Is.Empty);
        }
    }
}
