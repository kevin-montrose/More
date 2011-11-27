using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using More.Model;

namespace More.Compiler
{
    partial class Compiler
    {
        private List<Block> EvaluateValues(List<Block> statements)
        {
            var ret = new List<Block>();

            foreach (var statement in statements)
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
                    var evaluated = EvaluateValues(media.Blocks.ToList());

                    ret.Add(new MediaBlock(media.ForMedia.ToList(), evaluated, media.Start, media.Stop, media.FilePath));
                }

                if (keyframes != null)
                {
                    var frames = new List<KeyFrame>();
                    foreach (var frame in keyframes.Frames)
                    {
                        var blockEquiv = new SelectorAndBlock(InvalidSelector.Singleton, frame.Properties, frame.Start, frame.Stop, frame.FilePath);
                        var evald = EvaluateValues(new List<Block>() { blockEquiv });
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
