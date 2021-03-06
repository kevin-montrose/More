﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using MoreInternals.Parser;
using MoreInternals.Compiler;
using System.Diagnostics.CodeAnalysis;
using System.Collections;

namespace MoreInternals.Model
{
    /// <summary>
    /// http://www.w3.org/TR/css3-values/#relative0
    /// &amp;
    /// http://www.w3.org/TR/css3-values/#absolute0
    /// </summary>
    enum Unit
    {
        EM,
        EX,
        GD,
        REM,
        VW,
        VH,
        VM,
        CH,
        PX,
        Percent,
        IN,
        CM,
        MM,
        PT,
        PC,
        S,
        MS,
        DEG,
        GRAD,
        RAD,
        TURN,
        HZ,
        KHZ,

        // Only valid in media queries
        DPI,
        DPCM
    }

    /// <summary>
    /// http://www.w3.org/TR/css3-color/
    /// </summary>
    enum NamedColor
    {
        aliceblue = 0xF0F8FF,
        antiquewhite = 0xFAEBD7,
        aqua = 0x00FFFF,
        aquamarine = 0x7FFFD4,
        azure = 0xF0FFFF,
        beige = 0xF5F5DC,
        bisque = 0xFFE4C4,
        black = 0x000000,
        blanchedalmond = 0xFFEBCD,
        blue = 0x0000FF,
        blueviolet = 0x8A2BE2,
        brown = 0xA52A2A,
        burlywood = 0xDEB887,
        cadetblue = 0x5F9EA0,
        chartreuse = 0x7FFF00,
        chocolate = 0xD2691E,
        coral = 0xFF7F50,
        cornflowerblue = 0x6495ED,
        cornsilk = 0xFFF8DC,
        crimson = 0xDC143C,
        cyan = 0x00FFFF,
        darkblue = 0x00008B,
        darkcyan = 0x008B8B,
        darkgoldenrod = 0xB8860B,
        darkgray = 0xA9A9A9,
        darkgreen = 0x006400,
        darkgrey = 0xA9A9A9,
        darkkhaki = 0xBDB76B,
        darkmagenta = 0x8B008B,
        darkolivegreen = 0x556B2F,
        darkorange = 0xFF8C00,
        darkorchid = 0x9932CC,
        darkred = 0x8B0000,
        darksalmon = 0xE9967A,
        darkseagreen = 0x8FBC8F,
        darkslateblue = 0x483D8B,
        darkslategray = 0x2F4F4F,
        darkslategrey = 0x2F4F4F,
        darkturquoise = 0x00CED1,
        darkviolet = 0x9400D3,
        deeppink = 0xFF1493,
        deepskyblue = 0x00BFFF,
        dimgray = 0x696969,
        dimgrey = 0x696969,
        dodgerblue = 0x1E90FF,
        firebrick = 0xB22222,
        floralwhite = 0xFFFAF0,
        forestgreen = 0x228B22,
        fuchsia = 0xFF00FF,
        gainsboro = 0xDCDCDC,
        ghostwhite = 0xF8F8FF,
        gold = 0xFFD700,
        goldenrod = 0xDAA520,
        gray = 0x808080,
        green = 0x008000,
        greenyellow = 0xADFF2F,
        grey = 0x808080,
        honeydew = 0xF0FFF0,
        hotpink = 0xFF69B4,
        indianred = 0xCD5C5C,
        indigo = 0x4B0082,
        ivory = 0xFFFFF0,
        khaki = 0xF0E68C,
        lavender = 0xE6E6FA,
        lavenderblush = 0xFFF0F5,
        lawngreen = 0x7CFC00,
        lemonchiffon = 0xFFFACD,
        lightblue = 0xADD8E6,
        lightcoral = 0xF08080,
        lightcyan = 0xE0FFFF,
        lightgoldenrodyellow = 0xFAFAD2,
        lightgray = 0xD3D3D3,
        lightgreen = 0x90EE90,
        lightgrey = 0xD3D3D3,
        lightpink = 0xFFB6C1,
        lightsalmon = 0xFFA07A,
        lightseagreen = 0x20B2AA,
        lightskyblue = 0x87CEFA,
        lightslategray = 0x778899,
        lightslategrey = 0x778899,
        lightsteelblue = 0xB0C4DE,
        lightyellow = 0xFFFFE0,
        lime = 0x00FF00,
        limegreen = 0x32CD32,
        linen = 0xFAF0E6,
        magenta = 0xFF00FF,
        maroon = 0x800000,
        mediumaquamarine = 0x66CDAA,
        mediumblue = 0x0000CD,
        mediumorchid = 0xBA55D3,
        mediumpurple = 0x9370DB,
        mediumseagreen = 0x3CB371,
        mediumslateblue = 0x7B68EE,
        mediumspringgreen = 0x00FA9A,
        mediumturquoise = 0x48D1CC,
        mediumvioletred = 0xC71585,
        midnightblue = 0x191970,
        mintcream = 0xF5FFFA,
        mistyrose = 0xFFE4E1,
        moccasin = 0xFFE4B5,
        navajowhite = 0xFFDEAD,
        navy = 0x000080,
        oldlace = 0xFDF5E6,
        olive = 0x808000,
        olivedrab = 0x6B8E23,
        orange = 0xFFA500,
        orangered = 0xFF4500,
        orchid = 0xDA70D6,
        palegoldenrod = 0xEEE8AA,
        palegreen = 0x98FB98,
        paleturquoise = 0xAFEEEE,
        palevioletred = 0xDB7093,
        papayawhip = 0xFFEFD5,
        peachpuff = 0xFFDAB9,
        peru = 0xCD853F,
        pink = 0xFFC0CB,
        plum = 0xDDA0DD,
        powderblue = 0xB0E0E6,
        purple = 0x800080,
        red = 0xFF0000,
        rosybrown = 0xBC8F8F,
        royalblue = 0x4169E1,
        saddlebrown = 0x8B4513,
        salmon = 0xFA8072,
        sandybrown = 0xF4A460,
        seagreen = 0x2E8B57,
        seashell = 0xFFF5EE,
        sienna = 0xA0522D,
        silver = 0xC0C0C0,
        skyblue = 0x87CEEB,
        slateblue = 0x6A5ACD,
        slategray = 0x708090,
        slategrey = 0x708090,
        snow = 0xFFFAFA,
        springgreen = 0x00FF7F,
        steelblue = 0x4682B4,
        tan = 0xD2B48C,
        teal = 0x008080,
        thistle = 0xD8BFD8,
        tomato = 0xFF6347,
        turquoise = 0x40E0D0,
        violet = 0xEE82EE,
        wheat = 0xF5DEB3,
        white = 0xFFFFFF,
        whitesmoke = 0xF5F5F5,
        yellow = 0xFFFF00,
        yellowgreen = 0x9ACD32
    }

    partial class Value : IPosition
    {
        public int Start { get; internal set; }
        public int Stop { get; internal set; }
        public string FilePath { get; internal set; }

        public virtual bool NeedsEvaluate { get { return false; } }
        protected Scope Scope { get; set; }

        public virtual bool IsImportant()
        {
            return false;
        }

        public virtual Value Bind(Scope scope)
        {
            var clone = (Value)this.MemberwiseClone();
            clone.Scope = scope;

            return clone;
        }

        private static void Unroll(MathValue value, List<object> tokens)
        {
            if (value.LeftHand is MathValue)
            {
                Unroll((MathValue)value.LeftHand, tokens);
            }
            else
            {
                if (value.LeftHand is LeftExistsValue)
                {
                    tokens.Add(new LeftExistsValue(UnrollMath(((LeftExistsValue)value.LeftHand).IfExists)));
                }
                else
                {
                    tokens.Add(value.LeftHand);
                }
            }

            tokens.Add(value.Operator);

            if (value.RightHand is MathValue)
            {
                Unroll((MathValue)value.RightHand, tokens);
            }
            else
            {
                if (value.RightHand is LeftExistsValue)
                {
                    tokens.Add(new LeftExistsValue(UnrollMath(((LeftExistsValue)value.RightHand).IfExists)));
                }
                else
                {
                    tokens.Add(value.RightHand);
                }
            }
        }

