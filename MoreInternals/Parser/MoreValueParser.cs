using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;
using System.IO;

namespace MoreInternals.Parser
{
    class MoreValueParser
    {
        public static Value Parse(string value, IPosition forPosition, bool allowSelectorIncludes = false)
        {
            var ret = new List<Value>();

            using (var stream = new ParserStream(new StringReader(value)))
            {
                while (stream.HasMore())
                {
                    var buffer = new StringBuilder();
                    stream.ScanUntilWithNesting(buffer, ',', requireFound: false);
                    using (var subStream = new ParserStream(new StringReader(buffer.ToString())))
                    {
                        ret.Add(ParseImpl(subStream, forPosition, allowSelectorIncludes));
                    }
                }
            }

            if (ret.Count == 1) return ret[0];

            return new CommaDelimittedValue(ret);
        }

        private static MathValue ParseMathValue(char op, Value lhs, ParserStream stream, IPosition forPosition)
        {
            stream.Advance(); // skip operator

            if (lhs == null)
            {
                Current.RecordError(ErrorType.Parser, forPosition, "Expected value, found '" + op + "'");
                throw new StoppedParsingException();
            }

            Operator @operator;
            switch (op)
            {
                case '+': @operator = Operator.Plus; break;
                case '-': @operator = Operator.Minus; break;
                case '*': @operator = Operator.Mult; break;
                case '/': @operator = Operator.Div; break;
                case '%': @operator = Operator.Mod; break;
                default: throw new InvalidOperationException("Unexpected operator [" + op + "]");
            }

            var rhs = ParseImpl(stream, forPosition, allowSelectorIncludes: false);

            return new MathValue(lhs, @operator, rhs);
        }

        internal static Value Combine(Value cur, Value next)
        {
            if (cur == null) return next;

            var ret = new List<Value>();

            var left = cur as CompoundValue;
            if (left != null)
            {
                ret.AddRange(left.Values);
            }
            else
            {
                ret.Add(cur);
            }

            ret.Add(next);

            return new CompoundValue(ret);
        }

        private static Value ParseImportant(ParserStream stream)
        {
            var buffer = new StringBuilder();

            while (stream.HasMore() && (char.IsLetterOrDigit(stream.Peek()) || stream.Peek().In('!', '-', '_')))
            {
                buffer.Append(stream.Read());
            }

            var ret = buffer.ToString();

            if (ret.Equals("!important", StringComparison.InvariantCultureIgnoreCase)) return ImportantValue.Singleton;

            return new StringValue(ret);
        }

