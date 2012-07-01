using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;

namespace MoreInternals.Compiler.Tasks
{
    /// <summary>
    /// For every property that has a vendor prefix equivalent, generate that equivalent.
    /// 
    /// Informs if we encounter a prefixed property we could generate, ignores those we aren't aware of.
    /// 
    /// While the order of rules that aren't getting prefixed doesn't matter, the this task
    /// guarantees that rules which are prefixed appear before their equivalent unprefixed version.
    /// 
    /// This is important for future proofing, as initial browser implementation vagaries shouldn't override
    /// the final (hopefully) compliant implementation.
    /// </summary>
    public class AutoPrefix
    {
        private delegate IEnumerable<NameValueProperty> Prefixer(Prefix pre, NameValueProperty prop);

        private enum Prefix
        {
            MS,
            WEBKIT,
            O,
            MOZ,
            EPUB,
            WAP
        }

        #region KnownPrefixes

        private static Prefixer Simple =
            (pre, prop) =>
            {
                return
                    new[] 
                    {
                        new NameValueProperty(
                            "-" + pre.ToString().ToLowerInvariant() + "-" + prop.Name,
                            prop.Value
                        )
                    };
            };

        /// <summary>
        /// This list is coming from:
        /// http://peter.sh/experiments/vendor-prefixed-css-property-overview/
        /// More or less, documentation around prefixes is pretty lack luster.
        /// 
        /// It includes all properties that have vendor prefixes that are also at least
        /// an editors draft.  Purely proprietary extensions are not included.
        /// </summary>
        private static Dictionary<Tuple<Prefix, string>, Prefixer> KnownPrefixes =
        new Dictionary<Tuple<Prefix, string>, Prefixer>
        {
            { Tuple.Create(Prefix.WEBKIT, "align-content"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "align-items"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "align-self"), Simple },

            { Tuple.Create(Prefix.MOZ, "animation"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "animation"), Simple },
            { Tuple.Create(Prefix.MS, "animation"), Simple },

            { Tuple.Create(Prefix.MOZ, "animation-delay"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "animation-delay"), Simple },
            { Tuple.Create(Prefix.MS, "animation-delay"), Simple },

            { Tuple.Create(Prefix.MOZ, "animation-direction"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "animation-direction"), Simple },
            { Tuple.Create(Prefix.MS, "animation-direction"), Simple },

            { Tuple.Create(Prefix.MOZ, "animation-duration"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "animation-duration"), Simple },
            { Tuple.Create(Prefix.MS, "animation-duration"), Simple },

            { Tuple.Create(Prefix.MOZ, "animation-fill-mode"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "animation-fill-mode"), Simple },
            { Tuple.Create(Prefix.MS, "animation-fill-mode"), Simple },

            { Tuple.Create(Prefix.MOZ, "animation-iteration-count"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "animation-iteration-count"), Simple },
            { Tuple.Create(Prefix.MS, "animation-iteration-count"), Simple },

            { Tuple.Create(Prefix.MOZ, "animation-name"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "animation-name"), Simple },
            { Tuple.Create(Prefix.MS, "animation-name"), Simple },

            { Tuple.Create(Prefix.MOZ, "animation-play-state"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "animation-play-state"), Simple },
            { Tuple.Create(Prefix.MS, "animation-play-state"), Simple },

            { Tuple.Create(Prefix.MOZ, "animation-timing-function"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "animation-timing-function"), Simple },
            { Tuple.Create(Prefix.MS, "animation-timing-function"), Simple },

            { Tuple.Create(Prefix.MOZ, "appearance"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "appearance"), Simple },

            { Tuple.Create(Prefix.MOZ, "backface-visibility"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "backface-visibility"), Simple },
            { Tuple.Create(Prefix.MS, "backface-visibility"), Simple },

            { Tuple.Create(Prefix.MOZ, "background-clip"), MozBackgroundBoxAlt },
            { Tuple.Create(Prefix.WEBKIT, "background-clip"), WebkitBackgroundBoxAlt },

            { Tuple.Create(Prefix.MOZ, "background-origin"), MozBackgroundBoxAlt },
            { Tuple.Create(Prefix.WEBKIT, "background-origin"), WebkitBackgroundBoxAlt },

            { Tuple.Create(Prefix.MOZ, "background-size"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "background-size"), WebkitBackgroundSize },

            { Tuple.Create(Prefix.WEBKIT, "border-after"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "border-after-color"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "border-after-style"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "border-after-width"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "border-before"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "border-before-color"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "border-before-style"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "border-before-width"), Simple },

            { Tuple.Create(Prefix.MOZ, "border-bottom-left-radius"), Rename("border-radius-bottomleft") },
            { Tuple.Create(Prefix.WEBKIT, "border-bottom-left-radius"), Simple },

            { Tuple.Create(Prefix.MOZ, "border-bottom-right-radius"), Rename("border-radius-bottomright") },
            { Tuple.Create(Prefix.WEBKIT, "border-bottom-right-radius"), Simple },

            { Tuple.Create(Prefix.MOZ, "border-end"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "border-end"), Simple },

            { Tuple.Create(Prefix.MOZ, "border-end-color"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "border-end-color"), Simple },

            { Tuple.Create(Prefix.MOZ, "border-end-style"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "border-end-style"), Simple },

            { Tuple.Create(Prefix.MOZ, "border-end-width"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "border-end-width"), Simple },

            { Tuple.Create(Prefix.WEBKIT, "border-image"), Simple },
            { Tuple.Create(Prefix.O, "border-image"), Simple },

            { Tuple.Create(Prefix.WEBKIT, "border-radius"), WebkitBorderRadius },
            { Tuple.Create(Prefix.O, "border-radius"), Simple },

            { Tuple.Create(Prefix.MOZ, "border-start"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "border-start"), Simple },

            { Tuple.Create(Prefix.MOZ, "border-start-color"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "border-start-color"), Simple },

            { Tuple.Create(Prefix.MOZ, "border-start-style"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "border-start-style"), Simple },

            { Tuple.Create(Prefix.MOZ, "border-start-width"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "border-start-width"), Simple },

            { Tuple.Create(Prefix.MOZ, "border-top-left-radius"), Rename("border-radius-topleft") },
            { Tuple.Create(Prefix.WEBKIT, "border-top-left-radius"), Simple },

            { Tuple.Create(Prefix.MOZ, "border-top-right-radius"), Rename("border-radius-topright") },
            { Tuple.Create(Prefix.WEBKIT, "border-top-right-radius"), Simple },

            { Tuple.Create(Prefix.MOZ, "box-align"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "box-align"), Simple },
            { Tuple.Create(Prefix.MS, "box-align"), Simple },

            { Tuple.Create(Prefix.WEBKIT, "box-decoration-break"), Simple },

            { Tuple.Create(Prefix.MOZ, "box-direction"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "box-direction"), Simple },
            { Tuple.Create(Prefix.MS, "box-direction"), Simple },

            { Tuple.Create(Prefix.MOZ, "box-flex"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "box-flex"), Simple },
            { Tuple.Create(Prefix.MS, "box-flex"), Simple },

            { Tuple.Create(Prefix.WEBKIT, "box-flex-group"), Simple },

            { Tuple.Create(Prefix.WEBKIT, "box-line"), Simple },
            { Tuple.Create(Prefix.MS, "box-line"), Simple },

            { Tuple.Create(Prefix.MOZ, "box-ordinal-group"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "box-ordinal-group"), Simple },
            { Tuple.Create(Prefix.MS, "box-ordinal-group"), Simple },

            { Tuple.Create(Prefix.MOZ, "box-orient"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "box-orient"), Simple },
            { Tuple.Create(Prefix.MS, "box-orient"), Simple },

            { Tuple.Create(Prefix.MOZ, "box-pack"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "box-pack"), Simple },
            { Tuple.Create(Prefix.MS, "box-pack"), Simple },

            { Tuple.Create(Prefix.WEBKIT, "box-shadow"), Simple },

            { Tuple.Create(Prefix.MOZ, "box-sizing"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "box-sizing"), Simple },

            { Tuple.Create(Prefix.MOZ, "column-count"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "column-count"), Simple },

            { Tuple.Create(Prefix.MOZ, "column-gap"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "column-gap"), Simple },

            { Tuple.Create(Prefix.MOZ, "column-rule"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "column-rule"), Simple },

            { Tuple.Create(Prefix.MOZ, "column-rule-color"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "column-rule-color"), Simple },

            { Tuple.Create(Prefix.MOZ, "column-rule-style"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "column-rule-style"), Simple },

            { Tuple.Create(Prefix.MOZ, "column-rule-width"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "column-rule-width"), Simple },

            { Tuple.Create(Prefix.WEBKIT, "column-span"), Simple },

            { Tuple.Create(Prefix.MOZ, "column-width"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "column-width"), Simple },

            { Tuple.Create(Prefix.MOZ, "columns"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "columns"), Simple },

            { Tuple.Create(Prefix.MOZ, "display"), PrefixBox },
            { Tuple.Create(Prefix.WEBKIT, "display"), PrefixBox },

            { Tuple.Create(Prefix.WEBKIT, "grid-column"), Simple },
            { Tuple.Create(Prefix.MS, "grid-column"), Simple },

            { Tuple.Create(Prefix.MS, "grid-column-align"), Simple },

            { Tuple.Create(Prefix.MS, "grid-column-span"), Simple },

            { Tuple.Create(Prefix.WEBKIT, "grid-columns"), Simple },
            { Tuple.Create(Prefix.MS, "grid-columns"), Simple },

            { Tuple.Create(Prefix.MS, "grid-layer"), Simple },

            { Tuple.Create(Prefix.WEBKIT, "grid-row"), Simple },
            { Tuple.Create(Prefix.MS, "grid-row"), Simple },

            { Tuple.Create(Prefix.MS, "grid-row-align"), Simple },

            { Tuple.Create(Prefix.MS, "grid-row-span"), Simple },

            { Tuple.Create(Prefix.WEBKIT, "grid-rows"), Simple },
            { Tuple.Create(Prefix.MS, "grid-rows"), Simple },

            { Tuple.Create(Prefix.WEBKIT, "hyphenate-character"), Simple },

            { Tuple.Create(Prefix.MOZ, "hyphens"), Simple },
            { Tuple.Create(Prefix.EPUB, "hyphens"), Simple },
            { Tuple.Create(Prefix.MS, "hyphens"), Simple },

            { Tuple.Create(Prefix.WEBKIT, "logical-height"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "logical-width"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "logical-after"), Simple },

            { Tuple.Create(Prefix.WEBKIT, "margin-before"), Simple },

            { Tuple.Create(Prefix.MOZ, "margin-end"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "margin-end"), Simple },

            { Tuple.Create(Prefix.MOZ, "margin-start"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "margin-start"), Simple },

            { Tuple.Create(Prefix.WEBKIT, "marquee-direction"), Simple },

            { Tuple.Create(Prefix.WAP, "marquee-loop"), Simple },

            { Tuple.Create(Prefix.WEBKIT, "marquee-speed"), Simple },
            { Tuple.Create(Prefix.WAP, "marquee-speed"), Simple },

            { Tuple.Create(Prefix.WEBKIT, "marquee-style"), Simple },
            { Tuple.Create(Prefix.WAP, "marquee-style"), Simple },

            { Tuple.Create(Prefix.WEBKIT, "max-logical-width"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "min-logical-height"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "min-logical-width"), Simple },

            { Tuple.Create(Prefix.O, "object-fit"), Simple },
            { Tuple.Create(Prefix.O, "object-position"), Simple },

            { Tuple.Create(Prefix.WEBKIT, "opacity"), Simple },
            { Tuple.Create(Prefix.MS, "opacity"), IEOpacity },

            { Tuple.Create(Prefix.MS, "overflow-style"), Simple },

            { Tuple.Create(Prefix.MS, "overflow-x"), Simple },
            { Tuple.Create(Prefix.MS, "overflow-y"), Simple },

            { Tuple.Create(Prefix.WEBKIT, "padding-after"), Simple },
            
            { Tuple.Create(Prefix.WEBKIT, "padding-before"), Simple },
            
            { Tuple.Create(Prefix.MOZ, "padding-end"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "padding-end"), Simple },

            { Tuple.Create(Prefix.MOZ, "padding-start"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "padding-start"), Simple },

            { Tuple.Create(Prefix.MOZ, "perspective"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "perspective"), Simple },
            { Tuple.Create(Prefix.MS, "perspective"), Simple },

            { Tuple.Create(Prefix.MOZ, "perspective-origin"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "perspective-origin"), Simple },
            { Tuple.Create(Prefix.MS, "perspective-origin"), Simple },

            { Tuple.Create(Prefix.MOZ, "text-align-last"), Simple },
            { Tuple.Create(Prefix.MS, "text-align-last"), Simple },

            { Tuple.Create(Prefix.MS, "text-align-autospace"), Simple },

            { Tuple.Create(Prefix.MS, "text-justify"), Simple },

            { Tuple.Create(Prefix.MS, "text-overflow"), Simple },

            { Tuple.Create(Prefix.MOZ, "transform"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "transform"), Simple },
            { Tuple.Create(Prefix.O, "transform"), Simple },
            { Tuple.Create(Prefix.MS, "transform"), Simple },

            { Tuple.Create(Prefix.MOZ, "transform-origin"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "transform-origin"), Simple },
            { Tuple.Create(Prefix.O, "transform-origin"), Simple },
            { Tuple.Create(Prefix.MS, "transform-origin"), Simple },

            { Tuple.Create(Prefix.MOZ, "transform-style"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "transform-style"), Simple },
            { Tuple.Create(Prefix.MS, "transform-style"), Simple },

            { Tuple.Create(Prefix.MOZ, "transition"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "transition"), Simple },
            { Tuple.Create(Prefix.O, "transition"), Simple },
            { Tuple.Create(Prefix.MS, "transition"), Simple },

            { Tuple.Create(Prefix.MOZ, "transition-delay"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "transition-delay"), Simple },
            { Tuple.Create(Prefix.O, "transition-delay"), Simple },
            { Tuple.Create(Prefix.MS, "transition-delay"), Simple },

            { Tuple.Create(Prefix.MOZ, "transition-duration"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "transition-duration"), Simple },
            { Tuple.Create(Prefix.O, "transition-duration"), Simple },
            { Tuple.Create(Prefix.MS, "transition-duration"), Simple },

            { Tuple.Create(Prefix.MOZ, "transition-property"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "transition-property"), Simple },
            { Tuple.Create(Prefix.O, "transition-property"), Simple },
            { Tuple.Create(Prefix.MS, "transition-property"), Simple },

            { Tuple.Create(Prefix.MOZ, "transition-timing-function"), Simple },
            { Tuple.Create(Prefix.WEBKIT, "transition-timing-function"), Simple },
            { Tuple.Create(Prefix.O, "transition-timing-function"), Simple },
            { Tuple.Create(Prefix.MS, "transition-timing-function"), Simple },

            { Tuple.Create(Prefix.EPUB, "word-break"), Simple },
            { Tuple.Create(Prefix.MS, "word-break"), Simple },

            { Tuple.Create(Prefix.MS, "word-wrap"), Simple },

            { Tuple.Create(Prefix.EPUB, "writing-mode"), Simple },
            { Tuple.Create(Prefix.MS, "writing-mode"), Simple }
        };

