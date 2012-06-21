using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MoreInternals.Model;

namespace MoreTests
{
    [TestClass]
    public class ValueTests
    {
        [TestMethod]
        public void Simple()
        {
            var hex3 = Value.Parse("#f02");
            var hex6 = Value.Parse("#f1e2d3");
            var black = Value.Parse("black");
            var number = Value.Parse("5");
            var decimal1 = Value.Parse("0.5");
            var decimal2 = Value.Parse(".3");
            var px = Value.Parse("10px");
            var @string = Value.Parse("hello");
            var quotedString = Value.Parse(@"""indeed""");

            var url1 = Value.Parse("url(/hello/world1.png)");
            var url2 = Value.Parse("url('/hello/world2.png')");
            var url3 = Value.Parse(@"url(""/hello/world3.png"")");

            var rgb1 = Value.Parse("rgb(255, 100, 50)");
            var rgb2 = Value.Parse("rgb(10%, 20%, 30%)");

            var rgba1 = Value.Parse("rgba(255, 100, 50, 0.5)");
            var rgba2 = Value.Parse("rgba(10%, 20%, 30%, 0.2)");

            var hsl = Value.Parse("hsl(60, 50%, 25%)");

            var local1 = Value.Parse("local(hello)");
            var local2 = Value.Parse("local('hello world')");

            var format = Value.Parse("format('something')");

            Assert.AreEqual(typeof(HexTripleColorValue), hex3.GetType());
            Assert.AreEqual(typeof(HexSextupleColorValue), hex6.GetType());
            Assert.AreEqual(typeof(NamedColorValue), black.GetType());
            Assert.AreEqual(typeof(NumberValue), number.GetType());
            Assert.AreEqual(typeof(NumberValue), decimal1.GetType());
            Assert.AreEqual(typeof(NumberValue), decimal2.GetType());
            Assert.AreEqual(typeof(NumberWithUnitValue), px.GetType());
            Assert.AreEqual(typeof(StringValue), @string.GetType());
            Assert.AreEqual(typeof(QuotedStringValue), quotedString.GetType());
            Assert.AreEqual(typeof(UrlValue), url1.GetType());
            Assert.AreEqual(typeof(UrlValue), url2.GetType());
            Assert.AreEqual(typeof(UrlValue), url3.GetType());
            Assert.AreEqual(typeof(RGBColorValue), rgb1.GetType());
            Assert.AreEqual(typeof(RGBColorValue), rgb2.GetType());
            Assert.AreEqual(typeof(RGBAColorValue), rgba1.GetType());
            Assert.AreEqual(typeof(RGBAColorValue), rgba2.GetType());
            Assert.AreEqual(typeof(HSLColorValue), hsl.GetType());
            Assert.AreEqual(typeof(LocalValue), local1.GetType());
            Assert.AreEqual(typeof(LocalValue), local2.GetType());
            Assert.AreEqual(typeof(FormatValue), format.GetType());

            var hex3Color = (HexTripleColorValue)hex3;
            Assert.AreEqual(0xFF, hex3Color.Red);
            Assert.AreEqual(0x00, hex3Color.Green);
            Assert.AreEqual(0x22, hex3Color.Blue);

            var hex6Color = (HexSextupleColorValue)hex6;
            Assert.AreEqual(0xF1, hex6Color.Red);
            Assert.AreEqual(0xE2, hex6Color.Green);
            Assert.AreEqual(0xD3, hex6Color.Blue);

            var blackColor = (NamedColorValue)black;
            Assert.AreEqual(NamedColor.black, blackColor.Color);

            var numberValue = (NumberValue)number;
            Assert.AreEqual(5m, numberValue.Value);

            var dec1 = (NumberValue)decimal1;
            var dec2 = (NumberValue)decimal2;
            Assert.AreEqual(0.5m, dec1.Value);
            Assert.AreEqual(0.3m, dec2.Value);

            var pxValue = (NumberWithUnitValue)px;
            Assert.AreEqual(10, pxValue.Value);
            Assert.AreEqual(Unit.PX, pxValue.Unit);

            var stringValue = (StringValue)@string;
            Assert.AreEqual("hello", stringValue.Value);

            var quotedValue = (QuotedStringValue)quotedString;
            Assert.AreEqual("indeed", quotedValue.Value);

            var urlValue1 = (UrlValue)url1;
            var urlValue2 = (UrlValue)url2;
            var urlValue3 = (UrlValue)url3;
            Assert.AreEqual("/hello/world1.png", ((StringValue)urlValue1.UrlPath).Value);
            Assert.AreEqual("/hello/world2.png", ((QuotedStringValue)urlValue2.UrlPath).Value);
            Assert.AreEqual("/hello/world3.png", ((QuotedStringValue)urlValue3.UrlPath).Value);

            var rgbValue1 = (RGBColorValue)rgb1;
            var rgbValue2 = (RGBColorValue)rgb2;
            Assert.AreEqual(255, ((NumberValue)rgbValue1.Red).Value);
            Assert.AreEqual(100, ((NumberValue)rgbValue1.Green).Value);
            Assert.AreEqual(50, ((NumberValue)rgbValue1.Blue).Value);
            Assert.AreEqual(10, ((NumberWithUnitValue)rgbValue2.Red).Value);
            Assert.AreEqual(Unit.Percent, ((NumberWithUnitValue)rgbValue2.Red).Unit);
            Assert.AreEqual(20, ((NumberWithUnitValue)rgbValue2.Green).Value);
            Assert.AreEqual(Unit.Percent, ((NumberWithUnitValue)rgbValue2.Green).Unit);
            Assert.AreEqual(30, ((NumberWithUnitValue)rgbValue2.Blue).Value);
            Assert.AreEqual(Unit.Percent, ((NumberWithUnitValue)rgbValue2.Blue).Unit);

            var rgbaValue1 = (RGBAColorValue)rgba1;
            var rgbaValue2 = (RGBAColorValue)rgba2;
            Assert.AreEqual(255, ((NumberValue)rgbaValue1.Red).Value);
            Assert.AreEqual(100, ((NumberValue)rgbaValue1.Green).Value);
            Assert.AreEqual(50, ((NumberValue)rgbaValue1.Blue).Value);
            Assert.AreEqual(.5m, ((NumberValue)rgbaValue1.Alpha).Value);
            Assert.AreEqual(10, ((NumberWithUnitValue)rgbaValue2.Red).Value);
            Assert.AreEqual(20, ((NumberWithUnitValue)rgbaValue2.Green).Value);
            Assert.AreEqual(30, ((NumberWithUnitValue)rgbaValue2.Blue).Value);
            Assert.AreEqual(.2m, ((NumberValue)rgbaValue2.Alpha).Value);

            var hslValue = (HSLColorValue)hsl;
            Assert.AreEqual(60, ((NumberValue)hslValue.Hue).Value);
            Assert.AreEqual(50, ((NumberValue)hslValue.Saturation).Value);
            Assert.AreEqual(25, ((NumberValue)hslValue.Lightness).Value);

            var local1Value = (LocalValue)local1;
            var local2Value = (LocalValue)local2;
            Assert.AreEqual("hello", ((StringValue)local1Value.Value).Value);
            Assert.AreEqual("hello world", ((QuotedStringValue)local2Value.Value).Value);

            var formatValue = (FormatValue)format;
            Assert.AreEqual("something", ((QuotedStringValue)formatValue.Value).Value);
        }

