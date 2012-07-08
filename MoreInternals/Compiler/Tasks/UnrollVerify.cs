using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;

namespace MoreInternals.Compiler.Tasks
{
    /// <summary>
    /// Task that confirms that there are no remaining nested selector or media blocks left.
    /// 
    /// Any remaining ones are indicative of logic (if not syntax) errors.
    /// </summary>
    public static class UnrollVerify
    {
        private static void Verify(SelectorAndBlock block)
        {
            var nestedBlock = block.Properties.OfType<NestedBlockProperty>().ToList();
            var innerMedia = block.Properties.OfType<InnerMediaProperty>().ToList();

            if (nestedBlock.Count != 0)
            {
                throw new InvalidOperationException("It shouldn't be possible for nested blocks to remain here");
            }

            foreach (var media in innerMedia)
            {
                Current.RecordError(ErrorType.Compiler, media, "@media blocks cannot be nested within each other");
            }
        }

        public static List<Block> Task(List<Block> blocks)
        {
            foreach (var block in blocks)
            {
                var asSelBlock = block as SelectorAndBlock;
                var asMedia = block as MediaBlock;

                if (asSelBlock == null && asMedia == null) continue;

                if (asSelBlock != null)
                {
                    Verify(asSelBlock);
                    continue;
                }

                Task(asMedia.Blocks.ToList());
            }

            return blocks;
        }
    }
}
