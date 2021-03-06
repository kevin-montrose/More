﻿using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Threading;
using MoreInternals.Model;
using MoreInternals;
using MoreInternals.Parser;
using MoreInternals.Compiler;
using MoreInternals.Helpers;

namespace MoreTests
{
    [TestClass]
    public class ParserTests
    {
        private bool TryParse(string text)
        {
            Current.SetContext(new Context(new FileCache()));

            try
            {
                TryParseStatements(text);
                return true;
            }
            catch(Exception)
            {
                return false;
            }
        }

        private static int TryParseNumber = 0;
        private List<Block> TryParseStatements(string text)
        {
            Current.SetContext(new Context(new FileCache()));

            var toUse = Interlocked.Increment(ref TryParseNumber);

            using (var stream = new StringReader(text))
            {
                var compiler = Compiler.Get();
                return MoreInternals.Compiler.Tasks.Parse.ParseStreamImpl("!--parser test file " + toUse + " --!", stream);
            }
        }

        [TestMethod]
        public void Simple()
        {
            Assert.IsTrue(TryParse("div { rule1: value; }"), "1 rule element");
            Assert.IsTrue(TryParse(".class { }"), "no rule class");
            Assert.IsTrue(TryParse("#id { rule1: value; rule2: value2; }"), "1 rule i");
            Assert.IsTrue(TryParse("parent > child { }"), "parent child");
            Assert.IsTrue(TryParse("parent:hover { rule1: rule2; }"), "psuedo class");
            Assert.IsTrue(TryParse("something { @mixin(); }"), "trivial mixin");
            Assert.IsTrue(TryParse(@"something-else { @mixin(1,2,""3""); }"), "mixin with params");
            Assert.IsTrue(TryParse(@"outer { some: rule; .inner{ some: other; } }"), "nesting");
            Assert.IsTrue(TryParse(@"crazy { filter: progid:DXImageTransform.Microsoft.MotionBlur(strength=9, direction=90); }"), "something crazy!");

            Assert.IsTrue(TryParse(@".first, .second { rule: value; }"), "multiple selectors for a rule");

            Assert.IsTrue(TryParse("@mixin(@aa, @bb, @cc) { something: @aa; }"), "simple mixin decl");

            Assert.IsTrue(TryParse(@"@mixin(@a, @b, @c) { something: @a; @b(); inner { @c(); } }"), "complex mixin decl");

            Assert.IsTrue(TryParse(@"@var = ""value"";"), "1 variable");

            Assert.IsTrue(TryParse(@"@using ""~/something/else"";"), "import");
        }

        [TestMethod]
        public void Charset()
        {
            var statements =
                TryParseStatements(
                    @"@charset ""ISO-8859-1"";
                      img { rule: value; }"
                );

            Assert.AreEqual(2, statements.Count);
            Assert.AreEqual(typeof(CssCharset), statements[0].GetType());

            var charset = ((CssCharset)statements[0]).Charset;
            Assert.AreEqual(typeof(QuotedStringValue), charset.GetType());
            Assert.AreEqual("ISO-8859-1", ((QuotedStringValue)charset).Value);
        }

        [TestMethod]
        public void Imports()
        {
            var statements =
                TryParseStatements(
                    @"@import 'hello.txt';
                      @import url(../indeed.css);
                      @import 'world.txt' tv;
                      @import url(../other-stuff.css) braille, print;"
                );

            Assert.AreEqual(4, statements.Count);
            Assert.IsTrue(statements.All(t => t.GetType() == typeof(Import)));

            Assert.AreEqual("hello.txt", ((QuotedStringValue)((Import)statements[0]).ToImport).Value);
            Assert.AreEqual("../indeed.css", ((StringValue)((UrlValue)((Import)statements[1]).ToImport).UrlPath).Value);
            Assert.AreEqual("world.txt", ((QuotedStringValue)((Import)statements[2]).ToImport).Value);
            Assert.AreEqual("../other-stuff.css", ((StringValue)((UrlValue)((Import)statements[3]).ToImport).UrlPath).Value);

            var s1m = ((Import)statements[0]).MediaQuery as MediaType;
            Assert.IsNotNull(s1m);
            Assert.AreEqual(Media.all, s1m.Type);

            var s2m = ((Import)statements[1]).MediaQuery as MediaType;
            Assert.IsNotNull(s2m);
            Assert.AreEqual(Media.all, s2m.Type);

            var s3m = ((Import)statements[2]).MediaQuery as MediaType;
            Assert.IsNotNull(s3m);
            Assert.AreEqual(Media.tv, s3m.Type);

            var s4m = ((Import)statements[3]).MediaQuery as CommaDelimitedMedia;
            Assert.IsNotNull(s4m);
            var s4m1 = s4m.Clauses.ElementAt(0) as MediaType;
            var s4m2 = s4m.Clauses.ElementAt(1) as MediaType;
            Assert.IsNotNull(s4m1);
            Assert.IsNotNull(s4m2);
            Assert.AreEqual(Media.braille, s4m1.Type);
            Assert.AreEqual(Media.print, s4m2.Type);
        }

        [TestMethod]
        public void ConcatSelectors()
        {
            var nonConcat = TryParseStatements("div .class { }");
            var concat = TryParseStatements("div.class { }");

            Assert.AreEqual(1, nonConcat.Count);
            Assert.AreEqual(1, concat.Count);

            Assert.AreEqual(typeof(CompoundSelector), ((SelectorAndBlock)nonConcat[0]).Selector.GetType());
            Assert.AreEqual(typeof(ConcatSelector), ((SelectorAndBlock)concat[0]).Selector.GetType());

            var concatSel = (ConcatSelector)((SelectorAndBlock)concat[0]).Selector;

            Assert.AreEqual(typeof(ElementSelector), concatSel.Selectors.ElementAt(0).GetType());
            Assert.AreEqual(typeof(ClassSelector), concatSel.Selectors.ElementAt(1).GetType());
        }

        [TestMethod]
        public void HexColorId()
        {
            var simple = TryParseStatements("#abc { }");
            var complex = TryParseStatements("#ad502-rooms { }");

            Assert.AreEqual(1, simple.Count);
            Assert.AreEqual(1, complex.Count);
            Assert.AreEqual(typeof(IdSelector), ((SelectorAndBlock)simple[0]).Selector.GetType());
            Assert.AreEqual(typeof(IdSelector), ((SelectorAndBlock)complex[0]).Selector.GetType());

            var simpleSel = (IdSelector)((SelectorAndBlock)simple[0]).Selector;
            var complexSel = (IdSelector)((SelectorAndBlock)complex[0]).Selector; 

            Assert.AreEqual("abc", simpleSel.Name);
            Assert.AreEqual("ad502-rooms", complexSel.Name);
        }

        [TestMethod]
        public void SimpleImport()
        {
            var statements = TryParseStatements("@using 'hello.txt';");

            Assert.AreEqual(1, statements.Count);
            Assert.AreEqual(typeof(Using), statements[0].GetType());

            var import = statements[0] as Using;

            Assert.AreEqual("hello.txt", import.RawPath);
        }

        [TestMethod]
        public void MultiImport()
        {
            var statements = 
                TryParseStatements(
                    @"@using 'hello.txt';
                      @using ""foo.txt"";
                     "
                );

            Assert.AreEqual(2, statements.Count);
            Assert.AreEqual(typeof(Using), statements[0].GetType());
            Assert.AreEqual(typeof(Using), statements[1].GetType());

            var import1 = statements[0] as Using;
            Assert.AreEqual("hello.txt", import1.RawPath);

            var import2 = statements[1] as Using;
            Assert.AreEqual("foo.txt", import2.RawPath);
        }

