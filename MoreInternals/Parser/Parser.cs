using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MoreInternals.Model;
using System.Text.RegularExpressions;

namespace MoreInternals.Parser
{
    class StoppedParsingException : Exception { }

    class Parser
    {
        private static IEnumerable<string> ReservedWords = new[] { "arguments", "reset", "keyframes", "using", "import", "charset" };

        private Parser()
        {
        }

        internal static void AdvancePastWhiteSpace(string toParse, ref int starting)
        {
            while (starting < toParse.Length && char.IsWhiteSpace(toParse[starting]))
                starting++;
        }

        internal static MediaBlock ParseMediaDirective(ParserStream stream)
        {
            var start = stream.Position;

            var media = new StringBuilder();
            stream.ScanUntil(media, '{');

            var mediaStr = media.ToString().Trim();
            if (mediaStr.IsNullOrEmpty())
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start, stream.Position, Current.CurrentFilePath), "Expected media list");
                throw new StoppedParsingException();
            }

            var mediaQuery = MediaQueryParser.Parse(mediaStr, Position.Create(start, stream.Position, Current.CurrentFilePath));

            var contained = new List<Block>();

            char c;
            while ((c = stream.Peek()) != '}')
            {
                if (char.IsWhiteSpace(c))
                {
                    stream.AdvancePastWhiteSpace();
                    continue;
                }

                // More directive (probably)
                if (c == '@')
                {
                    contained.Add(ParseDirective(stream));
                    continue;
                }

                // Selector + block time!
                contained.Add(ParseSelectorAndBlock(stream));
            }

            var notAllowed = contained.Where(x => !(x is SelectorAndBlock || x is MoreVariable));

            foreach (var illegal in notAllowed)
            {
                Current.RecordError(ErrorType.Parser, illegal, "@media can only contain blocks and variable declarations");
            }

            if (notAllowed.Count() != 0)
            {
                throw new StoppedParsingException();
            }

            // Skip past }
            stream.Advance();

            return new MediaBlock(mediaQuery, contained, start, stream.Position, Current.CurrentFilePath);
        }

        internal static CssCharset ParseCharsetDirective(ParserStream stream)
        {
            var start = stream.Position;

            var ignored = new StringBuilder();
            var quote = stream.ScanUntil(ignored, '"', '\'');

            if (quote == null)
            {
                Current.RecordError(ErrorType.Parser, Model.Position.Create(start, stream.Position, Current.CurrentFilePath), "Expected quotation mark");
                throw new StoppedParsingException();
            }

            var isoName = new StringBuilder();
            stream.ScanUntil(isoName, quote.Value);

            stream.AdvancePast(";");

            var isoNameStr = isoName.ToString();

            if (!CssCharset.KnownCharset(isoNameStr))
            {
                Current.RecordWarning(ErrorType.Parser, Model.Position.Create(start, stream.Position, Current.CurrentFilePath), "Unrecognized charset");
            }

            return new CssCharset(new QuotedStringValue(isoNameStr), start, stream.Position);
        }

        internal static Using ParseUsingDirective(ParserStream stream)
        {
            var start = stream.Position;

            var ignored = new StringBuilder();
            var quote = stream.ScanUntil(ignored, '"', '\'');

            if (quote == null)
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start, stream.Position, Current.CurrentFilePath), "Expected quotation mark");
                throw new StoppedParsingException();
            }

            var file = new StringBuilder();
            stream.ScanUntil(file, quote.Value);

            int mediaStart = stream.Position;
            var mediaBuff = new StringBuilder();
            stream.ScanUntil(mediaBuff, ';');
            int mediaEnd = stream.Position;

            MediaQuery media;
            var mediaStr = mediaBuff.ToString().Trim();
            if (mediaStr.Length > 0)
            {
                media = MediaQueryParser.Parse(mediaStr, Position.Create(mediaStart, mediaEnd, Current.CurrentFilePath));
            }
            else
            {
                media = new MediaType(Media.all, Position.NoSite);
            }

            return new Using(file.ToString(), media, start, stream.Position, Current.CurrentFilePath);
        }

        internal static Import ParseImportDirective(ParserStream stream)
        {
            var start = stream.Position;

            var buffer = new StringBuilder();
            stream.ScanUntilWithNesting(buffer, ';');

            var toParse = buffer.ToString().Trim();

            Value val;
            MediaQuery media;
            string mediaStr;

            if (Regex.IsMatch(toParse, @"url\s*?\(", RegexOptions.IgnoreCase))
            {
                var i = toParse.IndexOf(')');

                if (i == -1)
                {
                    Current.RecordError(ErrorType.Parser, Position.Create(start, stream.Position, Current.CurrentFilePath), "Expected ')'"); ;
                    throw new StoppedParsingException();
                }

                val = Value.Parse(toParse.Substring(0, i + 1), start, start + i + 1, Current.CurrentFilePath);

                mediaStr = toParse.Substring(i + 1);
            }
            else
            {
                if (!(toParse.StartsWith("\"") || toParse.StartsWith("'")))
                {
                    Current.RecordError(ErrorType.Parser, Position.Create(start, stream.Position, Current.CurrentFilePath), "Expected quote");
                    throw new StoppedParsingException();
                }

                var i = toParse.LastIndexOf(toParse[0]);

                if (i == -1)
                {
                    Current.RecordError(ErrorType.Parser, Position.Create(start, stream.Position, Current.CurrentFilePath), "Expected '" + toParse[0] + "'");
                    throw new StoppedParsingException();
                }

                val = Value.Parse(toParse.Substring(0, i + 1), start, start + i + 1, Current.CurrentFilePath);
                mediaStr = toParse.Substring(i + 1);
            }

            mediaStr = mediaStr.Trim();
            if (mediaStr.Length > 0)
            {
                media = MediaQueryParser.Parse(mediaStr, Position.Create(start, stream.Position, Current.CurrentFilePath));
            }
            else
            {
                media = new MediaType(Media.all, Position.Create(start, stream.Position, Current.CurrentFilePath));
            }

            return new Import(val, media, start, stream.Position, Current.CurrentFilePath);
        }

        internal static SpriteRule ParseSpriteRule(ParserStream stream)
        {
            var start = stream.Position;

            if (stream.Peek() != '@')
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start, stream.Position, Current.CurrentFilePath), "Expected '@'");
                throw new StoppedParsingException();
            }

            stream.Advance(); // Advance past @

            var name = new StringBuilder();

            while (stream.HasMore() && stream.Peek() != '=')
            {
                name.Append(stream.Read());
            }

            stream.AdvancePast("="); // Advance past =

            var ignored = new StringBuilder();
            var quote = stream.ScanUntil(ignored, '\'', '"');

            if (quote == null)
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start, stream.Position, Current.CurrentFilePath), "Expected quotation mark");
                throw new StoppedParsingException();
            }

            var valueStart = stream.Position;
            var valueStr = new StringBuilder();
            valueStr.Append(quote.Value);
            stream.ScanUntil(valueStr, quote.Value);
            valueStr.Append(quote.Value);

            stream.AdvancePast(";");

            var value = (QuotedStringValue)Value.Parse(valueStr.ToString(), valueStart, stream.Position, Current.CurrentFilePath);

            return new SpriteRule(name.ToString().Trim(), value, start, stream.Position, Current.CurrentFilePath);
        }

        internal static List<SpriteRule> ParseSpriteRules(ParserStream stream)
        {
            var ret = new List<SpriteRule>();

            stream.AdvancePast("{");

            while (stream.HasMore() && stream.Peek() != '}')
            {
                var c = stream.Peek();

                if (char.IsWhiteSpace(c))
                {
                    stream.AdvancePastWhiteSpace();
                }
                else
                {
                    ret.Add(ParseSpriteRule(stream));
                }
            }

            stream.AdvancePast("}");

            return ret;
        }

        internal static SpriteBlock ParseSpriteDeclaration(ParserStream stream)
        {
            var start = stream.Position;

            stream.AdvancePast("(");

            var ignored = new StringBuilder();
            var adTo = stream.ScanUntil(ignored, '"', '\'');

            if (ignored.ToString().Any(a => !char.IsWhiteSpace(a)))
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start, stream.Position, Current.CurrentFilePath), "Expected quotation mark");
                throw new StoppedParsingException();
            }

            if (adTo == null)
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start, stream.Position, Current.CurrentFilePath), "Expected quotation mark");
                throw new StoppedParsingException();
            }

            var nameStart = stream.Position;
            var name = new StringBuilder();
            name.Append(adTo.Value);
            stream.ScanUntil(name, adTo.Value);
            name.Append(adTo.Value);
            var nameStop = stream.Position;

            stream.AdvancePast(")");

            var rules = ParseSpriteRules(stream);

            return new SpriteBlock((QuotedStringValue)Value.Parse(name.ToString(), nameStart, nameStop, Current.CurrentFilePath), rules, start, stream.Position, Current.CurrentFilePath);
        }

        internal static List<MixinParameter> ParseMixinDeclarationParameter(string parse, int start)
        {
            var stop = start + parse.Length;

            var ret = new List<MixinParameter>();

            parse = parse.Trim();

            if (parse.Length == 0) return ret;

            var pieces = parse.Split(',');

            int offset = -1;

            foreach (var piece in pieces)
            {
                offset += piece.Length+1;
                var x = piece.Trim();
                if (x[0] != '@')
                {
                    Current.RecordError(ErrorType.Parser, Position.Create(start, start + offset, Current.CurrentFilePath), "Expected '@'");
                    throw new StoppedParsingException();
                }

                var i = x.IndexOf('=');
                var q1 = piece.IndexOf('"');
                var q2 = piece.IndexOf('\'');
                if ((q1 != -1 && i > q1) || (q2 != -1 && i > q2))
                {
                    Current.RecordError(ErrorType.Parser, Position.Create(start, start + offset, Current.CurrentFilePath), "Unable to parse value");
                    throw new StoppedParsingException();
                }

                var name = i != -1 ? x.Substring(1, i - 1).Trim() : x.Substring(1);
                var @default = i != -1 ? x.Substring(i + 1).Trim() : null;

                Value value;
                if (name.EndsWith("?"))
                {
                    name = name.Substring(0, name.Length - 1);
                    value = ExcludeFromOutputValue.Singleton;
                }
                else
                {
                    if (@default == null)
                    {
                        value = NotFoundValue.Default.BindToPosition(start, stop, Current.CurrentFilePath);
                    }
                    else
                    {
                        value = Value.Parse(@default, start, stop, Current.CurrentFilePath);
                    }
                }

                if (name.ToLower().In(ReservedWords))
                {
                    Current.RecordError(ErrorType.Parser, Position.Create(start, stop, Current.CurrentFilePath), "'" + name + "' cannot be the name of a parameter to a mixin.");
                }

                ret.Add(new MixinParameter(name, value));
            }

            return ret;
        }

        internal static MixinBlock ParseMixinDeclaration(string name, ParserStream stream)
        {
            var start = stream.Position;
            var @params = new StringBuilder();
            stream.ScanUntil(@params, ')');

            name = name.Trim();

            if (name.Length == 0)
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start, stream.Position, Current.CurrentFilePath), "Expected mixin name");
                throw new StoppedParsingException();
            }

            
            if (name.ToLower().In(ReservedWords))
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start, stream.Position, Current.CurrentFilePath), "'" + name + "' cannot be the name of a mixin.");
            }

            stream.AdvancePast("{");

            return new MixinBlock(name, ParseMixinDeclarationParameter(@params.ToString(), start), ParseCssRules(stream), start, stream.Position, Current.CurrentFilePath);
        }

        internal static MoreVariable ParseMoreVariable(string name, ParserStream stream, int start)
        {
            name = name.Trim();

            var valueStart = stream.Position;
            var valueStr = new StringBuilder();
            stream.ScanUntilWithNesting(valueStr, ';');

            var value = Value.Parse(valueStr.ToString().Trim(), valueStart, stream.Position, Current.CurrentFilePath, allowSelectorIncludes: true);

            if (name.ToLower().In(ReservedWords))
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start, stream.Position, Current.CurrentFilePath), "'" + name + "' cannot be a variable name.");
            }

            return new MoreVariable(name, value, start, stream.Position, Current.CurrentFilePath);
        }

        internal static KeyFrame ParseKeyFrame(ParserStream stream)
        {
            var start = stream.Position;

            var buffer = new StringBuilder();
            stream.ScanUntil(buffer, '{');

            var percents = new List<decimal>();

            var percentsStr = buffer.ToString();
            foreach (var p in percentsStr.Split(',').Select(s => s.Trim()))
            {
                if (p.Length == 0)
                {
                    Current.RecordError(ErrorType.Parser, Position.Create(start, stream.Position, Current.CurrentFilePath), "Expected `from`, `to`, or a percentage");
                    throw new StoppedParsingException();
                }

                decimal percent;
                if (p.Equals("from", StringComparison.InvariantCultureIgnoreCase))
                {
                    percent = 0;
                }
                else
                {
                    if (p.Equals("to", StringComparison.InvariantCultureIgnoreCase))
                    {
                        percent = 100;
                    }
                    else
                    {
                        if (!p.EndsWith("%") || !decimal.TryParse(p.Substring(0, p.Length -1 ), out percent))
                        {
                            Current.RecordError(ErrorType.Parser, Position.Create(start, stream.Position, Current.CurrentFilePath), "Expected `from`, `to`, or a percentage.  Found `" + p + "`");
                            throw new StoppedParsingException();
                        }
                    }
                }

                percents.Add(percent);
            }

            var rules = ParseCssRules(stream);

            return new KeyFrame(percents, rules, start, stream.Position, Current.CurrentFilePath);
        }

        internal static KeyFramesBlock ParseKeyFramesDirective(string prefix, ParserStream stream, int start)
        {
            var buffer = new StringBuilder();

            stream.ScanUntil(buffer, '{');

            var name = buffer.ToString().Trim();

            if (name.Length == 0)
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start, stream.Position, Current.CurrentFilePath), "Expected a name for the keyframe animation");

                throw new StoppedParsingException();
            }

            if((name[0] != '-' && char.IsDigit(name[0])) || 
               (name[0] == '-' && name.Length > 1 && char.IsDigit(name[1])) ||
               name.Any(a => a != '-' && !char.IsLetterOrDigit(a)))
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start, stream.Position, Current.CurrentFilePath), "Animation name `"+name+"` is not a valid identifier");
                throw new StoppedParsingException();
            }

            var frames = new List<KeyFrame>();
            var rules = new List<VariableProperty>();

            while (stream.HasMore())
            {
                var c = stream.Peek();

                if (char.IsWhiteSpace(c))
                {
                    stream.AdvancePastWhiteSpace();
                    continue;
                }

                if (c == '}')
                {
                    break;
                }

                if (c == '@')
                {
                    var rule = ParseMixinOrVariableRule(stream);
                    if (!(rule is VariableProperty))
                    {
                        Current.RecordError(ErrorType.Parser, rule, "Expected variable declaration");
                        throw new StoppedParsingException();
                    }
                    rules.Add((VariableProperty)rule);

                    continue;
                }

                frames.Add(ParseKeyFrame(stream));
            }

            if (stream.Peek() != '}')
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start, stream.Position, Current.CurrentFilePath), "Expected '}'");
                throw new StoppedParsingException();
            }

            stream.Advance(); // Skip }

            return new KeyFramesBlock(prefix, name, frames, rules, start, stream.Position, Current.CurrentFilePath);
        }

        internal static FontFaceBlock ParseFontFace(ParserStream stream, int start)
        {
            var ignored = new StringBuilder();

            stream.ScanUntil(ignored, '{');

            var rules = ParseCssRules(stream);

            return new FontFaceBlock(rules, start, stream.Position, Current.CurrentFilePath);
        }

        internal static ResetBlock ParseResetDirective(ParserStream stream, int start)
        {
            var ignored = new StringBuilder();
            stream.ScanUntil(ignored, '{');

            var contained = new List<Block>();

            char c;
            while ((c = stream.Peek()) != '}')
            {
                if (char.IsWhiteSpace(c))
                {
                    stream.AdvancePastWhiteSpace();
                    continue;
                }

                // More directive (probably)
                if (c == '@')
                {
                    contained.Add(ParseDirective(stream));
                    continue;
                }

                // Selector + block time!
                contained.Add(ParseSelectorAndBlock(stream));
            }

            var notAllowed = contained.Where(x => !(x is SelectorAndBlock || x is MoreVariable));

            foreach (var illegal in notAllowed)
            {
                Current.RecordError(ErrorType.Parser, illegal, "@reset can only contain blocks and variable declarations");
            }

            if (notAllowed.Count() != 0)
            {
                throw new StoppedParsingException();
            }

            // The whole @reset{} block disappears pretty quickly, but the actual
            //   blocks need to know what they were "near" for variable resolution.
            var variables = contained.OfType<MoreVariable>();
            var bound = new List<Block>();
            foreach (var x in contained)
            {
                var asBlock = x as SelectorAndBlock;
                if (asBlock != null)
                {
                    bound.Add(asBlock.InReset(variables));
                }
                else
                {
                    bound.Add(x);
                }
            }

            // Skip past }
            stream.Advance();

            return new ResetBlock(bound, start, stream.Position, Current.CurrentFilePath);
        }

        internal static Block ParseDirective(ParserStream stream)
        {
            const string import = "import";
            const string @using = "using";
            const string sprite = "sprite";
            const string charset = "charset";
            const string media = "media";
            const string keyframes = "keyframes";
            const string mozKeyframes = "-moz-keyframes";
            const string webKeyframes = "-webkit-keyframes";
            const string fontFace = "font-face";
            const string reset = "reset";

            stream.Advance(); // Advance past @

            var bufferStart = stream.Position;
            var buffer = new StringBuilder();
            var next = stream.WhichNextInsensitive(buffer, @using, sprite, import, charset, media, keyframes, mozKeyframes, webKeyframes, fontFace, reset);

            if (next == @using)
            {
                return ParseUsingDirective(stream);
            }

            if (next == sprite)
            {
                return ParseSpriteDeclaration(stream);
            }

            if (next == import)
            {
                return ParseImportDirective(stream);
            }

            if (next == charset)
            {
                return ParseCharsetDirective(stream);
            }

            if (next == media)
            {
                return ParseMediaDirective(stream);
            }

            if (next.In(keyframes, mozKeyframes, webKeyframes))
            {
                string prefix = "";
                if (next == mozKeyframes) prefix = "-moz-";
                if (next == webKeyframes) prefix = "-webkit-";

                return ParseKeyFramesDirective(prefix, stream, bufferStart);
            }

            if (next == fontFace)
            {
                return ParseFontFace(stream, bufferStart);
            }

            if (next == reset)
            {
                return ParseResetDirective(stream, bufferStart);
            }

            stream.PushBack(buffer.ToString());

            var leader = new StringBuilder();

            var eqOrPara = stream.ScanUntil(leader, '=', '(');

            if (eqOrPara == '=')
            {
                return ParseMoreVariable(leader.ToString(), stream, bufferStart);
            }

            return ParseMixinDeclaration(leader.ToString(), stream);
        }

        internal static Value ParseMoreValue(ParserStream stream)
        {
            var start = stream.Position;
            var valueStr = new StringBuilder();
            stream.ScanUntilWithNesting(valueStr, ';');

            var value = valueStr.ToString();

            return Value.Parse(value, start, stream.Position, Current.CurrentFilePath);
        }

        internal static MixinApplicationParameter ParseMixinParameter(string piece, int start, int stop)
        {
            if (piece.Length == 0)
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start, stop, Current.CurrentFilePath), "Expected parameter");
                throw new StoppedParsingException();
            }

            if (piece[0] != '@')
            {
                return new MixinApplicationParameter(null, Value.Parse(piece, start, stop, Current.CurrentFilePath, allowSelectorIncludes: true));
            }

            int i = piece.IndexOf('=');
            if (i == -1)
            {
                return new MixinApplicationParameter(null, Value.Parse(piece, start, stop, Current.CurrentFilePath, allowSelectorIncludes: true));
            }

            var name = piece.Substring(1, i - 1);
            var value = piece.Substring(i + 1);

            return new MixinApplicationParameter(name.Trim(), Value.Parse(value.Trim(), start, stop, Current.CurrentFilePath, allowSelectorIncludes: true));
        }

        internal static List<MixinApplicationParameter> ParseApplicationParameters(string application, int start)
        {
            var pieces = new List<Tuple<string, int>>();
            var current = new StringBuilder();

            int i = 0;
            while (i < application.Length)
            {
                if (application[i] == ',')
                {
                    pieces.Add(Tuple.Create(current.ToString().Trim(), start + i));
                    current.Clear();
                    i++;
                    continue;
                }

                current.Append(application[i]);
                if (application[i].In('"', '\'', '('))
                {
                    var advanceTo = application[i];
                    if (advanceTo == '(') advanceTo = ')';
                    i++;
                    while (application[i] != advanceTo && i < application.Length)
                    {
                        current.Append(application[i]);
                        i++;
                    }

                    if (application[i] != advanceTo)
                    {
                        Current.RecordError(ErrorType.Parser, Position.Create(start, start + i, Current.CurrentFilePath), "Expected '" + advanceTo + "'");
                        throw new StoppedParsingException();
                    }
                    current.Append(application[i]);
                }
                i++;
            }

            if (current.Length != 0)
            {
                pieces.Add(Tuple.Create(current.ToString().Trim(), start + i));
            }

            return pieces.Select(s => ParseMixinParameter(s.Item1, start, s.Item2)).ToList();
        }

        internal static Property ParseMixinOrVariableRule(ParserStream stream)
        {
            var start = stream.Position;

            var name = new StringBuilder();
            stream.Advance(); // Skip @

            bool trimmingWhiteSpace = false;

            while (stream.HasMore() && !stream.Peek().In('(', '='))
            {
                var c = stream.Read();

                if (char.IsWhiteSpace(c))
                {
                    trimmingWhiteSpace = true;
                    continue;
                }

                if (trimmingWhiteSpace || (!char.IsLetterOrDigit(c) && !c.In('-', '_')))
                {
                    Current.RecordError(ErrorType.Parser, Position.Create(start, stream.Position, Current.CurrentFilePath), "Unexpected character '" + c + "'");
                    throw new StoppedParsingException();
                }
                name.Append(c);
            }

            if (!stream.Peek().In('(', '='))
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start, stream.Position, Current.CurrentFilePath), "Expected '(' or '='");
                throw new StoppedParsingException();
            }

            if (stream.Peek() == '=')
            {
                stream.Advance();

                var localValue = ParseMoreValue(stream);
                var varName = name.ToString().Trim();

                if (varName.ToLower().In(ReservedWords))
                {
                    Current.RecordError(ErrorType.Parser, Position.Create(start, stream.Position, Current.CurrentFilePath), "'" + varName + "' cannot be a variable name.");
                }

                return new VariableProperty(varName, localValue, start, stream.Position, Current.CurrentFilePath);
            }

            stream.Advance();

            var startParams = stream.Position;

            var paramStart = stream.Position;
            var @params = new StringBuilder();
            stream.ScanUntilWithNesting(@params, ')');
            var paramStop = stream.Position;

            var options = new StringBuilder();
            var optionsStart = stream.Position;
            stream.ScanUntil(options, ';');

            var nameStr = name.ToString().Trim();
            var paramsStr = @params.ToString().Trim();
            var optionsStr = options.ToString().Trim();

            var optional = optionsStr.Contains('?');
            var overrides = optionsStr.Contains('!');

            var unexpected = optionsStr.Where(c => !char.IsWhiteSpace(c) && c != '?' && c != '!');

            if (unexpected.Count() != 0)
            {
                if (unexpected.Count() == 0)
                {
                    Current.RecordError(ErrorType.Parser, Position.Create(start, optionsStart + options.Length, Current.CurrentFilePath), "Unexpected character '" + unexpected.ElementAt(0) + "'");
                }
                else
                {
                    Current.RecordError(
                        ErrorType.Parser, 
                        Position.Create(
                            start, 
                            optionsStart + options.Length, 
                            Current.CurrentFilePath
                        ),
                        "Unexpected characters "+
                            string.Join(", ", unexpected.Select(c => "'"+c+"'"))
                    );
                }
                
                throw new StoppedParsingException();
            }

            if (name.Length == 0)
            {
                if (optional)
                {
                    Current.RecordWarning(ErrorType.Parser, Position.Create(start, optionsStart + options.Length, Current.CurrentFilePath), "Include directives are always optional, no trailing '?' is needed.");
                }
                return new IncludeSelectorProperty(Selector.Parse(paramsStr, paramStart, paramStop, Current.CurrentFilePath), overrides, start, stream.Position, Current.CurrentFilePath);
            }

            if (name.ToString().Trim().Equals("reset", StringComparison.InvariantCultureIgnoreCase))
            {
                if (paramsStr.Trim().Length != 0)
                {
                    return new ResetProperty(Selector.Parse(paramsStr, paramStart, paramStop, Current.CurrentFilePath), start, stream.Position, Current.CurrentFilePath);
                }

                return new ResetSelfProperty(InvalidSelector.Singleton, start, stream.Position, Current.CurrentFilePath);
            }

            return new MixinApplicationProperty(nameStr, ParseApplicationParameters(paramsStr, startParams), optional: optional, overrides: overrides, start: start, stop: stream.Position, filePath: Current.CurrentFilePath);
        }

        internal static Value ParseFontValue(ParserStream stream)
        {
            var start = stream.Position;
            var valueStr = new StringBuilder();
            stream.ScanUntilWithNesting(valueStr, ';');

            var value = valueStr.ToString();

            // The shorthand isn't in use, so we can handle this
            if(value.IndexOf('/') == -1)
            {
                return MoreValueParser.Parse(value, Position.Create(start, stream.Position, Current.CurrentFilePath));
            }

            return new StringValue(value);
        }

        internal static Property ParseRule(ParserStream stream)
        {
            var start = stream.Position;

            if (stream.Peek() == '@')
            {
                return ParseMixinOrVariableRule(stream);
            }

            var ruleName = new StringBuilder();
            var found = stream.ScanUntil(ruleName, ';', '{', '}');

            // Final semi-colon in a block is optional, so inject a ';' if we encounter a '}' in this case
            if (found == '}')
            {
                found = ';';
                // trailing semi-colon may be optional
                if (ruleName[ruleName.Length - 1] == ';')
                {
                    ruleName = ruleName.Remove(ruleName.Length - 1, 1);
                }
                stream.PushBack(new[] { '}' });
            }

            if (found == '{')
            {
                var nestedBlock = ParseSelectorAndBlock(stream, ruleName.ToString().Trim());

                return new NestedBlockProperty(nestedBlock, start, stream.Position);
            }

            if (found == null)
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start, stream.Position, Current.CurrentFilePath), "Expected ';', '{', or '}'");
                throw new StoppedParsingException();
            }

            var colon = ruleName.ToString().IndexOf(':');

            if (colon == -1)
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start, stream.Position, Current.CurrentFilePath), "Expected ':'");
                throw new StoppedParsingException();
            }

            var name = ruleName.ToString().Substring(0, colon).Trim();
            var valStr = ruleName.ToString().Substring(colon + 1).Trim() + ";";

            if (valStr == ";")
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start, stream.Position, Current.CurrentFilePath), "Expected value");
                throw new StoppedParsingException();
            }

            stream.PushBack(valStr);

            Value value;

            if (name.Equals("font", StringComparison.InvariantCultureIgnoreCase))
            {
                // font has a goofy shorthand, needs special treatment
                value = ParseFontValue(stream);
            }
            else
            {
                value = ParseMoreValue(stream);
            }

            return new NameValueProperty(name, value, start, stream.Position, Current.CurrentFilePath);
        }

        internal static List<Property> ParseCssRules(ParserStream stream)
        {
            var ret = new List<Property>();

            while (stream.HasMore() && stream.Peek() != '}')
            {
                var c = stream.Peek();

                if (char.IsWhiteSpace(c))
                {
                    stream.AdvancePastWhiteSpace();
                }
                else
                {
                    ret.Add(ParseRule(stream));
                }
            }

            stream.AdvancePast("}");
            return ret;
        }

        internal static SelectorAndBlock ParseSelectorAndBlock(ParserStream stream, string selectorStr = null)
        {
            var mark = stream.Position;

            int selectorStop, selectorStart;

            if (selectorStr.IsNullOrEmpty())
            {
                selectorStart = stream.Position;
                var selector = new StringBuilder();
                stream.ScanUntil(selector, '{');
                selectorStop = stream.Position;

                selectorStr = selector.ToString().Trim();

                if (selectorStr.IsNullOrEmpty())
                {
                    Current.RecordError(ErrorType.Parser, Position.Create(mark, stream.Position, Current.CurrentFilePath), "Expected selector");
                    throw new StoppedParsingException();
                }
            }
            else
            {
                selectorStop = mark;
                selectorStart= selectorStop -  selectorStr.Length;
            }

            var sel = Selector.Parse(selectorStr, selectorStart, selectorStop, Current.CurrentFilePath);
            var cssRules = ParseCssRules(stream);

            // Bind @reset() properties to their location w.r.t. selectors
            cssRules =
                cssRules.Select(
                    delegate(Property prop)
                    {
                        if (!(prop is ResetSelfProperty)) return prop;

                        return ((ResetSelfProperty)prop).BindToSelector(sel);
                    }
                ).ToList();

            return new SelectorAndBlock(sel, cssRules, null, mark, stream.Position, Current.CurrentFilePath);
        }

        public List<Block> Parse(string filePath, TextReader reader)
        {
            try
            {
                using (var stream = new ParserStream(reader))
                {
                    Current.SwitchToFile(filePath);

                    var ret = new List<Block>();

                    while (stream.HasMore())
                    {
                        char c = stream.Peek();

                        //Ignore white space
                        if (char.IsWhiteSpace(c))
                        {
                            stream.AdvancePastWhiteSpace();
                            continue;
                        }

                        // More directive (probably)
                        if (c == '@')
                        {
                            ret.Add(ParseDirective(stream));
                            continue;
                        }

                        // Selector + block time!
                        ret.Add(ParseSelectorAndBlock(stream));
                    }

                    return ret;
                }
            }
            catch (StoppedParsingException)
            {
                return null;
            }
        }

        public static Parser CreateParser()
        {
            return new Parser();
        }
    }
}
