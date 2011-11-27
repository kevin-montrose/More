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
using More.Compiler.Tasks;

namespace More.Compiler
{
    internal delegate List<Block> CompilationTask(List<Block> blocks);

    partial class Compiler
    {
        private static readonly Compiler Singleton = new Compiler();

        private Compiler() { }

        public bool Compile(string currentDir, string inputFile, TextReader @in, TextWriter output, IFileLookup lookup)
        {
            CompilationTask noop = (List<Block> blocks) => blocks;

            var tasks = new List<CompilationTask>()
            {
                Tasks.Using.Task,
                References.Task,
                Charsets.Task,
                Tasks.Import.Task,
                Sprite.Task,
                Mixin.Task,
                Unroll.Task,
                Tasks.Media.Task,
                Includes.Task,
                Evaluate.Task,
                Important.Task,
                NoOps.Task,
                FontFace.Task,
                Current.Options.HasFlag(Options.Minify) ? Minify.Task : noop,
                Current.Options.HasFlag(Options.OptimizeCompression) ? Compress.Task : noop,
                Write.Task,
                WriteSprites.Task
            };

            try
            {
                Current.SetWorkingDirectory(currentDir);
                Current.SetInitialFile(inputFile);
                Current.SetFileLookup(lookup);
                Current.SetOutputStream(output);

                var blocks = Parse.Task(@in);

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
