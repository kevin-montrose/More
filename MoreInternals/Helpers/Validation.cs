using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MoreInternals.Helpers
{
    class Validation
    {
        /// <summary>
        /// An escape sequence is a \ followed by up to 7 hexadecimal characters
        /// </summary>
        private static bool IsValidEscape(string escapeSequence)
        {
            if (escapeSequence.Length < 7) escapeSequence = escapeSequence.Trim();

            return
                escapeSequence.Length > 1 &&
                escapeSequence.Length <= 7 &&
                escapeSequence[0] == '\\' &&
                escapeSequence.ToLowerInvariant().Skip(1).All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
        }

        /// <summary>
        /// Removes the \XXXX style encoding in identifiers.
        /// 
        /// Returns false if an escape sequence is found *and* it is malformed.
        /// </summary>
        private static string RemoveEscapes(string id)
        {
            bool inEscape = false;
            int escapeStarts = -1;

            var ret = new List<char>();

            for (var i = 0; i < id.Length; i++)
            {
                var c = id[i];

                if (c == '\\')
                {
                    inEscape = true;
                    escapeStarts = i;
                    continue;
                }

                if (!inEscape)
                {
                    ret.Add(c);
                    continue;
                }

                // in an escape '\r\n' needs to be collapsed to just a single whitespace character
                if (c == '\r')
                {
                    if (i + 1 < id.Length)
                    {
                        if (id[i + 1] == '\n')
                        {
                            i++;
                            c = id[i];
                        }
                    }
                }

                // end of the escape
                if (char.IsWhiteSpace(c) ||  (i - escapeStarts == 7))
                {
                    var escaped = id.Substring(escapeStarts, i - escapeStarts + 1);

                    var subsectionWelformed = IsValidEscape(escaped);

                    if (!subsectionWelformed)
                    {
                        ret.AddRange(escaped.Skip(1));
                    }

                    inEscape = false;
                }
            }

            return new string(ret.ToArray());
        }

        /// <summary>
        /// Returns true if the passed string is an "identifier" as defined by CSS.
        /// 
        /// http://www.w3.org/TR/CSS2/syndata.html#value-def-identifier
        /// 
        /// Sans the fancy encoding options.  It's 2012 (or whenever), you can ship UTF-8 
        /// across the internet.
        /// 
        /// TODO: Actually accept the full range of CSS identifiers, though I've never seen them in practice
        /// this is a CSS superset break.
        /// </summary>
        public static bool IsIdentifier(string id)
        {
            if (id.IsNullOrEmpty()) return false;
            if (id.StartsWith("--")) return false;
            if (char.IsDigit(id[0])) return false;
            if (id.Length >= 2 && id[0] == '-' && char.IsDigit(id[1])) return false;

            id = RemoveEscapes(id);

            for (int i = 0; i < id.Length; i++)
            {
                var c = id[i];

                var isGood =
                    char.IsLetterOrDigit(c) ||
                    c == '-' ||
                    c == '_';

                if(char.IsSurrogate(c) && i < id.Length - 1)
                {
                    i++;
                    var codePoint = char.ConvertToUtf32(c, id[i]);

                    isGood = codePoint >= 0xA0;
                }

                if (!isGood) return false;
            }

            return true;
        }
    }
}
