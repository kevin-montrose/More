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
                if (media == null)
                {
                    props.Add(prop);
                    continue;
                }

                var unrolled =
                    new MediaBlock(
                        media.MediaQuery,
                        new List<Block>
                        {
                            new SelectorAndBlock(
                                media.ContainingSelector,
                                media.Properties,
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

                ret.AddRange(UnrollNestedSelectors.Task(new List<Block> { unrolled }));
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
