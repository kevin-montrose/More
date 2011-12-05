using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;

namespace MoreInternals.Compiler.Tasks
{
    /// <summary>
    /// When this task completes alll values will have been evaluated.
    /// That is, scopes and bindings will no longer be significant.
    /// 
    /// "name: @a" will have been replaced with "name: value", in short.
    /// </summary>
    public class Evaluate
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
                    var processedRules = new List<NameValueProperty>();

                    // At this point, it is an error for any other type of rule to exist
                    foreach (var rule in block.Properties.Cast<NameValueProperty>())
                    {
                        var value = rule.Value.Evaluate();
                        while (value.NeedsEvaluate) value = value.Evaluate();

                        if (!(value is ExcludeFromOutputValue))
                        {
                            processedRules.Add(new NameValueProperty(rule.Name, value, rule.Start, rule.Stop, rule.FilePath));
                        }
                    }

                    ret.Add(new SelectorAndBlock(block.Selector, processedRules, block.Start, block.Stop, block.FilePath));
                }

                if (media != null)
                {
                    var evaluated = Task(media.Blocks.ToList());

                    ret.Add(new MediaBlock(media.MediaQuery, evaluated, media.Start, media.Stop, media.FilePath));
                }

                if (keyframes != null)
                {
                    var frames = new List<KeyFrame>();
                    foreach (var frame in keyframes.Frames)
                    {
                        var blockEquiv = new SelectorAndBlock(InvalidSelector.Singleton, frame.Properties, frame.Start, frame.Stop, frame.FilePath);
                        var evald = Task(new List<Block>() { blockEquiv });
                        frames.Add(new KeyFrame(frame.Percentages.ToList(), ((SelectorAndBlock)evald[0]).Properties.ToList(), frame.Start, frame.Stop, frame.FilePath));
                    }

                    ret.Add(new KeyFramesBlock(keyframes.Prefix, keyframes.Name, frames, keyframes.Variables.ToList(), keyframes.Start, keyframes.Stop, keyframes.FilePath));
                }

                if (fontface != null)
                {
                    var processedRules = new List<Property>();
                    foreach (var rule in fontface.Properties.Cast<NameValueProperty>())
                    {
                        var value = rule.Value.Evaluate();
                        while (value.NeedsEvaluate) value = value.Evaluate();

                        if (!(value is ExcludeFromOutputValue))
                        {
                            processedRules.Add(new NameValueProperty(rule.Name, value, rule.Start, rule.Stop, rule.FilePath));
                        }
                    }

                    ret.Add(new FontFaceBlock(processedRules, fontface.Start, fontface.Stop, fontface.FilePath));
                }
            }

            return ret;
        }
    }
}
