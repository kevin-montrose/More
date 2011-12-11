using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;

namespace MoreInternals.Compiler.Tasks
{
    /// <summary>
    /// This task copies properties between blocks as requested by selector includes.
    /// 
    /// @(.hover) and so forth are dealt with here, in short.
    /// </summary>
    public class Includes
    {
        public static List<Block> Task(List<Block> blocks)
        {
            return Impl(blocks);
        }

        private static List<Block> Impl(List<Block> blocks, List<SelectorAndBlock> parent = null)
        {
            var ret = new List<Block>();

            var forLookup = blocks.OfType<SelectorAndBlock>().ToList();

            // At this point, unrolled blocks have only NameValue (unevaluated) and Includes
            foreach (var statement in blocks)
            {
                var block = statement as SelectorAndBlock;
                var media = statement as MediaBlock;
                var keyframes = statement as KeyFramesBlock;
                var fontface = statement as FontFaceBlock;
                if (block == null && media == null && keyframes == null && fontface == null)
                {
                    ret.Add(statement);
                    continue;
                }

                if (block != null)
                {
                    var other = block.Properties.Where(w => !(w is NameValueProperty || w is IncludeSelectorProperty)).ToList();
                    var simple = block.Properties.OfType<NameValueProperty>().ToList();
                    var includes = block.Properties.OfType<IncludeSelectorProperty>();

                    var @override = new List<NameValueProperty>();

                    includes.Each(e =>
                    {
                        if (e.Overrides)
                        {
                            @override.AddRange(e.LookupMatch(forLookup, parent: parent));
                        }
                        else
                        {
                            simple.AddRange(e.LookupMatch(forLookup, parent: parent));
                        }
                    }
                    );

                    var doubleDefined = false;
                    foreach (var e in @override.GroupBy(g => g.Name).Where(g => g.Count() > 1))
                    {
                        Current.RecordError(ErrorType.Compiler, block, "After resolving selector includes, the [" + e.Key + "] rule would be included as an override " + e.Count() + " times.");
                        doubleDefined = true;
                    }
                    if (doubleDefined) throw new StoppedCompilingException();

                    foreach (var o in @override)
                    {
                        simple.RemoveAll(e => e.Name == o.Name);
                        simple.Add(o);
                    }

                    other.AddRange(simple);

                    ret.Add(new SelectorAndBlock(block.Selector, other, block.ResetContext, block.Start, block.Stop, block.FilePath));
                }

                if (media != null)
                {
                    var subBlocks = media.Blocks.ToList();
                    var copied = Impl(subBlocks, parent: forLookup);

                    ret.Add(new MediaBlock(media.MediaQuery, copied, media.Start, media.Stop, media.FilePath));
                }

                if (keyframes != null)
                {
                    var frames = new List<KeyFrame>();

                    foreach (var frame in keyframes.Frames)
                    {
                        var blockEquiv = new SelectorAndBlock(InvalidSelector.Singleton, frame.Properties, null, frame.Start, frame.Stop, frame.FilePath);
                        var copied = Impl(new List<Block>() { blockEquiv }, parent: parent);

                        frames.Add(new KeyFrame(frame.Percentages.ToList(), ((SelectorAndBlock)copied[0]).Properties.ToList(), frame.Start, frame.Stop, frame.FilePath));
                    }

                    ret.Add(new KeyFramesBlock(keyframes.Prefix, keyframes.Name, frames, keyframes.Variables.ToList(), keyframes.Start, keyframes.Stop, keyframes.FilePath));
                }

                if (fontface != null)
                {
                    var blockEquiv = new SelectorAndBlock(InvalidSelector.Singleton, fontface.Properties, null, fontface.Start, fontface.Stop, fontface.FilePath);
                    var copied = (SelectorAndBlock)Impl(new List<Block>() { blockEquiv }, parent)[0];

                    ret.Add(new FontFaceBlock(copied.Properties.ToList(), copied.Start, copied.Stop, copied.FilePath));
                }
            }

            return ret;
        }
    }
}
