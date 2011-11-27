using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;
using MoreInternals.Helpers;

namespace MoreInternals.Compiler.Tasks
{
    /// <summary>
    /// Reorders blocks in such a way as to maximize LZ77 compression.
    /// </summary>
    public class Compress
    {
        public static List<Block> Task(List<Block> blocks)
        {
            var before = (decimal)LZ77Optimizer.GZipSize(blocks.Cast<IWritable>());

            var ret = LZ77Optimizer.Optimize(blocks);

            var after = (decimal)LZ77Optimizer.GZipSize(ret.Cast<IWritable>());

            var saving = decimal.Round(((before - after) / before) * 100m, 1, MidpointRounding.AwayFromZero);

            Current.RecordInfo("Compression: Before " + before + " bytes, after " + after + " bytes\r\nSavings " + saving + "%");

            return ret;
        }
    }
}
