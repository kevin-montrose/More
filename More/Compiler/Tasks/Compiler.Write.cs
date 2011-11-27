using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using More.Model;

namespace More.Compiler
{
    partial class Compiler
    {
        internal void Write(List<Block> blocks)
        {
            var output = Current.OutputStream;

            var writer = Current.GetWriter(output);
            foreach (var statement in blocks.Cast<IWritable>())
            {
                statement.Write(writer);
            }
        }
    }
}
