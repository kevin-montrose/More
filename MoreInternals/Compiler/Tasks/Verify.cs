using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;
using System.Text.RegularExpressions;

namespace MoreInternals.Compiler.Tasks
{
    /// <summary>
    /// Task which does all sorts of "post CSS generation" checks for correctness.
    /// 
    /// This checks that media-queries make sense, and that cycle() doesn't violate some constraints.
    /// 
    /// In the future, this may come to mean all sorts of "property x should be of type y" sorts
    /// of checks.
    /// </summary>
    public class Verify
    {
        // from: https://developer.mozilla.org/en/CSS/Media_queries 
        private static Dictionary<string, IEnumerable<Type>> FeatureValueTypes = new Dictionary<string, IEnumerable<Type>>()
        {
            { "color", new[] { typeof(NumberValue) } },                 // further, must be an integer
            { "color-index", new[] { typeof(NumberValue) } },           // ditto
            { "aspect-ratio", new[] { typeof(RatioValue) } },          
            { "device-aspect-ratio", new[] { typeof(RatioValue) } },   
            { "device-height", new[] { typeof(NumberWithUnitValue) } },
            { "device-width", new[] { typeof(NumberWithUnitValue) } },
            { "grid", new[] { typeof(NumberValue) } },
            { "height", new[] { typeof(NumberWithUnitValue) } },
            { "monochrome", new [] { typeof(NumberValue) } },
            { "orientation", new [] { typeof(StringValue) } },          // further, must be one of landscape or portrait
            { "resolution", new [] { typeof(NumberWithUnitValue) } },   // further, unit must be one of dpi or dpcm
            { "scan", new [] { typeof(StringValue) } },                 // further, must be one of progressive or scan
            { "width", new [] { typeof(NumberWithUnitValue) } }
        };

        private static bool IsValidFeature(string featureName)
        {
            return FeatureValueTypes.Keys.Contains(featureName.ToLowerInvariant());
        }

        private static bool IsValidMinMax(string featureName)
        {
            return
                featureName.ToLowerInvariant().In(
                    "color",
                    "color-index",
                    "aspect-ratio",
                    "device-aspect-ratio",
                    "device-height",
                    "device-width",
                    "height",
                    "monochrome",
                    "resolution",
                    "width"
                );
        }

        private static bool IsValidTypeFor(string featureName, Value value)
        {
            var validTypes = FeatureValueTypes[featureName.ToLowerInvariant()];
            if (!validTypes.Contains(value.GetType())) return false;

            switch (featureName.ToLowerInvariant())
            {
                case "monochrome":
                case "color":
                case "color-index": 
                    // Must be a non-negative integer
                    var numValue = ((NumberValue)value).Value;
                    return Math.Sign(numValue) != -1 && Math.Truncate(numValue) == numValue;

                case "grid": return ((NumberValue)value).Value.In(0, 1);

                case "orientation": return ((StringValue)value).Value.ToLowerInvariant().In("landscape", "portrait");
                case "resolution": return ((NumberWithUnitValue)value).Unit.In(Unit.DPI, Unit.DPCM);

                case "scan": return ((StringValue)value).Value.ToLowerInvariant().In("progressive", "scan");
            }

            return true;
        }

