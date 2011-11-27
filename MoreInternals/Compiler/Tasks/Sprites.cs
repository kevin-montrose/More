using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;

namespace MoreInternals.Compiler.Tasks
{
    /// <summary>
    /// This task handles rendering @sprite blocks.
    /// 
    /// When it returns, there will be no more SpriteBlock blocks.
    /// </summary>
    public class Sprite
    {
        public static List<Block> Task(List<Block> blocks)
        {
            var inputFile = Current.InitialFilePath;

            var spriteDecls = blocks.OfType<SpriteBlock>().ToList();
            if (spriteDecls.Count == 0)
            {
                return blocks;
            }

            var ret = blocks.Where(w => w.GetType() != typeof(SpriteBlock)).ToList();

            foreach (var sprite in spriteDecls)
            {
                var output = sprite.OutputFile.Value.RebaseFile(inputFile);

                var input = sprite.Sprites.ToDictionary(k => k.MixinName, v => v.SpriteFilePath.Value.RebaseFile(inputFile));

                var export = SpriteExport.Create(output, inputFile, input);
                Current.SpritePending(export);

                ret.AddRange(export.MixinEquivalents());
            }

            return ret;
        }
    }
}
