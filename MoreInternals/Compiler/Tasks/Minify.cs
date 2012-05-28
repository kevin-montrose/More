using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;
using System.IO;
using MoreInternals.Helpers;

namespace MoreInternals.Compiler.Tasks
{
    /// <summary>
    /// This task re-writes values so that they take as little space as possible.
    /// 
    /// This includes coercing units (if the inch version is smaller than the centimeter
    /// it will be used, and so on) and choosing ideal color versions (hex triples when possible,
    /// dropping alpha when not needed, and so on).
    /// </summary>
    public partial class Minify
    {
        private static ColorValue MinifyColor(ColorValue value)
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

        private static NumberWithUnitValue MinifyNumberWithUnit(NumberWithUnitValue value)
        {
            var min = MinifyNumberValue(value);

            var ret = new NumberWithUnitValue(min.Value, value.Unit);
            string retStr;
            using (var buffer = new StringWriter())
            {
                ret.Write(buffer);
                retStr = buffer.ToString();
            }

            if (Value.ConvertableSizeUnits.ContainsKey(value.Unit))
            {
                var inMM = min.Value * Value.ConvertableSizeUnits[value.Unit];

                foreach (var unit in Value.ConvertableSizeUnits.Keys)
                {
                    var inUnit = inMM / Value.ConvertableSizeUnits[unit];

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
            }

            if (Value.ConvertableTimeUnits.ContainsKey(value.Unit))
            {
                var inS = min.Value * Value.ConvertableTimeUnits[value.Unit];

                foreach (var unit in Value.ConvertableTimeUnits.Keys)
                {
                    var inUnit = inS / Value.ConvertableTimeUnits[unit];
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
            }

            if (Value.ConvertableResolutionUnits.ContainsKey(value.Unit))
            {
                var inDpcm = min.Value * Value.ConvertableResolutionUnits[value.Unit];

                foreach (var unit in Value.ConvertableResolutionUnits.Keys)
                {
                    var inUnit = inDpcm / Value.ConvertableResolutionUnits[unit];
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
            }

            return ret;
        }

        private static NumberValue MinifyNumberValue(NumberValue value)
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

        private static Value MinifyValue(Value value)
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

        private static Property MinifyProperty(Property rule)
        {
            if (!(rule is NameValueProperty))
                throw new InvalidOperationException("Minify cannot be run on non name-value rules, found [" + rule + "]");

            var named = (NameValueProperty)rule;
            var value = named.Value;

            return new NameValueProperty(named.Name, MinifyValue(value));
        }

        private static IEnumerable<Property> MinifyPropertyList(IEnumerable<Property> p)
        {
            var original = new List<Property>(p);

            var ret = p.Cast<NameValueProperty>();

            // font needs special treatment, because of the / shorthand
            ret = MinifyFontProperties(ret);

            // These properties all take the form X: (X-*)+; the order of their inclusion is important,
            //   but ommisions don't need special treatment
            ret = 
                MinifyGenericShorthand(
                    ret,
                    "background",
                    "background-color", "background-image", "background-repeat", "background-attachment", "background-position"
                );
            ret = 
                MinifyGenericShorthand(
                    ret,
                    "border",
                    "border-width", "border-style", "border-color"
                );
            ret =
                MinifyGenericShorthand(
                    ret,
                    "border-bottom",
                    "border-bottom-width", "border-bottom-style", "border-bottom-color"
                );
            ret =
                MinifyGenericShorthand(
                    ret,
                    "border-bottom",
                    "border-bottom-width", "border-bottom-style", "border-bottom-color"
                );
            ret =
                MinifyGenericShorthand(
                    ret,
                    "border-left",
                    "border-left-width", "border-left-style", "border-left-color"
                );
            ret =
                MinifyGenericShorthand(
                    ret,
                    "border-right",
                    "border-right-width", "border-right-style", "border-right-color"
                );
            ret =
                MinifyGenericShorthand(
                    ret,
                    "border-top",
                    "border-top-width", "border-top-style", "border-top-color"
                );
            ret =
                MinifyGenericShorthand(
                    ret,
                    "list-style",
                    "list-style-type", "list-style-position", "list-style-image"
                );
            ret =
                MinifyGenericShorthand(
                    ret,
                    "outline",
                    "outline-color", "outline-style", "outline-width"
                );

            // Here we minify the trickier "border-width-style" rules
            ret =
                MinifyBorderWidthShorthand(
                    ret,
                    "border-width",
                    "border-width-top", "border-width-right", "border-width-bottom", "border-width-left",
                    new NumberValue(0)
                );
            ret =
                MinifyBorderWidthShorthand(
                    ret,
                    "border-style",
                    "border-style-top", "border-style-right", "border-style-bottom", "border-style-left",
                    new StringValue("none")
                );
            ret =
                MinifyBorderWidthShorthand(
                    ret,
                    "border-color",
                    "border-color-top", "border-color-right", "border-color-bottom", "border-color-left",
                    new StringValue("transparent")
                );
            ret =
                MinifyBorderWidthShorthand(
                    ret,
                    "margin",
                    "margin-top", "margin-right", "margin-bottom", "margin-left",
                    new NumberValue(0)
                );
            ret =
                MinifyBorderWidthShorthand(
                    ret,
                    "padding",
                    "padding-top", "padding-right", "padding-bottom", "padding-left",
                    new NumberValue(0)
                );

            // minifying something may open opportunities for further minification
            //   So continue trying until we're in a stable state.
            // Theoretically we're not guaranteed to get the ideal minification, but
            //   we'll find a local maximum this way.
            if (!Current.DisableMultipleMinificationPasses && (ret.Count() != original.Count() || !ret.All(r => original.Contains(r))))
            {
                return MinifyPropertyList(ret);
            }

            return ret;
        }

        private static string ValueToString(Value value)
        {
            using (var mem = new StringWriter())
            {
                value.Write(mem);

                return mem.ToString();
            }
        }

        private static MediaQuery ForQuery(MediaQuery query)
        {
            var not = query as NotMedia;
            if (not != null)
            {
                return new NotMedia(ForQuery(not.Clause), not);
            }

            var only = query as OnlyMedia;
            if (only != null)
            {
                return new OnlyMedia(ForQuery(only.Clause), only);
            }

            var type = query as MediaType;
            if (type != null)
            {
                return type;
            }

            var and = query as AndMedia;
            if (and != null)
            {
                return new AndMedia(ForQuery(and.LeftHand), ForQuery(and.RightHand), and);
            }

            var has = query as FeatureMedia;
            if (has != null) return has;

            var eq = query as EqualFeatureMedia;
            if (eq != null)
            {
                return new EqualFeatureMedia(eq.Feature, MinifyValue(eq.EqualsValue), eq);
            }

            var min = query as MinFeatureMedia;
            if (min != null)
            {
                return new MinFeatureMedia(min.Feature, MinifyValue(min.Min), min);
            }

            var max = query as MaxFeatureMedia;
            if (max != null)
            {
                return new MaxFeatureMedia(max.Feature, MinifyValue(max.Max), max);
            }

            throw new InvalidOperationException("Unexpected media clause [" + query + "]");
        }

        public static List<Block> Task(List<Block> blocks)
        {
            var ret = new List<Block>();

            ret.AddRange(blocks.OfType<CssCharset>());
            ret.AddRange(blocks.OfType<Model.Import>().Select(s => new Model.Import(MinifyValue(s.ToImport), ForQuery(s.MediaQuery), s.Start, s.Stop, s.FilePath)));
            ret.AddRange(blocks.Where(w => w is SelectorAndBlock && ((SelectorAndBlock)w).IsReset));

            var remainder = blocks.Where(w => !ret.Contains(w));

            foreach (var statement in remainder)
            {
                var block = statement as SelectorAndBlock;
                if (block != null)
                {
                    var rules = new List<Property>();
                    foreach (var prop in block.Properties)
                    {
                        rules.Add(MinifyProperty(prop));
                    }

                    rules = MinifyPropertyList(rules).ToList();

                    ret.Add(new SelectorAndBlock(block.Selector, rules, block.ResetContext, block.Start, block.Stop, block.FilePath));
                    continue;
                }

                var media = statement as MediaBlock;
                if (media != null)
                {
                    var subStatements = Task(media.Blocks.ToList());
                    ret.Add(new MediaBlock(ForQuery(media.MediaQuery), subStatements, media.Start, media.Stop, media.FilePath));
                    continue;
                }

                var keyframes = statement as KeyFramesBlock;
                if (keyframes != null)
                {
                    var frames = new List<KeyFrame>();

                    // minify each frame
                    foreach (var frame in keyframes.Frames)
                    {
                        var blockEquiv = new SelectorAndBlock(InvalidSelector.Singleton, frame.Properties, null, frame.Start, frame.Stop, frame.FilePath);
                        var mind = Task(new List<Block>() { blockEquiv });
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
                                        css.WriteRule(rule, lastRule: false);
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
