using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;

namespace MoreInternals.Compiler.Tasks
{
    /// <summary>
    /// Writes all blocks to the output stream set on Current.
    /// </summary>
    public class Write
    {
        public static List<Block> Task(List<Block> blocks)
        {
            var output = Current.OutputStream;

            var writer = Current.GetWriter(output);
            foreach (var statement in blocks.Cast<IWritable>())
            {
                statement.Write(writer);
            }

            return blocks;
        }
    }
}
