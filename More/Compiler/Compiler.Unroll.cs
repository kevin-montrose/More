using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using More.Model;

namespace More.Compiler
{
    partial class Compiler
    {
        internal List<Block> UnrollNestedBlocks(List<Block> statements)
        {
            var ret = new List<Block>();

            foreach (var statement in statements)
            {
                var block = statement as SelectorAndBlock;

                if (block != null)
                {
                    ret.AddRange(block.UnrollNestedBlocks());
                }
                else
                {
                    var media = statement as MediaBlock;
                    if (media != null)
                    {
                        var unrolled = UnrollNestedBlocks(media.Blocks.ToList());
                        ret.Add(new MediaBlock(media.ForMedia.ToList(), unrolled, media.Start, media.Stop, media.FilePath));
                    }
                    else
                    {
                        // KeyFrameDeclarations need special validation
                        var keyframe = statement as KeyFramesBlock;
                        if (keyframe != null)
                        {
                            foreach (var frame in keyframe.Frames)
                            {
                                foreach (var rule in frame.Properties)
                                {
                                    if (rule is NestedBlockProperty)
                                    {
                                        Current.RecordError(ErrorType.Compiler, rule, "Keyframes cannot include nested blocks");
                                        // we can continue from this, so do so
                                    }
                                }
                            }
                        }

                        // FontFaceDeclarations as well
                        var fontface = statement as FontFaceBlock;
                        if (fontface != null)
                        {
                            foreach (var rule in fontface.Properties)
                            {
                                if (rule is NestedBlockProperty)
                                {
                                    Current.RecordError(ErrorType.Compiler, rule, "@font-face declarations cannot include nested blocks");
                                    // we can continue
                                }
                            }
                        }

                        ret.Add(statement);
                    }
                }
            }

            return ret;
        }
    }
}
