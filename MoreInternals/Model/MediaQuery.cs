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

        public override string ToString()
        {
            return Type.ToString();
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

        public override string ToString()
        {
            return "not " + Clause.ToString();
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

        public override string ToString()
        {
            return "only " + Clause.ToString();
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

        public override string ToString()
        {
            return LeftHand.ToString() + " and " + RightHand.ToString();
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

        public override string ToString()
        {
            return string.Join(", ", Clauses.Select(s => s.ToString()));
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

        public override string ToString()
        {
            return "(min-" + Feature + ": " + Min.ToString() + ")";
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

        public override string ToString()
        {
            return "(max-" + Feature + ": " + Max.ToString() + ")";
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

        public override string ToString()
        {
            return "(" + Feature + ": " + EqualsValue.ToString() + ")";
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

        public override string ToString()
        {
            return "(" + Feature + ")";
        }
    }
}
