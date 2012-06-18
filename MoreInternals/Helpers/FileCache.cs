using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;
using System.IO;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace MoreInternals.Helpers
{
    public class FileCache
    {
        private object SyncLock = new object();

        private ConcurrentDictionary<string, ReadOnlyCollection<Block>> Cache = new ConcurrentDictionary<string, ReadOnlyCollection<Block>>();
        private HashSet<string> InProgress = new HashSet<string>();

        public int Count { get { return Cache.Count; } }

        private volatile bool Corrupted = false;
        private void SafetyCheck()
        {
            if (Corrupted) throw new Exception("FileCache has been corrupted");
        }

        public Tuple<string, List<Block>> Available(IEnumerable<string> paths, Func<string, List<Block>> load)
        {
            SafetyCheck();

            foreach (var path in paths)
            {
                ReadOnlyCollection<Block> cached;
                if (Cache.TryGetValue(path, out cached))
                {
                    return Tuple.Create(path, cached.ToList());
                }
            }

            var forcePath = paths.ElementAt(0);
            return Tuple.Create(forcePath, Demand(forcePath, load));
        }

        public List<Block> Demand(string path, Func<string, List<Block>> load)
        {
            SafetyCheck();

            try
            {
                lock (SyncLock)
                {
                    ReadOnlyCollection<Block> cached;
                    if (Cache.TryGetValue(path, out cached)) return cached.ToList();

                    if (InProgress.Contains(path))
                    {
                        while (!Cache.TryGetValue(path, out cached))
                        {
                            Monitor.Wait(SyncLock);
                        }

                        return cached.ToList();
                    }

                    InProgress.Add(path);
                }

                var toCache = load(path);

                Cache[path] = toCache != null ? toCache.AsReadOnly() : null;
                lock (SyncLock) Monitor.PulseAll(SyncLock);

                return toCache;
            }
// this exception is very useful when debugging, even if we don't use it during normal operation
#pragma warning disable 0168
            catch (Exception e)
            {
                Corrupted = true;
                throw;
            }
#pragma warning restore 0168
            finally
            {
                lock (InProgress)
                {
                    InProgress.Remove(path);
                }
            }
        }

        public List<string> Loaded()
        {
            return Cache.Select(s => s.Key).Union(InProgress).ToList();
        }
    }
}
