using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;
using System.IO;
using MoreInternals.Helpers;

namespace MoreInternals.Compiler.Tasks
{
    // Handles re-writting all the various "long form" CSS properties into their shorthands.
    // 
   /// Think background-color, background-position, etc. -> background
    public partial class Minify
    {
        /// <summary>
        /// Take the set of properties that serializes to a shorter string.
        /// 
        /// When (presumably equivalent) blocks of CSS are passed in, this method checks that certain attempts
        /// at minification actually resulted in shorter strings.
        /// 
        /// This isn't always guaranteed when you're injecting default values.
        /// </summary>
        private static IEnumerable<NameValueProperty> TakeShorter(IEnumerable<NameValueProperty> first, IEnumerable<NameValueProperty> second)
        {
            Func<IEnumerable<NameValueProperty>, string> toString =
                x => 
                {
                    using(var writer = new StringWriter())
                    using(var css = new MinimalCssWriter(writer))
                    {
                        x.Each(e => css.WriteRule(e, false));

                        return writer.ToString();
                    }
                };

            var map = new[] { Tuple.Create(first, toString(first)), Tuple.Create(second, toString(second)) };
            return map.OrderBy(o => o.Item2.Length).First().Item1;
        }

        /// <summary>
        /// Oddly named, but the CSS 2.1 spec actually says "like border-width" in a few places to describe this shorthand.
        /// 
        /// Takes the 4 pass properties and reduces them to `into` in the following manner.
        /// 
        /// If any of the four properties aren't defined, consider them to be @default.
        /// 
        /// http://www.w3.org/TR/CSS21/box.html#propdef-border-width
        /// ^ reduce as defined there
        /// </summary>
        private static IEnumerable<NameValueProperty> MinifyBorderWidthShorthand(IEnumerable<NameValueProperty> props, string into, string topProp, string rightProp, string bottomProp, string leftProp, Value @default)
        {
            var topProps = props.Where(p => p.Name == topProp);
            var bottomProps = props.Where(p => p.Name == bottomProp);
            var rightProps = props.Where(p => p.Name == rightProp);
            var leftProps = props.Where(p => p.Name == leftProp);

            // duplicate declarations, bail
            if (topProps.Count() > 1 || bottomProps.Count() > 1 || rightProps.Count() > 1 || leftProps.Count() > 1)
            {
                return props;
            }

            // nothing to minify
            if (topProps.Count() == 0 && bottomProps.Count() == 0 && rightProps.Count() == 0 && leftProps.Count() == 0)
            {
                return props;
            }

            var ret = props.Where(w => !w.Name.In(topProp, leftProp, rightProp, bottomProp)).ToList();

            var topVal = topProps.Select(s => s.Value).SingleOrDefault() ?? @default;
            var bottomVal = bottomProps.Select(s => s.Value).SingleOrDefault() ?? @default;
            var rightVal = rightProps.Select(s => s.Value).SingleOrDefault() ?? @default;
            var leftVal = leftProps.Select(s => s.Value).SingleOrDefault() ?? @default;

            if (topVal.Equals(bottomVal) && topVal.Equals(rightVal) && topVal.Equals(leftVal))
            {
                ret.Add(new NameValueProperty(into, topVal));
                return TakeShorter(ret, props);
            }

            if (topVal.Equals(bottomVal) && leftVal.Equals(rightVal))
            {
                ret.Add(new NameValueProperty(into, new CompoundValue(topVal, rightVal)));
                return TakeShorter(ret, props);
            }

            if (!topVal.Equals(bottomVal) && leftVal.Equals(rightVal))
            {
                ret.Add(new NameValueProperty(into, new CompoundValue(topVal, rightVal, bottomVal)));
                return TakeShorter(ret, props);
            }

            ret.Add(new NameValueProperty(into, new CompoundValue(topVal, rightVal, bottomVal, leftVal)));

            return TakeShorter(ret, props);
        }

        /// <summary>
        /// Takes any set of properties and collapses those that are in `subProps` into `into` provided `into` isn't already defined.
        /// 
        /// For the fairly typical CSS shorthand of X: X-sub1 X-sub2 X-sub3 this is a pretty DRY approach.
        /// </summary>
        private static IEnumerable<NameValueProperty> MinifyGenericShorthand(IEnumerable<NameValueProperty> props, string into, params string[] subProps)
        {
            // If the shorthand is already being used, we're SOL
            if (props.Any(a => a.Name == into)) return props;

            var removeable =
                props.Where(p => subProps.Contains(p.Name))
                .GroupBy(p => p.Name)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Nothing to minify
            if (removeable.Count == 0) return props;

            // Duplicate declarations, cannot minify
            if (removeable.Any(k => k.Value.Count > 1)) return props;

            var indexableSubProps = subProps.ToList();

            var ret = new List<NameValueProperty>();
            ret.Add(
                new NameValueProperty(
                    into,
                    new CompoundValue(
                        removeable.OrderBy(k => indexableSubProps.IndexOf(k.Key))
                        .Select(s => s.Value.Single().Value)
                    )
                )
            );

            ret.AddRange(props.Where(p => !p.Name.In(subProps)));

            return ret;
        }

        /// <summary>
        /// font-size, font-family, font-style, font-variant, font-weight, and line-height (because nothing's ever easy)
        /// into font.
        /// </summary>
        private static IEnumerable<NameValueProperty> MinifyFontProperties(IEnumerable<NameValueProperty> props)
        {
            // Don't introduce a new font property if one already exists.
            if (props.Any(a => a.Name == "font")) return props;

            var ret = new List<NameValueProperty>();

            var fontSize = props.Where(w => w.Name == "font-size");
            var fontFamily = props.Where(w => w.Name == "font-family");

            // missing or duplicate font-size and font-family make this an untenable optimization
            if (fontSize.Count() != 1 || fontFamily.Count() != 1) return props;

            var fontStyle = props.Where(w => w.Name == "font-style");
            var fontVariant = props.Where(w => w.Name == "font-variant");
            var fontWeight = props.Where(w => w.Name == "font-weight");
            var lineHeight = props.Where(w => w.Name == "line-height");

            // duplicate of these properties make this untenable
            if (fontSize.Count() > 1 || fontVariant.Count() > 1 || fontWeight.Count() > 1 || lineHeight.Count() > 1) return props;

            var value = new StringBuilder();

            var style = fontStyle.SingleOrDefault();
            if (style != null)
            {
                value.Append(ValueToString(style.Value));
                value.Append(' ');
            }

            var variant = fontVariant.SingleOrDefault();
            if (variant != null)
            {
                value.Append(ValueToString(variant.Value));
                value.Append(' ');
            }

            var weight = fontWeight.SingleOrDefault();
            if (weight != null)
            {
                value.Append(ValueToString(weight.Value));
                value.Append(' ');
            }

            var height = lineHeight.SingleOrDefault();
            value.Append(ValueToString(fontSize.Single().Value));
            if (height != null)
            {
                value.Append('/');
                value.Append(ValueToString(height.Value));
            }
            value.Append(' ');

            value.Append(ValueToString(fontFamily.Single().Value));

            ret.Add(new NameValueProperty("font", new StringValue(value.ToString().Trim())));
            ret.AddRange(props.Where(w => !w.Name.In("font-size", "font-family", "font-style", "font-variant", "font-weight", "line-height")));

            return ret;
        }
    }
}
