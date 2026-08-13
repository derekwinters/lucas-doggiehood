using System;
using System.Collections.Generic;
using System.Text;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// #704: escaping for the free-text fields of the save's line format
    /// (<see cref="SaveCodec"/>). The format uses '\n' between records, '|'
    /// between a record's fields and ',' between a list field's entries, so a
    /// field carrying authored copy — a quest's dialogue lines, an item name —
    /// has to be able to contain all three without corrupting the rest of the
    /// file. Every escape is a backslash pair, so the encoding is reversible
    /// and produces no separator characters of its own.
    /// </summary>
    public static class SaveFieldCodec
    {
        private const char Escaper = '\\';
        private const char FieldSeparator = '|';
        private const char ListSeparator = ',';
        private const char NewLine = '\n';

        private const char EscapedEscaper = 'b';
        private const char EscapedFieldSeparator = 'p';
        private const char EscapedListSeparator = 'c';
        private const char EscapedNewLine = 'n';

        /// <summary>Encodes one free-text field so it holds no separator and no
        /// newline. Null encodes as the empty string.</summary>
        public static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(text.Length);
            foreach (var character in text)
            {
                switch (character)
                {
                    case Escaper:
                        builder.Append(Escaper).Append(EscapedEscaper);
                        break;
                    case FieldSeparator:
                        builder.Append(Escaper).Append(EscapedFieldSeparator);
                        break;
                    case ListSeparator:
                        builder.Append(Escaper).Append(EscapedListSeparator);
                        break;
                    case NewLine:
                        builder.Append(Escaper).Append(EscapedNewLine);
                        break;
                    default:
                        builder.Append(character);
                        break;
                }
            }

            return builder.ToString();
        }

        /// <summary>Decodes a field written by <see cref="Escape"/>. An
        /// unrecognized escape (a hand-edited or future-format save) decodes to
        /// the escaped character itself rather than throwing.</summary>
        public static string Unescape(string field)
        {
            if (string.IsNullOrEmpty(field))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(field.Length);
            for (var i = 0; i < field.Length; i++)
            {
                if (field[i] != Escaper || i + 1 >= field.Length)
                {
                    builder.Append(field[i]);
                    continue;
                }

                i++;
                switch (field[i])
                {
                    case EscapedEscaper:
                        builder.Append(Escaper);
                        break;
                    case EscapedFieldSeparator:
                        builder.Append(FieldSeparator);
                        break;
                    case EscapedListSeparator:
                        builder.Append(ListSeparator);
                        break;
                    case EscapedNewLine:
                        builder.Append(NewLine);
                        break;
                    default:
                        builder.Append(field[i]);
                        break;
                }
            }

            return builder.ToString();
        }

        /// <summary>Encodes a list of free-text entries into one field —
        /// each entry escaped, joined by the list separator. An empty list
        /// encodes as the empty field.</summary>
        public static string JoinList(IEnumerable<string> entries)
        {
            var builder = new StringBuilder();
            foreach (var entry in entries)
            {
                if (builder.Length > 0)
                {
                    builder.Append(ListSeparator);
                }

                builder.Append(Escape(entry));
            }

            return builder.ToString();
        }

        /// <summary>Decodes a field written by <see cref="JoinList"/>.</summary>
        public static IReadOnlyList<string> SplitList(string field)
        {
            if (string.IsNullOrEmpty(field))
            {
                return Array.Empty<string>();
            }

            var entries = field.Split(ListSeparator);
            var decoded = new string[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                decoded[i] = Unescape(entries[i]);
            }

            return decoded;
        }
    }
}