        public static int Precedence(Operator o)
        {
            switch (o)
            {
                case Operator.Div: return 3;
                case Operator.Mod: return 3;
                case Operator.Mult: return 3;

                case Operator.Minus: return 2;
                case Operator.Plus: return 2;
                
                case Operator.Take_Exists: return 1;

                default: throw new NotImplementedException();
            }
        }

        private static MathValue DeRPN(IEnumerable<object> tokenized)
        {
            var stack = new Stack<Value>();

            foreach (var t in tokenized)
            {
                if (t is Value)
                {
                    stack.Push((Value)t);
                    continue;
                }

                var op = (Operator)t;
                var rhs = stack.Pop();
                var lhs = stack.Pop();
                stack.Push(new MathValue(lhs, op, rhs));
            }

            return (MathValue)stack.Pop();
        }

        private static MathValue UnrollMathImpl(MathValue value)
        {
            var tokens = new List<object>();
            Unroll(value, tokens);

            var outQueue = new Queue<object>();
            var opStack = new Stack<Operator>();

            foreach (var t in tokens)
            {
                if (t is Value)
                {
                    if (t is GroupedValue)
                    {
                        var grouped =  UnrollMath(((GroupedValue)t).Value);
                        outQueue.Enqueue(grouped);
                        continue;
                    }

                    if (t is LeftExistsValue)
                    {
                        var leftExist = new LeftExistsValue(UnrollMath(((LeftExistsValue)t).IfExists));
                        outQueue.Enqueue(leftExist);
                        continue;
                    }

                    outQueue.Enqueue((Value)t);
                    continue;
                }

                var o1 = (Operator)t;
                while (opStack.Count > 0 && Precedence(o1) <= Precedence(opStack.Peek()))
                {
                    var o2 = opStack.Pop();
                    outQueue.Enqueue(o2);
                }
                opStack.Push(o1);
            }

            while (opStack.Count > 0)
            {
                outQueue.Enqueue(opStack.Pop());
            }

            return DeRPN(outQueue);
        }

        private static Value UnrollMath(Value value)
        {
            var compound = value as CompoundValue;
            if (compound != null)
            {
                var ret = new List<Value>();
                foreach (var v in compound.Values)
                {
                    ret.Add(UnrollMath(v));
                }

                return new CompoundValue(ret);
            }

            var comma = value as CommaDelimittedValue;
            if (comma != null)
            {
                var ret = new List<Value>();
                foreach (var v in comma.Values)
                {
                    ret.Add(UnrollMath(v));
                }

                return new CommaDelimittedValue(ret);
            }

            var exists = value as LeftExistsValue;
            if (exists != null)
            {
                return new LeftExistsValue(UnrollMath(exists.IfExists));
            }

            var group = value as GroupedValue;
            if (group != null) return group.Value;

            var math = value as MathValue;
            if (math == null) return value;

            return UnrollMathImpl(math);
        }

        public static Value Parse(string rawValue, int start = -1, int stop = -1, string filePath = null, bool allowSelectorIncludes = false)
        {
            var ret = MoreValueParser.Parse(rawValue, Position.Create(start, stop, filePath), allowSelectorIncludes);

            var unrolled = UnrollMath(ret);

            unrolled.Start = start;
            unrolled.Stop = stop;
            unrolled.FilePath = filePath;

            return unrolled;
        }

        internal virtual Value Evaluate()
        {
            throw new NotImplementedException(this.GetType() + ".Evaluate");
        }

        internal virtual void Write(TextWriter output)
        {
            throw new NotImplementedException(this.GetType() + ".Write");
        }

        internal virtual List<string> ReferredToVariables()
        {
            throw new NotImplementedException(this.GetType() + ".ReferredToVariables");
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            try
            {
                using (var mem = new StringWriter())
                {
                    Write(mem);

                    return mem.ToString();
                }
            }
            catch 
            {
                return base.ToString();
            }
        }

        public override bool Equals(object obj)
        {
            throw new NotImplementedException(this.GetType().Name + ".Equals");
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException(this.GetType().Name + ".GetHashCode");
        }
    }

    class GroupedValue : Value
    {
        public override bool NeedsEvaluate
        {
            get
            {
                return Value.NeedsEvaluate;
            }
        }

        public Value Value { get; private set; }
        public GroupedValue(Value value)
        {
            Value = value;
        }

        internal override List<string> ReferredToVariables()
        {
            return Value.ReferredToVariables();
        }

        public override Value Bind(Scope scope)
        {
            return new GroupedValue(Value.Bind(scope));
        }

        internal override Value Evaluate()
        {
            var inner = Value.Evaluate();

            if (inner is MathValue) return new GroupedValue(inner);

            // there's no point in keeping the "group" around if it's non-functional
            return inner;
        }

        internal override void Write(TextWriter output)
        {
            output.Write('(');
            Value.Write(output);
            output.Write(')');
        }

        public override bool Equals(object obj)
        {
            var asGroup = obj as GroupedValue;
            if (asGroup == null) return false;

            return asGroup.Value.Equals(Value);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }

    // @(h1, h2, h3), etc.
    class IncludeSelectorValue : Value
    {
        public Selector Selector { get; private set; }

        public IncludeSelectorValue(Selector sel)
        {
            Selector = sel;
        }

        internal override List<string> ReferredToVariables()
        {
            return new List<string>();
        }
    }

    // @a, @b, etc.
    class FuncValue : Value
    {
        public override bool NeedsEvaluate { get { return true; } }
        public string Name { get; private set; }

        internal FuncValue(string name)
        {
            Name = name;
        }

        internal override Value Evaluate()
        {
            return Scope.LookupVariable(Name, Start, Stop, FilePath);
        }

        internal override List<string> ReferredToVariables()
        {
            return new List<string>() { this.Name };
        }

        internal override void Write(TextWriter output)
        {
            output.Write('@');
            output.Write(Name);
        }
    }

    // @a(1,2px,@c), etc.
    class FuncAppliationValue : Value
    {
        public string Name { get; private set; }
        public IEnumerable<Value> Parameters { get; private set;}

        internal FuncAppliationValue(string name, List<Value> @params)
        {
            Name = name;
            Parameters = @params.AsReadOnly();
        }

        public override Value Bind(Scope scope)
        {
            var @params = Parameters.Select(s => s.Bind(scope));

            var ret = new FuncAppliationValue(Name, @params.ToList());
            ret.Scope = scope;
            ret.Start = this.Start;
            ret.Stop = this.Stop;
            ret.FilePath = this.FilePath;

            return ret;
        }

        internal override Value Evaluate()
        {
            var func = this.Scope.LookupFunction(Name);

            if (func == null)
            {
                Current.RecordError(ErrorType.Compiler, this, "No function named [" + Name + "] found.");
                return ExcludeFromOutputValue.Singleton;
            }

            var @params = Parameters.Select(p => p.Evaluate());

            return func.Invoke(@params, this);
        }

        internal override List<string> ReferredToVariables()
        {
            var ret = new List<string>();
            ret.Add(Name);
            
            foreach (var var in this.Parameters)
            {
                ret.AddRange(var.ReferredToVariables());
            }

            return ret;
        }
    }

    // 1, 2.0, etc.
    class NumberValue : Value
    {
        public decimal Value { get; set; }

        internal NumberValue(decimal value)
        {
            Value = value;
        }

        internal override Value Evaluate()
        {
            return this;
        }

        internal override void Write(TextWriter output)
        {
            output.Write(Value);
        }

        internal override List<string> ReferredToVariables()
        {
            return new List<string>();
        }

        public override bool Equals(object obj)
        {
            var asNumber = obj as NumberValue;
            if (asNumber == null) return false;

            return asNumber.Value == Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }

    // 1px, 2em, 100%, etc.
    class NumberWithUnitValue : NumberValue
    {
        public Unit Unit { get; private set; }

