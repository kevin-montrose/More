using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;

namespace MoreInternals.Compiler.Tasks
{
    /// <summary>
    /// Pulls everything in a @reset as high up into the document as is possible.
    /// </summary>
    public class ResetReOrder
    {
        public static List<Block> Task(List<Block> blocks)
        {
            var ret = new List<Block>();

            ret.AddRange(blocks.OfType<CssCharset>());
            ret.AddRange(blocks.OfType<Model.Import>());
            ret.AddRange(blocks.Where(w => w is SelectorAndBlock && ((SelectorAndBlock)w).IsReset));
            ret.AddRange(
                blocks.Where(
                    w =>
                        !(w is SelectorAndBlock && ((SelectorAndBlock)w).IsReset) &&
                        !(w is CssCharset) &&
                        !(w is Model.Import)
                )
            );

            return ret;
        }
    }
}