        private static void VerifyTypes(MediaQuery query)
        {
            var compound = query as CommaDelimitedMedia;
            if (compound != null)
            {
                foreach (var part in compound.Clauses)
                {
                    VerifyTypes(part);
                }

                return;
            }

            var not = query as NotMedia;
            if (not != null)
            {
                VerifyTypes(not.Clause);
                return;
            }

            var only = query as OnlyMedia;
            if (only != null)
            {
                VerifyTypes(only.Clause);
                return;
            }

            var and = query as AndMedia;
            if (and != null)
            {
                VerifyTypes(and.LeftHand);
                VerifyTypes(and.RightHand);
                return;
            }

            var min = query as MinFeatureMedia;
            if (min != null)
            {
                if (!IsValidFeature(min.Feature))
                {
                    Current.RecordError(ErrorType.Compiler, min, "'" + min.Feature + "' is not a valid feature for a media query.");
                }
                else
                {
                    if (!IsValidMinMax(min.Feature))
                    {
                        Current.RecordError(ErrorType.Compiler, min, "'" + min.Feature + "' cannot have a minimum constraint in a media query.");
                    }
                    else
                    {
                        if (!IsValidTypeFor(min.Feature, min.Min))
                        {
                            Current.RecordError(ErrorType.Compiler, min, "'" + min.Min + "' is not a valid parameter for media query feature '" + min.Feature + "'.");
                        }
                    }
                }

                return;
            }

            var max = query as MaxFeatureMedia;
            if (max != null)
            {
                if (!IsValidFeature(max.Feature))
                {
                    Current.RecordError(ErrorType.Compiler, max, "'" + max.Feature + "' is not a valid feature for a media query.");
                }
                else
                {
                    if (!IsValidMinMax(max.Feature))
                    {
                        Current.RecordError(ErrorType.Compiler, max, "'" + max.Feature + "' cannot have a minimum constraint in a media query.");
                    }
                    else
                    {
                        if (!IsValidTypeFor(max.Feature, max.Max))
                        {
                            Current.RecordError(ErrorType.Compiler, max, "'" + max.Max + "' is not a valid parameter for media query feature '" + max.Feature + "'.");
                        }
                    }
                }

                return;
            }

            var eq = query as EqualFeatureMedia;
            if (eq != null)
            {
                if (!IsValidFeature(eq.Feature))
                {
                    Current.RecordError(ErrorType.Compiler, eq, "'" + eq.Feature + "' is not a valid feature for a media query.");
                }
                else
                {
                    if (!IsValidTypeFor(eq.Feature, eq.EqualsValue))
                    {
                        Current.RecordError(ErrorType.Compiler, eq, "'" + eq.EqualsValue + "' is not a valid parameter for media query feature '" + eq.Feature + "'.");
                    }
                }

                return;
            }

            var feature = query as FeatureMedia;
            if(feature != null)
            {
                if (!IsValidFeature(feature.Feature))
                {
                    Current.RecordError(ErrorType.Compiler, eq, "'" + feature.Feature + "' is not a valid feature for a media query.");
                }

                return;
            }

            if (query is MediaType)
            {
                return;
            }

            throw new InvalidOperationException("Unexpected media query [" + query + "]");
        }

        // These from http://www.w3.org/TR/2009/CR-CSS2-20090908/media.html section 7.3.1
        private static IEnumerable<Model.Media> Continuous = new[]{ 
            Model.Media.braille, Model.Media.screen, Model.Media.speech, Model.Media.tty, Model.Media.handheld
        };
        private static IEnumerable<Model.Media> Paged = new[]{
            Model.Media.embossed, Model.Media.print, Model.Media.projection, Model.Media.tv
        };
        private static IEnumerable<Model.Media> Tacticle = new[]{
            Model.Media.braille, Model.Media.embossed
        };
        private static IEnumerable<Model.Media> Visual = new[]{
            Model.Media.handheld, Model.Media.print, Model.Media.projection, Model.Media.screen, Model.Media.tty, Model.Media.tv
        };
        private static IEnumerable<Model.Media> Audio = new[]{
            Model.Media.handheld, Model.Media.screen, Model.Media.tv
        };
        private static IEnumerable<Model.Media> Speech = new[]{
            Model.Media.handheld, Model.Media.speech
        };
        private static IEnumerable<Model.Media> Grid = new[]{
            Model.Media.braille, Model.Media.embossed, Model.Media.handheld, Model.Media.tty
        };
        private static IEnumerable<Model.Media> Bitmap = new[]{
            Model.Media.handheld, Model.Media.print, Model.Media.projection, Model.Media.screen, Model.Media.tv
        };
        private static IEnumerable<Model.Media> Interactive = new[]{
            Model.Media.braille, Model.Media.handheld, Model.Media.projection, Model.Media.screen, Model.Media.speech, 
            Model.Media.tty, Model.Media.tv
        };
        private static IEnumerable<Model.Media> Static = new[]{
            Model.Media.braille, Model.Media.embossed, Model.Media.print, Model.Media.screen, Model.Media.speech, 
            Model.Media.tty, Model.Media.tv
        };

