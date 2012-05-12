using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;

namespace MoreInternals.Compiler.Tasks
{
    // Handles re-writting all the various "long form" CSS properties into their shorthands.
    // 
   /// Think background-color, background-position, etc. -> background
    public partial class Minify
    {
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
