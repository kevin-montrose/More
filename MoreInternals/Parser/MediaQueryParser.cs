using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;

namespace MoreInternals.Parser
{
    // Function lifted mostly from https://developer.mozilla.org/en/CSS/media_queries
    public class MediaQueryParser
    {
        private static bool IsValidFeatureName(string feature)
        {
            return
                feature.ToLowerInvariant().In(
                    "width",
                    "height",
                    "device-width",
                    "device-height",
                    "aspect-ratio",
                    "device-aspect-ratio",
                    "color",
                    "color-index",
                    "monochrome",
                    "resolution",
                    "scan",
                    "grid"
                );
        }

        private static bool IsValidMinMaxFeatureName(string feature)
        {
            return
                feature.ToLowerInvariant().In(
                    "width",
                    "height",
                    "device-width",
                    "device-height",
                    "aspect-ratio",
                    "device-aspect-ratio",
                    "color",
                    "color-index",
                    "monochrome",
                    "resolution"
                );
        }

        private static bool IsRatioFeature(string feature)
        {
            return
                feature.Equals("aspect-ratio", StringComparison.InvariantCultureIgnoreCase) ||
                feature.Equals("device-aspect-ratio", StringComparison.InvariantCultureIgnoreCase);
        }

        private static MediaQuery ParseMediaClause(string media, IPosition forPosition)
        {
            if (!media.StartsWith("(") || !media.EndsWith(")"))
            {
                Current.RecordError(ErrorType.Parser, forPosition, "Media features must be enclosed in paranthesis, found '" + media + "'");
                throw new StoppedParsingException();
            }

            // Trim leading ( and trailing )
            media = media.Substring(1, media.Length - 2).Trim();

            var i = media.IndexOf(':');
            if (i == -1)
            {
                if (!IsValidFeatureName(media))
                {
                    Current.RecordError(ErrorType.Parser, forPosition, "Media feature not recognized, found '" + media + "'");
                    throw new StoppedParsingException();
                }

                return new FeatureMedia(media, forPosition);
            }

            var feature = media.Substring(0, i).Trim();
            var valueStr = media.Substring(i + 1).Trim();
            var value = MoreValueParser.Parse(valueStr, forPosition);

            var math = value as MathValue;
            if (math != null)
            {
                // Ratio check
                if (math.Operator == Operator.Div && IsRatioFeature(feature))
                {
                    var ratio = new RatioValue(math.LeftHand, math.RightHand);
                    ratio.Start = value.Start;
                    ratio.Stop = value.Stop;
                    ratio.FilePath = value.FilePath;

                    value = ratio;
                }
            }

            if (feature.StartsWith("min-", StringComparison.InvariantCultureIgnoreCase))
            {
                feature = feature.Substring("min-".Length);
                if (!IsValidMinMaxFeatureName(feature))
                {
                    Current.RecordError(ErrorType.Parser, forPosition, "Media feature not recognized in min clause, found '" + feature + "'");
                    throw new StoppedParsingException();
                }

                return new MinFeatureMedia(feature, value, forPosition);
            }

            if (feature.StartsWith("max-", StringComparison.InvariantCultureIgnoreCase))
            {
                feature = feature.Substring("max-".Length);
                if (!IsValidMinMaxFeatureName(feature))
                {
                    Current.RecordError(ErrorType.Parser, forPosition, "Media feature not recognized in max clause, found '" + feature + "'");
                    throw new StoppedParsingException();
                }

                return new MaxFeatureMedia(feature, value, forPosition);
            }

            return new EqualFeatureMedia(feature, value, forPosition);
        }

        private static MediaQuery ParseQuery(string value, IPosition forPositon)
        {
            int i = value.IndexOf(' ');
            if (i == -1) i = value.Length;

            var mediaType = value.Substring(0, i);
            Media media;
            if (!Enum.TryParse<Media>(mediaType, ignoreCase: true, result: out media))
            {
                Current.RecordError(ErrorType.Parser, forPositon, "Unrecognized media type '" + mediaType + "'");
                throw new StoppedParsingException();
            }

            var clauses = new List<MediaQuery>();

            var rest = value.Substring(i).Trim();
            while (rest.StartsWith("and ", StringComparison.InvariantCultureIgnoreCase))
            {
                int j = rest.IndexOf(" and ", "and ".Length, StringComparison.InvariantCultureIgnoreCase);
                if (j == -1) j = rest.Length;

                var clause = rest.Substring("and ".Length, j - "and ".Length).Trim();

                clauses.Add(ParseMediaClause(clause, forPositon));

                rest = rest.Substring(j).Trim();
            }

            if (rest.Length != 0)
            {
                Current.RecordError(ErrorType.Parser, forPositon, "Unexpected tail on media query, found '" + rest + "'");
                throw new StoppedParsingException();
            }

            var mediaClause = new MediaType(media, forPositon);

            if (clauses.Count == 0) return mediaClause;

            MediaQuery ret = mediaClause;

            foreach (var clause in clauses)
            {
                ret = new AndMedia(ret, clause, forPositon);
            }

            return ret;
        }

        public static MediaQuery Parse(string value, IPosition forPosition)
        {
            if (value.Contains(','))
            {
                var parts = value.Split(',');

                var ret = new List<MediaQuery>();

                foreach (var part in parts)
                {
                    ret.Add(Parse(part.Trim(), forPosition));
                }

                if (ret.Count == 1) return ret[0];

                return new CommaDelimitedMedia(ret, forPosition);
            }

            if (value.StartsWith("only ", StringComparison.InvariantCultureIgnoreCase))
            {
                return new OnlyMedia(ParseQuery(value.Substring("only ".Length).Trim(), forPosition), forPosition);
            }

            if (value.StartsWith("not ", StringComparison.InvariantCultureIgnoreCase))
            {
                return new NotMedia(ParseQuery(value.Substring("not ".Length).Trim(), forPosition), forPosition);
            }

            return ParseQuery(value, forPosition);
        }
    }
}