        private static Value ParseString(ParserStream stream, IPosition forPosition)
        {
            var c = stream.Peek();

            if (c == '!')
            {
                return ParseImportant(stream);
            }

            var buffer = new StringBuilder();
            while (stream.HasMore() && !char.IsWhiteSpace(stream.Peek()))
            {
                buffer.Append(stream.Read());

                if (buffer.Length == 3 && (stream.HasMore() && stream.Peek() == '('))
                {
                    var toDate = buffer.ToString();
                    if (toDate.Equals("rgb", StringComparison.InvariantCultureIgnoreCase) || toDate.Equals("hsl", StringComparison.InvariantCultureIgnoreCase))
                    {
                        var group = ParseGroup(stream, forPosition);
                        var @params = group.Value as CommaDelimittedValue;
                        if (@params == null || @params.Values.Count() != 3)
                        {
                            Current.RecordError(ErrorType.Parser, forPosition, "Expected 3 parameters to '" + toDate + "'");
                            throw new StoppedParsingException();
                        }

                        if(toDate == "rgb") 
                        {
                            return
                                new RGBColorValue(
                                    @params.Values.ElementAt(0),
                                    @params.Values.ElementAt(1),
                                    @params.Values.ElementAt(2)
                                );
                        }

                        if (toDate == "hsl")
                        {
                            return
                                new HSLColorValue
                                (
                                    @params.Values.ElementAt(0),
                                    @params.Values.ElementAt(1),
                                    @params.Values.ElementAt(2)
                                );
                        }
                    }

                    if (toDate.Equals("url", StringComparison.InvariantCultureIgnoreCase))
                    {
                        var value = ParseGroup(stream, forPosition).Value;

                        if(value is StringValue || value is QuotedStringValue)
                        {
                            return new UrlValue(value);
                        }

                        Current.RecordError(ErrorType.Parser, forPosition, "Expected string or quoted string");
                        throw new StoppedParsingException();
                    }
                }

                if (buffer.Length == 4 && (stream.HasMore() && stream.Peek() == '('))
                {
                    var toDate = buffer.ToString();
                    if (toDate.Equals("rgba", StringComparison.InvariantCultureIgnoreCase))
                    {
                        var @params = ParseGroup(stream, forPosition).Value as CommaDelimittedValue;
                        if (@params == null || @params.Values.Count() != 4)
                        {
                            Current.RecordError(ErrorType.Parser, forPosition, "Expected 4 parameters to '" + toDate + "'");
                            throw new StoppedParsingException();
                        }

                        return
                            new RGBAColorValue(
                                @params.Values.ElementAt(0),
                                @params.Values.ElementAt(1),
                                @params.Values.ElementAt(2),
                                @params.Values.ElementAt(3)
                            );
                    }

                    if (toDate.Equals("attr", StringComparison.InvariantCultureIgnoreCase))
                    {
                        var @params = ParseGroup(stream, forPosition).Value;

                        Value attrAndType, attr, type, fallback;

                        var comma = @params as CommaDelimittedValue;
                        if (comma != null)
                        {
                            if (comma.Values.Count() > 2)
                            {
                                Current.RecordError(ErrorType.Parser, forPosition, "attr expects 1 or 2 parameters, found " + comma);
                                throw new StoppedParsingException();
                            }

                            attrAndType = comma.Values.ElementAt(0);

                            fallback = comma.Values.Count() == 2 ? comma.Values.ElementAt(1) : null;
                        }
                        else
                        {
                            attrAndType = @params;
                            fallback = null;
                        }

                        var compound = attrAndType as CompoundValue;
                        if (compound != null)
                        {
                            if (compound.Values.Count() > 2)
                            {
                                Current.RecordError(ErrorType.Parser, forPosition, "attr expects an attribute name and optionally a type, found " + compound);
                                throw new StoppedParsingException();
                            }

                            attr = compound.Values.ElementAt(0);
                            type = compound.Values.Count() == 2 ? compound.Values.ElementAt(1) : null;
                        }
                        else
                        {
                            attr = attrAndType;
                            type = null;
                        }

                        return new AttributeValue(attr, type, fallback);
                    }
                }

                if (buffer.Length == 5 && (stream.HasMore() && stream.Peek() == '('))
                {
                    var toDate = buffer.ToString();
                    if (toDate.Equals("local", StringComparison.InvariantCultureIgnoreCase))
                    {
                        var val = ParseGroup(stream, forPosition).Value;
                        var comma = val as CommaDelimittedValue;
                        if (comma != null)
                        {
                            Current.RecordError(ErrorType.Parser, forPosition, "Expected 1 parameter to local() value, found " + comma.Values.Count());
                            throw new StoppedParsingException();
                        }

                        return new LocalValue(val);
                    }

                    if (toDate.Equals("cycle", StringComparison.InvariantCultureIgnoreCase))
                    {
                        var val = ParseGroup(stream, forPosition).Value;
                        var param = new List<Value>();

                        var comma = val as CommaDelimittedValue;
                        if (comma != null)
                        {
                            param.AddRange(comma.Values);
                        }
                        else
                        {
                            param.Add(val);
                        }

                        return new CycleValue(param);
                    }
                }

                if (buffer.Length == 6 && (stream.HasMore() && stream.Peek() == '('))
                {
                    var toDate = buffer.ToString();
                    if (toDate.Equals("format", StringComparison.InvariantCultureIgnoreCase))
                    {
                        var val = ParseGroup(stream, forPosition).Value;
                        var comma = val as CommaDelimittedValue;
                        if (comma != null)
                        {
                            Current.RecordError(ErrorType.Parser, forPosition, "Expected 1 parameter to format() value, found " + comma.Values.Count());
                            throw new StoppedParsingException();
                        }

                        return new FormatValue(val);
                    }
                }
            }

            NamedColor color;
            if (Enum.TryParse<NamedColor>(buffer.ToString(), ignoreCase: true, result: out color))
            {
                return new NamedColorValue(color);
            }

            var str = buffer.ToString();

            if (str.Contains("!"))
            {
                var i = str.IndexOf('!');
                var left = str.Substring(0, i);
                var right = str.Substring(i);
                
                var ret = new List<Value>();
                var lhs = Parse(left, forPosition);
                var rhs = Parse(right, forPosition);

                if (lhs is CompoundValue)
                {
                    ret.AddRange(((CompoundValue)lhs).Values);
                }
                else
                {
                    ret.Add(lhs);
                }

                if (rhs is CompoundValue)
                {
                    ret.AddRange(((CompoundValue)rhs).Values);
                }
                else
                {
                    ret.Add(rhs);
                }

                return new CompoundValue(ret);
            }

            return new StringValue(buffer.ToString());
        }

        internal static GroupedValue ParseGroup(ParserStream stream, IPosition forPosition)
        {
            var toParse = new StringBuilder();

            stream.Advance(); // skip (
            stream.ScanUntilWithNesting(toParse, ')');

            var group = toParse.ToString().Trim();
            var ret = Parse(group, forPosition);

            return new GroupedValue(ret);
        }

