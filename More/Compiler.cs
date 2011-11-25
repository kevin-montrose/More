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

namespace More
{
    class StoppedCompilingException : Exception { }

    class Compiler
    {
        private static readonly Compiler Singleton = new Compiler();

        private object SyncLock = new object();

        private Dictionary<string, List<Block>> FileCache = new Dictionary<string, List<Block>>();
        private HashSet<string> InProgress = new HashSet<string>();

        private Compiler() { }

        internal void ClearFileCache()
        {
            FileCache.Clear();
            InProgress.Clear();
        }

        public bool InProgressParsing(string filePath)
        {
            lock (SyncLock)
            {
                return InProgress.Contains(filePath);
            }
        }

        public List<Block> ParseStream(string filePath, TextReader @in)
        {
            lock (SyncLock)
            {
                List<Block> cached;
                if (FileCache.TryGetValue(filePath, out cached))
                    return cached;

                if (InProgress.Contains(filePath))
                {
                    while (!FileCache.ContainsKey(filePath))
                        Monitor.Wait(SyncLock);

                    return FileCache[filePath];
                }
                else
                {
                    InProgress.Add(filePath);
                }
            }

            var newParser = Parser.Parser.CreateParser();

            var ret = newParser.Parse(filePath, @in);

            if (ret == null)
            {
                lock (SyncLock)
                {
                    FileCache[filePath] = ret;
                    InProgress.Remove(filePath);
                    Monitor.PulseAll(SyncLock);
                }
                return ret;
            }

            var lastImport = ret.LastOrDefault(r => r is Import);
            var firstNonImport = ret.FirstOrDefault(r => !(r is Import));

            if (lastImport == null || firstNonImport == null)
            {
                lock (SyncLock)
                {
                    FileCache[filePath] = ret;
                    InProgress.Remove(filePath);
                    Monitor.PulseAll(SyncLock);
                }
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
                        if (ret[i] is Import)
                        {
                            Current.RecordWarning(ErrorType.Parser, ret[i], "@import should appear before any other statements.  Statement will be moved.");
                        }
                    }
                }
            }

            lock (SyncLock)
            {
                FileCache[filePath] = ret;
                InProgress.Remove(filePath);
                Monitor.PulseAll(SyncLock);
            }

