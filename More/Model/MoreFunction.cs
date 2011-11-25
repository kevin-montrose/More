using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace More.Model
{
    delegate Value MoreFunction(IEnumerable<Value> parameters, IPosition position);

    static class BuiltInFunctions
    {
        public static ReadOnlyDictionary<string, MoreFunction> All { get; set; }

        static BuiltInFunctions()
        {
            var ret = new Dictionary<string, MoreFunction>();

            foreach (var method in typeof(BuiltInFunctions).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public))
            {
                var key = method.Name.ToLower();
                var copy = method;

                ret[key] = 
                    delegate(IEnumerable<Value> @params, IPosition position)
                    {
                        // This is rather slow, we could jazz it up
                        // TODO: JAZZ IT UP!
                        return (Value)copy.Invoke(null, new object[] { @params, position });
                    };
            }

            All = new ReadOnlyDictionary<string, MoreFunction>(ret);
        }

        private static Tuple<decimal, decimal, decimal> ConvertToRGB(ColorValue param, IPosition position)
        {
            decimal r, g, b;
            if (param is RGBColorValue)
            {
                var rgb = (RGBColorValue)param;
                if (!(ConvertColorPart(rgb.Red, position, out r) & ConvertColorPart(rgb.Green, position, out g) & ConvertColorPart(rgb.Blue, position, out b)))
                {
                    return null;
                }

                return Tuple.Create(r, g, b);
            }

            if (param is RGBAColorValue)
            {
                var rgba = (RGBAColorValue)param;
                if (!(ConvertColorPart(rgba.Red, position, out r) & ConvertColorPart(rgba.Green, position, out g) & ConvertColorPart(rgba.Blue, position, out b)))
                {
                    return null;
                }

                return Tuple.Create(r, g, b);
            }

            if (param is HexTripleColorValue)
            {
                var hex3 = (HexTripleColorValue)param;
                r = hex3.Red;
                g = hex3.Green;
                b = hex3.Blue;

                return Tuple.Create(r, g, b);
            }

            if (param is HexSextupleColorValue)
            {
                var hex6 = (HexSextupleColorValue)param;
                r = hex6.Red;
                g = hex6.Green;
                b = hex6.Blue;

                return Tuple.Create(r, g, b);
            }

            if (param is NamedColorValue)
            {
                var named = (NamedColorValue)param;
                r = ((int)named.Color >> 16) & 0xFF;
                g = ((int)named.Color >> 8) & 0xFF;
                b = ((int)named.Color >> 0) & 0xFF;

                return Tuple.Create(r, g, b);
            }

            decimal h, s, l;
            h = s = l = -1;

            if (param is HSLColorValue)
            {
                var hsl = (HSLColorValue)param;
                h = ((NumberValue)hsl.Hue).Value;
                s = ((NumberValue)hsl.Saturation).Value;
                l = ((NumberValue)hsl.Lightness).Value;
            }

            if (h == -1)
            {
                Current.RecordError(ErrorType.Compiler, position, "Couldn't convert [" + param + "] to RGB");
                return null;
            }

            r = g = b = -1;

            var c = (1 - Math.Abs(2 * l - 1)) * s;
            var hPrime = h / 60m;
            var x = c * (1 - Math.Abs(hPrime % 2 - 1));

            if (hPrime >= 0 && hPrime < 1)
            {
                r = c;
                g = x;
                b = 0;
            }

            if (hPrime >= 1 && hPrime < 2)
            {
                r = x;
                g = c;
                b = 0;
            }

            if (hPrime >= 2 && hPrime < 3)
            {
                r = 0;
                g = c;
                b = x;
            }

            if (hPrime >= 3 && hPrime < 4)
            {
                r = 0;
                g = x;
                b = c;
            }

            if (hPrime >= 4 && hPrime < 5)
            {
                r = x;
                g = 0;
                b = c;
            }

            if (hPrime >= 5 && hPrime < 6)
            {
                r = c;
                g = 0;
                b = x;
            }

            var m = l - 0.5m * c;

            r = (r + m) * 255m;
            g = (g + m) * 255m;
            b = (b + m) * 255m;

            r = decimal.Round(r, 2, MidpointRounding.AwayFromZero);
            g = decimal.Round(g, 2, MidpointRounding.AwayFromZero);
            b = decimal.Round(b, 2, MidpointRounding.AwayFromZero);

            return Tuple.Create(r, g, b);
        }

        public static Value Blue(IEnumerable<Value> parameters, IPosition position)
        {
            if (parameters.Count() != 1 || parameters.Any(a => !(a is ColorValue)))
            {
                Current.RecordError(ErrorType.Compiler, position, "Blue expects a single color parameter");
                return ExcludeFromOutputValue.Singleton;
            }

            var param = (ColorValue)parameters.ElementAt(0);

            var parts = ConvertToRGB(param, position);

            if (parts == null) return ExcludeFromOutputValue.Singleton;

            return new NumberValue(parts.Item3);
        }

        public static Value Green(IEnumerable<Value> parameters, IPosition position)
        {
            if (parameters.Count() != 1 || parameters.Any(a => !(a is ColorValue)))
            {
                Current.RecordError(ErrorType.Compiler, position, "Green expects a single color parameter");
                return ExcludeFromOutputValue.Singleton;
            }

            var param = (ColorValue)parameters.ElementAt(0);

            var parts = ConvertToRGB(param, position);

            if (parts == null) return ExcludeFromOutputValue.Singleton;

            return new NumberValue(parts.Item2);
        }

        public static Value Red(IEnumerable<Value> parameters, IPosition position)
        {
            if (parameters.Count() != 1 || parameters.Any(a => !(a is ColorValue)))
            {
                Current.RecordError(ErrorType.Compiler, position, "Red expects a single color parameter");
                return ExcludeFromOutputValue.Singleton;
            }

            var param = (ColorValue)parameters.ElementAt(0);

            var parts = ConvertToRGB(param, position);

            if (parts == null) return ExcludeFromOutputValue.Singleton;

            return new NumberValue(parts.Item1);
        }

        private static bool ConvertColorPart(Value val, IPosition position, out decimal part)
        {
            part = 0;

            if (val is NumberWithUnitValue)
            {
                var unit = (NumberWithUnitValue)val;
                if (unit.Unit != Unit.Percent)
                {
                    Current.RecordError(ErrorType.Compiler, position, "Color parts must be raw numbers or percents, found [" + val + "]");
                    return false;
                }

                var v = unit.Value;
                if(v > 100)
                {
                    v = 100;
                    Current.RecordWarning(ErrorType.Compiler, position, "Clipped color-part percent from [" + unit.Value + "] to 100");
                }
                if(v < 0) 
                {
                    v = 0;
                    Current.RecordWarning(ErrorType.Compiler, position, "Clipped color-part percent from [" + unit.Value + "] to 0");
                }

                part = decimal.Round(255m * (v / 100m), 0, MidpointRounding.AwayFromZero);
                return true;
            }

            var noUnit = (NumberValue)val;

            var x = noUnit.Value;
            if (x > 255)
            {
                x = 255;
                Current.RecordWarning(ErrorType.Compiler, position, "Clipped color-part from [" + noUnit.Value + "] to 255");
            }

            if (x < 0)
            {
                x = 0;
                Current.RecordWarning(ErrorType.Compiler, position, "Clipped color-part from [" + noUnit.Value + "] to 0");
            }

            if (x <= 1)
            {
                x *= 255m;
            }

            part = x;
            return true;
        }

        private static Tuple<decimal, decimal, decimal> ConvertToHSL(ColorValue param, IPosition position)
        {
            if (param is HSLColorValue)
            {
                var hsl = (HSLColorValue)param;
                decimal h,s,l;
                h = ((NumberValue)hsl.Hue).Value;
                s = ((NumberValue)hsl.Saturation).Value;
                l = ((NumberValue)hsl.Lightness).Value;

                return Tuple.Create(h, s, l);
            }

            decimal r, g, b;
            r = g = b = -1;

            if (param is RGBColorValue)
            {
                var rgb = (RGBColorValue)param;
                if (!(ConvertColorPart(rgb.Red, position, out r) & ConvertColorPart(rgb.Green, position, out g) & ConvertColorPart(rgb.Blue, position, out b)))
                {
                    return null;
                }
            }

            if (param is RGBAColorValue)
            {
                var rgba = (RGBAColorValue)param;
                if (!(ConvertColorPart(rgba.Red, position, out r) & ConvertColorPart(rgba.Green, position, out g) & ConvertColorPart(rgba.Blue, position, out b)))
                {
                    return null;
                }
            }

            if (param is HexTripleColorValue)
            {
                var hex3 = (HexTripleColorValue)param;
                r = hex3.Red;
                g = hex3.Green;
                b = hex3.Blue;
            }

            if (param is HexSextupleColorValue)
            {
                var hex6 = (HexSextupleColorValue)param;
                r = hex6.Red;
                g = hex6.Green;
                b = hex6.Blue;
            }

            if (param is NamedColorValue)
            {
                var named = (NamedColorValue)param;
                r = ((int)named.Color >> 16) & 0xFF;
                g = ((int)named.Color >>  8) & 0xFF;
                b = ((int)named.Color >>  0) & 0xFF;
            }

            if (r == -1)
            {
                Current.RecordError(ErrorType.Compiler, position, "Couldn't convert [" + param + "] to HSL");
                return null;
            }

            r /= 255m;
            g /= 255m;
            b /= 255m;

            var max = Math.Max(Math.Max(r, g), b);
            var min = Math.Min(Math.Min(r, g), b);

            decimal hue, saturation;
            hue = saturation = 0;
            var lightness = (max + min) / 2;
            var d = max - min;

            if (d != 0)
            {
                saturation = lightness >= 0.5m ? d / (2m - d) : d / (max + min);

                if (max == r) { hue = (g < b ? 6m : 0m) + (g - b) / d; }
                if (max == g) { hue = 2m + (b - r) / d; }
                if (max == b) { hue = 4m + (r - g) / d; }

                hue /= 6m;
            }

            hue *= 360;
            hue = decimal.Round(hue, 2, MidpointRounding.AwayFromZero);
            saturation = decimal.Round(saturation, 2, MidpointRounding.AwayFromZero);
            lightness = decimal.Round(lightness, 2, MidpointRounding.AwayFromZero);

            return Tuple.Create(hue, saturation, lightness);
        }

        public static Value Lightness(IEnumerable<Value> parameters, IPosition position)
        {
            if (parameters.Count() != 1 || parameters.Any(a => !(a is ColorValue)))
            {
                Current.RecordError(ErrorType.Compiler, position, "Lightness expects a single color parameter");
                return ExcludeFromOutputValue.Singleton;
            }

            var param = (ColorValue)parameters.ElementAt(0);

            var hsl = ConvertToHSL(param, position);

            if (hsl == null) return ExcludeFromOutputValue.Singleton;

            return new NumberValue(hsl.Item3);
        }

        public static Value Saturation(IEnumerable<Value> parameters, IPosition position)
        {
            if (parameters.Count() != 1 || parameters.Any(a => !(a is ColorValue)))
            {
                Current.RecordError(ErrorType.Compiler, position, "Saturation expects a single color parameter");
                return ExcludeFromOutputValue.Singleton;
            }

            var param = (ColorValue)parameters.ElementAt(0);

            var hsl = ConvertToHSL(param, position);

            if (hsl == null) return ExcludeFromOutputValue.Singleton;

            return new NumberValue(hsl.Item2);
        }

        public static Value Hue(IEnumerable<Value> parameters, IPosition position)
        {
            if (parameters.Count() != 1 || parameters.Any(a => !(a is ColorValue)))
            {
                Current.RecordError(ErrorType.Compiler, position, "Hue expects a single color parameter");
                return ExcludeFromOutputValue.Singleton;
            }

            var param = (ColorValue)parameters.ElementAt(0);

            var hsl = ConvertToHSL(param, position);

            if (hsl == null) return ExcludeFromOutputValue.Singleton;

            return new NumberValue(hsl.Item1);
        }

        public static Value Round(IEnumerable<Value> parameters, IPosition position)
        {
            if (parameters.Count() < 1 || parameters.Count() > 2)
            {
                Current.RecordError(ErrorType.Compiler, position, "Round expects a 1 or 2 numeric parameters");
                return ExcludeFromOutputValue.Singleton;
            }

            if (parameters.Any(a => a is ExcludeFromOutputValue)) return ExcludeFromOutputValue.Singleton;
            if (parameters.Any(a => a is NotFoundValue)) return NotFoundValue.Default.BindToPosition(position.Start, position.Stop, position.FilePath);

            var toRound = parameters.ElementAt(0);
            Value precision = new NumberValue(0);

            if (parameters.Count() == 2)
            {
                precision = parameters.ElementAt(1);
            }

            if (!(toRound is NumberValue))
            {
                Current.RecordError(ErrorType.Compiler, position, "Round expects a number, with optional unit, as its first parameter; found [" + toRound + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            if (!(precision is NumberValue) || precision is NumberWithUnitValue)
            {
                Current.RecordError(ErrorType.Compiler, position, "Round expects a number, without a unit, as its second parameter; found [" + precision + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            var nPrecision = ((NumberValue)precision).Value;

            if ((int)nPrecision != nPrecision)
            {
                Current.RecordError(ErrorType.Compiler, position, "Round expects an integer as its second parameter, found [" + nPrecision + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            if (toRound is NumberWithUnitValue)
            {
                var wUnit = (NumberWithUnitValue)toRound;

                return new NumberWithUnitValue(decimal.Round(wUnit.Value, (int)nPrecision, MidpointRounding.AwayFromZero), wUnit.Unit);
            }

            var woUnit = (NumberValue)toRound;

            return new NumberValue(decimal.Round(woUnit.Value, (int)nPrecision, MidpointRounding.AwayFromZero));
        }

        public static Value Grayscale(IEnumerable<Value> parameters, IPosition position)
        {
            return Gray(parameters, position);
        }

        public static Value Greyscale(IEnumerable<Value> parameters, IPosition position)
        {
            return Gray(parameters, position);
        }

        public static Value Grey(IEnumerable<Value> parameters, IPosition position)
        {
            return Gray(parameters, position);
        }

        public static Value Gray(IEnumerable<Value> parameters, IPosition position)
        {
            if (parameters.Count() != 1)
            {
                Current.RecordError(ErrorType.Compiler, position, "Gray expects a 1 color parameter");
                return ExcludeFromOutputValue.Singleton;
            }

            var param = parameters.ElementAt(0);

            if (param is ExcludeFromOutputValue) return ExcludeFromOutputValue.Singleton;
            if (param is NotFoundValue) return NotFoundValue.Default.BindToPosition(position.Start, position.Start, position.FilePath);

            if (!(param is ColorValue))
            {
                Current.RecordError(ErrorType.Compiler, position, "Gray expects a color value, found [" + param + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            var parts = ConvertToRGB((ColorValue)param, position);
            if (parts == null) return ExcludeFromOutputValue.Singleton;

            var newPart = new NumberValue(decimal.Round((parts.Item1 + parts.Item2 + parts.Item3) / 3.0m, 2));

            if(param is RGBAColorValue)
            {
                var alpha = ((RGBAColorValue)param).Alpha;

                // Need to keep the alpah component in this case
                return new RGBAColorValue(newPart, newPart, newPart, alpha);
            }

            return new RGBColorValue(newPart, newPart, newPart);
        }

        public static Value Lighten(IEnumerable<Value> parameters, IPosition position)
        {
            if (parameters.Count() != 2)
            {
                Current.RecordError(ErrorType.Compiler, position, "Lighten expects a color parameter, and a percentage");
                return ExcludeFromOutputValue.Singleton;
            }

            var color = parameters.ElementAt(0);
            var percent = parameters.ElementAt(1);

            if (!(color is ColorValue))
            {
                Current.RecordError(ErrorType.Compiler, position, "Lighten expects a color as its first parameter, found [" + color + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            if (!(percent is NumberWithUnitValue))
            {
                Current.RecordError(ErrorType.Compiler, position, "Lighten expects a percentage as its second parameter, found [" + percent + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            var colorV = (ColorValue)color;
            var percentV = (NumberWithUnitValue)percent;

            if (percentV.Unit != Unit.Percent)
            {
                Current.RecordError(ErrorType.Compiler, position, "Lighten expects a percentage as its second parameter, found [" + percent + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            var hsl = ConvertToHSL(colorV, position);
            if (hsl == null) return ExcludeFromOutputValue.Singleton;

            var h = new NumberValue(hsl.Item1);
            var s = new NumberWithUnitValue(hsl.Item2 * 100m, Unit.Percent);

            var l = decimal.Round(hsl.Item3 + percentV.Value / 100m, 2);
            if (l > 1m) l = 1m;

            return new HSLColorValue(h, s, new NumberWithUnitValue(l * 100m, Unit.Percent));
        }

        public static Value Darken(IEnumerable<Value> parameters, IPosition position)
        {
            if (parameters.Count() != 2)
            {
                Current.RecordError(ErrorType.Compiler, position, "Darken expects a color parameter, and a percentage");
                return ExcludeFromOutputValue.Singleton;
            }

            var color = parameters.ElementAt(0);
            var percent = parameters.ElementAt(1);

            if (!(color is ColorValue))
            {
                Current.RecordError(ErrorType.Compiler, position, "Darken expects a color as its first parameter, found [" + color + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            if (!(percent is NumberWithUnitValue))
            {
                Current.RecordError(ErrorType.Compiler, position, "Darken expects a percentage as its second parameter, found [" + percent + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            var colorV = (ColorValue)color;
            var percentV = (NumberWithUnitValue)percent;

            if (percentV.Unit != Unit.Percent)
            {
                Current.RecordError(ErrorType.Compiler, position, "Darken expects a percentage as its second parameter, found [" + percent + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            var hsl = ConvertToHSL(colorV, position);
            if (hsl == null) return ExcludeFromOutputValue.Singleton;

            var h = new NumberValue(hsl.Item1);
            var s = new NumberWithUnitValue(hsl.Item2 * 100m, Unit.Percent);

            var l = decimal.Round(hsl.Item3 - percentV.Value / 100m, 2);
            if (l < 0m) l = 0m;

            return new HSLColorValue(h, s, new NumberWithUnitValue(l * 100m, Unit.Percent));
        }

        public static Value Saturate(IEnumerable<Value> parameters, IPosition position)
        {
            if (parameters.Count() != 2)
            {
                Current.RecordError(ErrorType.Compiler, position, "Saturate expects a color parameter, and a percentage");
                return ExcludeFromOutputValue.Singleton;
            }

            var color = parameters.ElementAt(0);
            var percent = parameters.ElementAt(1);

            if (!(color is ColorValue))
            {
                Current.RecordError(ErrorType.Compiler, position, "Saturate expects a color as its first parameter, found [" + color + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            if (!(percent is NumberWithUnitValue))
            {
                Current.RecordError(ErrorType.Compiler, position, "Saturate expects a percentage as its second parameter, found [" + percent + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            var colorV = (ColorValue)color;
            var percentV = (NumberWithUnitValue)percent;

            if (percentV.Unit != Unit.Percent)
            {
                Current.RecordError(ErrorType.Compiler, position, "Saturate expects a percentage as its second parameter, found [" + percent + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            var hsl = ConvertToHSL(colorV, position);
            if (hsl == null) return ExcludeFromOutputValue.Singleton;

            var h = new NumberValue(hsl.Item1);
            var l = new NumberWithUnitValue(hsl.Item3 * 100m, Unit.Percent);

            var s = decimal.Round(hsl.Item2 + percentV.Value / 100m, 2);
            if (s > 1m) s = 1m;

            return new HSLColorValue(h, new NumberWithUnitValue(s * 100m, Unit.Percent), l);
        }

        public static Value Desaturate(IEnumerable<Value> parameters, IPosition position)
        {
            if (parameters.Count() != 2)
            {
                Current.RecordError(ErrorType.Compiler, position, "Desaturate expects a color parameter, and a percentage");
                return ExcludeFromOutputValue.Singleton;
            }

            var color = parameters.ElementAt(0);
            var percent = parameters.ElementAt(1);

            if (!(color is ColorValue))
            {
                Current.RecordError(ErrorType.Compiler, position, "Desaturate expects a color as its first parameter, found [" + color + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            if (!(percent is NumberWithUnitValue))
            {
                Current.RecordError(ErrorType.Compiler, position, "Desaturate expects a percentage as its second parameter, found [" + percent + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            var colorV = (ColorValue)color;
            var percentV = (NumberWithUnitValue)percent;

            if (percentV.Unit != Unit.Percent)
            {
                Current.RecordError(ErrorType.Compiler, position, "Desaturate expects a percentage as its second parameter, found [" + percent + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            var hsl = ConvertToHSL(colorV, position);
            if (hsl == null) return ExcludeFromOutputValue.Singleton;

            var h = new NumberValue(hsl.Item1);
            var l = new NumberWithUnitValue(hsl.Item3 * 100m, Unit.Percent);

            var s = decimal.Round(hsl.Item2 - percentV.Value / 100m, 2);
            if (s < 0m) s = 0m;

            return new HSLColorValue(h, new NumberWithUnitValue(s * 100m, Unit.Percent), l);
        }

        public static Value FadeIn(IEnumerable<Value> parameters, IPosition position)
        {
            if (parameters.Count() != 2)
            {
                Current.RecordError(ErrorType.Compiler, position, "FadeIn expects a color parameter, and a percentage");
                return ExcludeFromOutputValue.Singleton;
            }

            var color = parameters.ElementAt(0);
            var percent = parameters.ElementAt(1);

            if (!(color is ColorValue))
            {
                Current.RecordError(ErrorType.Compiler, position, "FadeIn expects a color as its first parameter, found [" + color + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            if (!(percent is NumberWithUnitValue))
            {
                Current.RecordError(ErrorType.Compiler, position, "FadeIn expects a percentage as its second parameter, found [" + percent + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            var colorV = (ColorValue)color;
            var percentV = (NumberWithUnitValue)percent;

            if (percentV.Unit != Unit.Percent)
            {
                Current.RecordError(ErrorType.Compiler, position, "FadeIn expects a percentage as its second parameter, found [" + percent + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            var alpha = new NumberWithUnitValue(100, Unit.Percent);
            if (colorV is RGBAColorValue)
            {
                var a = ((RGBAColorValue)colorV).Alpha;
                if (a is NumberWithUnitValue)
                {
                    alpha = (NumberWithUnitValue)a;
                }
                else
                {
                    alpha = new NumberWithUnitValue((100m * ((NumberValue)a).Value), Unit.Percent);
                }
            }

            var parts = ConvertToRGB(colorV, position);

            var newAlpha = alpha.Value + percentV.Value;
            if(newAlpha > 100m) newAlpha = 100m;

            newAlpha /= 100m;

            return new RGBAColorValue(new NumberValue(parts.Item1), new NumberValue(parts.Item2), new NumberValue(parts.Item3), new NumberValue(newAlpha));
        }

        public static Value FadeOut(IEnumerable<Value> parameters, IPosition position)
        {
            if (parameters.Count() != 2)
            {
                Current.RecordError(ErrorType.Compiler, position, "FadeOut expects a color parameter, and a percentage");
                return ExcludeFromOutputValue.Singleton;
            }

            var color = parameters.ElementAt(0);
            var percent = parameters.ElementAt(1);

            if (!(color is ColorValue))
            {
                Current.RecordError(ErrorType.Compiler, position, "FadeOut expects a color as its first parameter, found [" + color + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            if (!(percent is NumberWithUnitValue))
            {
                Current.RecordError(ErrorType.Compiler, position, "FadeOut expects a percentage as its second parameter, found [" + percent + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            var colorV = (ColorValue)color;
            var percentV = (NumberWithUnitValue)percent;

            if (percentV.Unit != Unit.Percent)
            {
                Current.RecordError(ErrorType.Compiler, position, "FadeOut expects a percentage as its second parameter, found [" + percent + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            var alpha = new NumberWithUnitValue(100, Unit.Percent);
            if (colorV is RGBAColorValue)
            {
                var a = ((RGBAColorValue)colorV).Alpha;
                if (a is NumberWithUnitValue)
                {
                    alpha = (NumberWithUnitValue)a;
                }
                else
                {
                    alpha = new NumberWithUnitValue((100m * ((NumberValue)a).Value), Unit.Percent);
                }
            }

            var parts = ConvertToRGB(colorV, position);

            var newAlpha = alpha.Value - percentV.Value;
            if (newAlpha < 0m) newAlpha = 0m;

            newAlpha /= 100m;

            return new RGBAColorValue(new NumberValue(parts.Item1), new NumberValue(parts.Item2), new NumberValue(parts.Item3), new NumberValue(newAlpha));
        }

        public static Value Fade(IEnumerable<Value> parameters, IPosition position)
        {
            if (parameters.Count() != 2)
            {
                Current.RecordError(ErrorType.Compiler, position, "Fade expects a color parameter, and a percentage");
                return ExcludeFromOutputValue.Singleton;
            }

            var color = parameters.ElementAt(0);
            var percent = parameters.ElementAt(1);

            if (!(color is ColorValue))
            {
                Current.RecordError(ErrorType.Compiler, position, "Fade expects a color as its first parameter, found [" + color + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            if (!(percent is NumberWithUnitValue))
            {
                Current.RecordError(ErrorType.Compiler, position, "Fade expects a percentage as its second parameter, found [" + percent + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            var colorV = (ColorValue)color;
            var percentV = (NumberWithUnitValue)percent;

            if (percentV.Unit != Unit.Percent)
            {
                Current.RecordError(ErrorType.Compiler, position, "Fade expects a percentage as its second parameter, found [" + percent + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            var parts = ConvertToRGB(colorV, position);

            var newAlpha = percentV.Value;
            if (newAlpha < 0m) newAlpha = 0m;
            if (newAlpha > 100m) newAlpha = 100m;

            newAlpha /= 100m;

            return new RGBAColorValue(new NumberValue(parts.Item1), new NumberValue(parts.Item2), new NumberValue(parts.Item3), new NumberValue(newAlpha));
        }

        public static Value Spin(IEnumerable<Value> parameters, IPosition position)
        {
            if (parameters.Count() != 2)
            {
                Current.RecordError(ErrorType.Compiler, position, "Spin expects a color parameter, and a unit-less number");
                return ExcludeFromOutputValue.Singleton;
            }

            var color = parameters.ElementAt(0);
            var number = parameters.ElementAt(1);

            if (!(color is ColorValue))
            {
                Current.RecordError(ErrorType.Compiler, position, "Spin expects a color as its first parameter, found [" + color + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            if (!(number is NumberValue) || (number is NumberWithUnitValue))
            {
                Current.RecordError(ErrorType.Compiler, position, "Spin expects a unit-less number as its second parameter, found [" + number + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            var colorV = (ColorValue)color;
            var numberV = (NumberValue)number;

            var parts = ConvertToHSL(colorV, position);
            if (parts == null) return ExcludeFromOutputValue.Singleton;

            var h = (int)(parts.Item1 + numberV.Value);
            h = ((h % 360) + 360) % 360;

            var s = new NumberWithUnitValue(parts.Item2 * 100m, Unit.Percent);
            var l = new NumberWithUnitValue(parts.Item3 * 100m, Unit.Percent);

            if (colorV is RGBAColorValue)
            {
                var hsl = new HSLColorValue(new NumberValue(h), s, l);

                var rgbParts = ConvertToRGB(hsl, position);
                return new RGBAColorValue(new NumberValue(rgbParts.Item1), new NumberValue(rgbParts.Item2), new NumberValue(rgbParts.Item3), ((RGBAColorValue)colorV).Alpha);
            }

            return new HSLColorValue(new NumberValue(h), s, l);
        }

        public static Value Mix(IEnumerable<Value> parameters, IPosition position)
        {
            if (parameters.Count() != 3)
            {
                Current.RecordError(ErrorType.Compiler, position, "Mix expects 3 parameters, 2 colors and 1 percentage or number");
                return ExcludeFromOutputValue.Singleton;
            }

            var c1 = parameters.ElementAt(0);
            var c2 = parameters.ElementAt(1);
            var p = parameters.ElementAt(2);

            if (!(c1 is ColorValue))
            {
                Current.RecordError(ErrorType.Compiler, position, "Mix expects its first parameter to be a color, found [" + c1 + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            if (!(c2 is ColorValue))
            {
                Current.RecordError(ErrorType.Compiler, position, "Mix expects its second parameter to be a color, found [" + c2 + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            if (!(p is NumberValue))
            {
                Current.RecordError(ErrorType.Compiler, position, "Mix expects its third parameter to be a number or percentage, found [" + p + "]");
                return ExcludeFromOutputValue.Singleton;
            }

            var percent = 1m;

            if (p is NumberWithUnitValue)
            {
                percent = ((NumberWithUnitValue)p).Value / 100m;
            }
            else
            {
                percent = ((NumberValue)p).Value;
            }

            if (percent > 1m) percent = 1m;
            if (percent < 0m) percent = 0m;

            var c1Parts = ConvertToRGB((ColorValue)c1, position);
            if (c1Parts == null) return ExcludeFromOutputValue.Singleton;

            var c2Parts = ConvertToRGB((ColorValue)c2, position);
            if (c2Parts == null) return ExcludeFromOutputValue.Singleton;

            var c1Alpah = 1m;
            var c2Alpha = 2m;

            if (c1 is RGBAColorValue)
            {
                var c1A = ((RGBAColorValue)c1).Alpha;
                if (c1A is NumberWithUnitValue)
                {
                    c1Alpah = ((NumberWithUnitValue)c1A).Value / 100m;
                }
                else
                {
                    c1Alpah = ((NumberValue)c1A).Value;
                }
            }

            if (c2 is RGBAColorValue)
            {
                var c2A = ((RGBAColorValue)c2).Alpha;
                if (c2A is NumberWithUnitValue)
                {
                    c2Alpha = ((NumberWithUnitValue)c2A).Value / 100m;
                }
                else
                {
                    c2Alpha = ((NumberValue)c2A).Value;
                }
            }

            var nR = c1Parts.Item1 * percent + c2Parts.Item1 * (1m - percent);
            var nG = c1Parts.Item2 * percent + c2Parts.Item2 * (1m - percent);
            var nB = c1Parts.Item3 * percent + c2Parts.Item3 * (1m - percent);
            var nA = c1Alpah * percent + c2Alpha * (1m - percent);

            return new RGBAColorValue(new NumberValue(nR), new NumberValue(nG), new NumberValue(nB), new NumberValue(nA));
        }

        public static Value NoUnit(IEnumerable<Value> parameters, IPosition position)
        {
            if (parameters.Count() != 1)
            {
                Current.RecordError(ErrorType.Compiler, position, "NoUnit expects 1 parameter of any type");
                return ExcludeFromOutputValue.Singleton;
            }

            var value = parameters.ElementAt(0);

            var asUnit = value as NumberWithUnitValue;

            if (asUnit == null) return value;

            return new NumberValue(asUnit.Value);
        }
    }
}
