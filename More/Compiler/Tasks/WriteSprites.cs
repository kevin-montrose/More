using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using More.Model;
using System.IO;

namespace More.Compiler.Tasks
{
    /// <summary>
    /// Writes all the sprites that have been generated and stored on Current to disk.
    /// </summary>
    public class WriteSprites
    {
        public static List<Block> Task(List<Block> blocks)
        {
            foreach (var sprite in Current.PendingSpriteExports)
            {
                var @out = sprite.OutputFile.Replace('/', Path.DirectorySeparatorChar);

                sprite.Sprite.Save(@out);
                sprite.Sprite.Dispose();

                Current.SpriteFileWritten(@out);
            }

            return blocks;
        }
    }
}