        [TestMethod]
        public void Variables()
        {
            var statements =
                TryParseStatements(
                    @"@x = 'indeed';
                      @yy = 26px;
                      @zzz = 17;
                     "
                );

            Assert.AreEqual(3, statements.Count);
            Assert.AreEqual(typeof(MoreVariable), statements[0].GetType());
            Assert.AreEqual(typeof(MoreVariable), statements[1].GetType());
            Assert.AreEqual(typeof(MoreVariable), statements[2].GetType());

            var var1 = statements[0] as MoreVariable;
            Assert.AreEqual("x", var1.Name);
            var value1 = (QuotedStringValue)var1.Value;
            Assert.AreEqual("indeed", value1.Value);

            var var2 = statements[1] as MoreVariable;
            Assert.AreEqual("yy", var2.Name);
            var value2 = (NumberWithUnitValue)var2.Value;
            Assert.AreEqual(26, value2.Value);
            Assert.AreEqual(Unit.PX, value2.Unit);

            var var3 = statements[2] as MoreVariable;
            Assert.AreEqual("zzz", var3.Name);
            var value3 = (NumberValue)var3.Value;
            Assert.AreEqual(17, value3.Value);
        }

        [TestMethod]
        public void SimpleSelectors()
        {
            var element = TryParseStatements("element {}");
            var @class = TryParseStatements(".class {}");
            var id = TryParseStatements("#id {}");
            var wildcard = TryParseStatements("* {}");
            var pseudo = TryParseStatements(":hover {}");
            var langPseudo = TryParseStatements(":lang(en-us) {}");
            var nthChild = TryParseStatements(":nth-child(5) {}");
            var attrSet = TryParseStatements("[att] {}");
            var attrEq = TryParseStatements("[att=val] {}");
            var attrContains = TryParseStatements("[att~=val] {}");
            var attrStarts = TryParseStatements("[att|=val] {}");

            Assert.AreEqual(1, element.Count);
            Assert.AreEqual(1, @class.Count);
            Assert.AreEqual(1, id.Count);
            Assert.AreEqual(1, wildcard.Count);
            Assert.AreEqual(1, pseudo.Count);
            Assert.AreEqual(1, langPseudo.Count);
            Assert.AreEqual(1, nthChild.Count);
            Assert.AreEqual(1, attrSet.Count);
            Assert.AreEqual(1, attrEq.Count);
            Assert.AreEqual(1, attrContains.Count);
            Assert.AreEqual(1, attrStarts.Count);

            Assert.AreEqual(typeof(SelectorAndBlock), element[0].GetType());
            Assert.AreEqual(typeof(SelectorAndBlock), @class[0].GetType());
            Assert.AreEqual(typeof(SelectorAndBlock), id[0].GetType());
            Assert.AreEqual(typeof(SelectorAndBlock), wildcard[0].GetType());
            Assert.AreEqual(typeof(SelectorAndBlock), pseudo[0].GetType());
            Assert.AreEqual(typeof(SelectorAndBlock), langPseudo[0].GetType());
            Assert.AreEqual(typeof(SelectorAndBlock), nthChild[0].GetType());
            Assert.AreEqual(typeof(SelectorAndBlock), attrSet[0].GetType());
            Assert.AreEqual(typeof(SelectorAndBlock), attrEq[0].GetType());
            Assert.AreEqual(typeof(SelectorAndBlock), attrContains[0].GetType());
            Assert.AreEqual(typeof(SelectorAndBlock), attrStarts[0].GetType());

            var elementCss = element[0] as SelectorAndBlock;
            Assert.AreEqual(typeof(ElementSelector), elementCss.Selector.GetType());

            var classCss = @class[0] as SelectorAndBlock;
            Assert.AreEqual(typeof(ClassSelector), classCss.Selector.GetType());

            var idCss = id[0] as SelectorAndBlock;
            Assert.AreEqual(typeof(IdSelector), idCss.Selector.GetType());

            var wildcardCss = wildcard[0] as SelectorAndBlock;
            Assert.AreEqual(WildcardSelector.Singleton, wildcardCss.Selector);

            var psuedoCss = pseudo[0] as SelectorAndBlock;
            Assert.AreEqual(PseudoClassSelector.HoverSingleton, psuedoCss.Selector);

            var langCss = langPseudo[0] as SelectorAndBlock;
            Assert.AreEqual(typeof(LangPseudoClassSelector), langCss.Selector.GetType());

            var nthCss = nthChild[0] as SelectorAndBlock;
            Assert.AreEqual(typeof(NthChildPsuedoClassSelector), nthCss.Selector.GetType());

            var elementSelector = elementCss.Selector as ElementSelector;
            Assert.AreEqual("element", elementSelector.Name);

            var classSelector = classCss.Selector as ClassSelector;
            Assert.AreEqual("class", classSelector.Name);

            var idSelector = idCss.Selector as IdSelector;
            Assert.AreEqual("id", idSelector.Name);

            var langSelector = langCss.Selector as LangPseudoClassSelector;
            Assert.AreEqual("en-us", langSelector.Language);

            var nthSelector = nthCss.Selector as NthChildPsuedoClassSelector;
            Assert.AreEqual(5, nthSelector.Child.B);

            var attrSetSelector = ((SelectorAndBlock)attrSet[0]).Selector as AttributeSetSelector;
            Assert.AreEqual("att", attrSetSelector.Attribute);

            var attrEqSelector = ((SelectorAndBlock)attrEq[0]).Selector as AttributeOperatorSelector;
            Assert.AreEqual("att", attrEqSelector.Attribute);
            Assert.AreEqual(AttributeOperator.Equals, attrEqSelector.Operator);
            Assert.AreEqual("val", attrEqSelector.Value);

            var attrContainsSelector = ((SelectorAndBlock)attrContains[0]).Selector as AttributeOperatorSelector;
            Assert.AreEqual("att", attrContainsSelector.Attribute);
            Assert.AreEqual(AttributeOperator.Contains, attrContainsSelector.Operator);
            Assert.AreEqual("val", attrContainsSelector.Value);

            var attrStartSelector = ((SelectorAndBlock)attrStarts[0]).Selector as AttributeOperatorSelector;
            Assert.AreEqual("att", attrStartSelector.Attribute);
            Assert.AreEqual(AttributeOperator.Starts, attrStartSelector.Operator);
            Assert.AreEqual("val", attrStartSelector.Value);
        }

        [TestMethod]
        public void SimpleChildSelectors()
        {
            var elementClass = TryParseStatements("element > .class {}");
            var psuedoClass = TryParseStatements(":hover > .class {}");

            Assert.AreEqual(1, elementClass.Count);
            Assert.AreEqual(1, psuedoClass.Count);

            Assert.AreEqual(typeof(SelectorAndBlock), elementClass[0].GetType());
            Assert.AreEqual(typeof(SelectorAndBlock), psuedoClass[0].GetType());

            var ecSelector = (elementClass[0] as SelectorAndBlock).Selector;
            var pcSelector = (psuedoClass[0] as SelectorAndBlock).Selector;

            Assert.AreEqual(typeof(ChildSelector), ecSelector.GetType());
            Assert.AreEqual(typeof(ChildSelector), pcSelector.GetType());

            var ecChild = ecSelector as ChildSelector;
            var pcChild = pcSelector as ChildSelector;

            Assert.AreEqual(typeof(ElementSelector), ecChild.Parent.GetType());
            Assert.AreEqual(typeof(ClassSelector), ecChild.Child.GetType());

            Assert.AreEqual(typeof(PseudoClassSelector), pcChild.Parent.GetType());
            Assert.AreEqual(typeof(ClassSelector), pcChild.Child.GetType());
        }

        [TestMethod]
        public void SimpleMultiSelectors()
        {
            var multiSimple = TryParseStatements("#id, .class, element {}");

            Assert.AreEqual(1, multiSimple.Count);
            Assert.AreEqual(typeof(SelectorAndBlock), multiSimple[0].GetType());

            var selector = ((SelectorAndBlock)multiSimple[0]).Selector as MultiSelector;

            Assert.AreEqual(3, selector.Selectors.Count());
            Assert.AreEqual(typeof(IdSelector), selector.Selectors.ElementAt(0).GetType());
            Assert.AreEqual(typeof(ClassSelector), selector.Selectors.ElementAt(1).GetType());
            Assert.AreEqual(typeof(ElementSelector), selector.Selectors.ElementAt(2).GetType());
        }

