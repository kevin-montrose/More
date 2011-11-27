using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using More.Model;

namespace More.Compiler
{
    partial class Compiler
    {
        private List<Block> MoveCssImports(List<Block> unordered)
        {
            var imports = unordered.Where(w => w is Import);

            if (imports.Count() == 0) return unordered;

            var copy = new List<Block>(unordered.Count);
            copy.AddRange(unordered.Where(w => !(w is Import)));

            foreach (var i in imports.Reverse())
            {
                copy.Insert(0, i);
            }

            return copy;
        }
    }
}