        #endregion

        /// <summary>
        /// Webkit's prefixed background-size differs from the spec
        /// in that a single value (background-size: X) is treated like (background-size: X X).
        /// 
        /// It should be treated as (background-size: X auto), this detects and inserts that case.
        /// 
        /// In the case where two values are passed, the prefixed version matches the final spec.
        /// </summary>
        private static IEnumerable<NameValueProperty> WebkitBackgroundSize(Prefix pre, NameValueProperty backgroundSize)
        {
            var asMulti = backgroundSize.Value as CompoundValue;
            if (asMulti != null) return Simple(pre, backgroundSize);

            var newValue = new CompoundValue(backgroundSize.Value, new StringValue("auto"));

            return Simple(pre, new NameValueProperty(backgroundSize.Name, newValue));
        }

        /// <summary>
        /// Webkit supports prefixed versions of background-clip & background-origin
        /// that accepts alternate versions of padding-box, border-box, and content-box; padding, border, and content respectively.
        /// </summary>
        private static IEnumerable<NameValueProperty> WebkitBackgroundBoxAlt(Prefix pre, NameValueProperty backgroundX)
        {
            if (pre != Prefix.WEBKIT) throw new InvalidOperationException("Prefixer only valid for WEBKIT");

            var asStr = backgroundX.Value as StringValue;
            if (asStr == null) return Enumerable.Empty<NameValueProperty>();

            if (asStr.Value.Equals("padding-box", StringComparison.InvariantCultureIgnoreCase))
            {
                return
                    Simple(
                        pre,
                        new NameValueProperty(backgroundX.Name, new StringValue("padding"))
                    );
            }

            if (asStr.Value.Equals("border-box", StringComparison.InvariantCultureIgnoreCase))
            {
                return
                    Simple(
                        pre,
                        new NameValueProperty(backgroundX.Name, new StringValue("border"))
                    );
            }

            if (asStr.Value.Equals("content-box", StringComparison.InvariantCultureIgnoreCase))
            {
                return
                    Simple(
                        pre,
                        new NameValueProperty(backgroundX.Name, new StringValue("content"))
                    );
            }

            return Enumerable.Empty<NameValueProperty>();
        }