        [TestMethod]
        public void Units()
        {
            var em = (NumberWithUnitValue)Value.Parse("1em");
            var ex = (NumberWithUnitValue)Value.Parse("2ex");
            var gd = (NumberWithUnitValue)Value.Parse("3gd");
            var rem = (NumberWithUnitValue)Value.Parse("4rem");
            var vw = (NumberWithUnitValue)Value.Parse("5vw");
            var vh = (NumberWithUnitValue)Value.Parse("6vh");
            var vm = (NumberWithUnitValue)Value.Parse("7vm");
            var ch = (NumberWithUnitValue)Value.Parse("8ch");
            var px = (NumberWithUnitValue)Value.Parse("9px");
            var percent = (NumberWithUnitValue)Value.Parse("10%");
            var inch = (NumberWithUnitValue)Value.Parse("11in");
            var cm = (NumberWithUnitValue)Value.Parse("12cm");
            var mm = (NumberWithUnitValue)Value.Parse("13mm");
            var pt = (NumberWithUnitValue)Value.Parse("14pt");
            var pc = (NumberWithUnitValue)Value.Parse("15pc");
            var s = (NumberWithUnitValue)Value.Parse("16s");
            var ms = (NumberWithUnitValue)Value.Parse("17ms");

            Assert.AreEqual(1m, em.Value);
            Assert.AreEqual(Unit.EM, em.Unit);
            Assert.AreEqual(2m, ex.Value);
            Assert.AreEqual(Unit.EX, ex.Unit);
            Assert.AreEqual(3m, gd.Value);
            Assert.AreEqual(Unit.GD, gd.Unit);
            Assert.AreEqual(4m, rem.Value);
            Assert.AreEqual(Unit.REM, rem.Unit);
            Assert.AreEqual(5m, vw.Value);
            Assert.AreEqual(Unit.VW, vw.Unit);
            Assert.AreEqual(6m, vh.Value);
            Assert.AreEqual(Unit.VH, vh.Unit);
            Assert.AreEqual(7m, vm.Value);
            Assert.AreEqual(Unit.VM, vm.Unit);
            Assert.AreEqual(8m, ch.Value);
            Assert.AreEqual(Unit.CH, ch.Unit);
            Assert.AreEqual(9m, px.Value);
            Assert.AreEqual(Unit.PX, px.Unit);
            Assert.AreEqual(10m, percent.Value);
            Assert.AreEqual(Unit.Percent, percent.Unit);
            Assert.AreEqual(11m, inch.Value);
            Assert.AreEqual(Unit.IN, inch.Unit);
            Assert.AreEqual(12m, cm.Value);
            Assert.AreEqual(Unit.CM, cm.Unit);
            Assert.AreEqual(13m, mm.Value);
            Assert.AreEqual(Unit.MM, mm.Unit);
            Assert.AreEqual(14m, pt.Value);
            Assert.AreEqual(Unit.PT, pt.Unit);
            Assert.AreEqual(15m, pc.Value);
            Assert.AreEqual(Unit.PC, pc.Unit);
            Assert.AreEqual(16m, s.Value);
            Assert.AreEqual(Unit.S, s.Unit);
            Assert.AreEqual(17m, ms.Value);
            Assert.AreEqual(Unit.MS, ms.Unit);
        }

