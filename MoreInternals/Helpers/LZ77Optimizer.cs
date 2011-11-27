using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;
using System.IO;
using System.IO.Compression;

namespace MoreInternals.Helpers
{
    class LZ77Optimizer
    {
        private static string ToString(IEnumerable<IWritable> writable)
        {
            using (var str = new StringWriter())
            using(var css = new MinimalCssWriter(str))
            {
                foreach (var w in writable)
                {
                    w.Write(css);
                }

                return str.ToString();
            }
        }

        private static int CountInstances(string @in, string of)
        {
            int ret = 0;
            int i = 0;

            while ((i = @in.IndexOf(of, i) + 1) != 0) ret++;

            return ret;
        }

        internal static int CountTotalCovered(FastString covered, FastString by)
        {
            var found = covered.SubStrings(by.Raw);

            if (found.Count == 0) return 0;

            return found.Max(i => i.Item1.Length);
        }

        private static IWritable MostCovering(List<IWritable> all)
        {
            var dict = all.ToDictionary(d => d, d => new FastString(ToString(new[] { d })));
            IWritable best = null;
            var bestCovered = 0;

            foreach (var f in dict)
            {
                var covered = 0;

                foreach (var g in dict)
                {
                    if (f.Key == g.Key) continue;

                    covered += CountTotalCovered(g.Value, f.Value);
                }

                if (best == null || covered > bestCovered)
                {
                    best = f.Key;
                    bestCovered = covered;
                }
            }

            if (bestCovered == 0) throw new InvalidOperationException("Should make a better choice here");

            return best;
        }

        internal static T MostCovered<T>(FastString all, List<Tuple<T, FastString>> potential)
            where T : class
        {
            T best = null;
            var bestCovered = 0;

            foreach (var f in potential)
            {
                var covered = CountTotalCovered(all, f.Item2);

                if (best == null || covered > bestCovered)
                {
                    best = f.Item1;
                    bestCovered = covered;
                }
            }

            return best;
        }

        private static IWritable MostCovered(List<IWritable> toDate, List<IWritable> potential)
        {
            var all = new FastString(ToString(toDate));

            var dict = potential.Select(x => Tuple.Create(x, new FastString(ToString(new[] { x })))).ToList();

            return MostCovered(all, dict);
        }

        private static SelectorAndBlock OptimizeSelectorAndBlock(SelectorAndBlock block)
        {
            var selector = block.Selector;

            var multi = selector as MultiSelector;
            if (multi != null)
            {
                selector =
                    new MultiSelector(
                        multi.Selectors.OrderBy(o => o.ToString()).ToList(),
                        selector.Start,
                        selector.Stop,
                        selector.FilePath
                    );
            }

            return
                new SelectorAndBlock(
                    selector,
                    block.Properties.Cast<NameValueProperty>().OrderBy(o => o.Name).ToList(),
                    block.Start,
                    block.Stop,
                    block.FilePath
                );
        }

        private static List<IWritable> ChooseRoots(List<IWritable> all)
        {
            var overlap = new List<Tuple<int, IWritable>>();

            foreach (var r in all)
            {
                var self = ToString(new[] { r });
                var others = new FastString(ToString(all.Where(a => a != r)));

                var substrs = others.SubStrings(self);

                var max = substrs.Count > 0 ? substrs.Max(a => a.Item1.Length) : 0;

                overlap.Add(Tuple.Create(max, r));
            }

            return overlap.OrderByDescending(o => o.Item1).Take(3).Select(s => s.Item2).ToList();
        }

        private static List<IWritable> ReOrderWritables(List<IWritable> all, List<IWritable> roots = null)
        {
            List<IWritable> best = null;
            var bestSize = 0;

            var candidateRoots = roots ?? ChooseRoots(all);

            for (int i = 0; i < candidateRoots.Count; i++)
            {
                var copy = new List<IWritable>(all);

                var root = candidateRoots[i];

                var inOrder = new List<IWritable>(copy.Count);
                inOrder.Add(root);
                copy.Remove(root);

                while (copy.Count > 0)
                {
                    var next = MostCovered(inOrder, copy);
                    copy.Remove(next);
                    inOrder.Add(next);
                }

                var size = GZipSize(inOrder);

                if (best == null || size < bestSize)
                {
                    best = inOrder;
                    bestSize = size;
                }
            }

            return best;
        }

        public static List<Block> Optimize(List<Block> statements)
        {
            var blocks = statements.OfType<SelectorAndBlock>().ToList();
            var medias = statements.OfType<MediaBlock>().ToList();
            var other = statements.Where(o => !(o is SelectorAndBlock || o is MediaBlock)).ToList();

            var newMedias = new List<MediaBlock>();
            foreach (var media in medias)
            {
                var optimized = Optimize(media.Blocks.ToList());

                newMedias.Add(new MediaBlock(media.ForMedia.ToList(), optimized, media.Start, media.Stop, media.FilePath));
            }
            medias = newMedias;

            var newBlocks = new List<SelectorAndBlock>();
            foreach (var block in blocks)
            {
                newBlocks.Add(OptimizeSelectorAndBlock(block));
            }
            blocks = newBlocks;

            var allStatements = blocks.Cast<Block>().Union(medias.Cast<Block>()).Cast<IWritable>().ToList();

            var best = ReOrderWritables(allStatements);

            var ret = new List<Block>();
            ret.AddRange(other);
            ret.AddRange(best.Cast<Block>());

            return ret;
        }

        internal static int GZipSize(IEnumerable<IWritable> parts)
        {
            using (var mem = new MemoryStream())
            using (var gzip = new GZipStream(mem, CompressionMode.Compress))
            using (var stream = new StreamWriter(gzip))
            using (var css = new MinimalCssWriter(stream))
            {
                foreach (var x in parts) x.Write(css);
                gzip.Flush();
                gzip.Close();

                return mem.ToArray().Length;
            }
        }
    }
}