        internal NumberWithUnitValue(decimal value, Unit unit)
            : base(value)
        {
            Unit = unit;
        }

        internal override Value Evaluate()
        {
            return this;
        }

        internal override void Write(TextWriter output)
        {
            base.Write(output);
            if (Unit != Model.Unit.Percent)
            {
                output.Write(Enum.GetName(typeof(Unit), Unit).ToLower());
            }
            else
            {
                output.Write('%');
            }
        }

        public override bool Equals(object obj)
        {
            var asNumUnit = obj as NumberWithUnitValue;
            if (asNumUnit == null) return false;

            return
                asNumUnit.Value == Value &&
                asNumUnit.Unit == Unit;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode() ^ (Unit.GetHashCode() * -1);
        }
    }

    class ColorValue : Value
    {
        internal override List<string> ReferredToVariables()
        {
            return new List<string>();
        }
    }

    // black, green, blue, etc.
    class NamedColorValue : ColorValue
    {
        public NamedColor Color { get; private set; }

        internal NamedColorValue(NamedColor color)
        {
            Color = color;
        }

        public static NamedColorValue Parse(NamedColor color)
        {
            return new NamedColorValue(color);
        }

        internal override Value Evaluate()
        {
            return this;
        }

        internal override void Write(TextWriter output)
        {
            output.Write(Enum.GetName(typeof(NamedColor), Color).ToLower());
        }

        public override bool Equals(object obj)
        {
            var asColor = obj as NamedColorValue;
            if (asColor == null) return false;

            return asColor.Color == Color;
        }

        public override int GetHashCode()
        {
            return Color.GetHashCode();
        }
    }

    // #000, #123, #ABC, etc.
    class HexTripleColorValue : ColorValue
    {
        public byte Red { get; private set; }
        public byte Green { get; private set; }
        public byte Blue { get; private set; }

        internal HexTripleColorValue(byte r, byte g, byte b)
        {
            Red = r;
            Green = g;
            Blue = b;
        }

        public static HexTripleColorValue Parse(string raw)
        {
            var r = raw[0] + "" + raw[0];
            var g = raw[1] + "" + raw[1];
            var b = raw[2] + "" + raw[2];

            return new HexTripleColorValue(byte.Parse(r, NumberStyles.HexNumber), byte.Parse(g, NumberStyles.HexNumber), byte.Parse(b, NumberStyles.HexNumber));
        }

        internal override Value Evaluate()
        {
            return this;
        }

        internal override void Write(TextWriter output)
        {
            output.Write('#');
            output.Write(Red.ToString("x")[0]);
            output.Write(Green.ToString("x")[0]);
            output.Write(Blue.ToString("x")[0]);
        }

        public override bool Equals(object obj)
        {
            var asColor = obj as HexTripleColorValue;
            if (asColor == null) return false;

            return
                asColor.Red == Red &&
                asColor.Green == Green &&
                asColor.Blue == Blue;
        }

        public override int GetHashCode()
        {
            return Red.GetHashCode() ^ (Green.GetHashCode() * -1) ^ ((Blue * -1).GetHashCode());
        }
    }

    // #FEDCBA, etc.
    class HexSextupleColorValue : ColorValue
    {
        public byte Red { get; private set; }
        public byte Green { get; private set; }
        public byte Blue { get; private set; }

        internal HexSextupleColorValue(byte r, byte g, byte b)
        {
            Red = r;
            Green = g;
            Blue = b;
        }

        public static HexSextupleColorValue Parse(string raw)
        {
            var r = raw.Substring(0, 2);
            var g = raw.Substring(2, 2);
            var b = raw.Substring(4, 2);

            return new HexSextupleColorValue(byte.Parse(r, NumberStyles.HexNumber), byte.Parse(g, NumberStyles.HexNumber), byte.Parse(b, NumberStyles.HexNumber));
        }

        internal override Value Evaluate()
        {
            return this;
        }

        internal override void Write(TextWriter output)
        {
            output.Write('#');
            var r = Red.ToString("x");
            var g = Green.ToString("x");
            var b = Blue.ToString("x");

            if (r.Length == 1) r = "0" + r;
            if (g.Length == 1) g = "0" + g;
            if (b.Length == 1) b = "0" + b;

            output.Write(r);
            output.Write(g);
            output.Write(b);
        }

        public override bool Equals(object obj)
        {
            var asColor = obj as HexSextupleColorValue;
            if (asColor == null) return false;

            return
                asColor.Red == Red &&
                asColor.Green == Green &&
                asColor.Blue == Blue;
        }

        public override int GetHashCode()
        {
            return Red.GetHashCode() ^ (Green.GetHashCode() * -1) ^ ((Blue * -1).GetHashCode());
        }
    }

    // rgb(255, 0, 1), rgb(100%, 0%, 10%), rgb(@r, @g, @b), etc.
    class RGBColorValue : ColorValue
    {
        public override bool NeedsEvaluate { get { return Red.NeedsEvaluate || Green.NeedsEvaluate || Blue.NeedsEvaluate; } }

        public Value Red { get; private set; }
        public Value Green { get; private set; }
        public Value Blue { get; private set; }

        internal RGBColorValue(Value r, Value g, Value b)
        {
            Red = r;
            Green = g;
            Blue = b;
        }

        internal static byte ParseColor(string color)
        {
            try
            {
                if (color.EndsWith("%"))
                {
                    var rP = decimal.Parse(color.Substring(0, color.Length - 1));
                    var temp = (255m * (rP / 100m));
                    if (temp > 255) temp = 255;
                    if (temp < 0) temp = 0;

                    return (byte)temp;
                }

                var ret = int.Parse(color);
                if (ret > 255) ret = 255;
                if (ret < 0) ret = 0;

                return (byte)ret;
            }
            catch (Exception)
            {
                throw new InvalidOperationException("Could not parse [" + color + "] as a color component value");
            }
        }

        internal override Value Evaluate()
        {
            return new RGBColorValue(Red.Evaluate(), Green.Evaluate(), Blue.Evaluate());
        }

        public override Value Bind(Scope scope)
        {
            return new RGBColorValue(Red.Bind(scope), Green.Bind(scope), Blue.Bind(scope));
        }

        // Sanity check on types, clips to proper range, and converts to (smaller, and consistent) [0,255] format
        internal static Value WriteSanityCheck(Value color, string name)
        {
            if(!(color is NumberValue) && !(color is NumberWithUnitValue))
            {
                throw new InvalidOperationException(name + " must be a number or a percentage, found ["+color+"]");
            }

            if(color is NumberWithUnitValue && ((NumberWithUnitValue)color).Unit != Unit.Percent)
            {
                throw new InvalidOperationException(name + " must be a number or a percentage, found ["+color+"]");
            }

            decimal val;
            if (color is NumberValue)
            {
                val = ((NumberValue)color).Value;
            }
            else
            {
                val = 255m * ((NumberWithUnitValue)color).Value;
            }

            if (val > 255) val = 255;
            if (val < 0) val = 0;

            return new NumberValue(decimal.Round(val));
        }

        internal override void Write(TextWriter output)
        {
            var r = WriteSanityCheck(Red, "red");
            var g = WriteSanityCheck(Green, "green");
            var b = WriteSanityCheck(Blue, "blue");

            output.Write("rgb(");
            r.Write(output);
            output.Write(',');
            g.Write(output);
            output.Write(',');
            b.Write(output);
            output.Write(')');
        }

        internal override List<string> ReferredToVariables()
        {
            var ret =  new List<string>();

            ret.AddRange(Red.ReferredToVariables());
            ret.AddRange(Green.ReferredToVariables());
            ret.AddRange(Blue.ReferredToVariables());

            return ret;
        }

        public override bool Equals(object obj)
        {
            var asColor = obj as RGBColorValue;
            if (asColor == null) return false;

            return
                asColor.Red == Red &&
                asColor.Green == Green &&
                asColor.Blue == Blue;
        }