        [TestMethod]
        public void CompoundValues()
        {
            var c1 = Value.Parse(@"5px 5 hello ""hello world""");
            var c2 = Value.Parse(@"#123 0 black");

            Assert.AreEqual(typeof(CompoundValue), c1.GetType());
            Assert.AreEqual(typeof(CompoundValue), c2.GetType());

            var compound1 = (CompoundValue)c1;
            Assert.AreEqual(4, compound1.Values.Count());
            Assert.AreEqual(typeof(NumberWithUnitValue), compound1.Values.ElementAt(0).GetType());
            Assert.AreEqual(typeof(NumberValue), compound1.Values.ElementAt(1).GetType());
            Assert.AreEqual(typeof(StringValue), compound1.Values.ElementAt(2).GetType());
            Assert.AreEqual(typeof(QuotedStringValue), compound1.Values.ElementAt(3).GetType());

            var compound2 = (CompoundValue)c2;
            Assert.AreEqual(3, compound2.Values.Count());
            Assert.AreEqual(typeof(HexTripleColorValue), compound2.Values.ElementAt(0).GetType());
            Assert.AreEqual(typeof(NumberValue), compound2.Values.ElementAt(1).GetType());
            Assert.AreEqual(typeof(NamedColorValue), compound2.Values.ElementAt(2).GetType());
        }