        [TestMethod]
        public void ComplexSelectors()
        {
            var statement = TryParseStatements("#id :hover, .class > #other-class, element .sub :hover {}");

            Assert.AreEqual(1, statement.Count);
            Assert.AreEqual(typeof(SelectorAndBlock), statement[0].GetType());

            var selector = ((SelectorAndBlock)statement[0]).Selector;
            Assert.AreEqual(typeof(MultiSelector), selector.GetType());

            var multi = selector as MultiSelector;
            Assert.AreEqual(3, multi.Selectors.Count());

            Assert.AreEqual(typeof(CompoundSelector), multi.Selectors.ElementAt(0).GetType());
            Assert.AreEqual(typeof(ChildSelector), multi.Selectors.ElementAt(1).GetType());
            Assert.AreEqual(typeof(CompoundSelector), multi.Selectors.ElementAt(2).GetType());

            var idPsuedo = multi.Selectors.ElementAt(0) as CompoundSelector;
            Assert.AreEqual(typeof(IdSelector), idPsuedo.Outer.GetType());
            Assert.AreEqual(typeof(PseudoClassSelector), idPsuedo.Inner.GetType());

            var child = multi.Selectors.ElementAt(1) as ChildSelector;
            Assert.AreEqual(typeof(ClassSelector), child.Parent.GetType());
            Assert.AreEqual(typeof(IdSelector), child.Child.GetType());

            var elClPsudo = multi.Selectors.ElementAt(2) as CompoundSelector;
            Assert.AreEqual(typeof(ElementSelector), elClPsudo.Outer.GetType());
            Assert.AreEqual(typeof(CompoundSelector), elClPsudo.Inner.GetType());
            Assert.AreEqual(typeof(ClassSelector), ((CompoundSelector)elClPsudo.Inner).Outer.GetType());
            Assert.AreEqual(typeof(PseudoClassSelector), ((CompoundSelector)elClPsudo.Inner).Inner.GetType());
        }

        [TestMethod]
        public void SiblingSelectors()
        {
            var simple = TryParseStatements(".class + #id {}");
            var complex = TryParseStatements(".class :hover + #hello .world {}");

            Assert.AreEqual(1, simple.Count);
            Assert.AreEqual(1, complex.Count);

            var simpleSel = (AdjacentSiblingSelector)((SelectorAndBlock)simple[0]).Selector;
            var complexSel = (AdjacentSiblingSelector)((SelectorAndBlock)complex[0]).Selector;

            Assert.AreEqual("class", ((ClassSelector)simpleSel.Older).Name);
            Assert.AreEqual("id", ((IdSelector)simpleSel.Younger).Name);

            var complexOlder = (CompoundSelector)complexSel.Older;
            var complexYounger = (CompoundSelector)complexSel.Younger;

            Assert.AreEqual("class", ((ClassSelector)complexOlder.Outer).Name);
            Assert.AreEqual(PseudoClassSelector.HoverSingleton, complexOlder.Inner);

            Assert.AreEqual("hello", ((IdSelector)complexYounger.Outer).Name);
            Assert.AreEqual("world", ((ClassSelector)complexYounger.Inner).Name);
        }

        [TestMethod]
        public void NameValueRule()
        {
            var single = TryParseStatements(".class { some-rule: value; }");
            var multi = TryParseStatements(@".class { first-rule: 1px; second-rule: ""some string""; }");
            
            var multiClass = 
                TryParseStatements
                (
                    @".class1 { first-rule: value1; second-rule: value2; }
                      .class2 { third-rule: other value of note; }"
                );

            Assert.AreEqual(1, single.Count);
            Assert.AreEqual(typeof(SelectorAndBlock), single[0].GetType());

            var sRules = ((SelectorAndBlock)single[0]).Properties;
            Assert.AreEqual(1, sRules.Count());

            Assert.AreEqual(typeof(NameValueProperty), sRules.ElementAt(0).GetType());

            var sRule = ((NameValueProperty)sRules.ElementAt(0));
            Assert.AreEqual("some-rule", sRule.Name);
            var sRuleVal = (StringValue)sRule.Value;
            Assert.AreEqual("value", sRuleVal.Value);

            Assert.AreEqual(1, multi.Count);
            Assert.AreEqual(typeof(SelectorAndBlock), multi[0].GetType());

            var mRules = ((SelectorAndBlock)multi[0]).Properties;
            Assert.AreEqual(2, mRules.Count());

            Assert.IsTrue(mRules.All(a => a.GetType() == typeof(NameValueProperty)));
            var firstRule = mRules.Cast<NameValueProperty>().Single(s => s.Name == "first-rule");
            Assert.AreEqual(typeof(NumberWithUnitValue), firstRule.Value.GetType());
            var firstValue = (NumberWithUnitValue)firstRule.Value;
            Assert.AreEqual(1, firstValue.Value);
            Assert.AreEqual(Unit.PX, firstValue.Unit);

            var secondRule = mRules.Cast<NameValueProperty>().Single(s => s.Name == "second-rule");
            Assert.AreEqual(typeof(QuotedStringValue), secondRule.Value.GetType());
            var secondValue = (QuotedStringValue)secondRule.Value;
            Assert.AreEqual("some string", secondValue.Value);

            Assert.AreEqual(2, multiClass.Count);
        }

        [TestMethod]
        public void MixinUse()
        {
            var simple = TryParseStatements(@".class { @mixin(); }");
            var withParams = TryParseStatements(@".class { @mixin(@a, 2, ""hello"", 3px); }");
            var withRules = TryParseStatements(@".class { @mixin(1); background-color: blue; }");
            var withWeirds = TryParseStatements(@".class { @mixin(url('hello'), #abc, #abcdef, hsl(1,2,3), rgb(1,2,3), rgba(1,2,3,4)); }");
            var withMulti = TryParseStatements(@".class { @mixin(a b c, 1 2 3); }");

            var simpleCss = ((SelectorAndBlock)simple[0]).Properties;
            Assert.AreEqual(typeof(MixinApplicationProperty), simpleCss.ElementAt(0).GetType());
            var mxSimpleCss = (MixinApplicationProperty)simpleCss.ElementAt(0);
            Assert.AreEqual("mixin", mxSimpleCss.Name);
            Assert.AreEqual(0, mxSimpleCss.Parameters.Count());

            var paramsCss = ((SelectorAndBlock)withParams[0]).Properties;
            Assert.AreEqual(typeof(MixinApplicationProperty), paramsCss.ElementAt(0).GetType());
            var mxParamsCss = (MixinApplicationProperty)paramsCss.ElementAt(0);
            Assert.AreEqual("mixin", mxParamsCss.Name);
            Assert.AreEqual(4, mxParamsCss.Parameters.Count());

            var rulesCss = ((SelectorAndBlock)withRules[0]).Properties;
            Assert.AreEqual(2, rulesCss.Count());
            Assert.IsTrue(rulesCss.OfType<NameValueProperty>().Single().Name == "background-color");
            Assert.IsTrue(rulesCss.OfType<MixinApplicationProperty>().Single().Parameters.Count() == 1);
        }

        [TestMethod]
        public void Nesting()
        {
            var simple = TryParseStatements(@".outer { .inner { rule: value; } }");

            Assert.AreEqual(1, simple.Count);
            
            var outerClass = (SelectorAndBlock)simple[0];
            Assert.AreEqual(1, outerClass.Properties.Count());

            var innerClass = (NestedBlockProperty)outerClass.Properties.ElementAt(0);
            Assert.AreEqual(typeof(ClassSelector), innerClass.Block.Selector.GetType());
            
            var innerSelector = (ClassSelector)innerClass.Block.Selector;
            Assert.AreEqual("inner", innerSelector.Name);

            Assert.AreEqual(1, innerClass.Block.Properties.Count());
            Assert.AreEqual(typeof(NameValueProperty), innerClass.Block.Properties.ElementAt(0).GetType());

            var innerRule = (NameValueProperty)innerClass.Block.Properties.ElementAt(0);
            Assert.AreEqual("rule", innerRule.Name);
            var value = (StringValue)innerRule.Value;
            Assert.AreEqual("value", value.Value);
        }