        public override int GetHashCode()
        {
            return Red.GetHashCode() ^ (Green.GetHashCode() * -1) ^ (Blue.GetHashCode());
        }
    }

    // rgba(255, 10, 5, 0.1), etc.
    class RGBAColorValue : ColorValue
    {
        public override bool NeedsEvaluate { get { return Red.NeedsEvaluate || Green.NeedsEvaluate || Blue.NeedsEvaluate || Alpha.NeedsEvaluate; } }

        public Value Red { get; private set; }
        public Value Green { get; private set; }
        public Value Blue { get; private set; }
        public Value Alpha { get; private set; }

        internal RGBAColorValue(Value red, Value green, Value blue, Value alpha)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }

        internal override Value Evaluate()
        {
            return new RGBAColorValue(Red.Evaluate(), Green.Evaluate(), Blue.Evaluate(), Alpha.Evaluate());
        }

        public override Value Bind(Scope scope)
        {
            return new RGBAColorValue(Red.Bind(scope), Green.Bind(scope), Blue.Bind(scope), Alpha.Bind(scope));
        }

        internal override void Write(TextWriter output)
        {
            var r = RGBColorValue.WriteSanityCheck(Red, "red");
            var g = RGBColorValue.WriteSanityCheck(Green, "green");
            var b = RGBColorValue.WriteSanityCheck(Blue, "blue");

            if (!(Alpha is NumberValue))
            {
                throw new InvalidOperationException("alpha must be a number, found [" + Alpha + "]");
            }

            var a = ((NumberValue)Alpha).Value;

            if (a > 1) a = 1;
            if (a < 0) a = 0;

            output.Write("rgba(");
            r.Write(output);
            output.Write(',');
            g.Write(output);
            output.Write(',');
            b.Write(output);
            output.Write(',');
            output.Write(a);
            output.Write(')');
        }

        internal override List<string> ReferredToVariables()
        {
            var ret = new List<string>();

            ret.AddRange(Red.ReferredToVariables());
            ret.AddRange(Green.ReferredToVariables());
            ret.AddRange(Blue.ReferredToVariables());
            ret.AddRange(Alpha.ReferredToVariables());

            return ret;
        }

        public override bool Equals(object obj)
        {
            var asColor = obj as RGBAColorValue;
            if (asColor == null) return false;

            return
                asColor.Red == Red &&
                asColor.Green == Green &&
                asColor.Blue == Blue;
        }

        public override int GetHashCode()
        {
            return Red.GetHashCode() ^ (Green.GetHashCode() * -1) ^ (Blue.GetHashCode());
        }
    }

    class HSLColorValue : ColorValue
    {
        public override bool NeedsEvaluate { get { return Hue.NeedsEvaluate || Saturation.NeedsEvaluate || Lightness.NeedsEvaluate; } }

        public Value Hue { get; private set; }
        public Value Saturation { get; private set; }
        public Value Lightness { get; private set; }

        internal HSLColorValue(Value hue, Value saturation, Value lightness)
        {
            Hue = hue;
            Saturation = saturation;
            Lightness = lightness;
        }

        public override Value Bind(Scope scope)
        {
            return new HSLColorValue(Hue.Bind(scope), Saturation.Bind(scope), Lightness.Bind(scope));
        }

        internal override Value Evaluate()
        {
            return new HSLColorValue(Hue.Evaluate(), Saturation.Evaluate(), Lightness.Evaluate());
        }

        internal override void Write(TextWriter output)
        {
            var hue = Hue as NumberValue;
            var sat = Saturation as NumberWithUnitValue;
            var lit = Lightness as NumberWithUnitValue;
            if (hue == null) throw new InvalidOperationException("hue must be a number, found [" + Hue + "]");
            if (sat == null || sat.Unit != Unit.Percent) throw new InvalidOperationException("satuartion must be a percentage, found [" + Saturation + "]");
            if (lit == null || lit.Unit != Unit.Percent) throw new InvalidOperationException("lightness must be a percentage, found [" + Lightness + "]");

            var s = sat.Value;
            var l = lit.Value;

            var h = (int)hue.Value;
            h = (h % 360 + 360) % 360;

            if (s < 0) s = 0;
            if (s > 100) s = 100;

            if (l < 0) l = 0;
            if (l > 100) l = 100;

            output.Write("hsl(");
            output.Write(h);
            output.Write(',');
            output.Write(s);
            output.Write('%');
            output.Write(',');
            output.Write(l);
            output.Write('%');
            output.Write(')');
        }

        internal override List<string> ReferredToVariables()
        {
            var ret = new List<string>();

            ret.AddRange(Hue.ReferredToVariables());
            ret.AddRange(Saturation.ReferredToVariables());
            ret.AddRange(Lightness.ReferredToVariables());

            return ret;
        }

        public override bool Equals(object obj)
        {
            var asColor = obj as HSLColorValue;
            if (asColor == null) return false;

            return
                asColor.Hue == Hue &&
                asColor.Saturation == Saturation &&
                asColor.Lightness == Lightness;
        }

        public override int GetHashCode()
        {
            return Hue.GetHashCode() ^ (Saturation.GetHashCode() * -1) ^ (Lightness.GetHashCode());
        }
    }

    // Handles replacements into string values
    class StringEvalMap
    {
        private Dictionary<Tuple<int, int>, Value> EvaluateMap { get; set; }

        internal static StringEvalMap Parse(string value, int valStart, int valStop, string filePath)
        {
            if (value.IndexOf('@') == -1) return new StringEvalMap { EvaluateMap = new Dictionary<Tuple<int, int>, Value>() };

            var map = new Dictionary<Tuple<int, int>, Value>();

            int start = -1;
            bool inValue = false;
            var val = new StringBuilder();
            int depth = -1;

            for (int i = 0; i < value.Length; i++)
            {
                var c = value[i];

                if (c == '@' && !inValue)
                {
                    depth = 0;
                    start = i;
                    inValue = true;
                    val.Append(c);
                    continue;
                }

                if (inValue && c == '(')
                {
                    depth++;
                    val.Append(c);
                    continue;
                }

                if (inValue && c == ')' && depth > 0)
                {
                    depth--;
                    val.Append(c);

                    if (depth == 0)
                    {
                        inValue = false;

                        var str = val.ToString();
                        val.Clear();
                        if (str.StartsWith("@("))
                        {
                            str = str.Substring(2, str.Length - 3);
                        }

                        var moreVal = Value.Parse(str, valStart, valStop, filePath);
                        map[Tuple.Create(start, i)] = moreVal;
                    }
                    continue;
                }

                if (!char.IsLetterOrDigit(c) && inValue && depth == 0)
                {
                    inValue = false;

                    var str = val.ToString();
                    val.Clear();

                    var moreVal = Value.Parse(str, valStart, valStop, filePath);
                    map[Tuple.Create(start, i - 1)] = moreVal;
                    continue;
                }

                if (inValue)
                {
                    val.Append(c);
                }
            }

            return new StringEvalMap { EvaluateMap = map };
        }

        internal StringEvalMap Bind(Scope scope)
        {
            var ret = new Dictionary<Tuple<int, int>, Value>();

            foreach (var v in this.EvaluateMap)
            {
                ret[v.Key] = v.Value.Bind(scope);
            }

            return new StringEvalMap { EvaluateMap = ret };
        }

        internal string Evaluate(string value)
        {
            var ret = new StringBuilder();

            var startStop = EvaluateMap.ToDictionary(d => d.Key.Item1, d => d.Key);

            for (int i = 0; i < value.Length; i++)
            {
                if (startStop.ContainsKey(i))
                {
                    var seg = startStop[i];
                    var val = EvaluateMap[seg];

                    using (var writer = new StringWriter())
                    {
                        val.Evaluate().Write(writer);
                        ret.Append(writer.ToString());
                    }

                    i = seg.Item2;
                }
                else
                {
                    ret.Append(value[i]);
                }
            }

            return ret.ToString();
        }

