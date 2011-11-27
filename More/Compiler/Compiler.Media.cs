using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using More.Model;

namespace More.Compiler
{
    partial class Compiler
    {
        internal List<Block> MergeMedia(List<Block> statements)
        {
            var ret = new List<Block>();

            ret.AddRange(statements.Where(w => !(w is MediaBlock)));

            var mediaRules =
                statements
                    .OfType<MediaBlock>()
                    .GroupBy(g => string.Join(",", g.ForMedia.OrderBy(m => m)));

            foreach (var m in mediaRules)
            {
                var media = new List<Media>();
                foreach (var x in m.Key.Split(',')) media.Add((Media)Enum.Parse(typeof(Media), x));

                var rules = new List<Block>();
                m.Each(a => rules.AddRange(a.Blocks));

                ret.Add(new MediaBlock(media, rules, -1, -1, null));
            }

            return ret;
        }
    }
}
