using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;

namespace MoreInternals.Compiler.Tasks
{
    /// <summary>
    /// This task converts all nested media properties
    /// into stand alone media blocks.
    /// </summary>
    public static class UnrollNestedMedia
    {
        private static IEnumerable<Block> Unroll(SelectorAndBlock block)
        {
            var ret = new List<Block>();
            var props = new List<Property>();

            foreach (var prop in block.Properties)
            {
                var media = prop as InnerMediaProperty;
                var nested = prop as NestedBlockProperty;
                if (media == null && nested == null)
                {
                    props.Add(prop);
                    continue;
                }

                if (nested != null)
                {
                    var inner = Unroll(nested.Block);
                    var innerMedia = inner.OfType<MediaBlock>();
                    var other = inner.Where(i => !(i is MediaBlock)).Cast<SelectorAndBlock>();

                    props.AddRange(other.Select(s => new NestedBlockProperty(s, s.Start, s.Stop)));

                    foreach (var m in innerMedia)
                    {
                        var selBlock = 
                            new SelectorAndBlock(
                                block.Selector,
                                m.Blocks.Cast<SelectorAndBlock>().Select(s => new NestedBlockProperty(s, s.Start, s.Stop)),
                                null,
                                m.Start,
                                m.Stop,
                                m.FilePath
                            );

                        var newMedia =
                            new MediaBlock(
                                m.MediaQuery,
                                new List<Block> { selBlock },
                                m.Start,
                                m.Stop,
                                m.FilePath
                            );

                        ret.Add(newMedia);
                    }

                    continue;
                }

                var unrolled =
                    new MediaBlock(
                        media.MediaQuery,
                        new List<Block>
                        {
                            new SelectorAndBlock(
                                block.Selector,
                                media.Block.Properties,
                                null,
                                -1,
                                -1,
                                media.FilePath
                            )
                        },
                        -1,
                        -1,
                        media.FilePath
                    );

                ret.Add(unrolled);
            }

            ret.Add(new SelectorAndBlock(block.Selector, props, null, block.Start, block.Stop, block.FilePath));

            return ret;
        }

        public static List<Block> Task(List<Block> blocks)
        {
            var ret = new List<Block>();

            foreach (var b in blocks)
            {
                var asSel = b as SelectorAndBlock;
                if (asSel == null)
                {
                    ret.Add(b);
                    continue;
                }

                ret.AddRange(Unroll(asSel));
            }

            return ret;
        }
    }
}
