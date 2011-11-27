using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using More.Model;

namespace More.Compiler
{
    partial class Compiler
    {
        private List<Block> ValidateCharsets(List<Block> statements)
        {
            var charsets = statements.OfType<CssCharset>();

            if (charsets.Count() == 0) return statements;

            var ret = new List<Block>(statements.Count);
            ret.AddRange(statements.Where(w => !(w is CssCharset)));

            var charsetStrings = charsets.Select(s => s.Charset.Value).Distinct();

            if (charsetStrings.Count() != 1)
            {
                foreach (var c in charsets)
                {
                    var others = string.Join(", ", charsetStrings.Where(s => s != c.Charset.Value));

                    Current.RecordError(ErrorType.Compiler, c, "@charset conflicts with " + others + ", defined elsewhere.");
                }

                return null;
            }

            ret.Insert(0, new CssCharset(new QuotedStringValue(charsetStrings.Single()), -1, -1));

            return ret;
        }
    }
}
