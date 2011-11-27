using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using More.Model;

namespace More.Compiler.Tasks
{
    /// <summary>
    /// Groups all @media blocks by the declared type of media.
    /// 
    /// @media tv { foo }
    /// @media tv { bar }
    /// 
    /// becomes
    /// 
    /// @media { foo bar }
    /// 
    /// basically.
    /// </summary>
    public class Media
    {
        public static List<Block> Task(List<Block> blocks)
        {
            var ret = new List<Block>();

            ret.AddRange(blocks.Where(w => !(w is MediaBlock)));

            var mediaRules =
                blocks
                    .OfType<MediaBlock>()
                    .GroupBy(g => string.Join(",", g.ForMedia.OrderBy(m => m)));

            foreach (var m in mediaRules)
            {
                var media = new List<More.Model.Media>();
                foreach (var x in m.Key.Split(',')) media.Add((More.Model.Media)Enum.Parse(typeof(More.Model.Media), x));

                var rules = new List<Block>();
                m.Each(a => rules.AddRange(a.Blocks));

                ret.Add(new MediaBlock(media, rules, -1, -1, null));
            }

            return ret;
        }
    }
}
