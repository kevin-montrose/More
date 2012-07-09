using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;
using System.IO;

namespace MoreInternals.Helpers
{
    public class DependencyGraph
    {
        // When "key" is changed, "value" has been invalidated
        private Dictionary<string, HashSet<string>> InvertedDependencies = new Dictionary<string, HashSet<string>>();
        
        // Some references are auto-generated (like sprites), so don't count those as "changes"
        private HashSet<string> Exclusions = new HashSet<string>();

        private DependencyGraph(Dictionary<string, HashSet<string>> known)
        {
            InvertedDependencies = known;
        }

        internal DependencyGraph() { }

        internal DependencyGraph Merge(DependencyGraph other)
        {
            var ret = new Dictionary<string, HashSet<string>>();

            foreach (var otherDs in other.InvertedDependencies)
            {
                HashSet<string> selfDs;
                if (!InvertedDependencies.TryGetValue(otherDs.Key, out selfDs))
                {
                    selfDs = new HashSet<string>();
                }

                ret[otherDs.Key] = new HashSet<string>(selfDs.Union(otherDs.Value));
            }

            return new DependencyGraph(ret);
        }

        /// <summary>
        /// Record that `file` depends on `dependsOn`.
        /// </summary>
        private void Add(string file, string dependsOn)
        {
            HashSet<string> use;
            if (!InvertedDependencies.TryGetValue(dependsOn, out use))
            {
                use = new HashSet<string>();
                InvertedDependencies[dependsOn] = use;
            }

            use.Add(file);
        }

        /// <summary>
        /// Expects that path is absolute.
        /// </summary>
        public bool Contains(string path)
        {
            return !Exclusions.Contains(path) && InvertedDependencies.ContainsKey(path);
        }

        internal void SpritesResolved(SpriteBlock block)
        {
            var relativeFiles = block.Sprites.Select(s => s.SpriteFilePath.Value).ToList();

            foreach (var file in relativeFiles)
            {
                var rebased = file.Replace('/', Path.DirectorySeparatorChar).RebaseFile(block.FilePath);
                if (!Current.FileLookup.Exists(rebased)) continue;

                Add(block.FilePath, rebased);
            }

            Exclusions.Add(block.OutputFile.Value.Replace('/', Path.DirectorySeparatorChar).RebaseFile(block.FilePath));
        }

        internal void UsingResolved(string compiledFile, string usedFile)
        {
            var rebased = usedFile.Replace('/', Path.DirectorySeparatorChar).RebaseFile(compiledFile);

            if (!Current.FileLookup.Exists(rebased)) return;

            Add(compiledFile, rebased);
        }

        private static List<UrlValue> GetUrlValues(IEnumerable<Value> values)
        {
            var ret = new List<UrlValue>();

            foreach(var v in values)
            {
                var asCompound = v as CompoundValue;
                if (asCompound != null)
                {
                    ret.AddRange(GetUrlValues(asCompound.Values));
                    continue;
                }

                var asComma = v as CommaDelimittedValue;
                if(asComma != null)
                {
                    ret.AddRange(GetUrlValues(asComma.Values));
                    continue;
                }

                var asUrl = v as UrlValue;
                if (asUrl != null)
                {
                    ret.Add(asUrl);
                    continue;
                }
            }

            return ret;
        }

        internal void FileCompiled(string compiledFile, List<Block> finalOutput)
        {
            // Change youself?  yeah, that's a dependency
            Add(compiledFile, compiledFile);

            var allProps =
                finalOutput.OfType<SelectorAndBlock>().SelectMany(s => s.Properties)
                .Union(
                    finalOutput.OfType<MediaBlock>()
                    .Select(m => m.Blocks).OfType<SelectorAndBlock>()
                    .SelectMany(s => s.Properties)
                );

            var values = 
                allProps.OfType<NameValueProperty>()
                .Select(s => s.Value)
                .Union(finalOutput.OfType<Import>().Select(s => s.ToImport))
                .ToList();

            var urls = GetUrlValues(values);

            foreach (var url in urls)
            {
                var path = url.UrlPath;
                string strPath = null;

                if(path is StringValue)
                {
                    strPath = ((StringValue)path).Value;
                }

                if(path is QuotedStringValue)
                {
                    strPath = ((QuotedStringValue)path).Value;
                }

                // Malformed CSS, carry on
                if (!strPath.HasValue()) continue;

                strPath = strPath.Replace('/', Path.DirectorySeparatorChar).RebaseFile(compiledFile);

                // There's not guarantee that we can find what's referred to by url, it could be something crazy
                if (!Current.FileLookup.Exists(strPath)) continue;

                Add(compiledFile, strPath);
            }
        }

        private List<string> TraceTo(string fromFile, IEnumerable<string> toAnyOf)
        {
            var candidates = new List<string>();
            var alreadyChecked = new HashSet<string>();

            candidates.Add(fromFile);

            var ret = new List<string>();

            while (candidates.Count > 0)
            {
                var toCheck = candidates[0];
                candidates.RemoveAt(0);

                if (toCheck.In(toAnyOf))
                {
                    ret.Add(toCheck);
                }

                alreadyChecked.Add(toCheck);

                HashSet<string> furtherDependencies;
                if (InvertedDependencies.TryGetValue(toCheck, out furtherDependencies))
                {
                    candidates.AddRange(furtherDependencies.Where(w => !alreadyChecked.Contains(w)));
                }
            }

            return ret;
        }

        /// <summary>
        /// Returns the members in rootFiles that need to be recompiled given that something in changedFiles
        /// has changed.
        /// 
        /// `changedFiles` and `rootFiles` are expected to contain only absolute paths.
        /// </summary>
        public IEnumerable<string> NeedRecompilation(IEnumerable<string> changedFiles, IEnumerable<string> rootFiles)
        {
            var ret = new List<string>();

            foreach (var changed in changedFiles)
            {
                if (Contains(changed))
                {
                    ret.AddRange(TraceTo(changed, rootFiles));
                }
            }

            return ret;
        }
    }
}