        [TestMethod]
        public void CommaDelimittedValues()
        {
            var v1 = Value.Parse(@"""Lucida Grande"",""Lucida Sans Unicode"",""Lucida Sans"",Tahoma,sans-serif");
            var v2 = Value.Parse(@"5px, 5, #abc");
            
            Assert.AreEqual(typeof(CommaDelimittedValue), v1.GetType());
            Assert.AreEqual(typeof(CommaDelimittedValue), v2.GetType());

            var comma1 = (CommaDelimittedValue)v1;
            Assert.AreEqual(5, comma1.Values.Count());
            Assert.AreEqual(typeof(QuotedStringValue), comma1.Values.ElementAt(0).GetType());
            Assert.AreEqual(typeof(QuotedStringValue), comma1.Values.ElementAt(1).GetType());
            Assert.AreEqual(typeof(QuotedStringValue), comma1.Values.ElementAt(2).GetType());
            Assert.AreEqual(typeof(StringValue), comma1.Values.ElementAt(3).GetType());
            Assert.AreEqual(typeof(StringValue), comma1.Values.ElementAt(4).GetType());

            Assert.AreEqual("Lucida Grande", ((QuotedStringValue)comma1.Values.ElementAt(0)).Value);
            Assert.AreEqual("Lucida Sans Unicode", ((QuotedStringValue)comma1.Values.ElementAt(1)).Value);
            Assert.AreEqual("Lucida Sans", ((QuotedStringValue)comma1.Values.ElementAt(2)).Value);
            Assert.AreEqual("Tahoma", ((StringValue)comma1.Values.ElementAt(3)).Value);
            Assert.AreEqual("sans-serif", ((StringValue)comma1.Values.ElementAt(4)).Value);

            var comma2 = (CommaDelimittedValue)v2;
            Assert.AreEqual(3, comma2.Values.Count());
            Assert.AreEqual(typeof(NumberWithUnitValue), comma2.Values.ElementAt(0).GetType());
            Assert.AreEqual(typeof(NumberValue), comma2.Values.ElementAt(1).GetType());
            Assert.AreEqual(typeof(HexTripleColorValue), comma2.Values.ElementAt(2).GetType());
        }

        [TestMethod]
        public void Functions()
        {
            var p = Value.Parse("@abc");
            var f = Value.Parse("@grey(@a, @b)");
            var fc = Value.Parse("@add(1, 2px)");

            Assert.AreEqual(typeof(FuncValue), p.GetType());
            Assert.AreEqual(typeof(FuncAppliationValue), f.GetType());
            Assert.AreEqual(typeof(FuncAppliationValue), fc.GetType());

            var pValue = (FuncValue)p;
            Assert.AreEqual("abc", pValue.Name);

            var fValue = (FuncAppliationValue)f;
            Assert.AreEqual("grey", fValue.Name);
            Assert.AreEqual(2, fValue.Parameters.Count());
            Assert.IsTrue(fValue.Parameters.All(a => a.GetType() == typeof(FuncValue)));
            Assert.AreEqual("a", ((FuncValue)fValue.Parameters.ElementAt(0)).Name);
            Assert.AreEqual("b", ((FuncValue)fValue.Parameters.ElementAt(1)).Name);

            var fcValue = (FuncAppliationValue)fc;
            Assert.AreEqual("add", fcValue.Name);
            Assert.AreEqual(2, fcValue.Parameters.Count());
            Assert.AreEqual(typeof(NumberValue), fcValue.Parameters.ElementAt(0).GetType());
            Assert.AreEqual(typeof(NumberWithUnitValue), fcValue.Parameters.ElementAt(1).GetType());

            var fcP1 = (NumberValue)fcValue.Parameters.ElementAt(0);
            var fcP2 = (NumberWithUnitValue)fcValue.Parameters.ElementAt(1);
            Assert.AreEqual(1, fcP1.Value);
            Assert.AreEqual(2, fcP2.Value);
            Assert.AreEqual(Unit.PX, fcP2.Unit);
        }

        [TestMethod]
        public void Math()
        {
            var add = Value.Parse("@a + @b + @c");
            var complex = Value.Parse("@a + @b * @c");
            var withFunc = Value.Parse("@add(1,2) + 3");
            var notFound = Value.Parse("@a ?? @b");

            Assert.AreEqual(typeof(MathValue), add.GetType());

            var math = (MathValue)add;
            Assert.AreEqual(typeof(FuncValue), math.RightHand.GetType());
            Assert.AreEqual("c", ((FuncValue)math.RightHand).Name);
            Assert.AreEqual(Operator.Plus, math.Operator);

            var rhs = (MathValue)math.LeftHand;
            Assert.AreEqual("a", ((FuncValue)rhs.LeftHand).Name);
            Assert.AreEqual("b", ((FuncValue)rhs.RightHand).Name);
            Assert.AreEqual(Operator.Plus, rhs.Operator);

            Assert.AreEqual(typeof(MathValue), complex.GetType());

            var c = (MathValue)complex;
            Assert.AreEqual(Operator.Plus, c.Operator);
            Assert.AreEqual("a", ((FuncValue)c.LeftHand).Name);
            
            var mult = (MathValue)c.RightHand;
            Assert.AreEqual(Operator.Mult, mult.Operator);
            Assert.AreEqual("b", ((FuncValue)mult.LeftHand).Name);
            Assert.AreEqual("c", ((FuncValue)mult.RightHand).Name);

            Assert.AreEqual(typeof(MathValue), withFunc.GetType());
            Assert.AreEqual(typeof(FuncAppliationValue), ((MathValue)withFunc).LeftHand.GetType());
            Assert.AreEqual(typeof(NumberValue), ((MathValue)withFunc).RightHand.GetType());

            Assert.AreEqual(typeof(MathValue), notFound.GetType());
            var nf = (MathValue)notFound;
            Assert.AreEqual(Operator.Take_Exists, nf.Operator);
            Assert.AreEqual("a", ((FuncValue)nf.LeftHand).Name);
            Assert.AreEqual("b", ((FuncValue)nf.RightHand).Name);
        }

        [TestMethod]
        public void EvaluateConstants()
        {
            var noUnits = Value.Parse("5 + 4 *3");
            var withUnits = Value.Parse("5px * 5");
            var stringConcat = Value.Parse(@"'hello' + "" "" + 'world!'");
            var ordered = Value.Parse("5+4*3-2/2");
            var grouped = Value.Parse("4*(3+2)/2");
            
            var noUnitsEval = noUnits.Evaluate();
            var withUnitsEval = withUnits.Evaluate();
            var stringConcatEval = stringConcat.Evaluate();
            var orderedEval = ordered.Evaluate();
            var groupedEval = grouped.Evaluate();

            Assert.AreEqual(typeof(NumberValue), noUnitsEval.GetType());
            Assert.AreEqual(17m, ((NumberValue)noUnitsEval).Value);

            Assert.AreEqual(typeof(NumberWithUnitValue), withUnitsEval.GetType());
            Assert.AreEqual(25m, ((NumberWithUnitValue)withUnitsEval).Value);
            Assert.AreEqual(Unit.PX, ((NumberWithUnitValue)withUnitsEval).Unit);

            Assert.AreEqual(typeof(QuotedStringValue), stringConcatEval.GetType());
            Assert.AreEqual("hello world!", ((QuotedStringValue)stringConcatEval).Value);

            Assert.AreEqual(typeof(NumberValue), orderedEval.GetType());
            Assert.AreEqual(16m, ((NumberValue)orderedEval).Value);

            Assert.AreEqual(typeof(NumberValue), groupedEval.GetType());
            Assert.AreEqual(10m, ((NumberValue)groupedEval).Value);
        }

        [TestMethod]
        public void ColorVariables()
        {
            var hslUnits = Value.Parse("hsl(@h, @s, @l)");
            var hslWithMath = Value.Parse("hsl(@a+@b, @c+5, @d*2)");
            var rgbUnits = Value.Parse("rgb(@r, @g, @b)");
            var rgbWithMath = Value.Parse("rgb(@a+@b, @c+5, @d*2)");
            var rgbaUnits = Value.Parse("rgba(@r, @g, @b, @a)");
            var rgbaWithMath = Value.Parse("rgba(@r + 5, @g + 6, @b + 7, @a)");
             
            Assert.AreEqual(typeof(HSLColorValue), hslUnits.GetType());
            Assert.AreEqual(typeof(HSLColorValue), hslWithMath.GetType());

            Assert.AreEqual(typeof(RGBColorValue), rgbUnits.GetType());
            Assert.AreEqual(typeof(RGBColorValue), rgbWithMath.GetType());

            Assert.AreEqual(typeof(RGBAColorValue), rgbaUnits.GetType());
            Assert.AreEqual(typeof(RGBAColorValue), rgbaWithMath.GetType());
        }

        [TestMethod]
        public void LeftExists()
        {
            var simple = Value.Parse("@a?");
            var grouped = Value.Parse("(@a + 5)?");
            var followOn = Value.Parse("(@b + 6)? + 7");

            Assert.AreEqual(typeof(LeftExistsValue), simple.GetType());
            Assert.AreEqual(typeof(LeftExistsValue), grouped.GetType());
            Assert.AreEqual(typeof(MathValue), followOn.GetType());

            Assert.AreEqual("a", ((FuncValue)((LeftExistsValue)simple).IfExists).Name);

            var groupedIf = (LeftExistsValue)grouped;
            Assert.AreEqual(typeof(MathValue), groupedIf.IfExists.GetType());
            var groupedMath = (MathValue)groupedIf.IfExists;
            Assert.AreEqual("a", ((FuncValue)groupedMath.LeftHand).Name);
            Assert.AreEqual(5m, ((NumberValue)groupedMath.RightHand).Value);

            var follow = (MathValue)followOn;
            var followLeft = (LeftExistsValue)follow.LeftHand;
            var followRight = (NumberValue)follow.RightHand;
            Assert.AreEqual("b", ((FuncValue)((MathValue)followLeft.IfExists).LeftHand).Name);
            Assert.AreEqual(6m, ((NumberValue)((MathValue)followLeft.IfExists).RightHand).Value);
            Assert.AreEqual(7m, followRight.Value);
        }

        [TestMethod]
        public void Important()
        {
            var compound = Value.Parse("url('hello') !important");

            var asCompound = (CompoundValue)compound;
            Assert.AreEqual(typeof(UrlValue), asCompound.Values.ElementAt(0).GetType());
            Assert.AreEqual(ImportantValue.Singleton, asCompound.Values.ElementAt(1));
        }

        [TestMethod]
        public void Decimals()
        {
            var simple = Value.Parse("0.9");
            var negative = Value.Parse("-1.2");
            var complex = Value.Parse(".2");
            var negativeComplex = Value.Parse("-.4");

            Assert.AreEqual(0.9m, ((NumberValue)simple).Value);
            Assert.AreEqual(-1.2m, ((NumberValue)negative).Value);
            Assert.AreEqual(.2m, ((NumberValue)complex).Value);
            Assert.AreEqual(-.4m, ((NumberValue)negativeComplex).Value);
        }

        [TestMethod]
        public void OrderOfOperations()
        {
            var a = Value.Parse("1.0 + 2.1");
            var b = Value.Parse("5 * 2 + 3");
            var c = Value.Parse("4 + 2 * 2");
            var d = Value.Parse("(4+1)*(2-1)");
            var e = Value.Parse("(4+1)?*2");
            var f = Value.Parse("(4 + 2 / (3+1)?)? * 5");

            var aE = a.Evaluate() as NumberValue;
            var bE = b.Evaluate() as NumberValue;
            var cE = c.Evaluate() as NumberValue;
            var dE = d.Evaluate() as NumberValue;
            var eE = e.Evaluate() as NumberValue;
            var fE = f.Evaluate() as NumberValue;

            Assert.AreEqual(3.1m, aE.Value);
            Assert.AreEqual(13m, bE.Value);
            Assert.AreEqual(8m, cE.Value);
            Assert.AreEqual(5m, dE.Value);
            Assert.AreEqual(10m, eE.Value);
            Assert.AreEqual(22.5m, fE.Value);
        }

        [TestMethod]
        public void TrailingImportant()
        {
            var a = Value.Parse("5!important");
            var b = Value.Parse("1px!important");
            var c = Value.Parse("#abc!important");
            var d = Value.Parse("#123456!important");
            var e = Value.Parse("rgb(5,10,15)!important");
            var f = Value.Parse("none!important");
            var g = Value.Parse("'hello world'!important");

            Assert.AreEqual(typeof(CompoundValue), a.GetType());
            Assert.AreEqual(typeof(CompoundValue), b.GetType());
            Assert.AreEqual(typeof(CompoundValue), c.GetType());
            Assert.AreEqual(typeof(CompoundValue), d.GetType());
            Assert.AreEqual(typeof(CompoundValue), e.GetType());
            Assert.AreEqual(typeof(CompoundValue), f.GetType());
            Assert.AreEqual(typeof(CompoundValue), g.GetType());

            var aC = (CompoundValue)a;
            var bC = (CompoundValue)b;
            var cC = (CompoundValue)c;
            var dC = (CompoundValue)d;
            var eC = (CompoundValue)e;
            var fC = (CompoundValue)f;
            var gC = (CompoundValue)g;

            Assert.AreEqual(typeof(NumberValue), aC.Values.ElementAt(0).GetType());
            Assert.AreEqual(typeof(NumberWithUnitValue), bC.Values.ElementAt(0).GetType());
            Assert.AreEqual(typeof(HexTripleColorValue), cC.Values.ElementAt(0).GetType());
            Assert.AreEqual(typeof(HexSextupleColorValue), dC.Values.ElementAt(0).GetType());
            Assert.AreEqual(typeof(RGBColorValue), eC.Values.ElementAt(0).GetType());
            Assert.AreEqual(typeof(StringValue), fC.Values.ElementAt(0).GetType());
            Assert.AreEqual(typeof(QuotedStringValue), gC.Values.ElementAt(0).GetType());

            Assert.AreEqual(typeof(ImportantValue), aC.Values.ElementAt(1).GetType());
            Assert.AreEqual(typeof(ImportantValue), bC.Values.ElementAt(1).GetType());
            Assert.AreEqual(typeof(ImportantValue), cC.Values.ElementAt(1).GetType());
            Assert.AreEqual(typeof(ImportantValue), dC.Values.ElementAt(1).GetType());
            Assert.AreEqual(typeof(ImportantValue), eC.Values.ElementAt(1).GetType());
            Assert.AreEqual(typeof(ImportantValue), fC.Values.ElementAt(1).GetType());
            Assert.AreEqual(typeof(ImportantValue), gC.Values.ElementAt(1).GetType());
        }

        [TestMethod]
        public void Attr()
        {
            var a = Value.Parse("attr(length)");
            var b = Value.Parse("attr(length px)");
            var c = Value.Parse("attr(length px, 10px)");

            Assert.AreEqual(typeof(AttributeValue), a.GetType());
            Assert.AreEqual(typeof(AttributeValue), b.GetType());
            Assert.AreEqual(typeof(AttributeValue), c.GetType());

            var aAttr = (AttributeValue)a;
            var bAttr = (AttributeValue)b;
            var cAttr = (AttributeValue)c;

            Assert.AreEqual("length", aAttr.Attribute.ToString());
            Assert.AreEqual("length", bAttr.Attribute.ToString());
            Assert.AreEqual("length", cAttr.Attribute.ToString());

            Assert.AreEqual(null, aAttr.Type);
            Assert.AreEqual("px", bAttr.Type.ToString());
            Assert.AreEqual("px", cAttr.Type.ToString());

            Assert.AreEqual(null, aAttr.Fallback);
            Assert.AreEqual(null, bAttr.Fallback);
            Assert.AreEqual("10px", cAttr.Fallback.ToString());
        }

        [TestMethod]
        public void Calc()
        {
            var a = Value.Parse("calc(20%)");
            var b = Value.Parse("calc(20 + 30px)");
            var c = Value.Parse("calc(20% - 2em)");
            var d = Value.Parse("calc((20% - 14) / 2 + 1rem)");

            Assert.AreEqual(typeof(CalcValue), a.GetType());
            Assert.AreEqual(typeof(CalcValue), b.GetType());
            Assert.AreEqual(typeof(CalcValue), c.GetType());
            Assert.AreEqual(typeof(CalcValue), d.GetType());

            var aCalc = (CalcValue)a;
            var bCalc = (CalcValue)b;
            var cCalc = (CalcValue)c;
            var dCalc = (CalcValue)d;

            Assert.AreEqual("20%", aCalc.Value.ToString());
            Assert.AreEqual("20 + 30px", bCalc.Value.ToString());
            Assert.AreEqual("20% - 2em", cCalc.Value.ToString());
            Assert.AreEqual("(20% - 14) / 2 + 1rem", dCalc.Value.ToString());
        }

        [TestMethod]
        public void Counter()
        {
            var a = Value.Parse("counter(hello)");
            var b = Value.Parse("counter(hello, world)");

            Assert.AreEqual(typeof(CounterValue), a.GetType());
            Assert.AreEqual(typeof(CounterValue), b.GetType());

            var aCounter = (CounterValue)a;
            var bCounter = (CounterValue)b;

            Assert.AreEqual("hello", aCounter.Counter.ToString());
            Assert.AreEqual("hello", bCounter.Counter.ToString());

            Assert.AreEqual(null, aCounter.Style);
            Assert.AreEqual("world", bCounter.Style.ToString());
        }
    }
}