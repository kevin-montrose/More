using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using More.Model;

namespace More.Compiler
{
    partial class Compiler
    {
        private List<Block> ExportSprites(string inputFile, List<Block> statements, out List<SpriteExport> sprites)
        {
            var spriteDecls = statements.OfType<SpriteBlock>().ToList();
            if (spriteDecls.Count == 0)
            {
                sprites = null;
                return statements;
            }

            sprites = new List<SpriteExport>();

            var ret = statements.Where(w => w.GetType() != typeof(SpriteBlock)).ToList();

            foreach (var sprite in spriteDecls)
            {
                var output = sprite.OutputFile.Value.RebaseFile(inputFile);

                var input = sprite.Sprites.ToDictionary(k => k.MixinName, v => v.SpriteFilePath.Value.RebaseFile(inputFile));

                var export = SpriteExport.Create(output, inputFile, input);
                sprites.Add(export);

                ret.AddRange(export.MixinEquivalents());
            }

            return ret;
        }
    }
}