        internal static Value ParseFuncValue(ParserStream stream, IPosition forPosition, bool allowSelectorIncludes)
        {
            stream.Advance(); // skip @
            var buffer = new StringBuilder();

            var c = stream.Peek();

            while (char.IsWhiteSpace(c))
            {
                stream.Advance();
                c = stream.Peek();
            }

            if (!char.IsLetter(c) && c != '(')
            {
                Current.RecordError(ErrorType.Parser, forPosition, "Expected letter or '(', found '" + c + "'");
                throw new StoppedParsingException();
            }

            if (c != '(')
            {
                buffer.Append(stream.Read());

                while (stream.HasMore() && char.IsLetterOrDigit(stream.Peek()))
                {
                    buffer.Append(stream.Read());
                }
            }

            var funcName = buffer.ToString();
            buffer.Clear();

            while (stream.HasMore() && char.IsWhiteSpace(stream.Peek()))
            {
                buffer.Append(stream.Read());
            }

            if (stream.HasMore() && stream.Peek() == '(')
            {
                stream.Read(); // Skip (

                buffer.Clear();
                stream.ScanUntilWithNesting(buffer, ')');

                if (funcName.Length == 0)
                {
                    if (allowSelectorIncludes)
                    {
                        var sel = Selector.Parse(buffer.ToString(), forPosition.Start, forPosition.Stop, forPosition.FilePath);
                        return new IncludeSelectorValue(sel);
                    }

                    return new StringValue("@(" + buffer + ")");
                }

                var value = MoreValueParser.Parse(buffer.ToString().Trim(), forPosition, allowSelectorIncludes: true);
                if (value is NotFoundValue) throw new StoppedParsingException();

                var @params = new List<Value>();
                if (value is CommaDelimittedValue)
                {
                    @params.AddRange(((CommaDelimittedValue)value).Values);
                }
                else
                {
                    @params.Add(value);
                }

                return new FuncAppliationValue(funcName, @params);
            }

            return new FuncValue(funcName);
        }

