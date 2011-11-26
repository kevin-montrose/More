﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using More.Model;
using System.Text.RegularExpressions;

namespace More.Parser
{
    class StoppedParsingException : Exception { }

    class Parser
    {
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
            var start = stream.MarkPos();

            var media = new StringBuilder();
            stream.ScanUntil(media, '{');

            var mediaStr = media.ToString().Trim();
            if (mediaStr.IsNullOrEmpty())
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start.Position, stream.Position, Current.CurrentFilePath), "Expected media list");
                throw new StoppedParsingException();
            }

            var mediaList = new List<Media>();
            foreach (var m in mediaStr.Split(','))
            {
                Media mParsed;
                if (!Enum.TryParse<Media>(m.Trim(), ignoreCase: true, result: out mParsed))
                {
                    Current.RecordWarning(ErrorType.Parser, Model.Position.Create(start.Position, stream.Position, Current.CurrentFilePath), "Unknown media type '" + m.Trim() + "', ignoring.");
                }
                else
                {
                    mediaList.Add(mParsed);
                }
            }

            if (mediaList.Count == 0)
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start.Position, stream.Position, Current.CurrentFilePath), "No recognized media found");
                throw new StoppedParsingException();
            }

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

            return new MediaBlock(mediaList, contained, start.Position, stream.Position, Current.CurrentFilePath);
        }

        internal static CssCharset ParseCharsetDirective(ParserStream stream)
        {
            var start = stream.MarkPos();

            var ignored = new StringBuilder();
            var quote = stream.ScanUntil(ignored, '"', '\'');

            if (quote == null)
            {
                Current.RecordError(ErrorType.Parser, Model.Position.Create(start.Position, stream.Position, Current.CurrentFilePath), "Expected quotation mark");
                throw new StoppedParsingException();
            }

            var isoName = new StringBuilder();
            stream.ScanUntil(isoName, quote.Value);

            stream.AdvancePast(";");

            stream.PopMark();

            var isoNameStr = isoName.ToString();

            if (!CssCharset.KnownCharset(isoNameStr))
            {
                Current.RecordWarning(ErrorType.Parser, Model.Position.Create(start.Position, stream.Position, Current.CurrentFilePath), "Unrecognized charset");
            }

            return new CssCharset(new QuotedStringValue(isoNameStr), start.Position, stream.Position);
        }

        internal static Using ParseUsingDirective(ParserStream stream)
        {
            var start = stream.MarkPos();

            var ignored = new StringBuilder();
            var quote = stream.ScanUntil(ignored, '"', '\'');

            if (quote == null)
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start.Position, stream.Position, Current.CurrentFilePath), "Expected quotation mark");
                throw new StoppedParsingException();
            }

            var file = new StringBuilder();
            stream.ScanUntil(file, quote.Value);

            var mediaBuff = new StringBuilder();
            stream.ScanUntil(mediaBuff, ';');

            var media = new List<Media>();
            var mediaStr = mediaBuff.ToString().Trim();
            if (mediaStr.Length > 0)
            {
                foreach (var m in mediaStr.Split(','))
                {
                    Media med;
                    if (!Enum.TryParse<Media>(m.Trim(), ignoreCase: true, result: out med))
                    {
                        Current.RecordWarning(ErrorType.Parser, Model.Position.Create(start.Position, stream.Position, Current.CurrentFilePath), "Unknown media type '" + m.Trim() + "', ignoring.");
                    }
                    else
                    {
                        media.Add(med);
                    }
                }
            }

            stream.PopMark();

            return new Using(file.ToString(), media, start.Position, stream.Position, Current.CurrentFilePath);
        }

        internal static Import ParseImportDirective(ParserStream stream)
        {
            var start = stream.MarkPos();

            var buffer = new StringBuilder();
            stream.ScanUntilWithNesting(buffer, ';');

            var toParse = buffer.ToString().Trim();

            Value val;
            List<Media> media = new List<Media>();
            string mediaStr;

            if (Regex.IsMatch(toParse, @"url\s*?\(", RegexOptions.IgnoreCase))
            {
                var i = toParse.LastIndexOf(')');

                if (i == -1)
                {
                    Current.RecordError(ErrorType.Parser, Position.Create(start.Position, stream.Position, Current.CurrentFilePath), "Expected ')'"); ;
                    throw new StoppedParsingException();
                }

                val = Value.Parse(toParse.Substring(0, i + 1), start.Position, start.Position + i + 1, Current.CurrentFilePath);

                mediaStr = toParse.Substring(i + 1);
            }
            else
            {
                if (!(toParse.StartsWith("\"") || toParse.StartsWith("'")))
                {
                    Current.RecordError(ErrorType.Parser, Position.Create(start.Position, stream.Position, Current.CurrentFilePath), "Expected quote");
                    throw new StoppedParsingException();
                }

                var i = toParse.LastIndexOf(toParse[0]);

                if (i == -1)
                {
                    Current.RecordError(ErrorType.Parser, Position.Create(start.Position, stream.Position, Current.CurrentFilePath), "Expected '" + toParse[0] + "'");
                    throw new StoppedParsingException();
                }

                val = Value.Parse(toParse.Substring(0, i + 1), start.Position, start.Position + i + 1, Current.CurrentFilePath);
                mediaStr = toParse.Substring(i + 1);
            }

            mediaStr = mediaStr.Trim();
            if (mediaStr.Length > 0)
            {
                foreach (var m in mediaStr.Split(','))
                {
                    Media mParsed;
                    if (!Enum.TryParse<Media>(m.Trim(), ignoreCase: true, result: out mParsed))
                    {
                        Current.RecordWarning(ErrorType.Parser, Model.Position.Create(start.Position, stream.Position, Current.CurrentFilePath), "Unknown media type '" + m.Trim() + "', ignoring.");
                    }
                    else
                    {
                        media.Add(mParsed);
                    }
                }
            }

            return new Import(val, media, start.Position, stream.Position);
        }

        internal static SpriteRule ParseSpriteRule(ParserStream stream)
        {
            var start = stream.MarkPos();

            if (stream.Peek() != '@')
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start.Position, stream.Position, Current.CurrentFilePath), "Expected '@'");
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
                Current.RecordError(ErrorType.Parser, Position.Create(start.Position, stream.Position, Current.CurrentFilePath), "Expected quotation mark");
                throw new StoppedParsingException();
            }

            var valueStart = stream.Position;
            var valueStr = new StringBuilder();
            valueStr.Append(quote.Value);
            stream.ScanUntil(valueStr, quote.Value);
            valueStr.Append(quote.Value);

            stream.AdvancePast(";");

            var value = (QuotedStringValue)Value.Parse(valueStr.ToString(), valueStart, stream.Position, Current.CurrentFilePath);

            stream.PopMark();

            return new SpriteRule(name.ToString().Trim(), value, start.Position, stream.Position, Current.CurrentFilePath);
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
            var start = stream.MarkPos();

            stream.AdvancePast("(");

            var ignored = new StringBuilder();
            var adTo = stream.ScanUntil(ignored, '"', '\'');

            if (ignored.ToString().Any(a => !char.IsWhiteSpace(a)))
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start.Position, stream.Position, Current.CurrentFilePath), "Expected quotation mark");
                throw new StoppedParsingException();
            }

            if (adTo == null)
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start.Position, stream.Position, Current.CurrentFilePath), "Expected quotation mark");
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

            stream.PopMark();

            return new SpriteBlock((QuotedStringValue)Value.Parse(name.ToString(), nameStart, nameStop, Current.CurrentFilePath), rules, start.Position, stream.Position, Current.CurrentFilePath);
        }

        internal static List<MixinParameter> ParseMixinDeclarationParameter(string parse, ParserStream.Mark start)
        {
            var stop = start.Position + parse.Length;

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
                    Current.RecordError(ErrorType.Parser, Position.Create(start.Position, start.Position + offset, Current.CurrentFilePath), "Expected '@'");
                    throw new StoppedParsingException();
                }

                var i = x.IndexOf('=');
                var q1 = piece.IndexOf('"');
                var q2 = piece.IndexOf('\'');
                if ((q1 != -1 && i > q1) || (q2 != -1 && i > q2))
                {
                    Current.RecordError(ErrorType.Parser, Position.Create(start.Position, start.Position + offset, Current.CurrentFilePath), "Unable to parse value");
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
                        value = NotFoundValue.Default.BindToPosition(start.Position, stop, Current.CurrentFilePath);
                    }
                    else
                    {
                        value = Value.Parse(@default, start.Position, stop, Current.CurrentFilePath);
                    }
                }

                if (name.Equals("arguments", StringComparison.InvariantCultureIgnoreCase))
                {
                    Current.RecordError(ErrorType.Parser, Position.Create(start.Position, stop, Current.CurrentFilePath), "arguments cannot be the name of a parameter to a mixin.");
                }

                ret.Add(new MixinParameter(name, value));
            }

            return ret;
        }

        internal static MixinBlock ParseMixinDeclaration(string name, ParserStream stream)
        {
            var start = stream.MarkPos();
            var @params = new StringBuilder();
            stream.ScanUntil(@params, ')');

            name = name.Trim();

            if (name.Length == 0)
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start.Position, stream.Position, Current.CurrentFilePath), "Expected mixin name");
                throw new StoppedParsingException();
            }

            stream.AdvancePast("{");

            stream.PopMark();

            return new MixinBlock(name, ParseMixinDeclarationParameter(@params.ToString(), start), ParseCssRules(stream), start.Position, stream.Position, Current.CurrentFilePath);
        }

        internal static MoreVariable ParseMoreVariable(string name, ParserStream stream, int start)
        {
            name = name.Trim();

            var valueStart = stream.Position;
            var valueStr = new StringBuilder();
            stream.ScanUntilWithNesting(valueStr, ';');

            var value = Value.Parse(valueStr.ToString().Trim(), valueStart, stream.Position, Current.CurrentFilePath, allowSelectorIncludes: true);

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

            stream.Advance(); // Advance past @

            var bufferStart = stream.MarkPos();
            var buffer = new StringBuilder();
            var next = stream.WhichNextInsensitive(buffer, @using, sprite, import, charset, media, keyframes, mozKeyframes, webKeyframes, fontFace);

            if (next == @using)
            {
                stream.PopMark();
                return ParseUsingDirective(stream);
            }

            if (next == sprite)
            {
                stream.PopMark();
                return ParseSpriteDeclaration(stream);
            }

            if (next == import)
            {
                stream.PopMark();
                return ParseImportDirective(stream);
            }

            if (next == charset)
            {
                stream.PopMark();
                return ParseCharsetDirective(stream);
            }

            if (next == media)
            {
                stream.PopMark();
                return ParseMediaDirective(stream);
            }

            if (next.In(keyframes, mozKeyframes, webKeyframes))
            {
                stream.PopMark();

                string prefix = "";
                if (next == mozKeyframes) prefix = "-moz-";
                if (next == webKeyframes) prefix = "-webkit-";

                return ParseKeyFramesDirective(prefix, stream, bufferStart.Position);
            }

            if (next == fontFace)
            {
                stream.PopMark();

                return ParseFontFace(stream, bufferStart.Position);
            }

            stream.PushBack(buffer.ToString());

            var leader = new StringBuilder();

            var eqOrPara = stream.ScanUntil(leader, '=', '(');

            if (eqOrPara == '=')
            {
                stream.PopMark();
                return ParseMoreVariable(leader.ToString(), stream, bufferStart.Position);
            }

            stream.PopMark();
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

        internal static List<MixinApplicationParameter> ParseApplicationParameters(string application, ParserStream.Mark start)
        {
            var pieces = new List<Tuple<string, int>>();
            var current = new StringBuilder();

            int i = 0;
            while (i < application.Length)
            {
                if (application[i] == ',')
                {
                    pieces.Add(Tuple.Create(current.ToString().Trim(), start.Position + i));
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
                        Current.RecordError(ErrorType.Parser, Position.Create(start.Position, start.Position + i, Current.CurrentFilePath), "Expected '" + advanceTo + "'");
                        throw new StoppedParsingException();
                    }
                    current.Append(application[i]);
                }
                i++;
            }

            if (current.Length != 0)
            {
                pieces.Add(Tuple.Create(current.ToString().Trim(), start.Position + i));
            }

            return pieces.Select(s => ParseMixinParameter(s.Item1, start.Position, s.Item2)).ToList();
        }

        internal static Property ParseMixinOrVariableRule(ParserStream stream)
        {
            var start = stream.MarkPos();

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
                    Current.RecordError(ErrorType.Parser, Position.Create(start.Position, stream.Position, Current.CurrentFilePath), "Unexpected character '" + c + "'");
                    throw new StoppedParsingException();
                }
                name.Append(c);
            }

            if (!stream.Peek().In('(', '='))
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start.Position, stream.Position, Current.CurrentFilePath), "Expected '(' or '='");
                throw new StoppedParsingException();
            }

            if (stream.Peek() == '=')
            {
                stream.Advance();

                var localValue = ParseMoreValue(stream);
                var varName = name.ToString().Trim();

                return new VariableProperty(varName, localValue, start.Position, stream.Position, Current.CurrentFilePath);
            }

            stream.Advance();

            var startParams = stream.MarkPos();
            stream.PopMark();

            var paramStart = stream.Position;
            var @params = new StringBuilder();
            stream.ScanUntilWithNesting(@params, ')');
            var paramStop = stream.Position;

            var options = new StringBuilder();
            var optionsStart = stream.MarkPos();
            stream.PopMark();
            stream.ScanUntil(options, ';');

            var nameStr = name.ToString().Trim();
            var paramsStr = @params.ToString().Trim();
            var optionsStr = options.ToString().Trim();

            stream.PopMark();

            var optional = optionsStr.Contains('?');
            var overrides = optionsStr.Contains('!');

            var unexpected = optionsStr.Where(c => !char.IsWhiteSpace(c) && c != '?' && c != '!');

            if (unexpected.Count() != 0)
            {
                if (unexpected.Count() == 0)
                {
                    Current.RecordError(ErrorType.Parser, Position.Create(start.Position, optionsStart.Position + options.Length, Current.CurrentFilePath), "Unexpected character '" + unexpected.ElementAt(0) + "'");
                }
                else
                {
                    Current.RecordError(
                        ErrorType.Parser, 
                        Position.Create(
                            start.Position, 
                            optionsStart.Position + options.Length, 
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
                    Current.RecordWarning(ErrorType.Parser, Position.Create(start.Position, optionsStart.Position + options.Length, Current.CurrentFilePath), "Include directives are always optional, no trailing '?' is needed.");
                }
                return new IncludeSelectorProperty(Selector.Parse(paramsStr, paramStart, paramStop, Current.CurrentFilePath), overrides, start.Position, stream.Position, Current.CurrentFilePath);
            }

            return new MixinApplicationProperty(nameStr, ParseApplicationParameters(paramsStr, startParams), optional: optional, overrides: overrides, start: start.Position, stop: stream.Position, filePath: Current.CurrentFilePath);
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
            var start = stream.MarkPos();
            stream.PopMark();

            if (stream.Peek() == '@')
            {
                return ParseMixinOrVariableRule(stream);
            }

            var ruleName = new StringBuilder();
            var found = stream.ScanUntil(ruleName, ':', '{');

            var minified = new String(ruleName.ToString().Where(s => !char.IsWhiteSpace(s)).ToArray());

            // Need this trick for nested blocks with psuedo classes
            if (minified.Length == 0 || minified == "&")
            {
                ruleName.Append(":");
                found = stream.ScanUntil(ruleName, '{');
            }

            if (found == '{')
            {
                var nestedBlock = ParseSelectorAndBlock(stream, ruleName.ToString().Trim());

                return new NestedBlockProperty(nestedBlock, start.Position, stream.Position);
            }

            if (found == null)
            {
                Current.RecordError(ErrorType.Parser, Position.Create(start.Position, stream.Position, Current.CurrentFilePath), "Expected '{' or ':'");
                throw new StoppedParsingException();
            }

            var name = ruleName.ToString().Trim();

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

            return new NameValueProperty(name, value, start.Position, stream.Position, Current.CurrentFilePath);
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
            var mark = stream.MarkPos();

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
                    Current.RecordError(ErrorType.Parser, Position.Create(mark.Position, stream.Position, Current.CurrentFilePath), "Expected selector");
                    throw new StoppedParsingException();
                }
            }
            else
            {
                selectorStop = mark.Position;
                selectorStart= selectorStop -  selectorStr.Length;
            }

            var sel = Selector.Parse(selectorStr, selectorStart, selectorStop, Current.CurrentFilePath);
            var cssRules = ParseCssRules(stream);

            stream.PopMark();

            return new SelectorAndBlock(sel, cssRules, mark.Position, stream.Position, Current.CurrentFilePath);
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