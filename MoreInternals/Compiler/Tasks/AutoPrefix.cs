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

        /// <summary>
        /// This list is coming from:
        /// http://peter.sh/experiments/vendor-prefixed-css-property-overview/
        /// More or less, documentation around prefixes is pretty lack luster.
        /// 
        /// It includes all properties that have vendor prefixes that are also at least
        /// an editors draft.  Purely proprietary extensions are not included.
        /// </summary>
        private static Dictionary<Tuple<Prefix, string>, Func<Value, Value>> KnownPrefixes = 
        new Dictionary<Tuple<Prefix, string>, Func<Value, Value>>
        {
            { Tuple.Create(Prefix.WEBKIT, "align-content"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "align-items"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "align-self"), x => x },

            { Tuple.Create(Prefix.MOZ, "animation"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "animation"), x => x },
            { Tuple.Create(Prefix.MS, "animation"), x => x },

            { Tuple.Create(Prefix.MOZ, "animation-delay"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "animation-delay"), x => x },
            { Tuple.Create(Prefix.MS, "animation-delay"), x => x },

            { Tuple.Create(Prefix.MOZ, "animation-direction"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "animation-direction"), x => x },
            { Tuple.Create(Prefix.MS, "animation-direction"), x => x },

            { Tuple.Create(Prefix.MOZ, "animation-duration"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "animation-duration"), x => x },
            { Tuple.Create(Prefix.MS, "animation-duration"), x => x },

            { Tuple.Create(Prefix.MOZ, "animation-fill-mode"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "animation-fill-mode"), x => x },
            { Tuple.Create(Prefix.MS, "animation-fill-mode"), x => x },

            { Tuple.Create(Prefix.MOZ, "animation-iteration-count"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "animation-iteration-count"), x => x },
            { Tuple.Create(Prefix.MS, "animation-iteration-count"), x => x },

            { Tuple.Create(Prefix.MOZ, "animation-name"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "animation-name"), x => x },
            { Tuple.Create(Prefix.MS, "animation-name"), x => x },

            { Tuple.Create(Prefix.MOZ, "animation-play-state"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "animation-play-state"), x => x },
            { Tuple.Create(Prefix.MS, "animation-play-state"), x => x },

            { Tuple.Create(Prefix.MOZ, "animation-timing-function"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "animation-timing-function"), x => x },
            { Tuple.Create(Prefix.MS, "animation-timing-function"), x => x },

            { Tuple.Create(Prefix.MOZ, "appearance"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "appearance"), x => x },

            { Tuple.Create(Prefix.MOZ, "backface-visibility"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "backface-visibility"), x => x },
            { Tuple.Create(Prefix.MS, "backface-visibility"), x => x },

            { Tuple.Create(Prefix.WEBKIT, "background-clip"), x => x },

            { Tuple.Create(Prefix.WEBKIT, "background-origin"), x => x },

            { Tuple.Create(Prefix.WEBKIT, "background-size"), x => x },

            { Tuple.Create(Prefix.WEBKIT, "border-after"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "border-after-color"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "border-after-style"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "border-after-width"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "border-before"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "border-before-color"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "border-before-style"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "border-before-width"), x => x },

            { Tuple.Create(Prefix.WEBKIT, "border-bottom-left-radius"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "border-bottom-right-radius"), x => x },

            { Tuple.Create(Prefix.MOZ, "border-end"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "border-end"), x => x },

            { Tuple.Create(Prefix.MOZ, "border-end-color"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "border-end-color"), x => x },

            { Tuple.Create(Prefix.MOZ, "border-end-style"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "border-end-style"), x => x },

            { Tuple.Create(Prefix.MOZ, "border-end-width"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "border-end-width"), x => x },

            { Tuple.Create(Prefix.WEBKIT, "border-image"), x => x },
            { Tuple.Create(Prefix.O, "border-image"), x => x },

            { Tuple.Create(Prefix.O, "border-radius"), x => x },

            { Tuple.Create(Prefix.MOZ, "border-start"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "border-start"), x => x },

            { Tuple.Create(Prefix.MOZ, "border-start-color"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "border-start-color"), x => x },

            { Tuple.Create(Prefix.MOZ, "border-start-style"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "border-start-style"), x => x },

            { Tuple.Create(Prefix.MOZ, "border-start-width"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "border-start-width"), x => x },

            { Tuple.Create(Prefix.WEBKIT, "border-top-left-radius"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "border-top-right-radius"), x => x },

            { Tuple.Create(Prefix.MOZ, "box-align"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "box-align"), x => x },
            { Tuple.Create(Prefix.MS, "box-align"), x => x },

            { Tuple.Create(Prefix.WEBKIT, "box-decoration-break"), x => x },

            { Tuple.Create(Prefix.MOZ, "box-direction"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "box-direction"), x => x },
            { Tuple.Create(Prefix.MS, "box-direction"), x => x },

            { Tuple.Create(Prefix.MOZ, "box-flex"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "box-flex"), x => x },
            { Tuple.Create(Prefix.MS, "box-flex"), x => x },

            { Tuple.Create(Prefix.WEBKIT, "box-flex-group"), x => x },

            { Tuple.Create(Prefix.WEBKIT, "box-line"), x => x },
            { Tuple.Create(Prefix.MS, "box-line"), x => x },

            { Tuple.Create(Prefix.MOZ, "box-ordinal-group"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "box-ordinal-group"), x => x },
            { Tuple.Create(Prefix.MS, "box-ordinal-group"), x => x },

            { Tuple.Create(Prefix.MOZ, "box-orient"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "box-orient"), x => x },
            { Tuple.Create(Prefix.MS, "box-orient"), x => x },

            { Tuple.Create(Prefix.MOZ, "box-pack"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "box-pack"), x => x },
            { Tuple.Create(Prefix.MS, "box-pack"), x => x },

            { Tuple.Create(Prefix.WEBKIT, "box-shadow"), x => x },

            { Tuple.Create(Prefix.MOZ, "box-sizing"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "box-sizing"), x => x },

            { Tuple.Create(Prefix.MOZ, "column-count"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "column-count"), x => x },

            { Tuple.Create(Prefix.MOZ, "column-gap"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "column-gap"), x => x },

            { Tuple.Create(Prefix.MOZ, "column-rule"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "column-rule"), x => x },

            { Tuple.Create(Prefix.MOZ, "column-rule-color"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "column-rule-color"), x => x },

            { Tuple.Create(Prefix.MOZ, "column-rule-style"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "column-rule-style"), x => x },

            { Tuple.Create(Prefix.MOZ, "column-rule-width"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "column-rule-width"), x => x },

            { Tuple.Create(Prefix.WEBKIT, "column-span"), x => x },

            { Tuple.Create(Prefix.MOZ, "column-width"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "column-width"), x => x },

            { Tuple.Create(Prefix.MOZ, "columns"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "columns"), x => x },

            { Tuple.Create(Prefix.WEBKIT, "grid-column"), x => x },
            { Tuple.Create(Prefix.MS, "grid-column"), x => x },

            { Tuple.Create(Prefix.MS, "grid-column-align"), x => x },

            { Tuple.Create(Prefix.MS, "grid-column-span"), x => x },

            { Tuple.Create(Prefix.WEBKIT, "grid-columns"), x => x },
            { Tuple.Create(Prefix.MS, "grid-columns"), x => x },

            { Tuple.Create(Prefix.MS, "grid-layer"), x => x },

            { Tuple.Create(Prefix.WEBKIT, "grid-row"), x => x },
            { Tuple.Create(Prefix.MS, "grid-row"), x => x },

            { Tuple.Create(Prefix.MS, "grid-row-align"), x => x },

            { Tuple.Create(Prefix.MS, "grid-row-span"), x => x },

            { Tuple.Create(Prefix.WEBKIT, "grid-rows"), x => x },
            { Tuple.Create(Prefix.MS, "grid-rows"), x => x },

            { Tuple.Create(Prefix.WEBKIT, "hyphenate-character"), x => x },

            { Tuple.Create(Prefix.MOZ, "hyphens"), x => x },
            { Tuple.Create(Prefix.EPUB, "hyphens"), x => x },
            { Tuple.Create(Prefix.MS, "hyphens"), x => x },

            { Tuple.Create(Prefix.WEBKIT, "logical-height"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "logical-width"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "logical-after"), x => x },

            { Tuple.Create(Prefix.WEBKIT, "margin-before"), x => x },

            { Tuple.Create(Prefix.MOZ, "margin-end"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "margin-end"), x => x },

            { Tuple.Create(Prefix.MOZ, "margin-start"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "margin-start"), x => x },

            { Tuple.Create(Prefix.WEBKIT, "marquee-direction"), x => x },

            { Tuple.Create(Prefix.WAP, "marquee-loop"), x => x },

            { Tuple.Create(Prefix.WEBKIT, "marquee-speed"), x => x },
            { Tuple.Create(Prefix.WAP, "marquee-speed"), x => x },

            { Tuple.Create(Prefix.WEBKIT, "marquee-style"), x => x },
            { Tuple.Create(Prefix.WAP, "marquee-style"), x => x },

            { Tuple.Create(Prefix.WEBKIT, "max-logical-width"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "min-logical-height"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "min-logical-width"), x => x },

            { Tuple.Create(Prefix.O, "object-fit"), x => x },
            { Tuple.Create(Prefix.O, "object-position"), x => x },

            { Tuple.Create(Prefix.WEBKIT, "opacity"), x => x },

            { Tuple.Create(Prefix.MS, "overflow-style"), x => x },

            { Tuple.Create(Prefix.MS, "overflow-x"), x => x },
            { Tuple.Create(Prefix.MS, "overflow-y"), x => x },

            { Tuple.Create(Prefix.WEBKIT, "padding-after"), x => x },
            
            { Tuple.Create(Prefix.WEBKIT, "padding-before"), x => x },
            
            { Tuple.Create(Prefix.MOZ, "padding-end"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "padding-end"), x => x },

            { Tuple.Create(Prefix.MOZ, "padding-start"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "padding-start"), x => x },

            { Tuple.Create(Prefix.MOZ, "perspective"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "perspective"), x => x },
            { Tuple.Create(Prefix.MS, "perspective"), x => x },

            { Tuple.Create(Prefix.MOZ, "perspective-origin"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "perspective-origin"), x => x },
            { Tuple.Create(Prefix.MS, "perspective-origin"), x => x },

            { Tuple.Create(Prefix.MOZ, "text-align-last"), x => x },
            { Tuple.Create(Prefix.MS, "text-align-last"), x => x },

            { Tuple.Create(Prefix.MS, "text-align-autospace"), x => x },

            { Tuple.Create(Prefix.MS, "text-justify"), x => x },

            { Tuple.Create(Prefix.MS, "text-overflow"), x => x },

            { Tuple.Create(Prefix.MOZ, "transform"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "transform"), x => x },
            { Tuple.Create(Prefix.O, "transform"), x => x },
            { Tuple.Create(Prefix.MS, "transform"), x => x },

            { Tuple.Create(Prefix.MOZ, "transform-origin"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "transform-origin"), x => x },
            { Tuple.Create(Prefix.O, "transform-origin"), x => x },
            { Tuple.Create(Prefix.MS, "transform-origin"), x => x },

            { Tuple.Create(Prefix.MOZ, "transform-style"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "transform-style"), x => x },
            { Tuple.Create(Prefix.MS, "transform-style"), x => x },

            { Tuple.Create(Prefix.MOZ, "transition"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "transition"), x => x },
            { Tuple.Create(Prefix.O, "transition"), x => x },
            { Tuple.Create(Prefix.MS, "transition"), x => x },

            { Tuple.Create(Prefix.MOZ, "transition-delay"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "transition-delay"), x => x },
            { Tuple.Create(Prefix.O, "transition-delay"), x => x },
            { Tuple.Create(Prefix.MS, "transition-delay"), x => x },

            { Tuple.Create(Prefix.MOZ, "transition-duration"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "transition-duration"), x => x },
            { Tuple.Create(Prefix.O, "transition-duration"), x => x },
            { Tuple.Create(Prefix.MS, "transition-duration"), x => x },

            { Tuple.Create(Prefix.MOZ, "transition-property"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "transition-property"), x => x },
            { Tuple.Create(Prefix.O, "transition-property"), x => x },
            { Tuple.Create(Prefix.MS, "transition-property"), x => x },

            { Tuple.Create(Prefix.MOZ, "transition-timing-function"), x => x },
            { Tuple.Create(Prefix.WEBKIT, "transition-timing-function"), x => x },
            { Tuple.Create(Prefix.O, "transition-timing-function"), x => x },
            { Tuple.Create(Prefix.MS, "transition-timing-function"), x => x },

            { Tuple.Create(Prefix.EPUB, "word-break"), x => x },
            { Tuple.Create(Prefix.MS, "word-break"), x => x },

            { Tuple.Create(Prefix.MS, "word-wrap"), x => x },

            { Tuple.Create(Prefix.EPUB, "writing-mode"), x => x },
            { Tuple.Create(Prefix.MS, "writing-mode"), x => x }
        };

        #endregion

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
                Func<Value, Value> generate;
                if (KnownPrefixes.TryGetValue(match, out generate))
                {
                    var prefixedName = '-' + match.Item1.ToString().ToLowerInvariant() + '-' + prop.Name;
                    var perBrowserValue = generate(prop.Value);

                    ret.Add(new NameValueProperty(prefixedName, perBrowserValue));
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
