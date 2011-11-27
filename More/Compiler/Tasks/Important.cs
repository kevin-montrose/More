using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using More.Model;

namespace More.Compiler.Tasks
{
    /// <summary>
    /// If any blocks have mutltiple properties with the same name, the property with !important (if any) will be taken.
    /// 
    /// If after this reduction, any duplicate properties still exist on a block this task records a warning.
    /// </summary>
    public class Important
    {
        public static List<Block> Task(List<Block> blocks)
        {
            var ret = new List<Block>();

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
                    var rules = new List<NameValueProperty>();

                    foreach (var group in block.Properties.Cast<NameValueProperty>().GroupBy(g => g.Name))
                    {
                        if (group.Count() == 1)
                        {
                            rules.Add(group.Single());
                        }
                        else
                        {
                            var important = group.SingleOrDefault(g => g.Value.IsImportant());

                            if (important == null)
                            {
                                Current.RecordWarning(ErrorType.Compiler, block, "More than one definition for [" + group.Key + "], did you mean for one to be !important?");

                                rules.AddRange(group);
                            }
                            else
                            {
                                rules.Add(important);
                            }
                        }
                    }

                    ret.Add(new SelectorAndBlock(block.Selector, rules, block.Start, block.Stop, block.FilePath));
                }

                if (media != null)
                {
                    var resolved = Task(media.Blocks.ToList());
                    ret.Add(new MediaBlock(media.ForMedia.ToList(), resolved, media.Start, media.Stop, media.FilePath));
                }

                if (keyframes != null)
                {
                    var frames = new List<KeyFrame>();
                    foreach (var frame in keyframes.Frames)
                    {
                        var blockEquiv = new SelectorAndBlock(InvalidSelector.Singleton, frame.Properties, frame.Start, frame.Stop, frame.FilePath);
                        var resolved = Task(new List<Block>() { blockEquiv });

                        frames.Add(new KeyFrame(frame.Percentages.ToList(), ((SelectorAndBlock)resolved[0]).Properties.ToList(), frame.Stop, frame.Stop, frame.FilePath));
                    }

                    ret.Add(new KeyFramesBlock(keyframes.Prefix, keyframes.Name, frames, keyframes.Variables.ToList(), keyframes.Start, keyframes.Stop, keyframes.FilePath));
                }

                if (fontface != null)
                {
                    var blockEquiv = new SelectorAndBlock(InvalidSelector.Singleton, fontface.Properties, fontface.Start, fontface.Stop, fontface.FilePath);
                    var resolved = (SelectorAndBlock)Task(new List<Block>() { blockEquiv })[0];

                    ret.Add(new FontFaceBlock(resolved.Properties.ToList(), fontface.Start, fontface.Stop, fontface.FilePath));
                }
            }

            return ret;
        }
    }
}