        /// <summary>
        /// Mozilla supports prefixed versions of background-clip & background-origin
        /// that accepts alternate versions of padding-box and border-box; padding and border respectively.
        /// </summary>
        private static IEnumerable<NameValueProperty> MozBackgroundBoxAlt(Prefix pre, NameValueProperty backgroundX)
        {
            if (pre != Prefix.MOZ) throw new InvalidOperationException("Prefixer only valid for MOZ");

            var asStr = backgroundX.Value as StringValue;
            if (asStr == null) return Enumerable.Empty<NameValueProperty>();

            if (asStr.Value.Equals("padding-box", StringComparison.InvariantCultureIgnoreCase))
            {
                return 
                    Simple(
                        pre,
                        new NameValueProperty(backgroundX.Name, new StringValue("padding"))
                    );
            }

            if(asStr.Value.Equals("border-box", StringComparison.InvariantCultureIgnoreCase))
            {
                return
                    Simple(
                        pre,
                        new NameValueProperty(backgroundX.Name, new StringValue("border"))
                    );
            }

            return Enumerable.Empty<NameValueProperty>();
        }

        /// <summary>
        /// Old versions of IE have two different syntax's for opacity:
        ///  - -ms-filter: progid:DXImageOhKillMeNow(Opacity=percent)
        ///  - filter: alpha(opacity=percent)
        /// </summary>
        private static IEnumerable<NameValueProperty> IEOpacity(Prefix pre, NameValueProperty opacity)
        {
            if (!opacity.Name.Equals("opacity", StringComparison.InvariantCultureIgnoreCase)) throw new InvalidOperationException("Prefix only valid on opacity property");
            if (pre != Prefix.MS) throw new InvalidOperationException("Prefixer only valid for MS prefix");

            var asNumber = opacity.Value as NumberValue;
            if (asNumber == null || asNumber is NumberWithUnitValue) return Enumerable.Empty<NameValueProperty>();

            var percent = decimal.Round(asNumber.Value * 100, 0);

            return
                new[] 
                {
                    new NameValueProperty("filter", new StringValue("progid:DXImageTransform.Microsoft.Alpha(Opacity=" + percent + ")"))
                };
        }

