using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;

namespace MoreInternals.Compiler.Tasks
{
    /// <summary>
    /// Task which takes duplicate declarations and collapses them
    /// 
    /// #id { a:b; }
    /// .class { c:d; }
    /// #id { e:f; }
    /// 
    /// becomes
    /// 
    /// #id { a:b; e:f; }
    /// .class { c:d; }
    /// </summary>
    public class Collapse
    {
        public static List<Block> Task(List<Block> blocks)
        {
            var copy = blocks.ToList();

            var ret = new List<Block>();

            int i = 0;

            while (i < copy.Count)
            {
                var newCopy = copy.ToList();

                var start = copy[i];
                i++;

                var media = start as MediaBlock;
                if (media != null)
                {
                    ret.Add(new MediaBlock(media.MediaQuery, Task(media.Blocks.ToList()), media.Start, media.Stop, media.FilePath));
                    continue;
                }

                var selBlock = start as SelectorAndBlock;
                if (selBlock == null)
                {
                    ret.Add(start);
                    continue;
                }

                var props = new List<Property>();
                props.AddRange(selBlock.Properties);

                for (var j = i; j < copy.Count; j++)
                {
                    var subBlock = copy[j] as SelectorAndBlock;
                    if (subBlock == null) continue;

                    if (subBlock.Selector.ToString() == selBlock.Selector.ToString())
                    {
                        props.AddRange(subBlock.Properties);
                        newCopy.Remove(subBlock);
                    }
                }

                ret.Add(new SelectorAndBlock(selBlock.Selector, props, null, -1, -1, null));
                copy = newCopy;
            }

            return ret;
        }
    }
}