        internal List<string> ReferredToVariables()
        {
            var ret = new List<string>();

            foreach (var v in EvaluateMap.Values)
            {
                ret.AddRange(v.ReferredToVariables());
            }

            return ret;
        }
    }

    // string constants
    class StringValue : Value
    {
        public string Value { get; private set; }

        internal StringEvalMap EvaluateMap { get; set; }

        public StringValue(string value)
        {
            Value = value;
            
            EvaluateMap = StringEvalMap.Parse(value, -1, -1, null);
        }

        public override Value Bind(Scope scope)
        {
            var @base = (StringValue)base.Bind(scope);

            @base.EvaluateMap = @base.EvaluateMap.Bind(scope);

            return @base;
        }

        internal override Value Evaluate()
        {
            if(EvaluateMap == null) return this;

            var val = EvaluateMap.Evaluate(this.Value);

            var evald = new StringValue(val);
            evald.Scope = this.Scope;

            return evald;
        }

        internal override void Write(TextWriter output)
        {
            output.Write(Value);
        }

        internal override List<string> ReferredToVariables()
        {
            return EvaluateMap.ReferredToVariables();
        }

        public override bool Equals(object obj)
        {
            var asStr = obj as StringValue;

            if (asStr == null) return false;

            return asStr.Value.Equals(Value, StringComparison.InvariantCultureIgnoreCase);
        }

        public override int GetHashCode()
        {
            return Value.ToLowerInvariant().GetHashCode();
        }
    }

    class QuotedStringValue : Value
    {
        public string Value { get; private set; }

        internal StringEvalMap EvaluateMap { get; set; }

        internal QuotedStringValue(string value)
        {
            Value = value;

            EvaluateMap = StringEvalMap.Parse(value, -1, -1, null);
        }

        public override Value Bind(Scope scope)
        {
            var @base = (QuotedStringValue)base.Bind(scope);

            @base.EvaluateMap = @base.EvaluateMap.Bind(scope);

            return @base;
        }

        internal override Value Evaluate()
        {
            if (EvaluateMap == null) return this;

            var val = EvaluateMap.Evaluate(this.Value);

            var evald = new QuotedStringValue(val);
            evald.Scope = this.Scope;

            return evald;
        }

        internal override void Write(TextWriter output)
        {
            if (Value.Contains('\''))
            {
                output.Write('\"');
                output.Write(Value);
                output.Write('\"');
                return;
            }

            output.Write('\'');
            output.Write(Value);
            output.Write('\'');
        }

        internal override List<string> ReferredToVariables()
        {
            return EvaluateMap.ReferredToVariables();
        }

        public override bool Equals(object obj)
        {
            var asStr = obj as QuotedStringValue;
            if (asStr == null) return false;

            return asStr.Value == Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }

    // background-position: 0 0 and so on
    class CompoundValue : Value
    {
        public override bool NeedsEvaluate { get { return Values.Any(a => a.NeedsEvaluate); } }

        public IEnumerable<Value> Values { get; private set; }
        
        internal CompoundValue(params Value[] values) : this(values.ToList()) { }

        internal CompoundValue(params IEnumerable<Value>[] values)
        {
            var vals = new List<Value>();
            foreach (var v in values) vals.AddRange(v);

            Values = vals.AsReadOnly();
        }

        public override bool IsImportant()
        {
            return Values.Any(a => a.IsImportant());
        }

        public override Value Bind(Scope scope)
        {
            var copyValues = new List<Value>(Values.Select(v => v.Bind(scope)));
            var ret = new CompoundValue(copyValues);
            ret.Scope = scope;

            return ret;
        }

        internal override Value Evaluate()
        {
            return new CompoundValue(Values.Select(s => s.Evaluate()).Where(s => s != ExcludeFromOutputValue.Singleton).ToList());
        }

        internal override void Write(TextWriter output)
        {
            Values.First().Write(output);

            foreach (var v in Values.Skip(1))
            {
                output.Write(' ');
                v.Write(output);
            }
        }

        internal override List<string> ReferredToVariables()
        {
            var ret = new List<string>();

            foreach (var value in Values)
            {
                ret.AddRange(value.ReferredToVariables());
            }

            return ret;
        }

        public override bool Equals(object obj)
        {
            var other = obj as CompoundValue;
            if (other == null) return false;

            if (other.Values.Count() != Values.Count()) return false;

            for (var i = 0; i < Values.Count(); i++)
            {
                if (!Values.ElementAt(i).Equals(other.Values.ElementAt(i))) return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            var ret = 0;
            for (var i = 0; i < Values.Count(); i++)
            {
                var vHash = Values.ElementAt(i).GetHashCode();

                var bytes = BitConverter.GetBytes(vHash).ToList();

                var rotateBy = i % bytes.Count;

                var firstI = bytes.Take(rotateBy);
                var theRest = bytes.Skip(rotateBy);

                var rotatedBytes = new List<byte>(4);
                rotatedBytes.AddRange(firstI);
                rotatedBytes.AddRange(theRest);

                var rotated = BitConverter.ToInt32(rotatedBytes.ToArray(), 0);

                ret ^= rotated;
            }

            return ret;
        }
    }

    // font-family: serif, sans-serif ; etc.
    class CommaDelimittedValue : Value
    {
        public override bool NeedsEvaluate { get { return Values.Any(a => a.NeedsEvaluate); } }

        public IEnumerable<Value> Values { get; private set; }

        internal CommaDelimittedValue(List<Value> values)
        {
            Values = values.AsReadOnly();
        }

        public override bool IsImportant()
        {
            return Values.Any(a => a.IsImportant());
        }

        public override Value Bind(Scope scope)
        {
            var values = new List<Value>(Values.Select(s => s.Bind(scope)));
            var ret = new CommaDelimittedValue(values);
            ret.Scope = scope;

            return ret;
        }
        
        internal override Value Evaluate()
        {
            return new CommaDelimittedValue(Values.Select(s => s.Evaluate()).Where(s => s != ExcludeFromOutputValue.Singleton).ToList());
        }

        internal override void Write(TextWriter output)
        {
            Values.First().Write(output);

            foreach (var v in Values.Skip(1))
            {
                output.Write(',');
                v.Write(output);
            }
        }

        internal override List<string> ReferredToVariables()
        {
            var ret = new List<string>();

            foreach (var value in Values)
            {
                ret.AddRange(value.ReferredToVariables());
            }

            return ret;
        }

        public override bool Equals(object obj)
        {
            var other = obj as CommaDelimittedValue;
            if (other == null) return false;

            if (other.Values.Count() != Values.Count()) return false;

            for (var i = 0; i < Values.Count(); i++)
            {
                if (!Values.ElementAt(i).Equals(other.Values.ElementAt(i))) return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            var ret = -1;
            for (var i = 0; i < Values.Count(); i++)
            {
                var vHash = Values.ElementAt(i).GetHashCode();

                var bytes = BitConverter.GetBytes(vHash).ToList();

                var rotateBy = i % bytes.Count;

                var firstI = bytes.Take(rotateBy);
                var theRest = bytes.Skip(rotateBy);

                var rotatedBytes = new List<byte>(4);
                rotatedBytes.AddRange(firstI);
                rotatedBytes.AddRange(theRest);

                var rotated = BitConverter.ToInt32(rotatedBytes.ToArray(), 0);

                ret ^= rotated;
            }

            return ret;
        }
    }

    class FormatValue : Value
    {
        public override bool NeedsEvaluate
        {
            get
            {
                return Value.NeedsEvaluate;
            }
        }
        public Value Value { get; private set; }

        internal FormatValue(Value val)
        {
            Value = val;
        }

        public override Value Bind(Scope scope)
        {
            return new FormatValue(Value.Bind(scope));
        }

        internal override Value Evaluate()
        {
            return new FormatValue(Value.Evaluate());
        }

        internal override List<string> ReferredToVariables()
        {
            return Value.ReferredToVariables();
        }

        internal override void Write(TextWriter output)
        {
            output.Write("format(");
            Value.Write(output);
            output.Write(')');
        }

