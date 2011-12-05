using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MoreInternals.Model
{
    public abstract class MediaQuery : IPosition
    {
        public int Start { get; protected set; }
        public int Stop { get; protected set; }
        public string FilePath { get; protected set; }

        public MediaQuery(IPosition pos)
        {
            Start = pos.Start;
            Stop = pos.Stop;
            FilePath = pos.FilePath;
        }

        internal static string AsString(Value val)
        {
            using (var writer = new StringWriter())
            {
                val.Write(writer);

                return writer.ToString();
            }
        }

        internal abstract MediaQuery Bind(Scope scope);
        internal abstract MediaQuery Evaluate();

        internal abstract void Write(TextWriter writer);
    }

    class MediaType : MediaQuery
    {
        public Media Type { get; private set; }

        public MediaType(Media type, IPosition forPosition) 
            : base(forPosition)
        {
            Type = type;
        }

        internal override void Write(TextWriter writer)
        {
            writer.Write(Enum.GetName(typeof(Media), Type).ToLowerInvariant());
        }

        internal override MediaQuery Bind(Scope scope)
        {
            return this;
        }

        internal override MediaQuery Evaluate()
        {
            return this;
        }

        public override string ToString()
        {
            return Type.ToString();
        }

        public override bool Equals(object obj)
        {
            var other = obj as MediaType;
            if (other == null) return false;

            return this.Type == other.Type;
        }

        public override int GetHashCode()
        {
            return Type.GetHashCode();
        }
    }

    class NotMedia : MediaQuery
    {
        public MediaQuery Clause { get; private set; }

        public NotMedia(MediaQuery inner, IPosition forPosition)
            : base(forPosition)
        {
            Clause = inner;
        }

        internal override void Write(TextWriter writer)
        {
            writer.Write("not ");
            Clause.Write(writer);
        }

        internal override MediaQuery Bind(Scope scope)
        {
            return new NotMedia(Clause.Bind(scope), this);
        }

        internal override MediaQuery Evaluate()
        {
            return new NotMedia(Clause.Evaluate(), this);
        }

        public override string ToString()
        {
            return "not " + Clause.ToString();
        }

        public override bool Equals(object obj)
        {
            var other = obj as NotMedia;
            if (other == null) return false;

            return this.Clause.Equals(other.Clause);
        }

        public override int GetHashCode()
        {
            return Clause.GetHashCode();
        }
    }

    class OnlyMedia : MediaQuery
    {
        public MediaQuery Clause { get; private set; }

        public OnlyMedia(MediaQuery inner, IPosition forPosition)
            : base(forPosition)
        {
            Clause = inner;
        }

        internal override void Write(TextWriter writer)
        {
            writer.Write("only ");
            Clause.Write(writer);
        }

        internal override MediaQuery Bind(Scope scope)
        {
            return new OnlyMedia(Clause.Bind(scope), this);
        }

        internal override MediaQuery Evaluate()
        {
            return new OnlyMedia(Clause.Evaluate(), this);
        }

        public override string ToString()
        {
            return "only " + Clause.ToString();
        }

        public override bool Equals(object obj)
        {
            var other = obj as OnlyMedia;
            if (other == null) return false;

            return this.Clause.Equals(other.Clause);
        }

        public override int GetHashCode()
        {
            return Clause.GetHashCode();
        }
    }

    class AndMedia : MediaQuery
    {
        public MediaQuery LeftHand {get; private set;}
        public MediaQuery RightHand { get; private set; }

        public AndMedia(MediaQuery left, MediaQuery right, IPosition forPosition)
            : base(forPosition)
        {
            LeftHand = left;
            RightHand = right;
        }

        internal override void Write(TextWriter writer)
        {
            LeftHand.Write(writer);
            writer.Write(" and ");
            RightHand.Write(writer);
        }

        internal override MediaQuery Bind(Scope scope)
        {
            return new AndMedia(LeftHand.Bind(scope), RightHand.Bind(scope), this);
        }

        internal override MediaQuery Evaluate()
        {
            return new AndMedia(LeftHand.Evaluate(), RightHand.Evaluate(), this);
        }

        public override string ToString()
        {
            return LeftHand.ToString() + " and " + RightHand.ToString();
        }

        private static List<MediaQuery> Flatten(params MediaQuery[] queries)
        {
            var ret = new List<MediaQuery>();

            foreach (var q in queries)
            {
                if (!(q is AndMedia))
                {
                    ret.Add(q);
                    continue;
                }

                var and = (AndMedia)q;
                ret.AddRange(Flatten(and.LeftHand, and.RightHand));
            }

            return ret;
        }

        public override bool Equals(object obj)
        {
            var other = obj as AndMedia;
            if (other == null) return false;

            var thisFlat = Flatten(this.LeftHand, this.RightHand);
            var thatFlat = Flatten(other.LeftHand, other.RightHand);

            if(thisFlat.Count != thatFlat.Count) return false;

            foreach (var thisClause in thisFlat)
            {
                var anyEqual = false;

                foreach (var thatClause in thatFlat)
                {
                    anyEqual |= thisClause.Equals(thatClause);
                }

                if (!anyEqual) return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            return LeftHand.GetHashCode() ^ (RightHand.GetHashCode() * -1);
        }
    }

    class CommaDelimitedMedia : MediaQuery
    {
        public IEnumerable<MediaQuery> Clauses { get; private set; }

        public CommaDelimitedMedia(List<MediaQuery> clauses, IPosition forPosition)
            : base(forPosition)
        {
            Clauses = clauses.AsReadOnly();
        }

        internal override void Write(TextWriter writer)
        {
            var first = Clauses.First();
            first.Write(writer);

            foreach (var next in Clauses.Skip(1))
            {
                writer.Write(',');
                next.Write(writer);
            }
        }

        internal override MediaQuery Bind(Scope scope)
        {
            return new CommaDelimitedMedia(Clauses.Select(c => c.Bind(scope)).ToList(), this);
        }

        internal override MediaQuery Evaluate()
        {
            return new CommaDelimitedMedia(Clauses.Select(c => c.Evaluate()).ToList(), this);
        }

        public override string ToString()
        {
            return string.Join(", ", Clauses.Select(s => s.ToString()));
        }

        public override bool Equals(object obj)
        {
            var other = obj as CommaDelimitedMedia;
            if (other == null) return false;

            foreach (var thisClause in this.Clauses)
            {
                var foundMatch = false;

                foreach (var otherClause in other.Clauses)
                {
                    foundMatch |= thisClause.Equals(otherClause);
                }

                if (!foundMatch) return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            var ret = 0x12345678;
            bool invert = false;
            foreach (var c in Clauses)
            {
                ret ^= c.GetHashCode();

                if (invert) ret *= -1;
                invert = !invert;
            }

            return ret;
        }
    }

    class MinFeatureMedia : MediaQuery
    {
        public string Feature { get; private set; }
        public Value Min { get; private set; }

        public MinFeatureMedia(string feature, Value min, IPosition forPosition)
            :base(forPosition)
        {
            Feature = feature;
            Min = min;
        }

        internal override void Write(TextWriter writer)
        {
            writer.Write("(min-");
            writer.Write(Feature);
            writer.Write(':');
            Min.Write(writer);
            writer.Write(')');
        }

        internal override MediaQuery Bind(Scope scope)
        {
            return new MinFeatureMedia(Feature, Min.Bind(scope), this);
        }

        internal override MediaQuery Evaluate()
        {
            return new MinFeatureMedia(Feature, Min.Evaluate(), this);
        }

        public override string ToString()
        {
            return "(min-" + Feature + ": " + Min.ToString() + ")";
        }

        public override bool Equals(object obj)
        {
            var other = obj as MinFeatureMedia;
            if (other == null) return false;

            var thisMinStr = AsString(this.Min);
            var otherMinStr = AsString(other.Min);

            return
                this.Feature == other.Feature &&
                thisMinStr == otherMinStr;
        }

        public override int GetHashCode()
        {
            return Feature.GetHashCode() ^ AsString(Min).GetHashCode();
        }
    }

    class MaxFeatureMedia : MediaQuery
    {
        public string Feature { get; private set; }
        public Value Max { get; private set; }

        public MaxFeatureMedia(string feature, Value max, IPosition forPosition)
            :base(forPosition)
        {
            Feature = feature;
            Max = max;
        }

        internal override void Write(TextWriter writer)
        {
            writer.Write("(max-");
            writer.Write(Feature);
            writer.Write(':');
            Max.Write(writer);
            writer.Write(')');
        }

        internal override MediaQuery Bind(Scope scope)
        {
            return new MaxFeatureMedia(Feature, Max.Bind(scope), this);
        }

        internal override MediaQuery Evaluate()
        {
            return new MaxFeatureMedia(Feature, Max.Evaluate(), this);
        }

        public override string ToString()
        {
            return "(max-" + Feature + ": " + Max.ToString() + ")";
        }

        public override bool Equals(object obj)
        {
            var other = obj as MaxFeatureMedia;
            if (other == null) return false;

            var thisMaxStr = AsString(this.Max);
            var otherMaxStr = AsString(other.Max);

            return
                this.Feature == other.Feature &&
                thisMaxStr == otherMaxStr;
        }

        public override int GetHashCode()
        {
            return Feature.GetHashCode() ^ AsString(Max).GetHashCode();
        }
    }

    class EqualFeatureMedia : MediaQuery
    {
        public string Feature { get; private set; }
        public Value EqualsValue { get; private set; }

        public EqualFeatureMedia(string feature, Value equals, IPosition forPosition)
            :base(forPosition)
        {
            Feature = feature;
            EqualsValue = equals;
        }

        internal override void Write(TextWriter writer)
        {
            writer.Write('(');
            writer.Write(Feature);
            writer.Write(':');
            EqualsValue.Write(writer);
            writer.Write(')');
        }

        internal override MediaQuery Bind(Scope scope)
        {
            return new EqualFeatureMedia(Feature, EqualsValue.Bind(scope), this);
        }

        internal override MediaQuery Evaluate()
        {
            return new EqualFeatureMedia(Feature, EqualsValue.Evaluate(), this);
        }

        public override string ToString()
        {
            return "(" + Feature + ": " + EqualsValue.ToString() + ")";
        }

        public override bool Equals(object obj)
        {
            var other = obj as EqualFeatureMedia;
            if (other == null) return false;

            var thisEqualsStr = AsString(this.EqualsValue);
            var otherEqualsStr = AsString(other.EqualsValue);

            return
                this.Feature == other.Feature &&
                thisEqualsStr == otherEqualsStr;
        }

        public override int GetHashCode()
        {
            return Feature.GetHashCode() ^ AsString(EqualsValue).GetHashCode();
        }
    }

    class FeatureMedia : MediaQuery
    {
        public string Feature { get; private set; }

        public FeatureMedia(string feature, IPosition forPosition)
            :base(forPosition)
        {
            Feature = feature;
        }

        internal override void Write(TextWriter writer)
        {
            writer.Write('(');
            writer.Write(Feature);
            writer.Write(')');
        }

        internal override MediaQuery Bind(Scope scope)
        {
            return this;
        }

        internal override MediaQuery Evaluate()
        {
            return this;
        }

        public override string ToString()
        {
            return "(" + Feature + ")";
        }

        public override bool Equals(object obj)
        {
            var other = obj as FeatureMedia;
            if (other == null) return false;

            return
                this.Feature == other.Feature;
        }

        public override int GetHashCode()
        {
            return Feature.GetHashCode();
        }
    }
}
