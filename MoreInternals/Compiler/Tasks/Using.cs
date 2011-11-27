using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;
using MoreInternals.Helpers;
using System.IO;

namespace MoreInternals.Compiler.Tasks
{
    /// <summary>
    /// Resolves all @using directives.
    /// 
    /// After this has run, we'll have a single flat collection of all more blocks and what not
    /// in a collection; while @imports persist (and will be written to disk) no more @usings will
    /// be found once this has returned.
    /// </summary>
    public class Using
    {
        public static List<Block> Task(List<Block> initialStatements)
        {
            var lookup = Current.FileLookup;
            var initialFile = Current.InitialFilePath;

            return EvaluateUsingsImpl(initialFile, initialStatements, lookup);
        }

        private static List<Block> EvaluateUsingsImpl(string initialFile, List<Block> initialStatements, IFileLookup lookup)
        {
            var imports = new List<Tuple<Model.Using, List<Block>>>();
            imports.Add(Tuple.Create((Model.Using)null, initialStatements));

            var unresolved = new List<Tuple<string, Model.Using>>();
            unresolved.AddRange(
                initialStatements.OfType<Model.Using>().Select(
                    u =>
                        Tuple.Create(
                            u.RawPath.Replace('/', Path.DirectorySeparatorChar).RebaseFile(initialFile),
                            u
                        )
                )
            );

            while (unresolved.Count > 0)
            {
                var allFiles = unresolved.Select(s => s.Item1);

                var loaded =
                    Current.FileCache.Available(
                        allFiles,
                        delegate(string file)
                        {
                            var toResolve = unresolved.Single(w => w.Item1 == file);

                            using (var @in = lookup.Find(file))
                            {
                                var newParser = Parser.Parser.CreateParser();
                                var statements = Parse.CheckPostImport(newParser.Parse(file, @in));

                                if (statements == null)
                                {
                                    Current.RecordError(ErrorType.Compiler, toResolve.Item2, "Could not resolve @using '" + toResolve.Item2.RawPath + "'");
                                    return null;
                                }

                                return statements;
                            }
                        }
                    );

                var @using = unresolved.Single(s => s.Item1 == loaded.Item1);

                imports.Add(Tuple.Create(@using.Item2, loaded.Item2));

                unresolved.RemoveAll(a => a.Item1 == loaded.Item1);

                if (loaded.Item2 != null)
                {
                    var references = loaded.Item2.OfType<Model.Using>().Where(a => !imports.Any(x => x.Item1 == a));
                    
                    foreach(var subRef in references)
                    {
                        unresolved.Add(
                            Tuple.Create(
                                subRef.RawPath.Replace('/', Path.DirectorySeparatorChar).RebaseFile(loaded.Item1),
                                subRef
                            )
                        );
                    }
                }
            }

            // Can't nest @media via @using
            foreach (var loaded in imports)
            {
                if (loaded.Item1 == null || loaded.Item1.ForMedia.Count() == 0) continue;

                if (loaded.Item2.OfType<MediaBlock>().Count() != 0)
                {
                    Current.RecordError(ErrorType.Compiler, loaded.Item1, "Cannot nest @media rules via @imports");
                }
            }

            // Can't continue if there's goofy nesting going on
            if (Current.HasErrors()) throw new StoppedCompilingException();

            var ret = new List<Block>();

            foreach (var loaded in imports.Where(w => w.Item1 == null || w.Item1.ForMedia.Count() == 0))
            {
                ret.AddRange(loaded.Item2);
            }

            foreach (var loaded in imports.Where(w => w.Item1 != null && w.Item1.ForMedia.Count() > 0))
            {
                var statements = loaded.Item2;
                ret.AddRange(statements.OfType<MixinBlock>());
                ret.AddRange(statements.OfType<KeyFramesBlock>());
                ret.AddRange(statements.OfType<FontFaceBlock>());
                var inner = statements.Where(w => !(w is MixinBlock || w is KeyFramesBlock || w is FontFaceBlock));

                ret.Add(new MediaBlock(loaded.Item1.ForMedia.ToList(), inner.ToList(), loaded.Item1.Start, loaded.Item1.Stop, loaded.Item1.FilePath));
            }

            return ret;
        }
    }
}
