using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using More.Model;
using System.IO;

namespace More.Compiler
{
    partial class Compiler
    {
        internal void WriteSprites(List<Block> blocks)
        {
            foreach (var sprite in Current.PendingSpriteExports)
            {
                var @out = sprite.OutputFile.Replace('/', Path.DirectorySeparatorChar);

                sprite.Sprite.Save(@out);
                sprite.Sprite.Dispose();

                Current.SpriteFileWritten(@out);
            }
        }
    }
}