        /// <summary>
        /// For the display: box; property, changes the value to display: -prefix-box;
        /// </summary>
        private static IEnumerable<NameValueProperty> PrefixBox(Prefix pre, NameValueProperty display)
        {
            if(!display.Name.Equals("display", StringComparison.InvariantCultureIgnoreCase)) throw new InvalidOperationException("Prefixer only valid on display property");

            var asStr = display.Value as StringValue;
            if (asStr == null) return Enumerable.Empty<NameValueProperty>();

            if (asStr.Value.Equals("box", StringComparison.InvariantCultureIgnoreCase))
            {
                return
                    new []
                    {
                        new NameValueProperty(
                            "display", 
                            new StringValue("-"+pre.ToString().ToLowerInvariant()+"-box")
                        )
                    };
            }

            return Enumerable.Empty<NameValueProperty>();
        }

        /// <summary>
        /// border-radius and -moz-border-radius differ.
        /// 
        /// -webkit-border-radius: a b;
        /// is equivalent to 
        /// -webkit-top-left-border-radius: a b;
        /// -webkit-top-right-border-radius: a b;
        /// etc.
        /// 
        /// border-radius: a b;
        /// is equivalent to
        /// border-top-left-radius: a;
        /// border-top-right-radius: b;
        /// border-bottom-right-radius: a;
        /// border-bottom-left-radius: b;
        /// 
        /// This is only true if border-radius has two values; for all other configurations they are equivalent 
        /// (at least so far as I can tell).
        /// </summary>
        private static IEnumerable<NameValueProperty> WebkitBorderRadius(Prefix pre, NameValueProperty borderRadius)
        {
            if (borderRadius.Name != "border-radius") throw new InvalidOperationException("Prefixer only valid for border-radius property");
            if (pre != Prefix.WEBKIT) throw new InvalidOperationException("Prefixer only valid for WEBKIT prefix");

            var asCompound = borderRadius.Value as CompoundValue;

            if (asCompound == null || asCompound.Values.Count() != 2)
            {   
                return Simple(pre, borderRadius);
            }

            var ret = new List<NameValueProperty>();

            var tlbr = asCompound.Values.ElementAt(0);
            var trbl = asCompound.Values.ElementAt(1);

            ret.Add(new NameValueProperty("-webkit-border-top-left-radius", tlbr));
            ret.Add(new NameValueProperty("-webkit-border-top-right-radius", trbl));
            ret.Add(new NameValueProperty("-webkit-border-bottom-left-radius", trbl));
            ret.Add(new NameValueProperty("-webkit-border-bottom-right-radius", tlbr));

            return ret;
        }