        public override bool Equals(object obj)
        {
            var asFormat = obj as FormatValue;
            if (asFormat == null) return false;

            return asFormat.Value.Equals(Value);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }

    class LocalValue : Value
    {
        public override bool NeedsEvaluate
        {
            get
            {
                return Value.NeedsEvaluate;
            }
        }
        public Value Value { get; private set; }

        internal LocalValue(Value val)
        {
            Value = val;
        }

        public override Value Bind(Scope scope)
        {
            return new LocalValue(Value.Bind(scope));
        }

        internal override Value Evaluate()
        {
            return new LocalValue(Value.Evaluate());
        }

        internal override List<string> ReferredToVariables()
        {
            return Value.ReferredToVariables();
        }

        internal override void Write(TextWriter output)
        {
            output.Write("local(");
            Value.Write(output);
            output.Write(')');
        }

        public override bool Equals(object obj)
        {
            var asLocal = obj as LocalValue;
            if (asLocal == null) return false;

            return asLocal.Value.Equals(Value);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }

    class CycleValue : Value
    {
        public IEnumerable<Value> Values { get; private set; }

        public override bool NeedsEvaluate
        {
            get
            {
                return Values.Any(a => a.NeedsEvaluate);
            }
        }

        internal CycleValue(IEnumerable<Value> values)
        {
            Values = values.ToList().AsReadOnly();
        }

        public override Value Bind(Scope scope)
        {
            return new CycleValue(Values.Select(s => s.Bind(scope)));
        }

        internal override Value Evaluate()
        {
            return new CycleValue(Values.Select(s => s.Evaluate()));
        }

        internal override List<string> ReferredToVariables()
        {
            return Values.SelectMany(s => s.ReferredToVariables()).ToList();
        }

        public override bool Equals(object obj)
        {
            var other = obj as CycleValue;
            if (other == null) return false;

            var otherValues = other.Values;

            if (otherValues.Count() != Values.Count()) return false;

            for (var i = 0; i < Values.Count(); i++)
            {
                var otherVal = otherValues.ElementAt(i);
                var val = Values.ElementAt(i);

                if (!val.Equals(otherVal)) return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            var ret = 0;

            for(var i = 0; i < Values.Count(); i++)
            {
                var val = Values.ElementAt(i);
                ret ^= val.GetHashCode();

                if (i % 2 == 0) ret *= -1;
            }

            return ret;
        }

        internal override void Write(TextWriter output)
        {
            output.Write("cycle(");

            var first = Values.First();
            first.Write(output);

            foreach (var val in Values.Skip(1))
            {
                output.Write(',');
                val.Write(output);
            }

            output.Write(')');
        }
    }

    class AttributeValue : Value
    {
        public Value Attribute { get; private set; }

        public Value Type { get; private set; }

        public Value Fallback { get; private set; }

        public override bool NeedsEvaluate
        {
            get
            {
                return 
                    Attribute.NeedsEvaluate || 
                    (Type != null && Type.NeedsEvaluate) || 
                    (Fallback != null && Fallback.NeedsEvaluate);
            }
        }

        internal AttributeValue(Value attr, Value type, Value fallback)
        {
            Attribute = attr;
            Type = type;
            Fallback = fallback;
        }

        public override Value Bind(Scope scope)
        {
            return 
                new AttributeValue(
                    Attribute.Bind(scope), 
                    Type != null ? Type.Bind(scope) : null, 
                    Fallback != null ? Fallback.Bind(scope) : null
                );
        }

        internal override Value Evaluate()
        {
            return 
                new AttributeValue(
                    Attribute.Evaluate(), 
                    Type != null ? Type.Evaluate() : null,
                    Fallback != null ? Fallback.Evaluate() : null
                );
        }

        internal override List<string> ReferredToVariables()
        {
            var ret = Attribute.ReferredToVariables();
            
            if (Type != null)
            {
                ret.AddRange(Type.ReferredToVariables());
            }

            if (Fallback != null)
            {
                ret.AddRange(Fallback.ReferredToVariables());
            }

            return ret;
        }

        public override bool Equals(object obj)
        {
            var other = obj as AttributeValue;
            if (other == null) return false;

            var typeEquality = Type != null ? Type.Equals(other.Type) : other.Type == null;
            var fallbackEquality = Fallback != null ? Fallback.Equals(other.Fallback) : other.Fallback == null;

            return
                Attribute.Equals(other.Attribute) &&
                typeEquality &&
                fallbackEquality;
        }

        public override int GetHashCode()
        {
            var attrHash = Attribute.GetHashCode();
            var typeHash = Type != null ? Type.GetHashCode() : 0.GetHashCode();
            var fallbackHash = Fallback != null ? Fallback.GetHashCode() : 0.GetHashCode();

            return
                attrHash ^
                (-1 * typeHash) ^
                fallbackHash;
        }

        internal override void Write(TextWriter output)
        {
            output.Write("attr(");

            Attribute.Write(output);

            if (Type != null)
            {
                output.Write(' ');
                Type.Write(output);
            }

            if (Fallback != null)
            {
                output.Write(',');
                Fallback.Write(output);
            }

            output.Write(')');
        }
    }

    class CounterValue : Value
    {
        public Value Counter { get; private set; }
        public Value Style { get; private set; }

        public override bool NeedsEvaluate
        {
            get
            {
                return
                    Counter.NeedsEvaluate ||
                    Style != null ? Style.NeedsEvaluate : false;
            }
        }

        internal CounterValue(Value counter, Value style)
        {
            Counter = counter;
            Style = style;
        }

        public override Value Bind(Scope scope)
        {
            return
                new CounterValue(
                    Counter.Bind(scope),
                    Style != null ? Style.Bind(scope) : null
                );
        }

        internal override Value Evaluate()
        {
            return
                new CounterValue(
                    Counter.Evaluate(),
                    Style != null ? Style.Evaluate() : null
                );
        }

        internal override List<string> ReferredToVariables()
        {
            return
                Counter.ReferredToVariables()
                .Union(Style != null ? Style.ReferredToVariables() : Enumerable.Empty<string>())
                .ToList();
        }

        internal override void Write(TextWriter output)
        {
            output.Write("counter(");
            Counter.Write(output);
            if (Style != null)
            {
                output.Write(',');
                Style.Write(output);
            }
            output.Write(')');
        }

        public override bool Equals(object obj)
        {
            var other = obj as CounterValue;
            if (other == null) return false;

            return
                Counter.Equals(other.Counter) &&
                (Style == null && other.Style == null || Style.Equals(other.Style));
        }

        public override int GetHashCode()
        {
            return
                Counter.GetHashCode() ^
                (Style != null ? -1 * Style.GetHashCode() : 0);
        }
    }

    class CountersValue : Value
    {
        public Value Counter { get; private set; }
        public Value Style { get; private set; }

        public override bool NeedsEvaluate
        {
            get
            {
                return
                    Counter.NeedsEvaluate ||
                    Style != null ? Style.NeedsEvaluate : false;
            }
        }

        internal CountersValue(Value counter, Value style)
        {
            Counter = counter;
            Style = style;
        }

        internal override List<string> ReferredToVariables()
        {
            return
                Counter.ReferredToVariables()
                .Union(Style != null ? Style.ReferredToVariables() : Enumerable.Empty<string>())
                .ToList();
        }

        public override Value Bind(Scope scope)
        {
            return
                new CountersValue(
                    Counter.Bind(scope),
                    Style != null ? Style.Bind(scope) : null
                );
        }

        internal override Value Evaluate()
        {
            return
                new CountersValue(
                    Counter.Evaluate(),
                    Style != null ? Style.Evaluate() : null
                );
        }

        internal override void Write(TextWriter output)
        {
            output.Write("counters(");
            Counter.Write(output);
            if (Style != null)
            {
                output.Write(',');
                Style.Write(output);
            }
            output.Write(')');
        }

        public override int GetHashCode()
        {
            return
                -1 * (
                    Counter.GetHashCode() ^
                    (Style != null ? -1 * Style.GetHashCode() : 0)
                );
        }

