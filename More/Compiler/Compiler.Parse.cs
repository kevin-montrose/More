using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using More.Model;
using System.IO;
using System.Threading;

namespace More.Compiler
{
    partial class Compiler
    {
        private object SyncLock = new object();

        internal Dictionary<string, List<Block>> FileCache = new Dictionary<string, List<Block>>();
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

        public List<Block> ParseStream(TextReader @in)
        {
            var filePath = Current.InitialFilePath;

            return ParseStreamImpl(filePath, @in);
        }

        internal List<Block> ParseStreamImpl(string filePath, TextReader @in)
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
    }
}
