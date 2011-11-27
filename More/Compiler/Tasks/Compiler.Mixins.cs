using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using More.Model;

namespace More.Compiler
{
    partial class Compiler
    {
        private Dictionary<string, V> MapAndWarnDupe<T, V>(IEnumerable<T> toMap, Func<T, string> key, Func<T, V> value = null)
        {
            var ret = new Dictionary<string, V>();

            var stopCompiling = false;

            foreach (var v in toMap.GroupBy(key))
            {
                if (v.Count() == 1)
                {
                    ret[v.Key] = value(v.Single());
                    continue;
                }

                Current.RecordError(ErrorType.Compiler, Position.NoSite, v.Key + " is defined " + v.Count() + " times.");
                stopCompiling = true;
            }

            if (stopCompiling)
            {
                throw new StoppedCompilingException();
            }

            return ret;
        }

        internal List<Block> BindAndEvaluateMixins(List<Block> statements)
        {
            var ret = new List<Block>();

            var mixins = MapAndWarnDupe(statements.OfType<MixinBlock>(), s => s.Name, v => v);
            var variables = MapAndWarnDupe(statements.OfType<MoreVariable>(), s => s.Name, v => v.Value);

            var globalScope = new Scope(variables, mixins);

            Current.SetGlobalScope(globalScope);

            foreach (var charset in statements.OfType<CssCharset>())
            {
                ret.Add(charset.Bind(globalScope));
            }

            foreach (var import in statements.OfType<Import>())
            {
                ret.Add(import.Bind(globalScope));
            }

            foreach (var block in statements.OfType<SelectorAndBlock>())
            {
                ret.Add(block.BindAndEvaluateMixins());
            }

            foreach (var animation in statements.OfType<KeyFramesBlock>())
            {
                var innerVariables = MapAndWarnDupe(animation.Variables, s => s.Name, s => s.Value);
                var innerScope = globalScope.Push(innerVariables, new Dictionary<string, MixinBlock>(), animation);

                var frames = new List<KeyFrame>();
                foreach (var frame in animation.Frames)
                {
                    var blockEquivalent = new SelectorAndBlock(InvalidSelector.Singleton, frame.Properties, frame.Start, frame.Stop, frame.FilePath);
                    var bound = blockEquivalent.BindAndEvaluateMixins(innerScope);

                    frames.Add(new KeyFrame(frame.Percentages.ToList(), bound.Properties.ToList(), frame.Start, frame.Stop, frame.FilePath));
                }

                ret.Add(new KeyFramesBlock(animation.Prefix, animation.Name, frames.ToList(), animation.Variables.ToList(), animation.Start, animation.Stop, animation.FilePath));
            }

            foreach (var media in statements.OfType<MediaBlock>())
            {
                var innerRet = new List<Block>();

                var innerVariable = MapAndWarnDupe(media.Blocks.OfType<MoreVariable>(), s => s.Name, v => v.Value);
                var innerScope = globalScope.Push(innerVariable, new Dictionary<string, MixinBlock>(), media);

                foreach (var block in media.Blocks.OfType<SelectorAndBlock>())
                {
                    innerRet.Add(block.BindAndEvaluateMixins(innerScope));
                }

                ret.Add(new MediaBlock(media.ForMedia.ToList(), innerRet, media.Start, media.Stop, media.FilePath));
            }

            foreach (var font in statements.OfType<FontFaceBlock>())
            {
                var blockEquiv = new SelectorAndBlock(InvalidSelector.Singleton, font.Properties, font.Start, font.Stop, font.FilePath);
                var bound = blockEquiv.BindAndEvaluateMixins(globalScope);

                ret.Add(new FontFaceBlock(bound.Properties.ToList(), font.Start, font.Stop, font.FilePath));
            }

            return ret;
        }
    }
}