        public override bool Equals(object obj)
        {
            var other = obj as CountersValue;
            if (other == null) return false;

            return
                Counter.Equals(other.Counter) &&
                (Style == null && other.Style == null || Style.Equals(other.Style));
        }
    }

    class CalcValue : Value
    {
        public StringValue Value { get; private set; }

        public override bool NeedsEvaluate
        {
            get
            {
                return Value.NeedsEvaluate;
            }
        }

        internal CalcValue(StringValue value)
        {
            Value = value;
        }

        public override Value Bind(Scope scope)
        {
            return new CalcValue((StringValue)Value.Bind(scope));
        }

        internal override Value Evaluate()
        {
            return new CalcValue((StringValue)Value.Evaluate());
        }

        internal override List<string> ReferredToVariables()
        {
            return Value.ReferredToVariables();
        }

        public override bool Equals(object obj)
        {
            var other = obj as CalcValue;
            if (other == null) return false;

            return Value.Equals(other.Value);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        internal override void Write(TextWriter output)
        {
            output.Write("calc(");
            Value.Write(output);
            output.Write(')');
        }
    }

    class StepsValue : Value
    {
        public Value NumberOfSteps { get; private set; }
        public Value Direction { get; private set; }

        public override bool NeedsEvaluate
        {
            get
            {
                return 
                    NumberOfSteps.NeedsEvaluate || 
                    Direction != null ? Direction.NeedsEvaluate : false;
            }
        }

        internal StepsValue(Value numSteps, Value dir)
        {
            NumberOfSteps = numSteps;
            Direction = dir;
        }

        public override Value Bind(Scope scope)
        {
            return 
                new StepsValue(
                    NumberOfSteps.Bind(scope), 
                    Direction != null ? Direction.Bind(scope) : null
                );
        }

        internal override Value Evaluate()
        {
            return 
                new StepsValue(
                    NumberOfSteps.Evaluate(), 
                    Direction != null ? Direction.Evaluate() : null
                );
        }

        internal override List<string> ReferredToVariables()
        {
            return 
                NumberOfSteps.ReferredToVariables()
                .Union(
                    Direction != null ? Direction.ReferredToVariables() : Enumerable.Empty<string>()
                ).ToList();
        }

        internal override void Write(TextWriter output)
        {
            output.Write("steps(");
            NumberOfSteps.Write(output);
            
            if (Direction != null)
            {
                output.Write(',');
                Direction.Write(output);
            }
            
            output.Write(')');
        }

        public override bool Equals(object obj)
        {
            var other = obj as StepsValue;
            if (other == null) return false;

            var dirEqual =
                other.Direction == null && Direction == null ||
                Direction != null && Direction.Equals(other.Direction);

            return
                NumberOfSteps.Equals(other.NumberOfSteps) &&
                dirEqual;
        }

        public override int GetHashCode()
        {
            return 
                NumberOfSteps.GetHashCode() ^ 
                (-1 * (Direction != null ? Direction.GetHashCode() : 0));
        }
    }

    class CubicBezierValue : Value
    {
        public Value X1 { get; private set; }
        public Value Y1 { get; private set; }

        public Value X2 { get; private set; }
        public Value Y2 { get; private set; }

        public override bool NeedsEvaluate
        {
            get
            {
                return X1.NeedsEvaluate || Y1.NeedsEvaluate || X2.NeedsEvaluate || Y2.NeedsEvaluate;
            }
        }

        internal CubicBezierValue(Value x1, Value y1, Value x2, Value y2)
        {
            X1 = x1;
            Y1 = y1;

            X2 = x2;
            Y2 = y2;
        }

        internal override List<string> ReferredToVariables()
        {
            return
                X1.ReferredToVariables()
                .Union(Y1.ReferredToVariables())
                .Union(X2.ReferredToVariables())
                .Union(Y2.ReferredToVariables())
                .ToList();
        }

        public override Value Bind(Scope scope)
        {
            return
                new CubicBezierValue(
                    X1.Bind(scope),
                    Y1.Bind(scope),
                    X2.Bind(scope),
                    Y2.Bind(scope)
                );
        }

        internal override Value Evaluate()
        {
            return
                new CubicBezierValue(
                    X1.Evaluate(),
                    Y1.Evaluate(),
                    X2.Evaluate(),
                    Y2.Evaluate()
                );
        }

        public override bool Equals(object obj)
        {
            var other = obj as CubicBezierValue;
            if (other == null) return false;

            return
                X1.Equals(other.X1) &&
                Y1.Equals(other.Y1) &&
                X2.Equals(other.X2) &&
                Y2.Equals(other.Y2);
        }

        public override int GetHashCode()
        {
            return
                X1.GetHashCode() ^
                (-1 * Y1.GetHashCode()) ^
                (1 + X2.GetHashCode()) ^
                (-1 * (Y2.GetHashCode() + 1));
        }

        internal override void Write(TextWriter output)
        {
            output.Write("cubic-bezier(");
            X1.Write(output);
            output.Write(',');
            Y1.Write(output);
            output.Write(',');
            X2.Write(output);
            output.Write(',');
            Y2.Write(output);
            output.Write(')');
        }
    }

    class UrlValue : Value
    {
        public Value UrlPath { get; private set; }

        internal UrlValue(Value path)
        {
            UrlPath = path;
        }

        public static UrlValue Parse(string raw, IPosition forPos)
        {
            int i = raw.IndexOf('(');
            var j = raw.IndexOf(')', i);

            var path = raw.Substring(i + 1, j - (i + 1)).Trim();

            return new UrlValue(MoreValueParser.Parse(path, Position.Create(forPos.Start + i, forPos.Stop + j, forPos.FilePath)));
        }

        public override Value Bind(Scope scope)
        {
            var ret = new UrlValue(UrlPath.Bind(scope));
            ret.Start = this.Start;
            ret.Start = this.Stop;
            ret.FilePath = this.FilePath;

            return ret;
        }

        internal override Value Evaluate()
        {
            var ret = new UrlValue(UrlPath.Evaluate());
            ret.Start = this.Start;
            ret.Start = this.Stop;
            ret.FilePath = this.FilePath;

            return ret;
        }

        internal override void Write(TextWriter output)
        {
            output.Write("url(");
            UrlPath.Write(output);
            output.Write(')');
        }

        internal override List<string> ReferredToVariables()
        {
            return new List<string>();
        }

        public override bool Equals(object obj)
        {
            var asUrl = obj as UrlValue;
            if (asUrl == null) return false;

            return asUrl.UrlPath.Equals(UrlPath);
        }

        public override int GetHashCode()
        {
            return UrlPath.GetHashCode();
        }
    }

    // Only valid in media queries, represents "30 / 13" or similar
    class RatioValue : Value
    {
        public Value LeftHand { get; private set; }
        public Value RightHand { get; private set; }

        internal RatioValue(Value lhs, Value rhs)
        {
            LeftHand = lhs;
            RightHand = rhs;
        }

        public override Value Bind(Scope scope)
        {
            return new RatioValue(LeftHand.Bind(scope), RightHand.Bind(scope));
        }

        internal override Value Evaluate()
        {
            return new RatioValue(LeftHand.Evaluate(), RightHand.Evaluate());
        }

        internal override List<string> ReferredToVariables()
        {
            var ret = new List<string>();

            ret.AddRange(LeftHand.ReferredToVariables());
            ret.AddRange(RightHand.ReferredToVariables());

            return ret;
        }

        internal override void Write(TextWriter output)
        {
            LeftHand.Write(output);
            output.Write('/');
            RightHand.Write(output);
        }

        public override bool Equals(object obj)
        {
            var asRatio = obj as RatioValue;
            if (asRatio == null) return false;

            return
                asRatio.LeftHand.Equals(LeftHand) &&
                asRatio.RightHand.Equals(RightHand);
        }

        public override int GetHashCode()
        {
            return LeftHand.GetHashCode() ^ (RightHand.GetHashCode() * -1);
        }
    }