        [TestMethod]
        public void MixinDecl()
        {
            var trivial = TryParseStatements("@mixin() { }");
            var withParm = TryParseStatements("@m(@a, @b, @c) { }");
            var withAll = TryParseStatements("@mx(@param) { rule1: value1; rule2: value2; }");

            Assert.AreEqual(1, trivial.Count);
            Assert.AreEqual(typeof(MixinBlock), trivial[0].GetType());

            var tMixin = (MixinBlock)trivial[0];
            Assert.AreEqual("mixin", tMixin.Name);
            Assert.AreEqual(0, tMixin.Parameters.Count());
            Assert.AreEqual(0, tMixin.Properties.Count());

            Assert.AreEqual(1, withParm.Count);
            Assert.AreEqual(typeof(MixinBlock), withParm[0].GetType());

            var twParam = (MixinBlock)withParm[0];
            Assert.AreEqual("m", twParam.Name);
            Assert.AreEqual(3, twParam.Parameters.Count());
            Assert.AreEqual(0, twParam.Properties.Count());

            Assert.AreEqual(1, withAll.Count);
            Assert.AreEqual(typeof(MixinBlock), withAll[0].GetType());

            var tmAll = (MixinBlock)withAll[0];
            Assert.AreEqual("mx", tmAll.Name);
            Assert.AreEqual(1, tmAll.Parameters.Count());
            Assert.AreEqual(2, tmAll.Properties.Count());
        }

        private static string TryStripComments(string str)
        {
            var read = new StringBuilder();

            using (var stream = new CommentlessStream(new StringReader(str)))
            {
                int i;
                while ((i = stream.Read()) != -1)
                {
                    read.Append((char)i);
                }
            }

            var peek = new StringBuilder();
            using (var stream = new CommentlessStream(new StringReader(str)))
            {
                int i;
                while ((i = stream.Peek()) != -1)
                {
                    peek.Append((char)i);
                    stream.Read();
                }
            }

            using (var stream = new CommentlessStream(new StringReader(str)))
            {
                int i;
                while ((i = stream.Peek()) != -1)
                {
                    Assert.AreEqual(i, stream.Read());
                }

                Assert.AreEqual(-1, stream.Read());
            }

            Assert.AreEqual(read.ToString(), peek.ToString());

            return read.ToString();
        }

        [TestMethod]
        public void CommentStream()
        {
            var a = TryStripComments("hello/*blah*/world");
            var b = TryStripComments("hello//world");
            var c = TryStripComments(
                @"hello/*

                  */world"
                );
            var d = TryStripComments("hello //world\r\nworld");
            var e = TryStripComments("'hello //world'");
            var f = TryStripComments("\"do /* it */\"");

            Assert.AreEqual("helloworld", a);
            Assert.AreEqual("hello", b);
            Assert.AreEqual("helloworld", c);
            Assert.AreEqual("hello world", d);
            Assert.AreEqual("'hello //world'", e);
            Assert.AreEqual("\"do /* it */\"", f);
        }

        [TestMethod]
        public void CommentStreamPosition()
        {
            using (var stream = new CommentlessStream(new StringReader("he /*blah*/ wo")))
            {
                Assert.AreEqual(0, stream.Position);
                Assert.AreEqual('h', stream.Read());
                Assert.AreEqual(1, stream.Position);
                Assert.AreEqual('e', stream.Read());
                Assert.AreEqual(2, stream.Position);
                Assert.AreEqual(' ', stream.Read());
                Assert.AreEqual(3, stream.Position);
                Assert.AreEqual(' ', stream.Read());
                Assert.AreEqual(12, stream.Position);
                Assert.AreEqual('w', stream.Read());
                Assert.AreEqual(13, stream.Position);
                Assert.AreEqual('o', stream.Read());
                Assert.AreEqual(14, stream.Position);
                Assert.AreEqual(-1, stream.Peek());
            }

            using (var stream = new CommentlessStream(new StringReader("hello\n//blah\nworld")))
            {
                Assert.AreEqual(0, stream.Position);
                
                Assert.AreEqual('h', stream.Read());
                Assert.AreEqual(1, stream.Position);
                Assert.AreEqual('e', stream.Read());
                Assert.AreEqual(2, stream.Position);
                Assert.AreEqual('l', stream.Read());
                Assert.AreEqual(3, stream.Position);
                Assert.AreEqual('l', stream.Read());
                Assert.AreEqual(4, stream.Position);
                Assert.AreEqual('o', stream.Read());
                Assert.AreEqual(5, stream.Position);
                Assert.AreEqual('\n', stream.Read());
                Assert.AreEqual(6, stream.Position);

                Assert.AreEqual('w', stream.Read());
                Assert.AreEqual(14, stream.Position);
                Assert.AreEqual('o', stream.Read());
                Assert.AreEqual(15, stream.Position);
                Assert.AreEqual('r', stream.Read());
                Assert.AreEqual(16, stream.Position);
                Assert.AreEqual('l', stream.Read());
                Assert.AreEqual(17, stream.Position);
                Assert.AreEqual('d', stream.Read());
                
                Assert.AreEqual(-1, stream.Peek());
            }
        }

        [TestMethod]
        public void StripComments()
        {
            var simple1 = TryParseStatements("/* Comment */ .hello { rule: value; }");
            var simple2 = TryParseStatements(".hello { rule: value; } // No More");

            var compound1 =
                TryParseStatements
                (
                    @".hello {
                        rule: value; // This does something
                      }"
                );

            var compound2 =
                TryParseStatements
                (
                    @".hello {
                        /*rule: value; // This does something */
                        other-rule: other-value;
                      }"
                );

            var strings1 =
                TryParseStatements
                (
                    @".hello { world: 'something//else'; }"
                );

            var strings2 =
                TryParseStatements
                (
                    @".hello { world: 'something//else'; }
                      /* .class { not: appearing; } */"
                );

