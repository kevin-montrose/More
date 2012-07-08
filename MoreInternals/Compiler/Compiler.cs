using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MoreInternals.Model;
using MoreInternals.Parser;
using MoreInternals.Helpers;
using System.Threading;
using System.Collections.Concurrent;
using System.IO.Compression;
using MoreInternals.Compiler.Tasks;

namespace MoreInternals.Compiler
{
    internal delegate List<Block> CompilationTask(List<Block> blocks);

    public partial class Compiler
    {
        private static readonly Compiler Singleton = new Compiler();

        private Compiler() { }

        public bool Compile(string currentDir, string inputFile, string outputFile, IFileLookup lookup, Context context, Options options, WriterMode writerMode)
        {
            Current.SetContext(context);
            Current.SetWriterMode(writerMode);
            Current.SetOptions(options);

            CompilationTask noop = (List<Block> blocks) => blocks;

            var tasks = new List<CompilationTask>()
            {
                Tasks.Using.Task,
                References.Task,
                Charsets.Task,
                Tasks.Import.Task,
                Reset.Task,
                Sprite.Task,
                Mixin.Task,
                UnrollNestedMedia.Task,
                UnrollNestedSelectors.Task,
                UnrollVerify.Task,
                Tasks.Media.Task,
                Includes.Task,
                ResetIncludes.Task,
                Evaluate.Task,
                Important.Task,
                NoOps.Task,
                FontFace.Task,
                ResetReOrder.Task,
                Verify.Task,
                Current.Options.HasFlag(Options.Minify) ? Minify.Task : noop,
                Collapse.Task,
                WriteSprites.Task,
                Current.Options.HasFlag(Options.GenerateCacheBreakers) ? CacheBreak.Task : noop,
                Current.Options.HasFlag(Options.AutomateVendorPrefixes) ? AutoPrefix.Task : noop,
                Write.Task
            };

            try
            {
                Current.SetWorkingDirectory(currentDir);
                Current.SetFileLookup(lookup);

                inputFile = Path.IsPathRooted(inputFile) ? inputFile : inputFile.RebaseFile();
                Current.SetInitialFile(inputFile);

                List<Block> blocks;
                using (var stream = lookup.Find(inputFile))
                {
                    blocks = Parse.Task(stream);
                }

                using (var output = lookup.OpenWrite(outputFile))
                {
                    Current.SetOutputStream(output);

                    if (blocks == null) return false;

                    foreach (var task in tasks)
                    {
                        blocks = task(blocks);
                        if (Current.HasErrors()) return false;
                    }
                }

                Current.Dependecies.FileCompiled(inputFile, blocks);

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