        // Taken from http://www.w3.org/TR/css3-mediaqueries/ section 4
        private static bool IsSetFeature(Model.Media type, string featureName)
        {
            switch (featureName.ToLowerInvariant())
            {
                case" grid":
                case "device-height":
                case "device-width":
                case "height":
                case "width": return type.In(Visual) || type.In(Tacticle);

                case "resolution":
                case "device-aspect-ratio":
                case "aspect-ratio":
                case "orientation": return type.In(Bitmap);

                case "monochrome":
                case "color-index":
                case "color": return type.In(Visual);

                case "scan": return type == Model.Media.tv;

                default: throw new InvalidOperationException("Unexpected feature name [" + featureName + "]");
            }
        }

        private static void VerifyPossible(MediaQuery query, IPosition on)
        {
            var comma = query as CommaDelimitedMedia;
            if (comma != null)
            {
                foreach (var part in comma.Clauses)
                {
                    VerifyPossible(part, part);
                }

                return;
            }

            var only = query as OnlyMedia;
            if (only != null)
            {
                VerifyPossible(only.Clause, on);
                return;
            }

            var not = query as NotMedia;
            if (not != null)
            {
                VerifyPossible(not.Clause, on);
                return;
            }

            // Type and nothing else is always possible
            var type = query as MediaType;
            if (type != null)
            {
                return;
            }

            var and = query as AndMedia;
            type = and.LeftHand as MediaType;
            var unrollStack = new Stack<MediaQuery>();
            unrollStack.Push(and);

            var eqs = new List<EqualFeatureMedia>();
            var mins = new List<MinFeatureMedia>();
            var maxs = new List<MaxFeatureMedia>();
            var has = new List<FeatureMedia>();

            while (unrollStack.Count > 0)
            {
                var top = unrollStack.Pop();

                if (top is AndMedia)
                {
                    var innerAnd = (AndMedia)top;
                    unrollStack.Push(innerAnd.LeftHand);
                    unrollStack.Push(innerAnd.RightHand);
                }

                if (top is EqualFeatureMedia)
                {
                    eqs.Add((EqualFeatureMedia)top);
                    continue;
                }

                if (top is MinFeatureMedia)
                {
                    mins.Add((MinFeatureMedia)top);
                    continue;
                }

                if (top is MaxFeatureMedia)
                {
                    maxs.Add((MaxFeatureMedia)top);
                    continue;
                }

                if (top is FeatureMedia)
                {
                    has.Add((FeatureMedia)top);
                    continue;
                }
            }

            var multipleMins = mins.GroupBy(g => g.Feature.ToLowerInvariant()).Where(w => w.Count() > 1);
            var multipleMaxs = maxs.GroupBy(g => g.Feature.ToLowerInvariant()).Where(w => w.Count() > 1);
            var multipleHas = has.GroupBy(g => g.Feature.ToLowerInvariant()).Where(w => w.Count() > 1);
            var multipleEqs = eqs.GroupBy(g => g.Feature.ToLowerInvariant()).Where(w => w.Count() > 1);

            var continueCheck = true;

            if (multipleMins.Count() > 0)
            {
                foreach (var a in multipleMins)
                {
                    Current.RecordError(ErrorType.Compiler, on, "'" + a.Key + "' has multiple minimum constraints");
                }

                continueCheck = false;
            }

            if (multipleMaxs.Count() > 0)
            {
                foreach (var a in multipleMaxs)
                {
                    Current.RecordError(ErrorType.Compiler, on, "'" + a.Key + "' has multiple maximum constraints");
                }

                continueCheck = false;
            }

            if (multipleHas.Count() > 0)
            {
                foreach (var a in multipleHas)
                {
                    Current.RecordError(ErrorType.Compiler, on, "'" + a.Key + "' has multiple present constraints");
                }

                continueCheck = false;
            }

            if (multipleEqs.Count() > 0)
            {
                foreach (var a in multipleEqs)
                {
                    Current.RecordError(ErrorType.Compiler, on, "'" + a.Key + "' has multiple equality constraints");
                }

                continueCheck = false;
            }

            if (!continueCheck) return;

            foreach (var h in has)
            {
                if (!IsSetFeature(type.Type, h.Feature))
                {
                    Current.RecordError(ErrorType.Compiler, on, "'" + h.Feature + "' is never set for media type '" + type.Type + "', making this query unsatisfiable");
                }
            }

            foreach (var min in mins)
            {
                var pairedMax = maxs.SingleOrDefault(w => w.Feature.Equals(min.Feature, StringComparison.InvariantCultureIgnoreCase));
                if (pairedMax == null) continue;

                if (min.Min > pairedMax.Max)
                {
                    Current.RecordError(ErrorType.Compiler, on, "'" + min.Feature + "' is impossibly constrained, [" + min.Min + " < " + pairedMax.Max + "] is impossible");
                }
            }
        }

