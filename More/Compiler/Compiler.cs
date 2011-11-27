using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using More.Model;
using More.Parser;
using More.Helpers;
using System.Threading;
using System.Collections.Concurrent;
using System.IO.Compression;

namespace More.Compiler
{
    internal delegate List<Block> CompilationTask(List<Block> blocks);

    partial class Compiler
    {
        private static readonly Compiler Singleton = new Compiler();

        public bool Compile(string currentDir, string inputFile, TextReader @in, TextWriter output, IFileLookup lookup)
        {
            CompilationTask noop = (List<Block> blocks) => blocks;

            var tasks = new List<CompilationTask>()
            {
                EvaluateUsings,
                VerifyVariableReferences,
                ValidateCharsets,
                MoveCssImports,
                ExportSprites,
                BindAndEvaluateMixins,
                UnrollNestedBlocks,
                MergeMedia,
                CopyIncludes,
                EvaluateValues,
                ResolveImportant,
                RemoveNops,
                ValidateFontFace,
                Current.Options.HasFlag(Options.Minify) ? Minify : noop,
                Current.Options.HasFlag(Options.OptimizeCompression) ? OptimizeForCompression : noop,
                Write,
                WriteSprites
            };

            try
            {
                Current.SetWorkingDirectory(currentDir);
                Current.SetInitialFile(inputFile);
                Current.SetFileLookup(lookup);
                Current.SetOutputStream(output);

                var blocks = ParseStream(@in);

                if (blocks == null) return false;

                foreach (var task in tasks)
                {
                    blocks = task(blocks);
                    if (Current.HasErrors()) return false;
                }

                return true;
            }
            catch (StoppedCompilingException)
            {
                return false;
            }
        }

        public static Compiler Get()
        {
            return Singleton;
        }
    }
}
