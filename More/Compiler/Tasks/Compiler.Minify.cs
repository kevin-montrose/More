using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using More.Model;
using System.IO;
using More.Helpers;

namespace More.Compiler
{
    partial class Compiler
    {
        private ColorValue MinifyColor(ColorValue value)
        {
            // There's no lossless conversion for RGBA values
            if (value is RGBAColorValue) return value;

            // The smallest form of a color will *always* be the hex version or the named color version
            //   So lets build the sextuple hex version
            var red = (byte)((NumberValue)BuiltInFunctions.Red(new[] { value }, Position.NoSite)).Value;
            var green = (byte)((NumberValue)BuiltInFunctions.Green(new[] { value }, Position.NoSite)).Value;
            var blue = (byte)((NumberValue)BuiltInFunctions.Blue(new[] { value }, Position.NoSite)).Value;

            string hex;
            using (var buffer = new StringWriter())
            {
                (new HexSextupleColorValue(red, green, blue)).Write(buffer);
                hex = buffer.ToString().Substring(1);
            }

            var asNum = int.Parse(hex, System.Globalization.NumberStyles.HexNumber);
            string asNamed = null;

            if (Enum.IsDefined(typeof(NamedColor), asNum))
            {
                asNamed = Enum.GetName(typeof(NamedColor), (NamedColor)asNum);
            }

            if (asNamed.HasValue() && asNamed.Length < 7)
            {
                return new NamedColorValue((NamedColor)asNum);
            }

            if (red.ToString("x").Distinct().Count() == 1 &&
               green.ToString("x").Distinct().Count() == 1 &&
               blue.ToString("x").Distinct().Count() == 1)
            {
                return new HexTripleColorValue(red, green, blue);
            }

            return new HexSextupleColorValue(red, green, blue);
        }

        private NumberWithUnitValue MinifyNumberWithUnit(NumberWithUnitValue value)
        {
            var min = MinifyNumberValue(value);

            var ret = new NumberWithUnitValue(min.Value, value.Unit);
            string retStr;
            using (var buffer = new StringWriter())
            {
                ret.Write(buffer);
                retStr = buffer.ToString();
            }

            if (!Value.ConvertableUnits.ContainsKey(value.Unit))
            {
                return ret;
            }

            var inMM = min.Value * Value.ConvertableUnits[value.Unit];

            foreach (var unit in Value.ConvertableUnits.Keys)
            {
                var inUnit = inMM / Value.ConvertableUnits[unit];

                var newMin = new NumberWithUnitValue(MinifyNumberValue(new NumberValue(inUnit)).Value, unit);
                string newMinStr;

                using (var buffer = new StringWriter())
                {
                    newMin.Write(buffer);
                    newMinStr = buffer.ToString();
                }

                if (newMinStr.Length < retStr.Length)
                {
                    ret = newMin;
                    retStr = newMinStr;
                }
            }

            return ret;
        }

        private NumberValue MinifyNumberValue(NumberValue value)
        {
            var asStr = value.Value.ToString();
            if (asStr.Contains('.'))
            {
                asStr = asStr.TrimEnd('0', '.');
            }

            asStr = asStr.TrimStart('0');

            if (asStr.Length == 0) asStr = "0";

            return new NumberValue(decimal.Parse(asStr));
        }

        private Value MinifyValue(Value value)
        {
            var comma = value as CommaDelimittedValue;
            if (comma != null)
            {
                var minified = comma.Values.Select(s => MinifyValue(s)).ToList();

                return new CommaDelimittedValue(minified);
            }

            var compound = value as CompoundValue;
            if (compound != null)
            {
                var minified = compound.Values.Select(s => MinifyValue(s)).ToList();

                return new CompoundValue(minified);
            }

            if (value is ColorValue)
            {
                return MinifyColor((ColorValue)value);
            }

            if (value is NumberWithUnitValue)
            {
                return MinifyNumberWithUnit((NumberWithUnitValue)value);
            }

            if (value is NumberValue)
            {
                return MinifyNumberValue((NumberValue)value);
            }

            return value;
        }

        private Property MinifyRule(Property rule)
        {
            if (!(rule is NameValueProperty))
                throw new InvalidOperationException("Minify cannot be run on non name-value rules, found [" + rule + "]");

            var named = (NameValueProperty)rule;
            var value = named.Value;

            return new NameValueProperty(named.Name, MinifyValue(value));
        }

        public List<Block> Minify(List<Block> statements)
        {
            var ret = new List<Block>();

            foreach (var statement in statements)
            {
                var block = statement as SelectorAndBlock;
                if (block != null)
                {
                    var rules = new List<Property>();
                    foreach (var prop in block.Properties)
                    {
                        rules.Add(MinifyRule(prop));
                    }

                    ret.Add(new SelectorAndBlock(block.Selector, rules, block.Start, block.Stop, block.FilePath));
                    continue;
                }


                var media = statement as MediaBlock;
                if (media != null)
                {
                    var subStatements = Minify(media.Blocks.ToList());
                    ret.Add(new MediaBlock(media.ForMedia.ToList(), subStatements, media.Start, media.Stop, media.FilePath));
                    continue;
                }

                var keyframes = statement as KeyFramesBlock;
                if (keyframes != null)
                {
                    var frames = new List<KeyFrame>();

                    // minify each frame
                    foreach (var frame in keyframes.Frames)
                    {
                        var blockEquiv = new SelectorAndBlock(InvalidSelector.Singleton, frame.Properties, frame.Start, frame.Stop, frame.FilePath);
                        var mind = Minify(new List<Block>() { blockEquiv });
                        frames.Add(new KeyFrame(frame.Percentages.ToList(), ((SelectorAndBlock)mind[0]).Properties.ToList(), frame.Start, frame.Stop, frame.FilePath));
                    }

                    // collapse frames if rules are identical
                    var frameMap =
                        frames.ToDictionary(
                            d =>
                            {
                                using (var str = new StringWriter())
                                using (var css = new MinimalCssWriter(str))
                                {
                                    foreach (var rule in d.Properties.Cast<NameValueProperty>())
                                    {
                                        css.WriteRule(rule);
                                    }

                                    return str.ToString();
                                }
                            },
                            d => d
                        );

                    frames.Clear();
                    foreach (var frame in frameMap.GroupBy(k => k.Key))
                    {
                        var allPercents = new List<decimal>();
                        foreach (var f in frame)
                        {
                            allPercents.AddRange(f.Value.Percentages);
                        }

                        var urFrame = frame.First().Value;

                        frames.Add(new KeyFrame(allPercents, urFrame.Properties.ToList(), urFrame.Start, urFrame.Stop, urFrame.FilePath));
                    }

                    ret.Add(new KeyFramesBlock(keyframes.Prefix, keyframes.Name, frames, keyframes.Variables.ToList(), keyframes.Start, keyframes.Stop, keyframes.FilePath));
                    continue;
                }

                ret.Add(statement);
            }

            return ret;
        }
    }
}