        /// <summary>
        /// Handles a straight up name change.
        /// 
        /// If a browser implements a property as "-pre-foo" but the spec comes down to "bar",
        /// this let's use build a Prefixer that will map "bar: x' to "-pre-foo: x"
        /// </summary>
        private static Prefixer Rename(string newName)
        {
            return
                (pre, prop) =>
                {
                    var renamed =
                        new NameValueProperty(
                            newName,
                            prop.Value
                        );

                    return Simple(pre, renamed);
                };
        }

        private static List<NameValueProperty> PrefixProperty(NameValueProperty prop)
        {
            var ret = new List<NameValueProperty>();

            var possibleMatches = new List<Tuple<Prefix, string>>();

            foreach (var val in Enum.GetValues(typeof(Prefix)))
            {
                possibleMatches.Add(Tuple.Create((Prefix)val, prop.Name));
            }

            foreach (var match in possibleMatches)
            {
                Prefixer generate;
                if (KnownPrefixes.TryGetValue(match, out generate))
                {
                    var perBrowserValue = generate(match.Item1, prop);

                    ret.AddRange(perBrowserValue);
                }
            }

            // No prefixed versions found
            if (ret.Count == 0) return null;

            // Just to make testing easier
            ret = ret.OrderBy(r => r.Name).ToList();

            ret.Add(prop);

            return ret;
        }

