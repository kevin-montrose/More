using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;
using System.IO;

namespace MoreInternals.Compiler.Tasks
{
    /// <summary>
    /// This task takes all @reset() or @reset(selector) properties
    /// and goes off to find any matches, copying those properties as if
    /// they were selector includes.
    /// 
    /// However, @reset() will *only* copy those properties defined on
    /// classes that are in @reset{} blocks, copy on the sub-block level, and
    /// don't override *any* rules of the same name.
    /// 
    /// Copying on the sub-block level means that given:
    /// @reset{ div { a:b; } }
    /// 
    /// that 
    /// 
    /// .class { c:d; div { @reset(); e:f; } }
    /// 
    /// evaluates to
    /// 
    /// .class { c:d; } .class div { a:b; e:f;}
    /// 
    /// Naturally, @reset(selector) will match on the actual select passed.
    /// </summary>
    public class ResetIncludes
    {
        private static bool AreEqual(Selector knownSingle, Selector other)
        {
            var multiOther = other as MultiSelector;
            if (multiOther != null)
            {
                return multiOther.Selectors.Any(a => AreEqual(knownSingle, a));
            }

            if (knownSingle.GetType() != other.GetType()) return false;

            string knownStr, otherStr;
            using (var str = new StringWriter())
            {
                knownSingle.Write(str);
                knownStr = str.ToString();
            }

            using (var str = new StringWriter())
            {
                other.Write(str);
                otherStr = str.ToString();
            }

            return knownStr.Equals(otherStr, StringComparison.InvariantCultureIgnoreCase);
        }

        private static List<NameValueProperty> FindMatches(Selector sel, List<SelectorAndBlock> resetBlocks)
        {
            var ret = new List<NameValueProperty>();

            var multiSel = sel as MultiSelector;
            if(multiSel != null)
            {
                foreach (var part in multiSel.Selectors)
                {
                    ret.AddRange(FindMatches(part, resetBlocks));
                }

                return ret;
            }

            foreach (var block in resetBlocks)
            {
                if (AreEqual(sel, block.Selector))
                {
                    ret.AddRange(block.Properties.Cast<NameValueProperty>());
                }
            }

            return ret;
        }

        public static List<Block> Task(List<Block> blocks)
        {
            var resets = blocks.OfType<SelectorAndBlock>().Where(w => w.IsReset).ToList();

            var ret = new List<Block>();
            ret.AddRange(blocks.Where(w => !(w is SelectorAndBlock || w is MediaBlock)));
            ret.AddRange(blocks.OfType<SelectorAndBlock>().Where(w => w.IsReset));

            var remaining = blocks.Where(w => !ret.Contains(w)).ToList();

            foreach (var block in remaining)
            {
                var selBlock = block as SelectorAndBlock;
                if (selBlock != null)
                {
                    var props = new List<NameValueProperty>();

                    var resetProps = selBlock.Properties.OfType<ResetProperty>();
                    var selfResetProps = selBlock.Properties.OfType<ResetSelfProperty>();

                    props.AddRange(selBlock.Properties.Where(w => !(resetProps.Contains(w) || selfResetProps.Contains(w))).Cast<NameValueProperty>());

                    var copySels = resetProps.Select(x => x.Selector).Union(selfResetProps.Select(x => x.EffectiveSelector)).ToList();

                    foreach (var sel in copySels)
                    {
                        var newProps = FindMatches(sel, resets);

                        foreach (var p in newProps)
                        {
                            // reset properties never override
                            if (props.Any(a => a.Name.Equals(p.Name, StringComparison.InvariantCultureIgnoreCase))) continue;

                            props.Add(p);
                        }
                    }

                    ret.Add(new SelectorAndBlock(selBlock.Selector, props, null, selBlock.Start, selBlock.Stop, selBlock.FilePath));
                }

                var media = block as MediaBlock;
                if (media != null)
                {
                    var subBlocks = Task(media.Blocks.ToList());

                    ret.Add(new MediaBlock(media.MediaQuery, subBlocks, media.Start, media.Start, media.FilePath));
                }
            }

            return ret;
        }
    }
}