        internal static ColorValue ParseHashColor(ParserStream stream, IPosition forPosition)
        {
            stream.Advance(); // skip #
            var buffer = new StringBuilder();

            while (buffer.Length < 6 && stream.HasMore() && char.ToLower(stream.Peek()).In('a', 'b', 'c', 'd', 'e', 'f', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0'))
            {
                buffer.Append(stream.Read());
            }

            if (buffer.Length != 3 && buffer.Length != 6)
            {
                Current.RecordError(ErrorType.Parser, forPosition, "Expected 3 or 6 hexidecimal characters");
                throw new StoppedParsingException();
            }

            if (buffer.Length == 3)
            {
                return HexTripleColorValue.Parse(buffer.ToString());
            }

            return HexSextupleColorValue.Parse(buffer.ToString());
        }

        private static QuotedStringValue ParseQuotedString(char quote, ParserStream stream, IPosition forPosition)
        {
            stream.Advance(); // skip the quote
            var buffer = new StringBuilder();

            var x = stream.ScanUntil(buffer, quote);

            if (x == null)
            {
                Current.RecordError(ErrorType.Parser, forPosition, "Expected '" + quote + "'");
                throw new StoppedParsingException();
            }

            return new QuotedStringValue(buffer.ToString());
        }

        private static Dictionary<string, Unit> UnitTwoLetterDictionary = Enum.GetNames(typeof(Unit)).Where(s => s.Length == 2).ToDictionary(k => k, v => (Unit)Enum.Parse(typeof(Unit), v));

        private static Unit? ParsePossibleUnit(string inStr, out List<char> pushBack)
        {
            if (inStr.StartsWith("%"))
            {
                pushBack = inStr.Skip(1).ToList();
                return Unit.Percent;
            }

            if (inStr.StartsWith("s", StringComparison.InvariantCultureIgnoreCase))
            {
                pushBack = inStr.Skip(1).ToList();
                return Unit.S;
            }

            foreach (var name in UnitTwoLetterDictionary.Keys)
            {
                if (inStr.StartsWith(name, StringComparison.InvariantCultureIgnoreCase))
                {
                    pushBack = inStr.Skip(2).ToList();
                    return UnitTwoLetterDictionary[name];
                }
            }

            if (inStr.StartsWith("khz", StringComparison.InvariantCultureIgnoreCase))
            {
                pushBack = inStr.Skip(3).ToList();
                return Unit.KHZ;
            }

            if (inStr.StartsWith("rem", StringComparison.InvariantCultureIgnoreCase))
            {
                pushBack = inStr.Skip(3).ToList();
                return Unit.REM;
            }

            if (inStr.StartsWith("deg", StringComparison.InvariantCultureIgnoreCase))
            {
                pushBack = inStr.Skip(3).ToList();
                return Unit.DEG;
            }

            if (inStr.StartsWith("rad", StringComparison.InvariantCultureIgnoreCase))
            {
                pushBack = inStr.Skip(3).ToList();
                return Unit.RAD;
            }

            if (inStr.StartsWith("dpi", StringComparison.InvariantCultureIgnoreCase))
            {
                pushBack = inStr.Skip(3).ToList();
                return Unit.DPI;
            }

            if (inStr.StartsWith("dpcm", StringComparison.InvariantCultureIgnoreCase))
            {
                pushBack = inStr.Skip(4).ToList();
                return Unit.DPCM;
            }

            if (inStr.StartsWith("grad", StringComparison.InvariantCultureIgnoreCase))
            {
                pushBack = inStr.Skip(4).ToList();
                return Unit.GRAD;
            }

            if (inStr.StartsWith("turn", StringComparison.InvariantCultureIgnoreCase))
            {
                pushBack = inStr.Skip(4).ToList();
                return Unit.TURN;
            }

            pushBack = inStr.ToList();
            return null;
        }

        private static Value ParseNumber(ParserStream stream, IPosition forPosition)
        {
            var pushbackBuffer = new StringBuilder();

            var buffer = new StringBuilder();

            bool negate = false;

            if (stream.Peek() == '-')
            {
                negate = true;
                stream.Advance();
                pushbackBuffer.Append('-');
            }

            bool decimalPassed = false;

            while (stream.HasMore() && (char.IsDigit(stream.Peek()) || (stream.Peek() == '.' && !decimalPassed)))
            {
                var c = stream.Read();
                buffer.Append(c);
                pushbackBuffer.Append(c);

                if (c == '.') decimalPassed = true;
            }

            Unit? unit = null;
            decimal digit;
            if (!decimal.TryParse(buffer.ToString(), out digit))
            {
                // Looked like a number, but wasn't!
                stream.PushBack(pushbackBuffer.ToString());
                return ParseString(stream, forPosition);
            }

            if (negate) digit *= -1m;

            buffer.Clear();
            while (stream.HasMore() && char.IsWhiteSpace(stream.Peek()))
            {
                buffer.Append(stream.Read());
            }

            var nextFour = new StringBuilder();
            for (int i = 0; i < 4; i++)
            {
                if (stream.HasMore())
                {
                    nextFour.Append(stream.Read());
                }
            }

            var possibleUnit = nextFour.ToString();
            List<char> pushBack;

            unit = ParsePossibleUnit(possibleUnit, out pushBack);

            if (unit == null)
            {
                stream.PushBack(nextFour.ToString());
                stream.PushBack(buffer.ToString());
            }
            else
            {
                stream.PushBack(pushBack);
                return new NumberWithUnitValue(digit, unit.Value);
            }

            return new NumberValue(digit);
        }

        internal static Value ParseImpl(ParserStream stream, IPosition forPosition, bool allowSelectorIncludes)
        {
            Value ret = null;

            while (stream.HasMore())
            {
                var c = stream.Peek();

                if (char.IsWhiteSpace(c))
                {
                    stream.AdvancePastWhiteSpace();
                    continue;
                }

                if (ret != null)
                {
                    if (c.In('+', '-', '*', '/', '%'))
                    {
                        ret = ParseMathValue(c, ret, stream, forPosition);
                        continue;
                    }

                    if (c == '?')
                    {
                        stream.Advance(); // skip ?
                        if (stream.HasMore() && stream.Peek() == '?')
                        {
                            if (ret == null)
                            {
                                Current.RecordError(ErrorType.Parser, forPosition, "Expected value, found '??'");
                                throw new StoppedParsingException();
                            }

                            stream.Advance(); // skip second ?
                            var rhs = ParseImpl(stream, forPosition, allowSelectorIncludes);
                            ret = new MathValue(ret, Operator.Take_Exists, rhs);
                            continue;
                        }

                        ret = new LeftExistsValue(ret);
                        continue;
                    }
                }

                if (char.IsDigit(c) || c == '.' || c == '-')
                {
                    ret = Combine(ret, ParseNumber(stream, forPosition));
                    continue;
                }

                if (c == '(')
                {
                    ret = Combine(ret, ParseGroup(stream, forPosition));
                    continue;
                }

                if (c.In('\'', '"'))
                {
                    ret = Combine(ret, ParseQuotedString(c, stream, forPosition));
                    continue;
                }

                if (c == '#')
                {
                    ret = Combine(ret, ParseHashColor(stream, forPosition));
                    continue;
                }

                if (c == '@')
                {
                    ret = Combine(ret, ParseFuncValue(stream, forPosition, allowSelectorIncludes));
                    continue;
                }

                ret = Combine(ret, ParseString(stream, forPosition));
            }

            return ret;
        }
    }
}
