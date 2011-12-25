using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;

namespace MoreInternals.Compiler.Tasks
{
    /// <summary>
    /// Groups all @media blocks by the declared type of media.
    /// 
    /// @media tv { foo; }
    /// @media tv { bar; }
    /// 
    /// becomes
    /// 
    /// @media tv { foo; bar; }
    /// 
    /// basically.
    /// </summary>
    public class Media
    {
        public static List<Block> Task(List<Block> blocks)
        {
            var ret = new List<Block>();
            ret.AddRange(blocks.Where(w => !(w is MediaBlock)));

            var mediaBlocks = blocks.OfType<MediaBlock>();
            bool @continue = true;
            
            while (@continue)
            {
                @continue = false;

                var collapsed = new List<MediaBlock>();
                var removed = new List<MediaBlock>();

                foreach (var thisMedia in mediaBlocks)
                {
                    if (removed.Contains(thisMedia)) continue;

                    var thisQuery = thisMedia.MediaQuery;
                    var subBlocks = new List<Block>();

                    subBlocks.AddRange(thisMedia.Blocks);

                    foreach (var otherMedia in mediaBlocks)
                    {
                        if (otherMedia == thisMedia || removed.Contains(otherMedia)) continue;

                        var otherQuery = otherMedia.MediaQuery;

                        if (thisQuery.Equals(otherQuery))
                        {
                            subBlocks.AddRange(otherMedia.Blocks);
                            removed.Add(otherMedia);
                        }
                    }

                    collapsed.Add(new MediaBlock(thisQuery, subBlocks, thisMedia.Start, thisMedia.Stop, thisMedia.FilePath));
                }

                if (removed.Count > 0)
                {
                    @continue = true;
                    mediaBlocks = collapsed;
                }
            }

            ret.AddRange(mediaBlocks);

            return ret;
        }
    }
}