        private static SelectorAndBlock PrefixBlock(SelectorAndBlock block)
        {
            var ret = new List<Property>();

            var asNameValue = block.Properties.Cast<NameValueProperty>().ToList();

            foreach (var prop in asNameValue)
            {
                var possible = PrefixProperty(prop);

                // No prefix versions, no point in any conflict checking; just put it back and continue
                if (possible == null)
                {
                    ret.Add(prop);
                    continue;
                }

                var alreadyPresent = asNameValue.Where(w => possible.Any(p => p.Name == w.Name && p.Name != prop.Name)).ToList();

                foreach (var dupe in alreadyPresent)
                {
                    Current.RecordInfo("Prefixed property [" + dupe.Name + "] could have been generated automatically");
                }

                possible.RemoveAll(x => alreadyPresent.Contains(x));

                ret.AddRange(possible);
            }

            return new SelectorAndBlock(block.Selector, ret, block.ResetContext, block.Start, block.Stop, block.FilePath);
        }

        public static List<Block> Task(List<Block> blocks)
        {
            var ret = new List<Block>();

            // Gotta maintain order at this point
            foreach (var block in blocks)
            {
                var selBlock = block as SelectorAndBlock;
                if (selBlock != null)
                {
                    ret.Add(PrefixBlock(selBlock));
                    continue;
                }

                var mediaBlock = block as MediaBlock;
                if (mediaBlock != null)
                {
                    var mediaRet = new List<Block>();
                    foreach (var subBlock in mediaBlock.Blocks.Cast<SelectorAndBlock>())
                    {
                        mediaRet.Add(PrefixBlock(subBlock));
                    }

                    ret.Add(new MediaBlock(mediaBlock.MediaQuery, mediaRet, mediaBlock.Start, mediaBlock.Stop, mediaBlock.FilePath));
                    continue;
                }

                var keyframesBlock = block as KeyFramesBlock;
                if (keyframesBlock != null)
                {
                    var keyframeRet = new List<KeyFrame>();
                    foreach (var frame in keyframesBlock.Frames)
                    {
                        var frameProp = new List<Property>();
                        foreach (var prop in frame.Properties.Cast<NameValueProperty>())
                        {
                            frameProp.AddRange(PrefixProperty(prop));
                        }

                        keyframeRet.Add(new KeyFrame(frame.Percentages.ToList(), frameProp, frame.Start, frame.Stop, frame.FilePath));
                    }

                    ret.Add(new KeyFramesBlock(keyframesBlock.Prefix, keyframesBlock.Name, keyframeRet, keyframesBlock.Variables.ToList(), keyframesBlock.Start, keyframesBlock.Stop, keyframesBlock.FilePath));
                    continue;
                }

                var fontBlock = block as FontFaceBlock;
                if (fontBlock != null)
                {
                    var fontRet = new List<Property>();
                    foreach (var rule in fontBlock.Properties.Cast<NameValueProperty>())
                    {
                        fontRet.AddRange(PrefixProperty(rule));
                    }

                    ret.Add(new FontFaceBlock(fontRet, fontBlock.Start, fontBlock.Stop, fontBlock.FilePath));
                    continue;
                }

                ret.Add(block);
            }

            return ret;
        }
    }
}
