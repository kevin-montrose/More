﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using More.Model;

namespace More.Compiler
{
    partial class Compiler
    {
        private List<Block> ExportSprites(List<Block> statements)
        {
            var inputFile = Current.InitialFilePath;

            var spriteDecls = statements.OfType<SpriteBlock>().ToList();
            if (spriteDecls.Count == 0)
            {
                return statements;
            }

            var ret = statements.Where(w => w.GetType() != typeof(SpriteBlock)).ToList();

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
