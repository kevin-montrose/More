using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;

namespace MoreInternals.Compiler.Tasks
{
    /// <summary>
    /// Validate @charset declarations, recording errors where more than a one charset is
    /// defined for the emited CSS.
    /// 
    /// At the end of this task, at most one @charset appears in the blocks and is in the first
    /// slot.
    /// </summary>
    public class Charsets
    {
        public static List<Block> Task(List<Block> blocks)
        {
            var charsets = blocks.OfType<CssCharset>();

            if (charsets.Count() == 0) return blocks;

            var ret = new List<Block>(blocks.Count);
            ret.AddRange(blocks.Where(w => !(w is CssCharset)));

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
