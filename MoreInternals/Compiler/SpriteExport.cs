using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.IO;
using MoreInternals.Model;
using System.Drawing.Imaging;

namespace MoreInternals.Compiler
{
    class Point
    {
        public int X { get; private set; }
        public int Y { get; private set; }

        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    class SpriteExport
    {
        public class SubSprite
        {
            public Point TopLeft { get; private set; }
            public int WidthPx { get; private set; }
            public int HeightPx { get; private set; }
            public string Name { get; private set; }
            public string OriginalFile { get; private set; }

            internal SubSprite(Point tl, int w, int h, string name, string file)
            {
                TopLeft = tl;
                WidthPx = w;
                HeightPx = h;
                Name = name;
                OriginalFile = file;
            }
        }

        public string OutputFile { get; private set; }
        public string RelativeToFile { get; private set; }
        public IEnumerable<SubSprite> SubFiles { get; private set; }
        public Bitmap Sprite { get; private set; }

        private SpriteExport(string output, string relativeTo, List<SubSprite> subFiles, Bitmap sprite)
        {
            OutputFile = output;
            RelativeToFile = relativeTo;
            SubFiles = subFiles.AsReadOnly();
            Sprite = sprite;
        }

        internal static SpriteExport Create(string output, string moreFile, Dictionary<string, string> input)
        {
            var images = 
                input.ToDictionary(
                    k => k.Key, 
                    v => 
                    {
                        if (!Current.FileLookup.Exists(v.Value))
                        {
                            Current.RecordError(ErrorType.Compiler, Position.NoSite, "Couldn't find sprite image [" + v.Value + "]");
                            throw new StoppedCompilingException();
                        }

                        Image bitmap;
                        using (var stream = Current.FileLookup.ReadRaw(v.Value))
                        {
                            bitmap = Bitmap.FromStream(stream);
                        }

                        return Tuple.Create(v.Value, bitmap);
                    }
                );

            var subSprites =
                images.Select(
                    s =>
                    new
                    {
                        Width = s.Value.Item2.Width,
                        Height = s.Value.Item2.Height,
                        Bitmap = s.Value.Item2,
                        Name = s.Key,
                        OriginalFile = s.Value.Item1
                    }
                );

            var sprites = new List<SubSprite>();

            // This is a very dumb spriting algorithm
            // TODO: Improve!
            var mainSprite = new Bitmap(subSprites.Max(s => s.Width), subSprites.Sum(s => s.Height), PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(mainSprite))
            {
                int y = 0;
                foreach (var sprite in subSprites)
                {
                    var w = sprite.Bitmap.Width;
                    var h = sprite.Bitmap.Height;
                    g.DrawImage(sprite.Bitmap, new PointF(0, y));

                    sprites.Add(new SubSprite(new Point(0, -y), w, h, sprite.Name, sprite.OriginalFile));

                    y += h;
                }
            }

            images.Each(e => e.Value.Item2.Dispose());

            return new SpriteExport(output, moreFile, sprites, mainSprite);
        }

        internal List<MixinBlock> MixinEquivalents()
        {
            var ret = new List<MixinBlock>();

            foreach (var sprite in this.SubFiles)
            {
                var mixinName = Guid.NewGuid().ToString().Replace("-", "");

                var rules = new List<Property>();
                rules.Add(new NameValueProperty("background-image", UrlValue.Parse("url(" + RelativePath(RelativeToFile, OutputFile) + ")")));
                rules.Add(new NameValueProperty("background-position", new CompoundValue(new List<Value>() { new NumberWithUnitValue(sprite.TopLeft.X, Unit.PX), new NumberWithUnitValue(sprite.TopLeft.Y, Unit.PX) })));
                rules.Add(new NameValueProperty("background-repeat", new StringValue("no-repeat")));
                rules.Add(new NameValueProperty("width", new NumberWithUnitValue(sprite.WidthPx, Unit.PX)));
                rules.Add(new NameValueProperty("height", new NumberWithUnitValue(sprite.HeightPx, Unit.PX)));
                rules.Add(new MixinApplicationProperty(mixinName, new List<MixinApplicationParameter>(), optional: true, overrides: false, start: -1, stop: -1, filePath: "artificial sprites mixin"));

                ret.Add(new MixinBlock(sprite.Name, new List<MixinParameter>() { new MixinParameter(mixinName, ExcludeFromOutputValue.Singleton) }, rules, -1, -1, null));
            }

            return ret;
        }

        internal static string RelativePath(string relativeToFile, string outputFile)
        {
            var relativeToDir =
                !Path.HasExtension(relativeToFile) ?
                relativeToFile :
                Path.GetDirectoryName(relativeToFile);

            relativeToDir += relativeToDir.EndsWith("" + Path.DirectorySeparatorChar) ? "" : "" + Path.DirectorySeparatorChar;

            if (!outputFile.StartsWith(relativeToDir))
            {
                Current.RecordError(ErrorType.Compiler, Position.NoSite, "Cannot find relative path between [" + relativeToFile + "] & [" + outputFile + "]");
                throw new StoppedCompilingException();
            }

            return outputFile.Substring(relativeToDir.Length).Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}
