using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using More.Model;

namespace More.Compiler.Tasks
{
    /// <summary>
    /// Moves all import blocks to the top of the emitted CSS file
    /// </summary>
    public class Import
    {
        public static List<Block> Task(List<Block> blocks)
        {
            var imports = blocks.Where(w => w is More.Model.Import);

            if (imports.Count() == 0) return blocks;

            var copy = new List<Block>(blocks.Count);
            copy.AddRange(blocks.Where(w => !(w is More.Model.Import)));

            foreach (var i in imports.Reverse())
            {
                copy.Insert(0, i);
            }

            return copy;
        }
    }
}