        private static void VerifyQuery(MediaQuery query)
        {
            VerifyTypes(query);
            VerifyPossible(query, query);
        }

        private static void VerifyCycle(IEnumerable<Value> values)
        {
            foreach(var value in values)
            {
                var comma = value as CommaDelimittedValue;
                if (comma != null)
                {
                    VerifyCycle(comma.Values);
                    continue;
                }

                var compound = value as CompoundValue;
                if (compound != null)
                {
                    VerifyCycle(compound.Values);
                    continue;
                }

                var cycle = value as CycleValue;
                if (cycle != null)
                {
                    // per http://www.w3.org/TR/css3-values/#cycle ; cycle containing attr() and calc() are invalid
                    if (cycle.Values.OfType<AttributeValue>().Count() != 0)
                    {
                        Current.RecordError(ErrorType.Compiler, cycle, "cycle() values cannot contain attr() values");
                    }

                    if (cycle.Values.OfType<CalcValue>().Count() != 0)
                    {
                        Current.RecordError(ErrorType.Compiler, cycle, "cycle() values cannot contain calc() values");
                    }
                }
            }
        }

        private static void VerifySteps(IEnumerable<Value> values)
        {
            foreach (var value in values)
            {
                var comma = value as CommaDelimittedValue;
                if (comma != null)
                {
                    VerifySteps(comma.Values);
                    continue;
                }

                var compound = value as CompoundValue;
                if (compound != null)
                {
                    VerifySteps(compound.Values);
                    continue;
                }

                var steps = value as StepsValue;
                if (steps != null)
                {
                    var numSteps = steps.NumberOfSteps as NumberValue;
                    var dir = steps.Direction as StringValue;

                    if (numSteps == null || numSteps is NumberWithUnitValue)
                    {
                        Current.RecordError(ErrorType.Compiler, value, "steps() expects a unit-less integer for its first parameter");
                    }
                    else
                    {
                        var v = numSteps.Value;

                        if (decimal.Round(v, 0) != v || v <= 0)
                        {
                            Current.RecordError(ErrorType.Compiler, value, "steps() expects an integer value > 0 as its first parameter");
                        }
                    }

                    if ((steps.Direction != null && dir == null) || (dir != null && !(dir.Value.ToLowerInvariant().In("end", "start"))))
                    {
                        Current.RecordError(ErrorType.Compiler, value, "steps() expects one of end or start as its optional second parameter");
                    }
                }
            }
        }

