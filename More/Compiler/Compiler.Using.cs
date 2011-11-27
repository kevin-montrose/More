using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using More.Model;
using More.Helpers;
using System.IO;

namespace More.Compiler
{
    partial class Compiler
    {
        private List<Block> EvaluateUsings(string initialFile, List<Block> initialStatements, IFileLookup lookup)
        {
            var imports = new Dictionary<string, Tuple<Using, List<Block>>>();
            imports[initialFile] = Tuple.Create((Using)null, initialStatements);

            var unresolved = new Queue<Using>();
            initialStatements.OfType<Using>().Each(a => unresolved.Enqueue(a));

            Func<bool> anyNotInProgress =
                delegate
                {
                    return
                        unresolved.Any(a =>
                        {
                            var file = a.RawPath;
                            file = file.Replace('/', Path.DirectorySeparatorChar).RebaseFile(initialFile);

                            return !InProgressParsing(file);
                        }
                        );
                };

            while (unresolved.Count > 0)
            {
                var toResolve = unresolved.Dequeue();

                var file = toResolve.RawPath;
                file = file.Replace('/', Path.DirectorySeparatorChar).RebaseFile(initialFile);

                if (InProgressParsing(file) && anyNotInProgress())
                {
                    unresolved.Enqueue(toResolve);
                    continue;
                }

                using (var @in = lookup.Find(file))
                {
                    var statements = ParseStream(file, @in);

                    if (statements == null)
                    {
                        Current.RecordError(ErrorType.Compiler, toResolve, "Could not resolve @using '" + toResolve.RawPath + "'");
                        continue;
                    }

                    imports[file] = Tuple.Create(toResolve, statements);

                    statements.OfType<Using>().Where(a => !imports.ContainsKey(a.RawPath.RebaseFile(initialFile))).Each(a => unresolved.Enqueue(a));
                }
            }

            // Can't nest @media via @using
            foreach (var loaded in imports)
            {
                if (loaded.Value.Item1 == null || loaded.Value.Item1.ForMedia.Count() == 0) continue;

                if (loaded.Value.Item2.OfType<MediaBlock>().Count() != 0)
                {
                    Current.RecordError(ErrorType.Compiler, loaded.Value.Item1, "Cannot nest @media rules via @imports");
                }
            }

            // Can't continue if there's goofy nesting going on
            if (Current.HasErrors()) throw new StoppedCompilingException();

            var ret = new List<Block>();

            foreach (var loaded in imports.Where(w => w.Value.Item1 == null || w.Value.Item1.ForMedia.Count() == 0))
            {
                ret.AddRange(loaded.Value.Item2);
            }

            foreach (var loaded in imports.Where(w => w.Value.Item1 != null && w.Value.Item1.ForMedia.Count() > 0))
            {
                var statements = loaded.Value.Item2;
                ret.AddRange(statements.OfType<MixinBlock>());
                ret.AddRange(statements.OfType<KeyFramesBlock>());
                ret.AddRange(statements.OfType<FontFaceBlock>());
                var inner = statements.Where(w => !(w is MixinBlock || w is KeyFramesBlock || w is FontFaceBlock));

                ret.Add(new MediaBlock(loaded.Value.Item1.ForMedia.ToList(), inner.ToList(), loaded.Value.Item1.Start, loaded.Value.Item1.Stop, loaded.Value.Item1.FilePath));
            }

            return ret;
        }
    }
}
