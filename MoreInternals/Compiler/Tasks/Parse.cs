using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;
using System.IO;
using System.Threading;

namespace MoreInternals.Compiler.Tasks
{
    /// <summary>
    /// Parses an initial file into blocks as a task.
    /// </summary>
    public class Parse
    {
        public static List<Block> Task(TextReader @in)
        {
            var filePath = Current.InitialFilePath;

            return ParseStreamImpl(filePath, @in);
        }

        internal static List<Block> CheckPostImport(List<Block> ret)
        {
            if (ret == null) return null;

            var lastImport = ret.LastOrDefault(r => r is Model.Import);
            var firstNonImport = ret.FirstOrDefault(r => !(r is Model.Import));

            if (lastImport == null || firstNonImport == null)
            {
                return ret;
            }

            var lix = ret.IndexOf(lastImport);
            var fnix = ret.IndexOf(firstNonImport);

            if (lix != -1 && fnix != -1)
            {
                if (fnix < lix)
                {
                    for (int i = fnix; i < ret.Count; i++)
                    {
                        if (ret[i] is Model.Import)
                        {
                            Current.RecordWarning(ErrorType.Parser, ret[i], "@import should appear before any other statements.  Statement will be moved.");
                        }
                    }
                }
            }

            return ret;
        }

        internal static List<Block> ParseStreamImpl(string filePath, TextReader @in)
        {
            var ret =
                Current.FileCache.Demand(
                    filePath,
                    delegate(string path)
                    {
                        var newParser = Parser.Parser.CreateParser();

                        return newParser.Parse(filePath, @in);
                    }
                );

            return CheckPostImport(ret);
        }
    }
}