            return ret;
        }

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

                    if(statements == null)
                    {
                        Current.RecordError(ErrorType.Compiler, toResolve, "Could not resolve @using '"+toResolve.RawPath+"'");
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

        private Dictionary<string, V> MapAndWarnDupe<T, V>(IEnumerable<T> toMap, Func<T, string> key, Func<T, V> value = null)
        {
            var ret = new Dictionary<string, V>();

            var stopCompiling = false;

            foreach (var v in toMap.GroupBy(key))
            {
                if (v.Count() == 1)
                {
                    ret[v.Key] = value(v.Single());
                    continue;
                }

                Current.RecordError(ErrorType.Compiler, Position.NoSite, v.Key + " is defined " + v.Count() + " times.");
                stopCompiling = true;
            }

            if (stopCompiling)
            {
                throw new StoppedCompilingException();
            }

            return ret;
        }

        internal List<Block> BindAndEvaluateMixins(List<Block> statements)
        {
            var ret = new List<Block>();

            var mixins = MapAndWarnDupe(statements.OfType<MixinBlock>(), s => s.Name, v => v);
            var variables = MapAndWarnDupe(statements.OfType<MoreVariable>(), s => s.Name, v => v.Value);

            var globalScope = new Scope(variables, mixins);

            Current.SetGlobalScope(globalScope);

            foreach (var charset in statements.OfType<CssCharset>())
            {
                ret.Add(charset.Bind(globalScope));
            }

            foreach (var import in statements.OfType<Import>())
            {
                ret.Add(import.Bind(globalScope));
            }

            foreach (var block in statements.OfType<SelectorAndBlock>())
            {
                ret.Add(block.BindAndEvaluateMixins());
            }

            foreach (var animation in statements.OfType<KeyFramesBlock>())
            {
                var innerVariables = MapAndWarnDupe(animation.Variables, s => s.Name, s => s.Value);
                var innerScope = globalScope.Push(innerVariables, new Dictionary<string, MixinBlock>(), animation);

                var frames = new List<KeyFrame>();
                foreach (var frame in animation.Frames)
                {
                    var blockEquivalent = new SelectorAndBlock(InvalidSelector.Singleton, frame.Properties, frame.Start, frame.Stop, frame.FilePath);
                    var bound = blockEquivalent.BindAndEvaluateMixins(innerScope);

                    frames.Add(new KeyFrame(frame.Percentages.ToList(), bound.Properties.ToList(), frame.Start, frame.Stop, frame.FilePath));
                }

                ret.Add(new KeyFramesBlock(animation.Prefix, animation.Name, frames.ToList(), animation.Variables.ToList(), animation.Start, animation.Stop, animation.FilePath));
            }

            foreach (var media in statements.OfType<MediaBlock>())
            {
                var innerRet = new List<Block>();

                var innerVariable = MapAndWarnDupe(media.Blocks.OfType<MoreVariable>(), s => s.Name, v => v.Value);
                var innerScope = globalScope.Push(innerVariable, new Dictionary<string, MixinBlock>(), media);

                foreach (var block in media.Blocks.OfType<SelectorAndBlock>())
                {
                    innerRet.Add(block.BindAndEvaluateMixins(innerScope));
                }

                ret.Add(new MediaBlock(media.ForMedia.ToList(), innerRet, media.Start, media.Stop, media.FilePath));
            }

            foreach (var font in statements.OfType<FontFaceBlock>())
            {
                var blockEquiv = new SelectorAndBlock(InvalidSelector.Singleton, font.Properties, font.Start, font.Stop, font.FilePath);
                var bound = blockEquiv.BindAndEvaluateMixins(globalScope);

                ret.Add(new FontFaceBlock(bound.Properties.ToList(), font.Start, font.Stop, font.FilePath));
            }

            return ret;
        }

        internal List<Block> MergeMedia(List<Block> statements)
        {
            var ret = new List<Block>();

            ret.AddRange(statements.Where(w => !(w is MediaBlock)));

            var mediaRules = 
                statements
                    .OfType<MediaBlock>()
                    .GroupBy(g => string.Join(",", g.ForMedia.OrderBy(m => m)));

            foreach(var m in mediaRules)
            {
                var media = new List<Media>();
                foreach (var x in m.Key.Split(',')) media.Add((Media)Enum.Parse(typeof(Media), x));

                var rules = new List<Block>();
                m.Each(a => rules.AddRange(a.Blocks));

                ret.Add(new MediaBlock(media, rules, -1, -1, null));
            }

            return ret;
        }

        internal List<Block> UnrollNestedBlocks(List<Block> statements)
        {
            var ret = new List<Block>();

            foreach (var statement in statements)
            {
                var block = statement as SelectorAndBlock;

                if (block != null)
                {
                    ret.AddRange(block.UnrollNestedBlocks());
                }
                else
                {
                    var media = statement as MediaBlock;
                    if (media != null)
                    {
                        var unrolled = UnrollNestedBlocks(media.Blocks.ToList());
                        ret.Add(new MediaBlock(media.ForMedia.ToList(), unrolled, media.Start, media.Stop, media.FilePath));
                    }
                    else
                    {
                        // KeyFrameDeclarations need special validation
                        var keyframe = statement as KeyFramesBlock;
                        if (keyframe != null)
                        {
                            foreach (var frame in keyframe.Frames)
                            {
                                foreach (var rule in frame.Properties)
                                {
                                    if (rule is NestedBlockProperty)
                                    {
                                        Current.RecordError(ErrorType.Compiler, rule, "Keyframes cannot include nested blocks");
                                        // we can continue from this, so do so
                                    }
                                }
                            }
                        }

                        // FontFaceDeclarations as well
                        var fontface = statement as FontFaceBlock;
                        if (fontface != null)
                        {
                            foreach (var rule in fontface.Properties)
                            {
                                if (rule is NestedBlockProperty)
                                {
                                    Current.RecordError(ErrorType.Compiler, rule, "@font-face declarations cannot include nested blocks");
                                    // we can continue
                                }
                            }
                        }
                        
                        ret.Add(statement);
                    }
                }
            }

            return ret;
        }

        private List<Block> EvaluateValues(List<Block> statements)
        {
            var ret = new List<Block>();

            foreach (var statement in statements)
            {
                var block = statement as SelectorAndBlock;
                var media = statement as MediaBlock;
                var keyframes = statement as KeyFramesBlock;
                var fontface = statement as FontFaceBlock;
                if (block == null && media == null && keyframes == null && fontface == null)
                {
                    ret.Add(statement);
                    continue;
                }

                if (block != null)
                {
                    var processedRules = new List<NameValueProperty>();

                    // At this point, it is an error for any other type of rule to exist
                    foreach (var rule in block.Properties.Cast<NameValueProperty>())
                    {
                        var value = rule.Value.Evaluate();
                        while (value.NeedsEvaluate) value = value.Evaluate();

                        if (!(value is ExcludeFromOutputValue))
                        {
                            processedRules.Add(new NameValueProperty(rule.Name, value, rule.Start, rule.Stop, rule.FilePath));
                        }
                    }

                    ret.Add(new SelectorAndBlock(block.Selector, processedRules, block.Start, block.Stop, block.FilePath));
                }

                if (media != null)
                {
                    var evaluated = EvaluateValues(media.Blocks.ToList());

                    ret.Add(new MediaBlock(media.ForMedia.ToList(), evaluated, media.Start, media.Stop, media.FilePath));
                }

                if (keyframes != null)
                {
                    var frames = new List<KeyFrame>();
                    foreach (var frame in keyframes.Frames)
                    {
                        var blockEquiv = new SelectorAndBlock(InvalidSelector.Singleton, frame.Properties, frame.Start, frame.Stop, frame.FilePath);
                        var evald = EvaluateValues(new List<Block>() { blockEquiv });
                        frames.Add(new KeyFrame(frame.Percentages.ToList(), ((SelectorAndBlock)evald[0]).Properties.ToList(), frame.Start, frame.Stop, frame.FilePath));
                    }

                    ret.Add(new KeyFramesBlock(keyframes.Prefix, keyframes.Name, frames, keyframes.Variables.ToList(), keyframes.Start, keyframes.Stop, keyframes.FilePath));
                }

                if (fontface != null)
                {
                    var processedRules = new List<Property>();
                    foreach (var rule in fontface.Properties.Cast<NameValueProperty>())
                    {
                        var value = rule.Value.Evaluate();
                        while (value.NeedsEvaluate) value = value.Evaluate();

                        if (!(value is ExcludeFromOutputValue))
                        {
                            processedRules.Add(new NameValueProperty(rule.Name, value, rule.Start, rule.Stop, rule.FilePath));
                        }
                    }

                    ret.Add(new FontFaceBlock(processedRules, fontface.Start, fontface.Stop, fontface.FilePath));
                }
            }

            return ret;
        }

        private List<Block> CopyIncludes(List<Block> unrolled, List<SelectorAndBlock> parent = null)
        {
            var ret = new List<Block>();

            var forLookup = unrolled.OfType<SelectorAndBlock>().ToList();

            // At this point, unrolled blocks have only NameValue (unevaluated) and Includes
            foreach (var statement in unrolled)
            {
                var block = statement as SelectorAndBlock;
                var media = statement as MediaBlock;
                var keyframes = statement as KeyFramesBlock;
                var fontface = statement as FontFaceBlock;
                if (block == null && media == null && keyframes == null && fontface == null)
                {
                    ret.Add(statement);
                    continue;
                }

                if (block != null)
                {
                    var simple = block.Properties.OfType<NameValueProperty>().ToList();
                    var includes = block.Properties.OfType<IncludeSelectorProperty>();

                    var @override = new List<NameValueProperty>();

                    includes.Each(e =>
                    {
                        if (e.Overrides)
                        {
                            @override.AddRange(e.LookupMatch(forLookup, parent: parent));
                        }
                        else
                        {
                            simple.AddRange(e.LookupMatch(forLookup, parent: parent));
                        }
                    }
                    );

                    var doubleDefined = false;
                    foreach (var e in @override.GroupBy(g => g.Name).Where(g => g.Count() > 1))
                    {
                        Current.RecordError(ErrorType.Compiler, block, "After resolving selector includes, the [" + e.Key + "] rule would be included as an override " + e.Count() + " times.");
                        doubleDefined = true;
                    }
                    if (doubleDefined) throw new StoppedCompilingException();

                    foreach (var o in @override)
                    {
                        simple.RemoveAll(e => e.Name == o.Name);
                        simple.Add(o);
                    }

                    ret.Add(new SelectorAndBlock(block.Selector, simple, block.Start, block.Stop, block.FilePath));
                }

                if (media != null)
                {
                    var subBlocks = media.Blocks.ToList();
                    var copied = CopyIncludes(subBlocks, parent: forLookup);

                    ret.Add(new MediaBlock(media.ForMedia.ToList(), copied, media.Start, media.Stop, media.FilePath));
                }

                if (keyframes != null)
                {
                    var frames = new List<KeyFrame>();

                    foreach (var frame in keyframes.Frames)
                    {
                        var blockEquiv = new SelectorAndBlock(InvalidSelector.Singleton, frame.Properties, frame.Start, frame.Stop, frame.FilePath);
                        var copied = CopyIncludes(new List<Block>() { blockEquiv }, parent: parent);

                        frames.Add(new KeyFrame(frame.Percentages.ToList(), ((SelectorAndBlock)copied[0]).Properties.ToList(), frame.Start, frame.Stop, frame.FilePath));
                    }

                    ret.Add(new KeyFramesBlock(keyframes.Prefix, keyframes.Name, frames, keyframes.Variables.ToList(), keyframes.Start, keyframes.Stop, keyframes.FilePath));
                }

                if (fontface != null)
                {
                    var blockEquiv = new SelectorAndBlock(InvalidSelector.Singleton, fontface.Properties, fontface.Start, fontface.Stop, fontface.FilePath);
                    var copied = (SelectorAndBlock)CopyIncludes(new List<Block>() { blockEquiv }, parent)[0];

                    ret.Add(new FontFaceBlock(copied.Properties.ToList(), copied.Start, copied.Stop, copied.FilePath));
                }
            }

            return ret;
        }

        private List<Block> ExportSprites(string inputFile, List<Block> statements, out List<SpriteExport> sprites)
        {
            var spriteDecls = statements.OfType<SpriteBlock>().ToList();
            if (spriteDecls.Count == 0)
            {
                sprites = null;
                return statements;
            }

            sprites = new List<SpriteExport>();

            var ret = statements.Where(w => w.GetType() != typeof(SpriteBlock)).ToList();

            foreach (var sprite in spriteDecls)
            {
                var output = sprite.OutputFile.Value.RebaseFile(inputFile);

                var input = sprite.Sprites.ToDictionary(k => k.MixinName, v => v.SpriteFilePath.Value.RebaseFile(inputFile));

                var export = SpriteExport.Create(output, inputFile, input);
                sprites.Add(export);

                ret.AddRange(export.MixinEquivalents());
            }

            return ret;
        }

        private List<Block> ResolveImportant(List<Block> blocks)
        {
            var ret = new List<Block>();

            foreach (var statement in blocks)
            {
                var block = statement as SelectorAndBlock;
                var media = statement as MediaBlock;
                var keyframes = statement as KeyFramesBlock;
                var fontface = statement as FontFaceBlock;
                if (block == null && media == null && keyframes == null && fontface == null)
                {
                    ret.Add(statement);
                    continue;
                }

                if (block != null)
                {
                    var rules = new List<NameValueProperty>();

                    foreach (var group in block.Properties.Cast<NameValueProperty>().GroupBy(g => g.Name))
                    {
                        if (group.Count() == 1)
                        {
                            rules.Add(group.Single());
                        }
                        else
                        {
                            var important = group.SingleOrDefault(g => g.Value.IsImportant());

                            if (important == null)
                            {
                                Current.RecordWarning(ErrorType.Compiler, block, "More than one definition for [" + group.Key + "], did you mean for one to be !important?");

                                rules.AddRange(group);
                            }
                            else
                            {
                                rules.Add(important);
                            }
                        }
                    }

                    ret.Add(new SelectorAndBlock(block.Selector, rules, block.Start, block.Stop, block.FilePath));
                }

                if (media != null)
                {
                    var resolved = ResolveImportant(media.Blocks.ToList());
                    ret.Add(new MediaBlock(media.ForMedia.ToList(), resolved, media.Start, media.Stop, media.FilePath));
                }

                if (keyframes != null)
                {
                    var frames = new List<KeyFrame>();
                    foreach (var frame in keyframes.Frames)
                    {
                        var blockEquiv = new SelectorAndBlock(InvalidSelector.Singleton, frame.Properties, frame.Start, frame.Stop, frame.FilePath);
                        var resolved = ResolveImportant(new List<Block>() { blockEquiv });

                        frames.Add(new KeyFrame(frame.Percentages.ToList(), ((SelectorAndBlock)resolved[0]).Properties.ToList(), frame.Stop, frame.Stop, frame.FilePath));
                    }

                    ret.Add(new KeyFramesBlock(keyframes.Prefix, keyframes.Name, frames, keyframes.Variables.ToList(), keyframes.Start, keyframes.Stop, keyframes.FilePath));
                }

                if (fontface != null)
                {
                    var blockEquiv = new SelectorAndBlock(InvalidSelector.Singleton, fontface.Properties, fontface.Start, fontface.Stop, fontface.FilePath);
                    var resolved = (SelectorAndBlock)ResolveImportant(new List<Block>() { blockEquiv })[0];

                    ret.Add(new FontFaceBlock(resolved.Properties.ToList(), fontface.Start, fontface.Stop, fontface.FilePath));
                }
            }

            return ret;
        }

        private List<Block> MoveCssImports(List<Block> unordered)
        {
            var imports = unordered.Where(w => w is Import);

            if (imports.Count() == 0) return unordered;

            var copy = new List<Block>(unordered.Count);
            copy.AddRange(unordered.Where(w => !(w is Import)));

            foreach (var i in imports.Reverse())
            {
                copy.Insert(0, i);
            }

            return copy;
        }

        private List<Block> ValidateCharsets(List<Block> statements)
        {
            var charsets = statements.OfType<CssCharset>();

            if (charsets.Count() == 0) return statements;

            var ret = new List<Block>(statements.Count);
            ret.AddRange(statements.Where(w => !(w is CssCharset)));

            var charsetStrings = charsets.Select(s => s.Charset.Value).Distinct();

            if (charsetStrings.Count() != 1)
            {
                foreach (var c in charsets)
                {
                    var others = string.Join(", ", charsetStrings.Where(s => s != c.Charset.Value));

                    Current.RecordError(ErrorType.Compiler, c, "@charset conflicts with " + others + ", defined elsewhere.");
                }

                return null;
            }

            ret.Insert(0, new CssCharset(new QuotedStringValue(charsetStrings.Single()), -1, -1));

            return ret;
        }

        private void VerifyBlockVariableReferences(List<string> outer, IEnumerable<Property> rules)
        {
            var locallyDeclared = new List<string>();

            rules = rules.OrderBy(o => o is VariableProperty ? 0 : 1);

            foreach (var x in rules)
            {
                if (x is VariableProperty)
                {
                    var var = (VariableProperty)x;
                    var rhs = var.Value.ReferredToVariables();
                    var earlyReferences = rhs.Where(r => !outer.Contains(r) && !locallyDeclared.Contains(r));
                    foreach (var early in earlyReferences)
                    {
                        Current.RecordError(ErrorType.Compiler, x, "@" + early + " has not been defined");
                    }

                    locallyDeclared.Add(var.Name);
                }

                if (x is NameValueProperty)
                {
                    var name = (NameValueProperty)x;
                    var rhs = name.Value.ReferredToVariables();
                    var earlyReferences = rhs.Where(r => !outer.Contains(r) && !locallyDeclared.Contains(r));
                    foreach (var early in earlyReferences)
                    {
                        Current.RecordError(ErrorType.Compiler, x, "@" + early + " has not been defined");
                    }
                }

                if (x is NestedBlockProperty)
                {
                    var newOuter = new List<string>();
                    newOuter.AddRange(outer);
                    newOuter.AddRange(locallyDeclared);
                    VerifyBlockVariableReferences(newOuter, ((NestedBlockProperty)x).Block.Properties);
                }

                if (x is MixinApplicationProperty)
                {
                    var app = (MixinApplicationProperty)x;
                    var referredTo = app.ReferredToVariables();
                    var earlyReferences = referredTo.Where(r => !outer.Contains(r) && !locallyDeclared.Contains(r));
                    foreach (var early in earlyReferences)
                    {
                        Current.RecordError(ErrorType.Compiler, x, "@" + early + " has not been defined");
                    }
                }
            }
        }

        private void VerifyMixinVariableReferences(List<string> globals, MixinBlock mixin)
        {
            var locallyDeclared = new List<string>();

            locallyDeclared.AddRange(mixin.Parameters.Select(s => s.Name));

            if (mixin.Parameters.Count() > 0)
            {
                locallyDeclared.Add("arguments");
            }

            var outer = new List<string>();
            outer.AddRange(globals);
            outer.AddRange(locallyDeclared);

            VerifyBlockVariableReferences(outer, mixin.Properties);
        }

        private void VerifyVariableReferences(List<Block> statements, List<string> globallyDeclared = null)
        {
            var globals = statements.OfType<MoreVariable>().OrderBy(a => a.Id);

            globallyDeclared = globallyDeclared ?? new List<string>();

            globallyDeclared.AddRange(statements.OfType<MixinBlock>().Select(s => s.Name));
            globallyDeclared.AddRange(BuiltInFunctions.All.Keys);

            foreach (var global in globals)
            {
                var rhs = global.Value;
                var referredTo = rhs.ReferredToVariables();
                var earlyReferences = referredTo.Where(r => !globallyDeclared.Contains(r));

                foreach (var early in earlyReferences)
                {
                    Current.RecordError(ErrorType.Compiler, global, "@" + early + " has not been defined");
                }

                globallyDeclared.Add(global.Name);
            }

            foreach (var rule in statements.OfType<SpriteBlock>())
            {
                var rhs = rule.OutputFile.ReferredToVariables();
                var earlyReferences = rhs.Where(r => !globallyDeclared.Contains(r));

                foreach (var early in earlyReferences)
                {
                    Current.RecordError(ErrorType.Compiler, rule, "@" + early + " has not been defined");
                }

                foreach (var decl in rule.Sprites)
                {
                    var declRhs = decl.SpriteFilePath.ReferredToVariables();
                    var declEarlies = declRhs.Where(r => !globallyDeclared.Contains(r));

                    foreach (var early in declEarlies)
                    {
                        Current.RecordError(ErrorType.Compiler, decl, "@" + early + " has not been defined");
                    }

                    globallyDeclared.Add(decl.MixinName);
                }
            }

            foreach (var keyframes in statements.OfType<KeyFramesBlock>())
            {
                foreach (var varDecl in keyframes.Variables)
                {
                    var rhs = varDecl.Value.ReferredToVariables();
                    var earlyRefs = rhs.Where(r => !globallyDeclared.Contains(r));
                    foreach (var early in earlyRefs)
                    {
                        Current.RecordError(ErrorType.Compiler, varDecl, "@" + early + " has not been defined");
                    }
                }

                var scoped = keyframes.Variables.Select(s => s.Name).ToList();
                scoped.AddRange(globallyDeclared);

                foreach (var frame in keyframes.Frames)
                {
                    VerifyBlockVariableReferences(scoped, frame.Properties);
                }
            }

            foreach (var mixin in statements.OfType<MixinBlock>())
            {
                VerifyMixinVariableReferences(globallyDeclared, mixin);
            }

            foreach (var rule in statements.OfType<SelectorAndBlock>())
            {
                VerifyBlockVariableReferences(globallyDeclared, rule.Properties);
            }

            foreach (var font in statements.OfType<FontFaceBlock>())
            {
                VerifyBlockVariableReferences(globallyDeclared, font.Properties);
            }

            foreach (var rule in statements.OfType<CssCharset>())
            {
                var rhs = rule.Charset.ReferredToVariables();
                var earlyReferences = rhs.Where(r => !globallyDeclared.Contains(r));

                foreach (var early in earlyReferences)
                {
                    Current.RecordError(ErrorType.Compiler, rule, "@" + early + " has not been defined");
                }
            }

            foreach (var rule in statements.OfType<Import>())
            {
                var rhs = rule.ToImport.ReferredToVariables();
                var earlyReferences = rhs.Where(r => !globallyDeclared.Contains(r));

                foreach (var early in earlyReferences)
                {
                    Current.RecordError(ErrorType.Compiler, rule, "@" + early + " has not been defined");
                }
            }

            foreach (var parts in statements.OfType<MediaBlock>())
            {
                VerifyVariableReferences(parts.Blocks.ToList(), globallyDeclared);
            }
        }

        private static List<Block> RemoveNops(List<Block> readyToWrite)
        {
            var ret = new List<Block>();

            // Remove no-ops
            foreach (var statement in readyToWrite)
            {
                var media = statement as MediaBlock;

                if (media == null)
                {
                    ret.Add(statement);
                    continue;
                }

                var subStatements = media.Blocks.ToList();

                subStatements.RemoveAll(a =>
                    {
                        var block = a as SelectorAndBlock;
                        if (block == null) return false;

                        return block.Properties.Count() == 0;
                    }
                );

                ret.Add(new MediaBlock(media.ForMedia.ToList(), subStatements, media.Start, media.Stop, media.FilePath));
            }

            ret = 
                ret.Select(
                    delegate(Block x)
                    {
                        var keyframes = x as KeyFramesBlock;
                        if (keyframes == null) return x;

                        var frames = keyframes.Frames.Where(f => f.Properties.Count() > 0).ToList();

                        return new KeyFramesBlock(keyframes.Prefix, keyframes.Name, frames, keyframes.Variables.ToList(), keyframes.Start, keyframes.Stop, keyframes.FilePath);
                    }
                ).ToList();

            ret.RemoveAll(a =>
                {
                    var block = a as SelectorAndBlock;
                    var media = a as MediaBlock;
                    var keyframes = a as KeyFramesBlock;
                    if (block == null && media == null && keyframes == null) return false;

                    if (block != null) return block.Properties.Count() == 0;
                    if (media != null) return media.Blocks.Count() == 0;
                    if (keyframes != null) return keyframes.Frames.Count() == 0;

                    throw new InvalidOperationException();
                }
            );

            return ret;
        }

        private ColorValue MinifyColor(ColorValue value)
        {
            // There's no lossless conversion for RGBA values
            if (value is RGBAColorValue) return value;

            // The smallest form of a color will *always* be the hex version or the named color version
            //   So lets build the sextuple hex version
            var red = (byte)((NumberValue)BuiltInFunctions.Red(new[] { value }, Position.NoSite)).Value;
            var green = (byte)((NumberValue)BuiltInFunctions.Green(new[] { value }, Position.NoSite)).Value;
            var blue = (byte)((NumberValue)BuiltInFunctions.Blue(new[] { value }, Position.NoSite)).Value;

            string hex;
            using (var buffer = new StringWriter())
            {
                (new HexSextupleColorValue(red, green, blue)).Write(buffer);
                hex = buffer.ToString().Substring(1);
            }

            var asNum = int.Parse(hex, System.Globalization.NumberStyles.HexNumber);
            string asNamed = null;

            if (Enum.IsDefined(typeof(NamedColor), asNum))
            {
                asNamed = Enum.GetName(typeof(NamedColor), (NamedColor)asNum);
            }

            if (asNamed.HasValue() && asNamed.Length < 7)
            {
                return new NamedColorValue((NamedColor)asNum);
            }

            if (red.ToString("x").Distinct().Count() == 1 &&
               green.ToString("x").Distinct().Count() == 1 &&
               blue.ToString("x").Distinct().Count() == 1)
            {
                return new HexTripleColorValue(red, green, blue);
            }

            return new HexSextupleColorValue(red, green, blue);
        }

        private NumberWithUnitValue MinifyNumberWithUnit(NumberWithUnitValue value)
        {
            var min = MinifyNumberValue(value);

            var ret = new NumberWithUnitValue(min.Value, value.Unit);
            string retStr;
            using (var buffer = new StringWriter())
            {
                ret.Write(buffer);
                retStr = buffer.ToString();
            }

            if (!Value.ConvertableUnits.ContainsKey(value.Unit))
            {
                return ret;
            }

            var inMM = min.Value * Value.ConvertableUnits[value.Unit];

            foreach (var unit in Value.ConvertableUnits.Keys)
            {
                var inUnit = inMM / Value.ConvertableUnits[unit];

                var newMin = new NumberWithUnitValue(MinifyNumberValue(new NumberValue(inUnit)).Value, unit);
                string newMinStr;

                using(var buffer = new StringWriter())
                {
                    newMin.Write(buffer);
                    newMinStr = buffer.ToString();
                }

                if (newMinStr.Length < retStr.Length)
                {
                    ret = newMin;
                    retStr = newMinStr;
                }
            }

            return ret;
        }

        private NumberValue MinifyNumberValue(NumberValue value)
        {
            var asStr = value.Value.ToString();
            if (asStr.Contains('.'))
            {
                asStr = asStr.TrimEnd('0', '.');
            }

            asStr = asStr.TrimStart('0');

            if (asStr.Length == 0) asStr = "0";

            return new NumberValue(decimal.Parse(asStr));
        }

        private Value MinifyValue(Value value)
        {
            var comma = value as CommaDelimittedValue;
            if (comma != null)
            {
                var minified = comma.Values.Select(s => MinifyValue(s)).ToList();

                return new CommaDelimittedValue(minified);
            }

            var compound = value as CompoundValue;
            if (compound != null)
            {
                var minified = compound.Values.Select(s => MinifyValue(s)).ToList();

                return new CompoundValue(minified);
            }

            if(value is ColorValue)
            {
                return MinifyColor((ColorValue)value);
            }

            if (value is NumberWithUnitValue)
            {
                return MinifyNumberWithUnit((NumberWithUnitValue)value);
            }

            if (value is NumberValue)
            {
                return MinifyNumberValue((NumberValue)value);
            }

            return value;
        }

        private Property MinifyRule(Property rule)
        {
            if (!(rule is NameValueProperty))
                throw new InvalidOperationException("Minify cannot be run on non name-value rules, found [" + rule + "]");

            var named = (NameValueProperty)rule;
            var value = named.Value;

            return new NameValueProperty(named.Name, MinifyValue(value));
        }

        public List<Block> Minify(List<Block> statements)
        {
            var ret = new List<Block>();

            foreach (var statement in statements)
            {
                var block = statement as SelectorAndBlock;
                if(block != null)
                {
                    var rules = new List<Property>();
                    foreach (var prop in block.Properties)
                    {
                        rules.Add(MinifyRule(prop));
                    }

                    ret.Add(new SelectorAndBlock(block.Selector, rules, block.Start, block.Stop, block.FilePath));
                    continue;
                }


                var media = statement as MediaBlock;
                if(media != null)
                {
                    var subStatements = Minify(media.Blocks.ToList());
                    ret.Add(new MediaBlock(media.ForMedia.ToList(), subStatements, media.Start, media.Stop, media.FilePath));
                    continue;
                }

                var keyframes = statement as KeyFramesBlock;
                if (keyframes != null)
                {
                    var frames = new List<KeyFrame>();
                    
                    // minify each frame
                    foreach (var frame in keyframes.Frames)
                    {
                        var blockEquiv = new SelectorAndBlock(InvalidSelector.Singleton, frame.Properties, frame.Start, frame.Stop, frame.FilePath);
                        var mind = Minify(new List<Block>() { blockEquiv });
                        frames.Add(new KeyFrame(frame.Percentages.ToList(), ((SelectorAndBlock)mind[0]).Properties.ToList(), frame.Start, frame.Stop, frame.FilePath));
                    }

                    // collapse frames if rules are identical
                    var frameMap =
                        frames.ToDictionary(
                            d =>
                            {
                                using (var str = new StringWriter())
                                using (var css = new MinimalCssWriter(str))
                                {
                                    foreach (var rule in d.Properties.Cast<NameValueProperty>())
                                    {
                                        css.WriteRule(rule);
                                    }

                                    return str.ToString();
                                }
                            },
                            d => d
                        );

                    frames.Clear();
                    foreach (var frame in frameMap.GroupBy(k => k.Key))
                    {
                        var allPercents = new List<decimal>();
                        foreach (var f in frame)
                        {
                            allPercents.AddRange(f.Value.Percentages);
                        }

                        var urFrame = frame.First().Value;

                        frames.Add(new KeyFrame(allPercents, urFrame.Properties.ToList(), urFrame.Start, urFrame.Stop, urFrame.FilePath));
                    }

                    ret.Add(new KeyFramesBlock(keyframes.Prefix, keyframes.Name, frames, keyframes.Variables.ToList(), keyframes.Start, keyframes.Stop, keyframes.FilePath));
                    continue;
                }

                ret.Add(statement);
            }

            return ret;
        }

        public List<Block> OptimizeForCompression(List<Block> statements)
        {
            var before = (decimal)LZ77Optimizer.GZipSize(statements.Cast<IWritable>());

            var ret =  LZ77Optimizer.Optimize(statements);

            var after = (decimal)LZ77Optimizer.GZipSize(ret.Cast<IWritable>());

            var saving = decimal.Round(((before - after) / before) * 100m, 1, MidpointRounding.AwayFromZero);

            Current.RecordInfo("Compression: Before " + before + " bytes, after " + after + " bytes\r\nSavings " + saving + "%");

            return ret;
        }

        private void ValidateFontFace(List<Block> statements)
        {
            var fonts = statements.OfType<FontFaceBlock>().ToList();
            if (fonts.Count == 0) return;

            var map = new Dictionary<string, FontFaceBlock>();

            // check for font-family & src rules
            foreach (var font in fonts)
            {
                var fontFamily = font.Properties.Cast<NameValueProperty>().FirstOrDefault(a => a.Name.Equals("font-family", StringComparison.InvariantCultureIgnoreCase));
                var src = font.Properties.Cast<NameValueProperty>().FirstOrDefault(a => a.Name.Equals("src", StringComparison.InvariantCultureIgnoreCase));

                if (src == null)
                {
                    Current.RecordError(ErrorType.Compiler, font, "No src rule found in @font-face declaration");
                }


                if (fontFamily == null)
                {
                    Current.RecordError(ErrorType.Compiler, font, "No font-family rule found in @font-face declaration");
                }
                else
                {
                    using(var mem = new StringWriter())
                    {
                        fontFamily.Value.Write(mem);
                        map[mem.ToString()] = font;
                    }
                }
            }

            var fontRules = new List<NameValueProperty>();

            foreach (var block in statements.OfType<SelectorAndBlock>())
            {
                fontRules.AddRange(block.Properties.Cast<NameValueProperty>().Where(w => w.Name.Equals("font", StringComparison.InvariantCultureIgnoreCase) || w.Name.Equals("font-family")));
            }

            foreach (var media in statements.OfType<MediaBlock>())
            {
                foreach (var block in media.Blocks.OfType<SelectorAndBlock>())
                {
                    fontRules.AddRange(block.Properties.Cast<NameValueProperty>().Where(w => w.Name.Equals("font", StringComparison.InvariantCultureIgnoreCase) || w.Name.Equals("font-family")));
                }
            }

            foreach (var keyframes in statements.OfType<KeyFramesBlock>())
            {
                foreach (var frame in keyframes.Frames)
                {
                    fontRules.AddRange(frame.Properties.Cast<NameValueProperty>().Where(w => w.Name.Equals("font", StringComparison.InvariantCultureIgnoreCase) || w.Name.Equals("font-family")));
                }
            }

            var raw =
                fontRules.Select(
                    s =>
                    {
                        using (var mem = new StringWriter())
                        {
                            s.Value.Write(mem);

                            return mem.ToString();
                        }
                    }
                );

            foreach (var font in map.Keys)
            {
                if (!raw.Any(a => a.IndexOf(font, StringComparison.InvariantCultureIgnoreCase) != -1))
                {
                    Current.RecordWarning(ErrorType.Compiler, map[font], "`" + font + "` does not appear to be used in any CSS rule.");
                }
            }
        }

        public bool Compile(string currentDir, string inputFile, TextReader @in, TextWriter output, IFileLookup lookup, out List<string> sprites)
        {
            sprites = null;

            try
            {
                Current.SetWorkingDirectory(currentDir);

                List<SpriteExport> spriteExports;

                var initial = ParseStream(inputFile, @in);

                if (initial == null) { return false; }

                if (Current.HasErrors()) { return false; }

                var flattened = EvaluateUsings(inputFile, initial, lookup);

                if (Current.HasErrors()) { return false; }

                VerifyVariableReferences(flattened);

                if (Current.HasErrors()) { return false; }

                var valid = ValidateCharsets(flattened);

                if (Current.HasErrors()) { return false; }

                var ordered = MoveCssImports(valid);

                if (Current.HasErrors()) { return false; }

                var sprited = ExportSprites(inputFile, ordered, out spriteExports);

                if (Current.HasErrors()) { return false; }

                var bound = BindAndEvaluateMixins(sprited);

                if (Current.HasErrors()) { return false; }

                var unrolled = UnrollNestedBlocks(bound);

                if (Current.HasErrors()) { return false; }

                var mergedMedia = MergeMedia(unrolled);

                if (Current.HasErrors()) { return false; }

                var included = CopyIncludes(mergedMedia);

                if (Current.HasErrors()) { return false; }

                var evaluated = EvaluateValues(included);

                if (Current.HasErrors()) { return false; }

                var readyToWrite = ResolveImportant(evaluated);

                if (Current.HasErrors()) { return false; }

                var minimal = RemoveNops(readyToWrite);

                if (Current.HasErrors()) { return false; }

                ValidateFontFace(readyToWrite);

                if (Current.HasErrors()) { return false; }

                List<Block> minified;

                if (Current.Options.HasFlag(Options.Minify))
                {
                    minified = Minify(minimal);

                    if (Current.HasErrors()) { return false; }
                }
                else
                {
                    minified = minimal;
                }

                List<Block> compressionOptimized;

                if (Current.Options.HasFlag(Options.OptimizeCompression))
                {
                    compressionOptimized = OptimizeForCompression(minified);

                    if (Current.HasErrors()) { return false; }
                }
                else
                {
                    compressionOptimized = minified;
                }

                var writer = Current.GetWriter(output);
                foreach (var statement in compressionOptimized.Cast<IWritable>())
                {
                    statement.Write(writer);
                }

                sprites = new List<string>();

                if (spriteExports != null)
                {
                    foreach (var sprite in spriteExports)
                    {
                        sprite.Sprite.Save(sprite.OutputFile);
                        sprite.Sprite.Dispose();
                    }

                    sprites = spriteExports.Select(s => s.OutputFile.Replace('/', Path.DirectorySeparatorChar)).ToList();
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
