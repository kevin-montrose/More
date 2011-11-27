using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using More.Model;

namespace More.Compiler
{
    partial class Compiler
    {
        private List<Block> CopyIncludes(List<Block> unrolled, List<SelectorAndBlock> parent = null)
        {
            var ret = new List<Block>();

            var forLookup = unrolled.OfType<SelectorAndBlock>().ToList();

            // At this point, unrolled blocks have only NameValue (unevaluated) and Includes
            foreach (var statement in unrolled)
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

                    ret.Add(new SelectorAndBlock(block.Selector, simple, block.Start, block.Stop, block.FilePath));
                }

                if (media != null)
                {
                    var subBlocks = media.Blocks.ToList();
                    var copied = CopyIncludes(subBlocks, parent: forLookup);

                    ret.Add(new MediaBlock(media.ForMedia.ToList(), copied, media.Start, media.Stop, media.FilePath));
                }

                if (keyframes != null)
                {
                    var frames = new List<KeyFrame>();

                    foreach (var frame in keyframes.Frames)
                    {
                        var blockEquiv = new SelectorAndBlock(InvalidSelector.Singleton, frame.Properties, frame.Start, frame.Stop, frame.FilePath);
                        var copied = CopyIncludes(new List<Block>() { blockEquiv }, parent: parent);

                        frames.Add(new KeyFrame(frame.Percentages.ToList(), ((SelectorAndBlock)copied[0]).Properties.ToList(), frame.Start, frame.Stop, frame.FilePath));
                    }

                    ret.Add(new KeyFramesBlock(keyframes.Prefix, keyframes.Name, frames, keyframes.Variables.ToList(), keyframes.Start, keyframes.Stop, keyframes.FilePath));
                }

                if (fontface != null)
                {
                    var blockEquiv = new SelectorAndBlock(InvalidSelector.Singleton, fontface.Properties, fontface.Start, fontface.Stop, fontface.FilePath);
                    var copied = (SelectorAndBlock)CopyIncludes(new List<Block>() { blockEquiv }, parent)[0];

                    ret.Add(new FontFaceBlock(copied.Properties.ToList(), copied.Start, copied.Stop, copied.FilePath));
                }
            }

            return ret;
        }
    }
}
