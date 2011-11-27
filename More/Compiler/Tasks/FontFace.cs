using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using More.Model;
using System.IO;

namespace More.Compiler.Tasks
{
    /// <summary>
    /// Validates @font-face declarations.
    /// 
    /// Emits warnings if the font-face doesn't appear to be in use (we cannot safely remove these due
    /// to the possibility they are in use in other CSS files or in inline styles).
    /// 
    /// Emits errors if src or font-family properties are missing in a declaration.
    /// </summary>
    public class FontFace
    {
        public static List<Block> Task(List<Block> blocks)
        {
            var fonts = blocks.OfType<FontFaceBlock>().ToList();
            if (fonts.Count == 0) return blocks;

            var map = new Dictionary<string, FontFaceBlock>();

            // check for font-family & src rules
            foreach (var font in fonts)
            {
                var fontFamily = font.Properties.Cast<NameValueProperty>().FirstOrDefault(a => a.Name.Equals("font-family", StringComparison.InvariantCultureIgnoreCase));
                var src = font.Properties.Cast<NameValueProperty>().FirstOrDefault(a => a.Name.Equals("src", StringComparison.InvariantCultureIgnoreCase));

                if (src == null)
                {
                    Current.RecordError(ErrorType.Compiler, font, "No src rule found in @font-face declaration");
                }

                if (fontFamily == null)
                {
                    Current.RecordError(ErrorType.Compiler, font, "No font-family rule found in @font-face declaration");
                }
                else
                {
                    using (var mem = new StringWriter())
                    {
                        fontFamily.Value.Write(mem);
                        map[mem.ToString()] = font;
                    }
                }
            }

            var fontRules = new List<NameValueProperty>();

            foreach (var block in blocks.OfType<SelectorAndBlock>())
            {
                fontRules.AddRange(block.Properties.Cast<NameValueProperty>().Where(w => w.Name.Equals("font", StringComparison.InvariantCultureIgnoreCase) || w.Name.Equals("font-family")));
            }

            foreach (var media in blocks.OfType<MediaBlock>())
            {
                foreach (var block in media.Blocks.OfType<SelectorAndBlock>())
                {
                    fontRules.AddRange(block.Properties.Cast<NameValueProperty>().Where(w => w.Name.Equals("font", StringComparison.InvariantCultureIgnoreCase) || w.Name.Equals("font-family")));
                }
            }

            foreach (var keyframes in blocks.OfType<KeyFramesBlock>())
            {
                foreach (var frame in keyframes.Frames)
                {
                    fontRules.AddRange(frame.Properties.Cast<NameValueProperty>().Where(w => w.Name.Equals("font", StringComparison.InvariantCultureIgnoreCase) || w.Name.Equals("font-family")));
                }
            }

            var raw =
                fontRules.Select(
                    s =>
                    {
                        using (var mem = new StringWriter())
                        {
                            s.Value.Write(mem);

                            return mem.ToString();
                        }
                    }
                );

            foreach (var font in map.Keys)
            {
                if (!raw.Any(a => a.IndexOf(font, StringComparison.InvariantCultureIgnoreCase) != -1))
                {
                    Current.RecordWarning(ErrorType.Compiler, map[font], "`" + font + "` does not appear to be used in any CSS rule.");
                }
            }

            return blocks;
        }
    }
}