        private static void VerifyCubicBezier(IEnumerable<Value> values)
        {
            foreach (var value in values)
            {
                var comma = value as CommaDelimittedValue;
                if (comma != null)
                {
                    VerifySteps(comma.Values);
                    continue;
                }

                var compound = value as CompoundValue;
                if (compound != null)
                {
                    VerifySteps(compound.Values);
                    continue;
                }

                var bezier = value as CubicBezierValue;
                if (bezier != null)
                {
                    var x1 = bezier.X1 as NumberValue;
                    var y1 = bezier.Y1 as NumberValue;
                    var x2 = bezier.X2 as NumberValue;
                    var y2 = bezier.Y2 as NumberValue;

                    if (x1 == null || y1 == null || x2 == null || y2 == null ||
                       (x1 is NumberWithUnitValue) || (y1 is NumberWithUnitValue) ||
                       (x2 is NumberWithUnitValue) || (y2 is NumberWithUnitValue))
                    {
                        Current.RecordError(ErrorType.Compiler, value, "cubic-bezier() expects 4 unit-less parameters");
                        continue;
                    }

                    if (x1.Value < 0 || x1.Value > 1)
                    {
                        Current.RecordError(ErrorType.Compiler, value, "cubic-bezier() expects its first value to be in the range [0, 1]");
                    }

                    if (x2.Value < 0 || x2.Value > 1)
                    {
                        Current.RecordError(ErrorType.Compiler, value, "cubic-bezier() expects its third value to be in the range [0, 1]");
                    }
                }
            }
        }

        private static void VerifyLinearGradient(IEnumerable<Value> values)
        {
            foreach (var value in values)
            {
                var comma = value as CommaDelimittedValue;
                if (comma != null)
                {
                    VerifySteps(comma.Values);
                    continue;
                }

                var compound = value as CompoundValue;
                if (compound != null)
                {
                    VerifySteps(compound.Values);
                    continue;
                }

                var linGrad = value as LinearGradientValue;
                if (linGrad != null)
                {
                    if (linGrad.Parameters.Skip(1).Any(a => !(a is ColorValue)))
                    {
                        Current.RecordError(ErrorType.Compiler, value, "linear-gradient expects an optional angle parameter followed by at least one color parameter");
                        continue;
                    }

                    var first = linGrad.Parameters.First();
                    var asNum = first as NumberWithUnitValue;
                    if (asNum != null)
                    {
                        if (!asNum.Unit.In(Unit.DEG, Unit.GRAD, Unit.RAD, Unit.TURN))
                        {
                            Current.RecordError(ErrorType.Compiler, first, "linear-gradient's first parameter, if not a color, must be an angle");
                            continue;
                        }
                    }

                    var asString = first as StringValue;
                    if (asString != null)
                    {
                        if (!asString.Value.StartsWith("to ", StringComparison.InvariantCultureIgnoreCase))
                        {
                            Current.RecordError(ErrorType.Compiler, first, "linear-gradient's first parameter, if an angle shorthand, must begin 'to'");
                            continue;
                        }

                        var parts = asString.Value.Substring(3).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
                        if (parts.Count > 2 || parts.Count == 0)
                        {
                            Current.RecordError(ErrorType.Compiler, first, "linear-gradient's first parameter, if an angle shorthand, must be of the form 'to [left | right] || [top | bottom]'");
                            continue;
                        }

                        if (parts.Any(a => !a.ToLowerInvariant().In("top", "left", "bottom", "right")))
                        {
                            Current.RecordError(ErrorType.Compiler, first, "linear-gradient's first parameter, if an angle shorthand, must be of the form 'to [left | right] || [top | bottom]'");
                            continue;
                        }
                    }
                }
            }
        }

        public static List<Block> Task(List<Block> blocks)
        {
            var media = blocks.OfType<MediaBlock>().Select(t => t.MediaQuery);
            var import = blocks.OfType<Model.Import>().Select(t => t.MediaQuery);
            var @using = blocks.OfType<Model.Using>().Select(t => t.MediaQuery);

            var toVerify = media.Union(import).Union(@using).ToList();

            toVerify.Each(v => VerifyQuery(v));

            var allProps =
                blocks.OfType<SelectorAndBlock>().SelectMany(s => s.Properties)
                .Union(
                    blocks.OfType<MediaBlock>().Select(m => m.Blocks).OfType<SelectorAndBlock>().SelectMany(s => s.Properties)
                );

            var values = allProps.OfType<NameValueProperty>().Select(s => s.Value).ToList();

            VerifyCycle(values);
            VerifySteps(values);
            VerifyCubicBezier(values);
            VerifyLinearGradient(values);

            return blocks;
        }
    }
}