    class LinearGradientValue : Value
    {
        public IEnumerable<Value> Parameters { get; private set; }

        public override bool NeedsEvaluate
        {
            get
            {
                return Parameters.Any(a => a.NeedsEvaluate);
            }
        }

        internal LinearGradientValue(IEnumerable<Value> @params)
        {
            Parameters = @params.ToList().AsReadOnly();
        }

        internal override List<string> ReferredToVariables()
        {
            return Parameters.SelectMany(c => c.ReferredToVariables()).ToList();
        }

        public override Value Bind(Scope scope)
        {
            return 
                new LinearGradientValue(
                    Parameters.Select(c => c.Bind(scope))
                );
        }

        internal override Value Evaluate()
        {
            return new LinearGradientValue(
                Parameters.Select(c => c.Evaluate())
            );
        }

        public override int GetHashCode()
        {
            var ret = 0;

            for (var i = 0; i < Parameters.Count(); i++)
            {
                ret ^= (Parameters.ElementAt(i).GetHashCode() * i);
            }

            return ret;
        }

        public override bool Equals(object obj)
        {
            var other = obj as LinearGradientValue;
            if (other == null) return false;

            if (Parameters.Count() != other.Parameters.Count()) return false;

            for (var i = 0; i < Parameters.Count(); i++)
            {
                if (!Parameters.ElementAt(i).Equals(other.Parameters.ElementAt(i))) return false;
            }

            return true;
        }

        internal override void Write(TextWriter output)
        {
            output.Write("linear-gradient(");

            var first = Parameters.First();
            first.Write(output);

            foreach (var color in Parameters.Skip(1))
            {
                output.Write(',');
                color.Write(output);
            }

            output.Write(')');
        }
    }

    class RadialGradientValue : Value
    {
        public IEnumerable<Value> Parameters { get; private set; }

        public override bool NeedsEvaluate
        {
            get
            {
                return Parameters.Any(a => a.NeedsEvaluate);
            }
        }

        internal RadialGradientValue(IEnumerable<Value> param)
        {
            Parameters = param.ToList().AsReadOnly();
        }

        internal override List<string> ReferredToVariables()
        {
            return Parameters.SelectMany(m => m.ReferredToVariables()).ToList();
        }

        public override Value Bind(Scope scope)
        {
            return new RadialGradientValue(Parameters.Select(s => s.Bind(scope)).ToList());
        }

        internal override Value Evaluate()
        {
            return new RadialGradientValue(Parameters.Select(s => s.Evaluate()).ToList());
        }

        public override bool Equals(object obj)
        {
            var other = obj as RadialGradientValue;
            if (other == null) return false;

            if (Parameters.Count() != other.Parameters.Count()) return false;

            for (var i = 0; i < Parameters.Count(); i++)
            {
                if (!Parameters.ElementAt(i).Equals(other.Parameters.ElementAt(i))) return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            var ret = 0;

            for (var i = 0; i < Parameters.Count(); i++)
            {
                ret ^= (i % 2 == 0 ? 1 : -1) * Parameters.ElementAt(i).GetHashCode();
            }

            return ret;
        }

        internal override void Write(TextWriter output)
        {
            output.Write("radial-gradient(");
            
            var first = Parameters.First();
            first.Write(output);

            foreach(var p in Parameters.Skip(1))
            {
                output.Write(',');
                p.Write(output);
            }

            output.Write(')');
        }
    }

    // @a?, (@a + 5)?, etc.
    class LeftExistsValue : Value
    {
        public override bool NeedsEvaluate { get { return true; } }

        public Value IfExists { get; private set; }

        internal LeftExistsValue(Value ifExists)
        {
            IfExists = ifExists;
        }

        public override Value Bind(Scope scope)
        {
            return new LeftExistsValue(IfExists.Bind(scope));
        }

        internal override Value Evaluate()
        {
            var value = IfExists.Evaluate();

            if (value is NotFoundValue) return ExcludeFromOutputValue.Singleton;

            return value;
        }

        internal override List<string> ReferredToVariables()
        {
            // This doesn't count as a proper reference, since it does not actually expect
            //    IfExists to, well, exist
            return new List<string>();
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return "(" + IfExists + ")?";
        }
    }

    // (@a + @b) * 3
    // 5px + @z
    // ... and similar
    class MathValue : Value
    {
        public override bool NeedsEvaluate { get { return true; } }

        public Value LeftHand { get; private set; }
        public Value RightHand { get; private set; }
        public Operator Operator { get; private set; }

        internal MathValue(Value lhs, Operator op, Value rhs)
        {
            LeftHand = lhs;
            Operator = op;
            RightHand = rhs;
        }

        public override Value Bind(Scope scope)
        {
            var ret = new MathValue(LeftHand.Bind(scope), Operator, RightHand.Bind(scope));
            ret.Scope = scope;

            return ret;
        }

        internal override Value Evaluate()
        {
            var lhs = LeftHand.Evaluate();
            var rhs = RightHand.Evaluate();

            switch (Operator)
            {
                case Model.Operator.Plus: return lhs + rhs;
                case Model.Operator.Minus: return lhs - rhs;
                case Model.Operator.Mult: return lhs * rhs;
                case Model.Operator.Div: return lhs / rhs;
                case Model.Operator.Mod: return lhs % rhs;
                case Model.Operator.Take_Exists: return !(lhs is NotFoundValue) ? lhs : rhs;
                default: throw new InvalidOperationException("Unknown operator " + Operator);
            }
        }

        internal override List<string> ReferredToVariables()
        {
            var ret = new List<string>();

            if (Operator == Model.Operator.Take_Exists) return ret;

            ret.AddRange(LeftHand.ReferredToVariables());
            ret.AddRange(RightHand.ReferredToVariables());

            return ret;
        }

        internal override void Write(TextWriter output)
        {
            LeftHand.Write(output);
            output.Write(' ');
            switch (Operator)
            {
                case Model.Operator.Div: output.Write('/'); break;
                case Model.Operator.Minus: output.Write('-'); break;
                case Model.Operator.Mod: output.Write('%'); break;
                case Model.Operator.Mult: output.Write('*'); break;
                case Model.Operator.Plus: output.Write('+'); break;
                case Model.Operator.Take_Exists: output.Write("??"); break;
                default: throw new InvalidOperationException("Unknown operator: " + Operator);
            }
            output.Write(' ');
            RightHand.Write(output);
        }
    }

    class NotFoundValue : Value
    {
        public static readonly NotFoundValue Default = new NotFoundValue();

        public NotFoundValue BindToPosition(int start, int stop, string filePath)
        {
            var copy = (NotFoundValue)this.MemberwiseClone();
            
            copy.Start = start;
            copy.Stop = stop;
            copy.FilePath = filePath;

            return copy;
        }

        internal override Value Evaluate()
        {
            return this;
        }

        internal override void Write(TextWriter output)
        {
            Current.RecordError(ErrorType.Compiler, this, "Value not found");
            throw new StoppedCompilingException();
        }
    }

    // Signals that any rule: value where value = ExcludeFromOutputValue should be omitted wholesale.
    //   Note that the only way to get this is via certain operators on NotFoundValue
    class ExcludeFromOutputValue : Value
    {
        public static readonly ExcludeFromOutputValue Singleton = new ExcludeFromOutputValue();

        private ExcludeFromOutputValue() { }

        internal override Value Evaluate()
        {
            return this;
        }
    }

    // !important
    class ImportantValue : Value
    {
        public static readonly ImportantValue Singleton = new ImportantValue();

        private ImportantValue() { }

        public override bool IsImportant()
        {
            return true;
        }

        internal override Value Evaluate()
        {
            return this;
        }

        internal override void Write(TextWriter output)
        {
            output.Write("!important");
        }

        internal override List<string> ReferredToVariables()
        {
            return new List<string>();
        }

        public override bool Equals(object obj)
        {
            return obj == Singleton;
        }

        public override int GetHashCode()
        {
            return 1;
        }
    }
}
