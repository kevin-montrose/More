using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;

namespace MoreInternals.Compiler.Tasks
{
    /// <summary>
    /// Unrolls everything in @reset declarations,
    /// so they can participate in the rest of unrolling, mixin evals, and so on.
    /// </summary>
    public class Reset
    {
        public static List<Block> Task(List<Block> blocks)
        {
            var ret = new List<Block>();

            ret.AddRange(blocks.Where(w => !(w is ResetBlock)));

            foreach (var b in blocks.OfType<ResetBlock>())
            {
                ret.AddRange(b.Blocks);
            }

            return ret;
        }
    }
}
