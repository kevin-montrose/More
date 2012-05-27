using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MoreInternals.Helpers
{
    class Validation
    {
        /// <summary>
        /// Returns true if the passed string is an "identifier" as defined by CSS.
        /// 
        /// http://www.w3.org/TR/CSS2/syndata.html#value-def-identifier
        /// 
        /// sans the fancy encoding options.  It's 2012 (or whenever), you can ship UTF-8 
        /// across the internet.
        /// </summary>
        public static bool IsIdentifier(string id)
        {
            if (id.Length == 0) return false;
            if (id.StartsWith("--")) return false;
            if (char.IsDigit(id[0])) return false;
            if (id.Length >= 2 && char.IsDigit(id[0]) && id[1] == '-') return false;

            for (int i = 0; i < id.Length; i++)
            {
                var c = id[i];

                var isGood =
                    char.IsLetterOrDigit(c) ||
                    c == '-' ||
                    c == '_';

                if(!isGood && char.IsSurrogate(c) && i < id.Length - 1)
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
