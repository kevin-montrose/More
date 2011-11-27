using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using More.Model;

namespace More.Compiler
{
    partial class Compiler
    {
        private void VerifyBlockVariableReferences(List<string> outer, IEnumerable<Property> rules)
        {
            var locallyDeclared = new List<string>();

            rules = rules.OrderBy(o => o is VariableProperty ? 0 : 1);

            foreach (var x in rules)
            {
                if (x is VariableProperty)
                {
                    var var = (VariableProperty)x;
                    var rhs = var.Value.ReferredToVariables();
                    var earlyReferences = rhs.Where(r => !outer.Contains(r) && !locallyDeclared.Contains(r));
                    foreach (var early in earlyReferences)
                    {
                        Current.RecordError(ErrorType.Compiler, x, "@" + early + " has not been defined");
                    }

                    locallyDeclared.Add(var.Name);
                }

                if (x is NameValueProperty)
                {
                    var name = (NameValueProperty)x;
                    var rhs = name.Value.ReferredToVariables();
                    var earlyReferences = rhs.Where(r => !outer.Contains(r) && !locallyDeclared.Contains(r));
                    foreach (var early in earlyReferences)
                    {
                        Current.RecordError(ErrorType.Compiler, x, "@" + early + " has not been defined");
                    }
                }

                if (x is NestedBlockProperty)
                {
                    var newOuter = new List<string>();
                    newOuter.AddRange(outer);
                    newOuter.AddRange(locallyDeclared);
                    VerifyBlockVariableReferences(newOuter, ((NestedBlockProperty)x).Block.Properties);
                }

                if (x is MixinApplicationProperty)
                {
                    var app = (MixinApplicationProperty)x;
                    var referredTo = app.ReferredToVariables();
                    var earlyReferences = referredTo.Where(r => !outer.Contains(r) && !locallyDeclared.Contains(r));
                    foreach (var early in earlyReferences)
                    {
                        Current.RecordError(ErrorType.Compiler, x, "@" + early + " has not been defined");
                    }
                }
            }
        }

        private void VerifyMixinVariableReferences(List<string> globals, MixinBlock mixin)
        {
            var locallyDeclared = new List<string>();

            locallyDeclared.AddRange(mixin.Parameters.Select(s => s.Name));

            if (mixin.Parameters.Count() > 0)
            {
                locallyDeclared.Add("arguments");
            }

            var outer = new List<string>();
            outer.AddRange(globals);
            outer.AddRange(locallyDeclared);

            VerifyBlockVariableReferences(outer, mixin.Properties);
        }

        internal List<Block> VerifyVariableReferences(List<Block> blocks)
        {
            VerifyVariableReferencesImpl(blocks);

            return blocks;
        }

        private void VerifyVariableReferencesImpl(List<Block> statements, List<string> globallyDeclared = null)
        {
            var globals = statements.OfType<MoreVariable>().OrderBy(a => a.Id);

            globallyDeclared = globallyDeclared ?? new List<string>();

            globallyDeclared.AddRange(statements.OfType<MixinBlock>().Select(s => s.Name));
            globallyDeclared.AddRange(BuiltInFunctions.All.Keys);

            foreach (var global in globals)
            {
                var rhs = global.Value;
                var referredTo = rhs.ReferredToVariables();
                var earlyReferences = referredTo.Where(r => !globallyDeclared.Contains(r));

                foreach (var early in earlyReferences)
                {
                    Current.RecordError(ErrorType.Compiler, global, "@" + early + " has not been defined");
                }

                globallyDeclared.Add(global.Name);
            }

            foreach (var rule in statements.OfType<SpriteBlock>())
            {
                var rhs = rule.OutputFile.ReferredToVariables();
                var earlyReferences = rhs.Where(r => !globallyDeclared.Contains(r));

                foreach (var early in earlyReferences)
                {
                    Current.RecordError(ErrorType.Compiler, rule, "@" + early + " has not been defined");
                }

                foreach (var decl in rule.Sprites)
                {
                    var declRhs = decl.SpriteFilePath.ReferredToVariables();
                    var declEarlies = declRhs.Where(r => !globallyDeclared.Contains(r));

                    foreach (var early in declEarlies)
                    {
                        Current.RecordError(ErrorType.Compiler, decl, "@" + early + " has not been defined");
                    }

                    globallyDeclared.Add(decl.MixinName);
                }
            }

            foreach (var keyframes in statements.OfType<KeyFramesBlock>())
            {
                foreach (var varDecl in keyframes.Variables)
                {
                    var rhs = varDecl.Value.ReferredToVariables();
                    var earlyRefs = rhs.Where(r => !globallyDeclared.Contains(r));
                    foreach (var early in earlyRefs)
                    {
                        Current.RecordError(ErrorType.Compiler, varDecl, "@" + early + " has not been defined");
                    }
                }

                var scoped = keyframes.Variables.Select(s => s.Name).ToList();
                scoped.AddRange(globallyDeclared);

                foreach (var frame in keyframes.Frames)
                {
                    VerifyBlockVariableReferences(scoped, frame.Properties);
                }
            }

            foreach (var mixin in statements.OfType<MixinBlock>())
            {
                VerifyMixinVariableReferences(globallyDeclared, mixin);
            }

            foreach (var rule in statements.OfType<SelectorAndBlock>())
            {
                VerifyBlockVariableReferences(globallyDeclared, rule.Properties);
            }

            foreach (var font in statements.OfType<FontFaceBlock>())
            {
                VerifyBlockVariableReferences(globallyDeclared, font.Properties);
            }

            foreach (var rule in statements.OfType<CssCharset>())
            {
                var rhs = rule.Charset.ReferredToVariables();
                var earlyReferences = rhs.Where(r => !globallyDeclared.Contains(r));

                foreach (var early in earlyReferences)
                {
                    Current.RecordError(ErrorType.Compiler, rule, "@" + early + " has not been defined");
                }
            }

            foreach (var rule in statements.OfType<Import>())
            {
                var rhs = rule.ToImport.ReferredToVariables();
                var earlyReferences = rhs.Where(r => !globallyDeclared.Contains(r));

                foreach (var early in earlyReferences)
                {
                    Current.RecordError(ErrorType.Compiler, rule, "@" + early + " has not been defined");
                }
            }

            foreach (var parts in statements.OfType<MediaBlock>())
            {
                VerifyVariableReferencesImpl(parts.Blocks.ToList(), globallyDeclared);
            }
        }
    }
}
