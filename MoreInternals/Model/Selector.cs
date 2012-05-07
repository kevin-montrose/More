using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using MoreInternals.Parser;
using MoreInternals.Helpers;
using System.Diagnostics.CodeAnalysis;

namespace MoreInternals.Model
{
    /* Selector = NAME [Selector] | 
                  ClassSelector [Selector] | 
		          WILDCARD [Selector] | 
		          IdSelector [Selector] | 
		          CHILD Selector |
		          Pseudo [Selector] |
		          AttributeSelector [Selector] |
		          PLUS Selector |
		          COMMA Selector
     */
    class Selector : IPosition, IWritable
    {
        public int Start { get; protected set; }
        public int Stop { get; protected set; }
        public string FilePath { get; protected set; }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var other = obj as Selector;
            if (other == null) return false;

            return ToString().Equals(other.ToString());
        }

        private static int _NextIndexOf(string raw, int start, params char[] of)
        {
            for (int i = start; i < raw.Length; i++)
            {
                if (of.Contains(raw[i])) return i;
            }

            return -1;
        }

        private static Selector ParseRawCompoundSelector(string raw, int start, int stop, string filePath)
        {
            if (raw[0] == '&')
            {
                return new ConcatWithParentSelector(ParseRawCompoundSelector(raw.Substring(1), start, stop, filePath), start, stop, filePath);
            }

            var ret = new List<Selector>();

            for (int i = 0; i < raw.Length; i++)
            {
                var c = raw[i];

                if (c == '*')
                {
                    ret.Add(WildcardSelector.Singleton);
                    continue;
                }

                var j = _NextIndexOf(raw, i + 1, '#', ':', '.', '[', ']');
                if (j == -1) j = raw.Length;

                var name = raw.Substring(i, j - i);

                if (name.StartsWith(":not(") && name[name.Length - 1] != ')')
                {
                    j = raw.IndexOf(')', j);
                    if (j == -1) throw new InvalidOperationException("Expected to find closing )");
                    j++;
                    name = raw.Substring(i, j - i);
                }

                i = j - 1;

                if (c == '#')
                {
                    ret.Add(new IdSelector(name.Substring(1), start, stop, filePath));
                    continue;
                }

                if (c == '.')
                {
                    ret.Add(new ClassSelector(name.Substring(1), start, stop, filePath));
                    continue;
                }

                if (c == ':')
                {
                    ret.Add(PseudoClassSelector.Parse(name, start, stop, filePath));

                    continue;
                }

                if (c == '[')
                {
                    ret.Add(AttributeSelector.Parse(name, start, stop, filePath));
                    i++;
                    continue;
                }

                ret.Add(new ElementSelector(name, start, stop, filePath));
            }

            if (ret.Count == 1) return ret[0];

            return new ConcatSelector(ret, start, stop, filePath);
        }

        private static Selector ParseRawCommaDelimittedSelector(string[] parts, int start, int stop, string filePath)
        {
            var ret = new List<Selector>();

            foreach (var raw in parts)
            {
                ret.Add(ParseRawSelector(raw, start, stop, filePath));
            }

            return new MultiSelector(ret, start, stop, filePath);
        }

        private static Selector ParseRawChildSelector(string[] parts, int start, int stop, string filePath)
        {
            if (parts.Length <= 2)
            {
                Selector parent, child;

                if (parts.Length == 1)
                {
                    parent = WildcardSelector.Singleton;
                    child = ParseRawSelector(parts[0], start, stop, filePath);
                }
                else
                {
                    parent = ParseRawSelector(parts[0], start, stop, filePath);
                    child = ParseRawSelector(parts[1], start, stop, filePath);
                }

                return new ChildSelector(parent, child, start, stop, filePath);
            }

            var left = ParseRawSelector(parts[0], start, stop, filePath);

            return new ChildSelector(left, ParseRawChildSelector(parts.Skip(1).ToArray(), start, stop, filePath), start, stop, filePath);
        }