            Assert.AreEqual(1, simple1.Count);
            Assert.AreEqual(1, simple2.Count);
            Assert.AreEqual(1, compound1.Count);
            Assert.AreEqual(1, compound2.Count);
            Assert.AreEqual(1, strings1.Count);
            Assert.AreEqual(1, strings2.Count);
        }

        [TestMethod]
        public void MixinParameterOptions()
        {
            var optional = TryParseStatements("@optional(@a, @b?) { }");
            var withDefault = TryParseStatements("@defaults(@a, @b=2) { }");
            var combo = TryParseStatements("@both(@a, @c=black, @b?) { }");

            Assert.AreEqual(1, optional.Count);
            Assert.AreEqual(1, withDefault.Count);
            Assert.AreEqual(1, combo.Count);

            var optMx = (MixinBlock)optional[0];
            var defMx = (MixinBlock)withDefault[0];
            var comMx = (MixinBlock)combo[0];

            Assert.AreEqual(2, optMx.Parameters.Count());
            Assert.AreEqual("a", optMx.Parameters.ElementAt(0).Name);
            Assert.IsTrue(optMx.Parameters.ElementAt(0).DefaultValue is NotFoundValue);
            Assert.AreEqual("b", optMx.Parameters.ElementAt(1).Name);
            Assert.AreEqual(ExcludeFromOutputValue.Singleton, optMx.Parameters.ElementAt(1).DefaultValue);

            Assert.AreEqual(2, defMx.Parameters.Count());
            Assert.AreEqual("a", defMx.Parameters.ElementAt(0).Name);
            Assert.IsTrue(defMx.Parameters.ElementAt(0).DefaultValue is NotFoundValue);
            Assert.AreEqual("b", defMx.Parameters.ElementAt(1).Name);
            Assert.AreEqual(2, ((NumberValue)defMx.Parameters.ElementAt(1).DefaultValue).Value);

            Assert.AreEqual(3, comMx.Parameters.Count());
            Assert.AreEqual("a", comMx.Parameters.ElementAt(0).Name);
            Assert.IsTrue(comMx.Parameters.ElementAt(0).DefaultValue is NotFoundValue);
            Assert.AreEqual("b", comMx.Parameters.ElementAt(2).Name);
            Assert.AreEqual(ExcludeFromOutputValue.Singleton, comMx.Parameters.ElementAt(2).DefaultValue);
            Assert.AreEqual("c", comMx.Parameters.ElementAt(1).Name);
            Assert.AreEqual(NamedColor.black, ((NamedColorValue)comMx.Parameters.ElementAt(1).DefaultValue).Color);
        }

        [TestMethod]
        public void BadParameterOrder()
        {
            TryParseStatements("@mixin(@a?, @b) {}");
            var e1 = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, e1.Count);
            Assert.AreEqual("Optional mixin parameters must appear at the end of a parameter list, and those with default values after those without.", e1[0].Message);

            
            TryParseStatements("@mixin(@a=#fff, @b) {}");
            var e2 = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, e2.Count);
            Assert.AreEqual("Optional mixin parameters must appear at the end of a parameter list, and those with default values after those without.", e2[0].Message);

            TryParseStatements("@mixin(@a?, @b=#fff) {}");
            var e3 = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, e3.Count);
            Assert.AreEqual("Optional mixin parameters must appear at the end of a parameter list, and those with default values after those without.", e3[0].Message);

            TryParseStatements("@mixin(@c, @a?, @b) {}");
            var e4 = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, e4.Count);
            Assert.AreEqual("Optional mixin parameters must appear at the end of a parameter list, and those with default values after those without.", e4[0].Message);

            
            TryParseStatements("@mixin(@c, @a=#fff, @b) {}");
            var e5 = Current.GetErrors(ErrorType.Parser);
            Assert.AreEqual(1, e5.Count);
            Assert.AreEqual("Optional mixin parameters must appear at the end of a parameter list, and those with default values after those without.", e5[0].Message);
        }

        [TestMethod]
        public void IncludeRules()
        {
            var one = TryParseStatements(".class { @(.other); }");
            var multi = TryParseStatements(".class { @(em, #id, .class, parent > child); }");

            Assert.AreEqual(1, one.Count);
            Assert.AreEqual(1, multi.Count);

            var oneCss = (SelectorAndBlock)one[0];
            var multiCss = (SelectorAndBlock)multi[0];

            Assert.AreEqual(1, oneCss.Properties.Count());
            Assert.AreEqual(1, multiCss.Properties.Count());

            var oneInclude = (IncludeSelectorProperty)oneCss.Properties.ElementAt(0);
            var multiInclude = (IncludeSelectorProperty)multiCss.Properties.ElementAt(0);

            Assert.AreEqual(typeof(ClassSelector), oneInclude.Selector.GetType());
            Assert.AreEqual(typeof(MultiSelector), multiInclude.Selector.GetType());

            var multiParts = (MultiSelector)multiInclude.Selector;
            Assert.AreEqual(4, multiParts.Selectors.Count());
            Assert.AreEqual(typeof(ElementSelector), multiParts.Selectors.ElementAt(0).GetType());
            Assert.AreEqual(typeof(IdSelector), multiParts.Selectors.ElementAt(1).GetType());
            Assert.AreEqual(typeof(ClassSelector), multiParts.Selectors.ElementAt(2).GetType());
            Assert.AreEqual(typeof(ChildSelector), multiParts.Selectors.ElementAt(3).GetType());
        }

        [TestMethod]
        public void SubclassConcat()
        {
            var simple =
                TryParseStatements(
                    @".class {
                        &:hover {
                            a:b;
                        }
                      }"
                );

            Assert.AreEqual(1, simple.Count);

            var rules = (SelectorAndBlock)simple[0];
            Assert.AreEqual(1, rules.Properties.Count());

            var subclass = (NestedBlockProperty)rules.Properties.ElementAt(0);
            Assert.AreEqual(typeof(ConcatWithParentSelector), subclass.Block.Selector.GetType());

            var sel = (ConcatWithParentSelector)subclass.Block.Selector;
            Assert.AreEqual(PseudoClassSelector.HoverSingleton, sel.Selector);
        }

        [TestMethod]
        public void Sprites()
        {
            var decl =
                TryParseStatements(
                    @"@sprite('abc.gif') {
                         @hello = 'world.gif';
                         @other = 'guy.jpg';
                      }"
                );

            Assert.AreEqual(1, decl.Count);

            var spriteDecl = decl[0];
            Assert.AreEqual(typeof(SpriteBlock), spriteDecl.GetType());

            var sprites = (SpriteBlock)spriteDecl;
            Assert.AreEqual(2, sprites.Sprites.Count());
            Assert.AreEqual("abc.gif", sprites.OutputFile.Value);

            Assert.IsTrue(sprites.Sprites.Any(a => a.MixinName == "hello" && a.SpriteFilePath.Value == "world.gif"));
            Assert.IsTrue(sprites.Sprites.Any(a => a.MixinName == "other" && a.SpriteFilePath.Value == "guy.jpg"));
        }

        [TestMethod]
        public void NamedParameters()
        {
            var simple = 
                TryParseStatements(
                    @"@mixin(@a, @b?) { a: @a; b: @b; }

                      img {
                        @mixin(@a = 123, @b = #abc);
                      }"
                );

            Assert.AreEqual(2, simple.Count);
            Assert.AreEqual(typeof(MixinBlock), simple[0].GetType());
            Assert.AreEqual(typeof(SelectorAndBlock), simple[1].GetType());

            var img = (SelectorAndBlock)simple[1];
            Assert.AreEqual(1, img.Properties.Count());

            var mixinInv = (MixinApplicationProperty)img.Properties.ElementAt(0);
            Assert.AreEqual(2, mixinInv.Parameters.Count());
            Assert.AreEqual("a", mixinInv.Parameters.ElementAt(0).Name);
            Assert.AreEqual(123m, ((NumberValue)mixinInv.Parameters.ElementAt(0).Value).Value);
            Assert.AreEqual("b", mixinInv.Parameters.ElementAt(1).Name);
            Assert.AreEqual(typeof(HexTripleColorValue), mixinInv.Parameters.ElementAt(1).Value.GetType());
        }

        [TestMethod]
        public void PseudoClasses()
        {
            var statements =
                TryParseStatements(
                    @":not(#id){a:b;}
                      :checked{a:b;}
                      :disabled{a:b;}
                      :nth-last-child(1){a:b;}
                      :nth-of-type(1){a:b;}
                      :nth-last-of-type(1){a:b;}
                      :first-of-type{a:b;}
                      :last-of-type{a:b;}
                      :only-child{a:b;}
                      :only-of-type{a:b;}
                      :root{a:b;}
                      :target{a:b;}
                      :enabled{a:b;}
                      :default{a:b;}
                      :valid{a:b;}
                      :invalid{a:b;}
                      :in-range{a:b;}
                      :out-of-range{a:b;}
                      :required{a:b;}
                      :optional{a:b;}
                      :read-only{a:b;}
                      :read-write{a:b;}"
                );

            Assert.AreEqual(22, statements.Count);

            var classes = statements.Cast<SelectorAndBlock>().ToList();
            Assert.AreEqual(typeof(IdSelector), ((NotPseudoClassSelector)classes[0].Selector).Selector.GetType());
            Assert.AreEqual(PseudoClassSelector.CheckedSingleton, classes[1].Selector);
            Assert.AreEqual(PseudoClassSelector.DisabledSingleton, classes[2].Selector);
            Assert.AreEqual(1, ((NthLastChildPseudoClassSelector)classes[3].Selector).LastChild.B);
            Assert.AreEqual(1, ((NthOfTypePseudoClassSelector)classes[4].Selector).N.B);
            Assert.AreEqual(1, ((NthLastOfTypePseudoClassSelector)classes[5].Selector).N.B);
            Assert.AreEqual(PseudoClassSelector.FirstOfTypeSingleton, classes[6].Selector);
            Assert.AreEqual(PseudoClassSelector.LastOfTypeSingleton, classes[7].Selector);
            Assert.AreEqual(PseudoClassSelector.OnlyChildSingleton, classes[8].Selector);
            Assert.AreEqual(PseudoClassSelector.OnlyOfTypeSingleton, classes[9].Selector);
            Assert.AreEqual(PseudoClassSelector.RootSingleton, classes[10].Selector);
            Assert.AreEqual(PseudoClassSelector.TargetSingleton, classes[11].Selector);
            Assert.AreEqual(PseudoClassSelector.EnabledSingleton, classes[12].Selector);
            Assert.AreEqual(PseudoClassSelector.DefaultSingleton, classes[13].Selector);
            Assert.AreEqual(PseudoClassSelector.ValidSingleton, classes[14].Selector);
            Assert.AreEqual(PseudoClassSelector.InvalidSingleton, classes[15].Selector);
            Assert.AreEqual(PseudoClassSelector.InRangeSingleton, classes[16].Selector);
            Assert.AreEqual(PseudoClassSelector.OutOfRangeSingleton, classes[17].Selector);
            Assert.AreEqual(PseudoClassSelector.RequiredSingleton, classes[18].Selector);
            Assert.AreEqual(PseudoClassSelector.OptionalSingleton, classes[19].Selector);
            Assert.AreEqual(PseudoClassSelector.ReadOnlySingleton, classes[20].Selector);
            Assert.AreEqual(PseudoClassSelector.ReadWriteSingleton, classes[21].Selector);
        }

        [TestMethod]
        public void MultiLineSelector()
        {
            var statements =
                TryParseStatements(
                    @"html, body,
                      mark, audio
                      { name: value; }"
                );

            Assert.AreEqual(1, statements.Count);

            var block = (SelectorAndBlock)statements[0];
            var selector = (MultiSelector)block.Selector;
            Assert.AreEqual(4, selector.Selectors.Count());
            Assert.IsTrue(selector.Selectors.Cast<ElementSelector>().Any(a => a.Name == "html"));
            Assert.IsTrue(selector.Selectors.Cast<ElementSelector>().Any(a => a.Name == "body"));
            Assert.IsTrue(selector.Selectors.Cast<ElementSelector>().Any(a => a.Name == "mark"));
            Assert.IsTrue(selector.Selectors.Cast<ElementSelector>().Any(a => a.Name == "audio"));

            Assert.AreEqual(1, block.Properties.Count());
        }

        [TestMethod]
        public void OptionalMixinRule()
        {
            var a =
                TryParseStatements(
                    @"img{
                        @may-not-exist()?;
                      }"
                );

            var b =
                TryParseStatements(
                    @"img{
                        @should-override()!;
                      }"
                );

            var c =
                TryParseStatements(
                    @"img{
                        @if-exists()!?;
                      }"
                );

            Assert.AreEqual(1, a.Count);
            var aRules = (SelectorAndBlock)a[0];
            Assert.AreEqual(1, aRules.Properties.Count());
            var aRule = aRules.Properties.ElementAt(0);
            Assert.AreEqual(typeof(MixinApplicationProperty), aRule.GetType());
            var optional = (MixinApplicationProperty)aRule;
            Assert.AreEqual("may-not-exist", optional.Name);
            Assert.IsTrue(optional.IsOptional);

            Assert.AreEqual(1, b.Count);
            var bRules = (SelectorAndBlock)b[0];
            Assert.AreEqual(1, bRules.Properties.Count());
            var bRule = bRules.Properties.ElementAt(0);
            Assert.AreEqual(typeof(MixinApplicationProperty), bRule.GetType());
            var overrides = (MixinApplicationProperty)bRule;
            Assert.AreEqual("should-override", overrides.Name);
            Assert.IsTrue(overrides.DoesOverride);

            Assert.AreEqual(1, c.Count);
            var cRules = (SelectorAndBlock)c[0];
            Assert.AreEqual(1, cRules.Properties.Count());
            var cRule = cRules.Properties.ElementAt(0);
            Assert.AreEqual(typeof(MixinApplicationProperty), cRule.GetType());
            var both = (MixinApplicationProperty)cRule;
            Assert.AreEqual("if-exists", both.Name);
            Assert.IsTrue(both.DoesOverride);
            Assert.IsTrue(both.IsOptional);
        }

        [TestMethod]
        public void LocalVariables()
        {
            var statements =
                TryParseStatements(
                    @"img {
                        @x = 5px;
                        color: red;
                      }"
                );

            Assert.AreEqual(1, statements.Count);
            var block = (SelectorAndBlock)statements[0];
            var rules = block.Properties;
            Assert.AreEqual(2, rules.Count());
            
            var var = (VariableProperty)rules.ElementAt(0);
            Assert.AreEqual("x", var.Name);
            Assert.AreEqual(5m, ((NumberWithUnitValue)var.Value).Value);

            var color = (NameValueProperty)rules.ElementAt(1);
            Assert.AreEqual("color", color.Name);
            Assert.AreEqual(NamedColor.red, ((NamedColorValue)color.Value).Color);
        }

        [TestMethod]
        public void MediaRule()
        {
            var statements =
                TryParseStatements(
                    @"@media tv {
                         @x = 5;
                          .class {
                              rule: value;
                              p {
                                sub-rule: value;
                              }
                          }
                      }"
                );

            Assert.AreEqual(1, statements.Count);
            Assert.AreEqual(typeof(MediaBlock), statements[0].GetType());

            var subStatements = ((MediaBlock)statements[0]).Blocks;
            Assert.AreEqual(2, subStatements.Count());
            var var = (MoreVariable)subStatements.ElementAt(0);
            Assert.AreEqual("x", var.Name);
            var css = (SelectorAndBlock)subStatements.ElementAt(1);
            Assert.AreEqual(".class", css.Selector.ToString());

            var rules = css.Properties;
            Assert.AreEqual(2, rules.Count());
            var rule = (NameValueProperty)rules.ElementAt(0);
            Assert.AreEqual("rule", rule.Name);

            var nestedBlock = (NestedBlockProperty)rules.ElementAt(1);
            Assert.AreEqual("p", nestedBlock.Block.Selector.ToString());
            Assert.AreEqual(1, nestedBlock.Block.Properties.Count());
            var subRule = (NameValueProperty)nestedBlock.Block.Properties.ElementAt(0);
            Assert.AreEqual("sub-rule", subRule.Name);
        }

        [TestMethod]
        public void UsingWithMedia()
        {
            var statements =
                TryParseStatements(
                    @"@using 'hello-world.txt' tv, print;
                      img {
                        rule: value;
                      }"
                );

            Assert.AreEqual(2, statements.Count);
            var @using = statements[0];
            Assert.AreEqual(typeof(Using), @using.GetType());
            var asUsing = (Using)@using;
            Assert.AreEqual("hello-world.txt", asUsing.RawPath);

            var media = asUsing.MediaQuery;
            var mediaComma = media as CommaDelimitedMedia;
            Assert.IsNotNull(mediaComma);
            Assert.AreEqual(2, mediaComma.Clauses.Count());
            var tv = mediaComma.Clauses.ElementAt(0) as MediaType;
            var print = mediaComma.Clauses.ElementAt(1) as MediaType;
            Assert.IsNotNull(tv);
            Assert.IsNotNull(print);
            Assert.AreEqual(Media.tv, tv.Type);
            Assert.AreEqual(Media.print, print.Type);
        }

        [TestMethod]
        public void Animations()
        {
            var statements = 
                TryParseStatements(
                    @"@keyframes my-animation {
                        @x = 5;
                        from { a:b; }
                        to { c:d; }
                        15% { e: f; }
                        20%, 30% { g: h; i:j; }
                      }"
                );

            Assert.AreEqual(1, statements.Count);
            Assert.AreEqual(typeof(KeyFramesBlock), statements[0].GetType());

            var decl = (KeyFramesBlock)statements[0];
            Assert.AreEqual("", decl.Prefix);
            Assert.AreEqual("my-animation", decl.Name);
            Assert.AreEqual(4, decl.Frames.Count());

            Assert.AreEqual(1, decl.Variables.Count());
            Assert.AreEqual("x", decl.Variables.ElementAt(0).Name);
            Assert.AreEqual(5m, ((NumberValue)decl.Variables.ElementAt(0).Value).Value);

            var from = decl.Frames.ElementAt(0);
            Assert.AreEqual(1, from.Percentages.Count());
            Assert.AreEqual(0m, from.Percentages.ElementAt(0));
            Assert.AreEqual(1, from.Properties.Count());
            Assert.AreEqual("a", ((NameValueProperty)from.Properties.ElementAt(0)).Name);

            var to = decl.Frames.ElementAt(1);
            Assert.AreEqual(1, to.Percentages.Count());
            Assert.AreEqual(100m, to.Percentages.ElementAt(0));
            Assert.AreEqual(1, to.Properties.Count());
            Assert.AreEqual("c", ((NameValueProperty)to.Properties.ElementAt(0)).Name);

            var percent = decl.Frames.ElementAt(2);
            Assert.AreEqual(1, percent.Percentages.Count());
            Assert.AreEqual(15m, percent.Percentages.ElementAt(0));
            Assert.AreEqual(1, percent.Properties.Count());
            Assert.AreEqual("e", ((NameValueProperty)percent.Properties.ElementAt(0)).Name);

            var combo = decl.Frames.ElementAt(3);
            Assert.AreEqual(2, combo.Percentages.Count());
            Assert.AreEqual(20m, combo.Percentages.ElementAt(0));
            Assert.AreEqual(30m, combo.Percentages.ElementAt(1));
            Assert.AreEqual(2, combo.Properties.Count());
            Assert.AreEqual("g", ((NameValueProperty)combo.Properties.ElementAt(0)).Name);
            Assert.AreEqual("i", ((NameValueProperty)combo.Properties.ElementAt(1)).Name);
        }

        [TestMethod]
        public void FontFace()
        {
            var statements =
                TryParseStatements(
                    @"@font-face {
                        font-family: Delicious; 
                        src: local('Blah blah'), url('Delicious-Roman.otf') format('OTF');
                      }"
                );

            Assert.AreEqual(1, statements.Count);
            Assert.AreEqual(typeof(FontFaceBlock), statements[0].GetType());
            
            var ffDecl = (FontFaceBlock)statements[0];
            Assert.AreEqual(2, ffDecl.Properties.Count());
            var rules = ffDecl.Properties.Cast<NameValueProperty>();
            Assert.IsTrue(rules.Any(a => a.Name == "font-family"));
            Assert.IsTrue(rules.Any(a => a.Name == "src"));

            Assert.IsNotNull(rules.SingleOrDefault(a => a.Name == "font-family" && (a.Value is StringValue)));
            Assert.IsNotNull(rules.SingleOrDefault(a => a.Name == "src" && (a.Value is CommaDelimittedValue)));
            Assert.IsTrue(((CommaDelimittedValue)rules.Single(w => w.Name == "src").Value).Values.All(a => a is LocalValue || a is CompoundValue));

            var urlAndFormat = (CompoundValue)((CommaDelimittedValue)rules.Single(a => a.Name == "src").Value).Values.ElementAt(1);
            Assert.AreEqual(typeof(UrlValue), urlAndFormat.Values.ElementAt(0).GetType());
            Assert.AreEqual(typeof(FormatValue), urlAndFormat.Values.ElementAt(1).GetType());

            var format = (FormatValue)urlAndFormat.Values.ElementAt(1);
            Assert.AreEqual("OTF", ((QuotedStringValue)format.Value).Value);
        }

        [TestMethod]
        public void SimpleMediaQuery()
        {
            Current.SetContext(new Context(new FileCache()));

            var query = MediaQueryParser.Parse("not screen and (color), print and (color)", Position.NoSite);

            var comma = query as CommaDelimitedMedia;
            Assert.IsNotNull(comma);
            Assert.AreEqual(2, comma.Clauses.Count());
            
            var not = comma.Clauses.ElementAt(0) as NotMedia;
            Assert.IsNotNull(not);
            var notAnd = not.Clause as AndMedia;
            Assert.IsNotNull(notAnd);
            var notLeft = notAnd.LeftHand as MediaType;
            var notRight = notAnd.RightHand as FeatureMedia;
            Assert.IsNotNull(notLeft);
            Assert.IsNotNull(notRight);
            Assert.AreEqual(Media.screen, notLeft.Type);
            Assert.AreEqual("color", notRight.Feature);

            var print = comma.Clauses.ElementAt(1) as AndMedia;
            Assert.IsNotNull(print);
            var printLeft = print.LeftHand as MediaType;
            var printRight = print.RightHand as FeatureMedia;
            Assert.IsNotNull(printLeft);
            Assert.IsNotNull(printRight);
            Assert.AreEqual(Media.print, printLeft.Type);
            Assert.AreEqual("color", printRight.Feature);
        }

        [TestMethod]
        public void MinMaxMediaQuery()
        {
            Current.SetContext(new Context(new FileCache()));

            var query = MediaQueryParser.Parse("only screen and (min-color: @a * @b) and (max-width: 400px) and (grid: progressive)", Position.NoSite);

            var only = query as OnlyMedia;
            Assert.IsNotNull(only);
            var and = only.Clause as AndMedia;
            Assert.IsNotNull(and);

            var and2 = and.LeftHand as AndMedia;
            var grid = and.RightHand as EqualFeatureMedia;
            Assert.IsNotNull(and2);
            Assert.IsNotNull(grid);
            Assert.AreEqual("grid", grid.Feature);
            var gridValue = grid.EqualsValue as StringValue;
            Assert.IsNotNull(gridValue);
            Assert.AreEqual("progressive", gridValue.Value);

            var and3 = and2.LeftHand as AndMedia;
            var maxWidth = and2.RightHand as MaxFeatureMedia;
            Assert.IsNotNull(and3);
            Assert.IsNotNull(maxWidth);
            Assert.AreEqual("width", maxWidth.Feature);
            var widthValue = maxWidth.Max as NumberWithUnitValue;
            Assert.AreEqual(400m, widthValue.Value);
            Assert.AreEqual(Unit.PX, widthValue.Unit);

            var screen = and3.LeftHand as MediaType;
            var minColor = and3.RightHand as MinFeatureMedia;
            Assert.IsNotNull(screen);
            Assert.IsNotNull(minColor);
            Assert.AreEqual(Media.screen, screen.Type);
            Assert.AreEqual("color", minColor.Feature);
            var colorValue = minColor.Min as MathValue;
            Assert.IsNotNull(colorValue);
            var a = colorValue.LeftHand as FuncValue;
            var b = colorValue.RightHand as FuncValue;
            Assert.IsNotNull(a);
            Assert.IsNotNull(b);
            Assert.AreEqual("a", a.Name);
            Assert.AreEqual("b", b.Name);
        }

        [TestMethod]
        public void ResetBlock()
        {
            var blocks =
                TryParseStatements(
                    @"@reset {
                        @x = 5;
                        .class {
                          rule: value;
                          :hover {
                            inner: @x;
                          }
                        }
                      }

                      #id {
                        a: b;
                      }"
                );

            Assert.AreEqual(2, blocks.Count);
            var reset = blocks[0] as ResetBlock;
            Assert.IsNotNull(reset);
            var id = blocks[1] as SelectorAndBlock;
            Assert.IsNotNull(id);
            
            Assert.AreEqual(1, id.Properties.Count());

            Assert.AreEqual(2, reset.Blocks.Count());
            var var = reset.Blocks.ElementAt(0) as MoreVariable;
            Assert.IsNotNull(var);
            Assert.AreEqual("x", var.Name);
            var block = reset.Blocks.ElementAt(1) as SelectorAndBlock;
            Assert.IsNotNull(block);
        }

        [TestMethod]
        public void ResetRule()
        {
            var statements =
                TryParseStatements(
                    @"id {
                        a:b;
                        @reset(.class);
                        @reset();

                        #sub {
                            @reset();
                        }

                        a:hover {
                            @reset();
                            a:b;
                        }
                      }"
                );

            Assert.AreEqual(1, statements.Count);
            var block = statements.ElementAt(0) as SelectorAndBlock;
            Assert.IsNotNull(block);
            Assert.AreEqual(5, block.Properties.Count());
            var rule = block.Properties.ElementAt(0) as NameValueProperty;
            var reset = block.Properties.ElementAt(1) as ResetProperty;
            var resetSelf = block.Properties.ElementAt(2) as ResetSelfProperty;
            var sub = block.Properties.ElementAt(3) as NestedBlockProperty;
            var hoverSub = block.Properties.ElementAt(4) as NestedBlockProperty;
            
            Assert.IsNotNull(rule);

            Assert.IsNotNull(reset);
            var resetSel = reset.Selector;
            Assert.AreEqual(typeof(ClassSelector), resetSel.GetType());

            Assert.IsNotNull(resetSelf);
            var selfSel = resetSelf.EffectiveSelector as ElementSelector;
            Assert.IsNotNull(selfSel);

            Assert.IsNotNull(sub);
            Assert.AreEqual(1, sub.Block.Properties.Count());
            var subSelf = sub.Block.Properties.ElementAt(0) as ResetSelfProperty;
            Assert.IsNotNull(subSelf);
            var subSelfSel = subSelf.EffectiveSelector as IdSelector;
            Assert.IsNotNull(subSelfSel);

            Assert.IsNotNull(hoverSub);
            Assert.AreEqual(2, hoverSub.Block.Properties.Count());
            var hoverSelf = hoverSub.Block.Properties.ElementAt(0) as ResetSelfProperty;
            Assert.IsNotNull(hoverSub);
            Assert.AreEqual(typeof(ConcatSelector), hoverSelf.EffectiveSelector.GetType());
            var hoverProp = hoverSub.Block.Properties.ElementAt(1) as NameValueProperty;
            Assert.IsNotNull(hoverProp);
        }

        [TestMethod]
        public void StreamPushBack()
        {
            using (var stream = new ParserStream(new StringReader("")))
            {
                var buffer = new StringBuilder();

                stream.PushBack("hello world!");
                stream.ScanUntil(buffer, '!');
                Assert.AreEqual("hello world", buffer.ToString());
                Assert.IsFalse(stream.HasMore());

                buffer.Clear();

                stream.PushBack("world!");
                stream.PushBack("hello ");
                stream.ScanUntil(buffer, '!');
                Assert.AreEqual("hello world", buffer.ToString());
                Assert.IsFalse(stream.HasMore());
            }
        }

        [TestMethod]
        public void LastSemiColonOptional()
        {
            var a =
                @"#a {
                    a:a
                  }
                  #b {
                    b:b;
                    c:c
                  }";

            var statements = TryParseStatements(a);
            Assert.AreEqual(2, statements.Count);

            var aBlock = (SelectorAndBlock)statements[0];
            Assert.AreEqual("a", ((IdSelector)aBlock.Selector).Name);
            Assert.AreEqual(1, aBlock.Properties.Count());

            var bBlock = (SelectorAndBlock)statements[1];
            Assert.AreEqual("b", ((IdSelector)bBlock.Selector).Name);
            Assert.AreEqual(2, bBlock.Properties.Count());
        }

        [TestMethod]
        public void Escaping()
        {
            var a = @"\&b { a:b }";
            var aStatements = TryParseStatements(a);
            Assert.AreEqual(1, aStatements.Count);
            var aBlock = (SelectorAndBlock)aStatements[0];
            Assert.AreEqual(@"\&b", ((ElementSelector)aBlock.Selector).Name);

            var b = @"hello\&b { a:b }";
            var bStatements = TryParseStatements(b);
            Assert.AreEqual(1, bStatements.Count);
            var bBlock = (SelectorAndBlock)bStatements[0];
            Assert.AreEqual(@"hello\&b", ((ElementSelector)bBlock.Selector).Name);

            var c = @".hello\.world { a:b }";
            var cStatements = TryParseStatements(c);
            Assert.AreEqual(1, cStatements.Count);
            var cBlock = (SelectorAndBlock)cStatements[0];
            Assert.AreEqual(@"hello\.world", ((ClassSelector)cBlock.Selector).Name);

            var d = @"#hello\02b world { a:b }";
            var dStatements = TryParseStatements(d);
            Assert.AreEqual(1, dStatements.Count);
            var dBlock = (SelectorAndBlock)dStatements[0];
            Assert.AreEqual(@"hello\02b world", ((IdSelector)dBlock.Selector).Name);

            var e = @"#fizz\{buzz { a:b }";
            var eStatements = TryParseStatements(e);
            Assert.AreEqual(1, eStatements.Count);
            var eBlock = (SelectorAndBlock)eStatements[0];
            Assert.AreEqual(@"fizz\{buzz", ((IdSelector)eBlock.Selector).Name);

            var f = @"hello { a: b\; and more; }";
            var fStatements = TryParseStatements(f);
            Assert.AreEqual(1, fStatements.Count);
            var fBlock = (SelectorAndBlock)fStatements[0];
            Assert.AreEqual(@"hello", ((ElementSelector)fBlock.Selector).Name);
            var fRule = (NameValueProperty)fBlock.Properties.ElementAt(0);
            Assert.AreEqual("a", fRule.Name);
            Assert.AreEqual(@"b\; and more", fRule.Value.ToString());

            var g = @"h { a: "" \"" "" }";
            var gStatements = TryParseStatements(g);
            Assert.AreEqual(1, gStatements.Count);
            var gBlock = (SelectorAndBlock)gStatements[0];
            Assert.AreEqual(@"h", ((ElementSelector)gBlock.Selector).Name);
            var gRule = (NameValueProperty)gBlock.Properties.ElementAt(0);
            Assert.AreEqual("a", gRule.Name);
            Assert.AreEqual(@"' \"" '", gRule.Value.ToString());
        }

        [TestMethod]
        public void Calc()
        {
            var aStatements =
                TryParseStatements(
                    @"a { 
                        b: cycle(foo);
                        c: hello world, cycle(bar), even more;
                        d: cycle(fizz, buzz, whatever);
                      }"
                );
            Assert.AreEqual(1, aStatements.Count);
            var aBlock = (SelectorAndBlock)aStatements[0];
            var props = aBlock.Properties.Cast<NameValueProperty>();
            Assert.AreEqual(3, props.Count());
            var aRule1 = props.Single(a => a.Name == "b").Value;
            var aRule2 = props.Single(a => a.Name == "c").Value;
            var aRule3 = props.Single(a => a.Name == "d").Value;
            
            var cycle1 = aRule1 as CycleValue;
            Assert.IsNotNull(cycle1);
            Assert.AreEqual(1, cycle1.Values.Count());
            Assert.AreEqual("foo", cycle1.Values.Single().ToString());

            var comma2 = aRule2 as CommaDelimittedValue;
            Assert.IsNotNull(comma2);
            Assert.AreEqual(3, comma2.Values.Count());
            var cycle2 = comma2.Values.ElementAt(1) as CycleValue;
            Assert.IsNotNull(cycle2);
            Assert.AreEqual(1, cycle2.Values.Count());
            Assert.AreEqual("bar", cycle2.Values.Single().ToString());

            var cycle3 = aRule3 as CycleValue;
            Assert.IsNotNull(cycle3);
            Assert.AreEqual(3, cycle3.Values.Count());
            Assert.AreEqual("fizz", cycle3.Values.ElementAt(0).ToString());
            Assert.AreEqual("buzz", cycle3.Values.ElementAt(1).ToString());
            Assert.AreEqual("whatever", cycle3.Values.ElementAt(2).ToString());
        }

        [TestMethod]
        public void InnerMediaProperty()
        {
            var aStatements =
                TryParseStatements(
                    @"#hello {
                        a: b;
                        @media only tv {
                          c: d;
                          .world {
                            e: f;
                          }
                        }
                      }"
                );
            Assert.AreEqual(1, aStatements.Count);
            
            var selBlock = (SelectorAndBlock)aStatements[0];
            Assert.AreEqual("hello", ((IdSelector)selBlock.Selector).Name);

            Assert.AreEqual(2, selBlock.Properties.Count());
            var nameVal = (NameValueProperty)selBlock.Properties.ElementAt(0);
            Assert.AreEqual("a", nameVal.Name);
            Assert.AreEqual("b", nameVal.Value.ToString());

            var innerMedia = (InnerMediaProperty)selBlock.Properties.ElementAt(1);
            Assert.AreEqual(2, innerMedia.Block.Properties.Count());
            var nameVal2 = (NameValueProperty)innerMedia.Block.Properties.ElementAt(0);
            Assert.AreEqual("c", nameVal2.Name);
            Assert.AreEqual("d", nameVal2.Value.ToString());

            var nested = (NestedBlockProperty)innerMedia.Block.Properties.ElementAt(1);
            Assert.AreEqual(".world", nested.Block.Selector.ToString());
        }
    }
}