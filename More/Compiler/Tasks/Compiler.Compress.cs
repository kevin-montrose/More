using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using More.Model;
using More.Helpers;

namespace More.Compiler
{
    partial class Compiler
    {
        public List<Block> OptimizeForCompression(List<Block> statements)
        {
            var before = (decimal)LZ77Optimizer.GZipSize(statements.Cast<IWritable>());

            var ret = LZ77Optimizer.Optimize(statements);

            var after = (decimal)LZ77Optimizer.GZipSize(ret.Cast<IWritable>());

            var saving = decimal.Round(((before - after) / before) * 100m, 1, MidpointRounding.AwayFromZero);

            Current.RecordInfo("Compression: Before " + before + " bytes, after " + after + " bytes\r\nSavings " + saving + "%");

            return ret;
        }
    }
}