        private static Selector ParseRawSiblingSelector(string[] parts, int start, int stop, string filePath)
        {
            if (parts.Length != 2)
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start, stop, filePath), "Sibling selectors can only have 2 components");
                throw new StoppedParsingException();
            }

            var older = ParseRawSelector(parts[0], start, stop, filePath);
            var younger = ParseRawSelector(parts[1], start, stop, filePath);

            return new AdjacentSiblingSelector(older, younger, start, stop, filePath);
        }

        private static Selector ParseRawSelector(string raw, int start, int stop, string filePath)
        {
            raw = raw.Trim();

            if (raw.Contains(',')) return ParseRawCommaDelimittedSelector(raw.Split(','), start, stop, filePath);
            if (raw.Contains('>')) return ParseRawChildSelector(raw.Split('>'), start, stop, filePath);
            if (raw.Contains('+')) return ParseRawSiblingSelector(raw.Split('+'), start, stop, filePath);

            var parts = new List<Selector>();

            foreach (var x in raw.Split(' '))
            {
                if (x.Trim().Length == 0) continue;

                parts.Add(ParseRawCompoundSelector(x, start, stop, filePath));
            }

            if (parts.Count == 1) { return parts[0]; }

            var ret = parts[parts.Count - 1];

            for (int i = parts.Count - 2; i >= 0; i--)
            {
                ret = CompoundSelector.CombineSelectors(parts[i], ret, start, stop, filePath);
            }

            return ret;
        }

        public static Selector Parse(string raw, int start, int stop, string filePath)
        {
            return ParseRawSelector(raw, start, stop, filePath);
        }

        public virtual void Write(TextWriter output)
        {
            throw new NotImplementedException();
        }

        public void Write(ICssWriter output)
        {
            output.WriteSelector(this);
        }
    }

    class ConcatWithParentSelector : Selector
    {
        public Selector Selector { get; private set; }

        public ConcatWithParentSelector(Selector sel, int start, int stop, string filePath)
        {
            Selector = sel;
            Start = start;
            Stop = stop;
            FilePath = filePath;
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return "&" + Selector.ToString();
        }

    }

    class ConcatSelector : Selector
    {
        public IEnumerable<Selector> Selectors { get; set; }

        public ConcatSelector(List<Selector> selectors, int start, int stop, string filePath)
        {
            Selectors = selectors.AsReadOnly();
            Start = start;
            Stop = stop;
            FilePath = filePath;
        }

        public override void Write(TextWriter output)
        {
            foreach (var s in Selectors)
            {
                s.Write(output);
            }
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            var ret = "";
            foreach (var s in Selectors)
            {
                ret += s;
            }

            return ret;
        }

    }

    enum AttributeOperator
    {
        Equals,   // =
        Contains, // ~=
        Starts    // |=
    }

    // AttributeSelector = START_ATTR NAME [AttrMatch CssValue] END_ATTR
    class AttributeSelector : Selector
    {
        public string Attribute { get; protected set; }

        public static new AttributeSelector Parse(string raw, int start, int stop, string filePath)
        {
            raw = raw.Trim('[', ']');

            var i = raw.IndexOf('=');

            if (i == -1) return new AttributeSetSelector(raw, start, stop, filePath);

            if (raw[i - 1] == '~') return new AttributeOperatorSelector(raw.Substring(0, i - 1), AttributeOperator.Contains, raw.Substring(i - 1 + 2), start, stop, filePath);
            if (raw[i - 1] == '|') return new AttributeOperatorSelector(raw.Substring(0, i - 1), AttributeOperator.Starts, raw.Substring(i - 1 + 2), start, stop, filePath);

            return new AttributeOperatorSelector(raw.Substring(0, i), AttributeOperator.Equals, raw.Substring(i + 1), start, stop, filePath);
        }
    }

    class AttributeSetSelector : AttributeSelector
    {
        public AttributeSetSelector(string attr, int start, int stop, string filePath)
        {
            Attribute = attr;
            Start = start;
            Stop = stop;
            FilePath = filePath;
        }

        public override void Write(TextWriter output)
        {
            output.Write('[');
            output.Write(Attribute);
            output.Write(']');
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return "[" + Attribute + "]";
        }

    }

    class AttributeOperatorSelector : AttributeSelector
    {
        public AttributeOperator Operator { get; private set; }
        public string Value { get; private set; }

        public AttributeOperatorSelector(string attr, AttributeOperator op, string value, int start, int stop, string filePath)
        {
            Attribute = attr;
            Operator = op;
            Value = value;

            Start = start;
            Stop = stop;
            FilePath = filePath;
        }

        public override void Write(TextWriter output)
        {
            output.Write('[');
            output.Write(Attribute);
            switch (Operator)
            {
                case AttributeOperator.Contains: output.Write("~="); break;
                case AttributeOperator.Equals: output.Write('='); break;
                case AttributeOperator.Starts: output.Write("|="); break;
                default: throw new InvalidOperationException("Unknown Attribute Operator [" + Operator + "]");
            }
            output.Write(Value);
            output.Write(']');
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            var ret = "[" + Attribute;

            switch (Operator)
            {
                case AttributeOperator.Contains: ret += "~="; break;
                case AttributeOperator.Equals: ret += '='; break;
                case AttributeOperator.Starts: ret += "|="; break;
                default: throw new InvalidOperationException("Unknown Attribute Operator [" + Operator + "]");
            }
            ret += Value + "]";

            return ret;
        }
    }

    /// <summary>
    /// A compound selector
    /// 
    /// The Outer selector is further constrained by the Inner one
    /// 
    /// So, [.hello] [.world] matches all .world(s) in .hello(s).
    /// 
    /// Technically, all selectors are compound selectors descending from
    /// the Wildcard selector.
    /// </summary>
    class CompoundSelector : Selector
    {
        public Selector Outer { get; private set; }
        public Selector Inner { get; private set; }

        private CompoundSelector(Selector outer, Selector inner, int start, int stop, string filePath)
        {
            Outer = outer;
            Inner = inner;

            Start = start;
            Stop = stop;
            FilePath = filePath;
        }

        public static Selector CombineSelectors(Selector outer, Selector inner, int start, int stop, string filePath)
        {
            if (outer is MultiSelector) throw new InvalidOperationException("Cannot combine MultiSelectors, outer was a MultiSelector");
            if (inner is MultiSelector) throw new InvalidOperationException("Cannot combine MultiSelectors, inner was a MultiSelector");

            var outerConcat = outer as ConcatWithParentSelector;
            var innerConcat = inner as ConcatWithParentSelector;

            if (outerConcat == null && innerConcat == null) return new CompoundSelector(outer, inner, start, stop, filePath);

            if (outerConcat == null)
            {
                return new ConcatSelector(new List<Selector>(){outer, innerConcat.Selector}, start, stop, filePath);
            }

            if (innerConcat == null)
            {
                return new ConcatWithParentSelector(new CompoundSelector(outerConcat.Selector, inner, start, stop, filePath), start, stop, filePath);
            }

            return new ConcatWithParentSelector(new ConcatSelector(new List<Selector>() { outerConcat.Selector, innerConcat.Selector }, start, stop, filePath), start, stop, filePath);
        }

        public override void Write(TextWriter output)
        {
            Outer.Write(output);
            output.Write(' ');
            Inner.Write(output);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return Outer.ToString() + " " + Inner.ToString();
        }

    }

    /// <summary>
    /// Matches a list of selectors.
    /// 
    /// [.hello] [.world] matches all elements with either .hello or .world on them..
    /// </summary>
    class MultiSelector : Selector
    {
        public IEnumerable<Selector> Selectors { get; private set; }

        public MultiSelector(IEnumerable<Selector> selectors, int start, int stop, string filePath)
        {
            Selectors = selectors.ToList().AsReadOnly();

            Start = start;
            Stop = stop;
            FilePath = filePath;
        }

        public override void Write(TextWriter output)
        {
            Selectors.First().Write(output);

            foreach (var selector in Selectors.Skip(1))
            {
                output.Write(',');
                selector.Write(output);
            }
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return string.Join(", ", Selectors);
        }

    }

    // http://www.w3.org/TR/CSS2/selector.html#adjacent-selectors
    class AdjacentSiblingSelector : Selector
    {
        public Selector Older { get; private set; }
        public Selector Younger { get; private set; }

        public AdjacentSiblingSelector(Selector older, Selector younger, int start, int stop, string filePath)
        {
            Older = older;
            Younger = younger;

            Start = start;
            Stop = stop;
            FilePath = filePath;
        }

        public override void Write(TextWriter output)
        {
            Older.Write(output);
            output.Write('+');
            Younger.Write(output);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return Older + "+" + Younger;
        }

    }

    class ClassSelector : Selector
    {
        public string Name { get; private set; }

        public ClassSelector(string name, int start, int stop, string filePath)
        {
            Name = name;
            Start = start;
            Stop = stop;

            FilePath = filePath;
        }

        public override void Write(TextWriter output)
        {
            output.Write('.');
            output.Write(Name);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return "." + Name;
        }
    }

    /*
     * IdSelector = ID NAME | HEX_COLOR [IdSelectorTail];
     * IdSelectorTail = NAME [IdSelectorTail] | NUMBER [IdSelectorTail];
     */
    class IdSelector : Selector
    {
        public string Name { get; private set; }

        public IdSelector(string name, int start, int stop, string filePath)
        {
            Name = name;

            Start = start;
            Stop = stop;
            FilePath = filePath;
        }

        public override void Write(TextWriter output)
        {
            output.Write('#');
            output.Write(Name);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return "#" + Name;
        }
    }

    class ElementSelector : Selector
    {
        public string Name { get; private set; }

        public ElementSelector(string name, int start, int stop, string filePath)
        {
            Name = name;
            Start = start;
            Stop = stop;
            FilePath = filePath;
        }

        public override void Write(TextWriter output)
        {
            output.Write(Name);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return Name;
        }
    }

    // Pseudo = LINK | VISITED | ACTIVE | HOVER | FOCUS | FIRST_LETTER | FIRST_LINE | FIRST_CHILD | BEFORE | AFTER | LANG | NTH_CHILD | LAST_CHILD | EMPTY | NOT | CHECKED | DISABLED | NTH_LAST_CHILD | NTH_OF_TYPE | NTH_LAST_TYPE | FIRST_OF_TYPE | LAST_OF_TYPE | ONLY_CHILD | ONLY_OF_TYPE | ROOT | TARGET | ENABLED | DEFAULT | VALID | INVALID | IN_RANGE | OUT_OF_RANGE | REQUIRED | OPTIONAL | READ_ONLY | READ_WRITE
    class PseudoClassSelector : Selector
    {
        private string Name { get; set; }

        public static readonly PseudoClassSelector LinkSingleton = new PseudoClassSelector("link");
        public static readonly PseudoClassSelector VisitedSingleton = new PseudoClassSelector("visited");
        public static readonly PseudoClassSelector ActiveSingleton = new PseudoClassSelector("active");
        public static readonly PseudoClassSelector HoverSingleton = new PseudoClassSelector("hover");
        public static readonly PseudoClassSelector FocusSingleton = new PseudoClassSelector("focus");
        public static readonly PseudoClassSelector FirstLetterSingleton = new PseudoClassSelector("first-letter");
        public static readonly PseudoClassSelector FirstLineSingleton = new PseudoClassSelector("first-line");
        public static readonly PseudoClassSelector FirstChildSingleton = new PseudoClassSelector("first-child");
        public static readonly PseudoClassSelector BeforeSingleton = new PseudoClassSelector("before");
        public static readonly PseudoClassSelector AfterSingleton = new PseudoClassSelector("after");
        public static readonly PseudoClassSelector LastChildSingleton = new PseudoClassSelector("last-child");
        public static readonly PseudoClassSelector EmptySingleton = new PseudoClassSelector("empty");
        public static readonly PseudoClassSelector CheckedSingleton = new PseudoClassSelector("checked");
        public static readonly PseudoClassSelector DisabledSingleton = new PseudoClassSelector("disabled");
        public static readonly PseudoClassSelector FirstOfTypeSingleton = new PseudoClassSelector("first-of-type");
        public static readonly PseudoClassSelector LastOfTypeSingleton = new PseudoClassSelector("last-of-type");
        public static readonly PseudoClassSelector OnlyChildSingleton = new PseudoClassSelector("only-child");
        public static readonly PseudoClassSelector OnlyOfTypeSingleton = new PseudoClassSelector("only-of-type");
        public static readonly PseudoClassSelector RootSingleton = new PseudoClassSelector("root");
        public static readonly PseudoClassSelector TargetSingleton = new PseudoClassSelector("target");
        public static readonly PseudoClassSelector EnabledSingleton = new PseudoClassSelector("enabled");
        public static readonly PseudoClassSelector DefaultSingleton = new PseudoClassSelector("default");
        public static readonly PseudoClassSelector ValidSingleton = new PseudoClassSelector("valid");
        public static readonly PseudoClassSelector InvalidSingleton = new PseudoClassSelector("invalid");
        public static readonly PseudoClassSelector InRangeSingleton = new PseudoClassSelector("in-range");
        public static readonly PseudoClassSelector OutOfRangeSingleton = new PseudoClassSelector("out-of-range");
        public static readonly PseudoClassSelector RequiredSingleton = new PseudoClassSelector("required");
        public static readonly PseudoClassSelector OptionalSingleton = new PseudoClassSelector("optional");
        public static readonly PseudoClassSelector ReadOnlySingleton = new PseudoClassSelector("read-only");
        public static readonly PseudoClassSelector ReadWriteSingleton = new PseudoClassSelector("read-write");

        protected PseudoClassSelector(string name) { Name = name; }

        public PseudoClassSelector BindToPosition(int start, int stop, string filePath)
        {
            var copy = (PseudoClassSelector)this.MemberwiseClone();
            copy.Start = start;
            copy.Stop = stop;
            copy.FilePath = filePath;

            return copy;
        }

        public override void Write(TextWriter output)
        {
            output.Write(':');
            output.Write(Name);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return ":" + Name;
        }

        internal static new Selector Parse(string name, int start, int stop, string filePath)
        {
            name = name.Substring(1);
            switch (name)
            {
                case "link": return LinkSingleton.BindToPosition(start, stop, filePath);
                case "visited": return VisitedSingleton.BindToPosition(start, stop, filePath);
                case "active": return ActiveSingleton.BindToPosition(start, stop, filePath);
                case "hover": return HoverSingleton.BindToPosition(start, stop, filePath);
                case "focus": return FocusSingleton.BindToPosition(start, stop, filePath);
                case "first-letter": return FirstLetterSingleton.BindToPosition(start, stop, filePath);
                case "first-child": return FirstChildSingleton.BindToPosition(start, stop, filePath);
                case "before": return BeforeSingleton.BindToPosition(start, stop, filePath);
                case "after": return AfterSingleton.BindToPosition(start, stop, filePath);
                case "last-child": return LastChildSingleton.BindToPosition(start, stop, filePath);
                case "empty": return EmptySingleton.BindToPosition(start, stop, filePath);
                case "checked": return CheckedSingleton.BindToPosition(start, stop, filePath);
                case "disabled": return DisabledSingleton.BindToPosition(start, stop, filePath);
                case "first-of-type": return FirstOfTypeSingleton.BindToPosition(start, stop, filePath);
                case "last-of-type": return LastOfTypeSingleton.BindToPosition(start, stop, filePath);
                case "only-child": return OnlyChildSingleton.BindToPosition(start, stop, filePath);
                case "only-of-type": return OnlyOfTypeSingleton.BindToPosition(start, stop, filePath);
                case "root": return RootSingleton.BindToPosition(start, stop, filePath);
                case "target": return TargetSingleton.BindToPosition(start, stop, filePath);
                case "enabled": return EnabledSingleton.BindToPosition(start, stop, filePath);
                case "default": return DefaultSingleton.BindToPosition(start, stop, filePath);
                case "valid": return ValidSingleton.BindToPosition(start, stop, filePath);
                case "invalid": return InvalidSingleton.BindToPosition(start, stop, filePath);
                case "in-range": return InRangeSingleton.BindToPosition(start, stop, filePath);
                case "out-of-range": return OutOfRangeSingleton.BindToPosition(start, stop, filePath);
                case "required": return RequiredSingleton.BindToPosition(start, stop, filePath);
                case "optional": return OptionalSingleton.BindToPosition(start, stop, filePath);
                case "read-only": return ReadOnlySingleton.BindToPosition(start, stop, filePath);
                case "read-write": return ReadWriteSingleton.BindToPosition(start, stop, filePath);
            }

            int i = name.IndexOf('(');
            if (!name.EndsWith(")"))
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start, stop, filePath), "Unknown pseudo class [" + name + "]");
                throw new StoppedParsingException();
            }

            string inner = name.Substring(i + 1, name.Length - 1 - (i + 1));

            if (name.StartsWith("lang(")) return new LangPseudoClassSelector(inner, start, stop, filePath);
            if (name.StartsWith("not(")) return new NotPseudoClassSelector(Selector.Parse(inner, start, stop, filePath), start, stop, filePath);
            if (name.StartsWith("nth-last-child(")) return new NthLastChildPseudoClassSelector(NthParameter.Parse(inner), start, stop, filePath);
            if (name.StartsWith("nth-of-type(")) return new NthOfTypePseudoClassSelector(NthParameter.Parse(inner), start, stop, filePath);
            if (name.StartsWith("nth-last-of-type(")) return new NthLastOfTypePseudoClassSelector(NthParameter.Parse(inner), start, stop, filePath);
            if (name.StartsWith("nth-child(")) return new NthChildPsuedoClassSelector(NthParameter.Parse(inner), start, stop, filePath);

            Current.RecordError(ErrorType.Parser, Position.Create(start, stop, filePath), "Unknown pseudo class [" + name + "]");
            throw new StoppedParsingException();
        }
    }

    class NotPseudoClassSelector : PseudoClassSelector
    {
        public Selector Selector { get; private set; }

        internal NotPseudoClassSelector(Selector sel, int start, int stop, string filePath) : base("not")
        {
            Selector = sel;

            Start = start;
            Stop = stop;
            FilePath = filePath;
        }

        public override void Write(TextWriter output)
        {
            base.Write(output);
            output.Write('(');
            Selector.Write(output);
            output.Write(')');
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return "not(" + Selector.ToString() + ")";
        }
    }

    // Of the form A*n+B
    class NthParameter
    {
        public static readonly NthParameter EvenSingleton = new NthParameter(2, null);
        public static readonly NthParameter OddSingleton = new NthParameter(2, 1);

        public int? A { get; private set; }
        public int? B { get; private set; }

        internal NthParameter(int? a, int? b)
        {
            A = a;
            B = b;
        }

        public static NthParameter Parse(string param)
        {
            if (param.Equals("even", StringComparison.InvariantCultureIgnoreCase)) return EvenSingleton;
            if (param.Equals("odd", StringComparison.InvariantCultureIgnoreCase)) return OddSingleton;

            int i;
            if (int.TryParse(param, out i)) return new NthParameter(null, i);

            var beforeN = param.Substring(0, param.IndexOf('n'));
            var afterN = param.Substring(param.IndexOf('n') + 1);

            var a = int.Parse(beforeN);
            int? b = null;
            if (afterN.Length > 0) b = int.Parse(afterN);

            return new NthParameter(a, b);
        }

        public void Write(TextWriter output)
        {
            var needsPlus = false;

            if (A.HasValue && A.Value != 0)
            {
                output.Write(A.Value);
                output.Write('n');
                needsPlus = true;
            }

            if (B.HasValue && B.Value != 0)
            {
                var b = B.Value;
                if (needsPlus)
                {
                    if (b > 0)
                    {
                        output.Write('+');
                    }
                    else
                    {
                        output.Write('-');
                    }
                }

                output.Write(Math.Abs(b));
            }
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            using (var @out = new StringWriter())
            {
                Write(@out);
                return @out.ToString();
            }
        }
    }

    class NthLastChildPseudoClassSelector : PseudoClassSelector
    {
        public NthParameter LastChild { get; private set; }

        internal NthLastChildPseudoClassSelector(NthParameter lastChild, int start, int stop, string filePath)
            : base("nth-last-child")
        {
            LastChild = lastChild;

            Start = start;
            Stop = stop;
            FilePath = filePath;
        }

        public override void Write(TextWriter output)
        {
            base.Write(output);
            output.Write('(');
            LastChild.Write(output);
            output.Write(')');
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return "nth-last-child(" + LastChild + ")";
        }
    }

    class NthOfTypePseudoClassSelector : PseudoClassSelector
    {
        public NthParameter N { get; private set; }

        internal NthOfTypePseudoClassSelector(NthParameter n, int start, int stop, string filePath)
            : base("nth-of-type")
        {
            N = n;

            Start = start;
            Stop = stop;
            FilePath = filePath;
        }

        public override void Write(TextWriter output)
        {
            base.Write(output);
            output.Write('(');
            N.Write(output);
            output.Write(')');
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return "nth-of-type(" + N + ")";
        }
    }

    class NthLastOfTypePseudoClassSelector : PseudoClassSelector
    {
        public NthParameter N { get; private set; }

        internal NthLastOfTypePseudoClassSelector(NthParameter n, int start, int stop, string filePath)
            : base("nth-last-of-type")
        {
            N = n;

            Start = start;
            Stop = stop;
            FilePath = filePath;
        }

        public override void Write(TextWriter output)
        {
            base.Write(output);
            output.Write('(');
            N.Write(output);
            output.Write(')');
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return "nth-last-of-type(" + N + ")";
        }
    }

    class NthChildPsuedoClassSelector : PseudoClassSelector
    {
        public NthParameter Child { get; private set; }

        internal NthChildPsuedoClassSelector(NthParameter child, int start, int stop, string filePath)
            : base("nth-child")
        {
            Child = child;

            Start = start;
            Stop = stop;
            FilePath = filePath;
        }

        public override void Write(TextWriter output)
        {
            base.Write(output);
            output.Write('(');
            Child.Write(output);
            output.Write(')');
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return "nth-child(" + Child + ")";
        }
    }

    class LangPseudoClassSelector : PseudoClassSelector
    {
        public string Language { get; set; }

        internal LangPseudoClassSelector(string language, int start, int stop, string filePath) : base("lang")
        {
            Language = language;

            Start = start;
            Stop = stop;
            FilePath = filePath;
        }

        public override void Write(TextWriter output)
        {
            base.Write(output);
            output.Write('(');
            output.Write(Language);
            output.Write(')');
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return "lang(" + Language + ")";
        }
    }

    class ChildSelector : Selector
    {
        public Selector Parent { get; private set; }
        public Selector Child { get; private set; }

        public ChildSelector(Selector parent, Selector child, int start, int stop, string filePath)
        {
            Parent = parent;
            Child = child;

            Start = start;
            Stop = stop;
            FilePath = filePath;
        }

        public override void Write(TextWriter output)
        {
            Parent.Write(output);
            output.Write('>');
            Child.Write(output);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return Parent.ToString() + " > " + Child.ToString();
        }
    }

    class WildcardSelector : Selector
    {
        public static readonly WildcardSelector Singleton = new WildcardSelector();

        private WildcardSelector() { }

        public override void Write(TextWriter output)
        {
            output.Write('*');
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return "*";
        }
    }

    // A place holder selector for when we need a selector, but don't want it to survive to output
    class InvalidSelector : Selector
    {
        public static readonly InvalidSelector Singleton = new InvalidSelector();
    }
}
