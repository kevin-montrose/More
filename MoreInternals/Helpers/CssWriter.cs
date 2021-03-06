﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using MoreInternals.Model;

namespace MoreInternals.Helpers
{
    interface ICssWriter
    {
        void WriteSelector(Selector selector);
        
        void StartClass();
        void EndClass();

        void WriteSelectorBlock(SelectorAndBlock block);

        void WriteImport(Value toImport, MediaQuery forMedia);
        void WriteCharset(QuotedStringValue charset);
        void WriteMedia(MediaQuery forMedia);
        void WriteKeyframes(KeyFramesBlock keyframes);
        void WriteFontFace(FontFaceBlock fontface);
    }

    class MinimalCssWriter : IDisposable, ICssWriter
    {
        private TextWriter _wrapped;

        public MinimalCssWriter(TextWriter writer)
        {
            _wrapped = writer;
        }

        public void WriteSelector(Selector selector)
        {
            selector.Write(_wrapped);
        }

        public void StartClass()
        {
            _wrapped.Write('{');
        }

        public void EndClass()
        {
            _wrapped.Write('}');
        }

        internal void WriteRule(NameValueProperty rule, bool lastRule)
        {
            _wrapped.Write(rule.Name);
            _wrapped.Write(':');
            rule.Value.Write(_wrapped);
            
            if (!lastRule)
            {
                _wrapped.Write(';');
            }
        }

        public void WriteSelectorBlock(SelectorAndBlock block)
        {
            WriteSelector(block.Selector);
            StartClass();

            var properties = block.Properties.Cast<NameValueProperty>();
            var firstN = properties.Take(properties.Count() - 1);
            var last = properties.LastOrDefault();

            foreach (var rule in firstN)
            {
                WriteRule(rule, lastRule: false);
            }

            if (last != null)
            {
                WriteRule(last, lastRule: true);
            }
            EndClass();
        }

        public void WriteImport(Value toImport, MediaQuery forMedia)
        {
            _wrapped.Write("@import");
            _wrapped.Write(' ');
            toImport.Write(_wrapped);

            if (!(forMedia is MediaType) || ((MediaType)forMedia).Type != Media.all)
            {
                _wrapped.Write(' ');
                forMedia.Write(_wrapped);
            }

            _wrapped.Write(';');
        }

        public void WriteCharset(QuotedStringValue charset)
        {
            // you have to be really precise about this
            // http://www.w3.org/TR/CSS2/syndata.html#charset
            _wrapped.Write("@charset \"");
            _wrapped.Write(charset.Value);
            _wrapped.Write("\";");
        }

        public void WriteMedia(MediaQuery forMedia)
        {
            _wrapped.Write("@media ");
            forMedia.Write(_wrapped);
        }

        public void WriteFontFace(FontFaceBlock fontface)
        {
            _wrapped.Write("@font-face");
            StartClass();
            
            var properties = fontface.Properties.Cast<NameValueProperty>();
            var firstN = properties.Take(properties.Count() - 1);
            var last = properties.LastOrDefault();
            foreach (var rule in firstN)
            {
                WriteRule(rule, lastRule: false);
            }

            if (last != null)
            {
                WriteRule(last, lastRule: true);
            }

            EndClass();
        }

        public void WriteKeyframes(KeyFramesBlock keyframes)
        {
            _wrapped.Write("@");
            _wrapped.Write(keyframes.Prefix);
            _wrapped.Write("keyframes ");
            _wrapped.Write(keyframes.Name);
            StartClass();
            foreach (var frame in keyframes.Frames)
            {
                var percents =
                    string.Join(
                        ",",
                        frame.Percentages.Select(
                            p =>
                            {
                                // to is shorter than 100%
                                if (p == 100m) return "to";

                                return p + "%";
                            }
                        )
                    );

                _wrapped.Write(percents);
                StartClass();

                var properties = frame.Properties.Cast<NameValueProperty>();
                var firstN = properties.Take(properties.Count() - 1);
                var last = properties.LastOrDefault();

                foreach (var rule in firstN)
                {
                    WriteRule(rule, lastRule: false);
                }

                if (last != null)
                {
                    WriteRule(last, lastRule: true);
                }
                EndClass();
            }
            EndClass();
        }

        #region IDisposable Members

        public void Dispose()
        {
            try
            {
                _wrapped.Dispose();
            }
            catch { }
        }

        #endregion
    }

    class PrettyCssWriter : IDisposable, ICssWriter
    {
        private TextWriter _wrapped;

        public PrettyCssWriter(TextWriter writer)
        {
            _wrapped = writer;
        }

        public void WriteSelector(Selector selector)
        {
            selector.Write(_wrapped);
            _wrapped.WriteLine();
        }

        public void StartClass()
        {
            _wrapped.WriteLine('{');
        }

        public void EndClass()
        {
            _wrapped.WriteLine('}');
        }

        internal void WriteRule(NameValueProperty rule)
        {
            _wrapped.Write("  ");
            _wrapped.Write(rule.Name);
            _wrapped.Write(": ");
            rule.Value.Write(_wrapped);
            _wrapped.WriteLine(';');
        }

        public void WriteSelectorBlock(SelectorAndBlock block)
        {
            WriteSelector(block.Selector);
            StartClass();
            foreach (var rule in block.Properties.Cast<NameValueProperty>())
            {
                WriteRule(rule);
            }
            EndClass();
        }

        public void WriteImport(Value toImport, MediaQuery forMedia)
        {
            _wrapped.Write("@import");
            _wrapped.Write(' ');
            toImport.Write(_wrapped);

            if (!(forMedia is MediaType) || ((MediaType)forMedia).Type != Media.all)
            {
                _wrapped.Write(' ');
                forMedia.Write(_wrapped);
            }

            _wrapped.Write(';');
        }

        public void WriteCharset(QuotedStringValue charset)
        {
            // you have to be really precise about this
            // http://www.w3.org/TR/CSS2/syndata.html#charset
            _wrapped.Write("@charset \"");
            _wrapped.Write(charset.Value);
            _wrapped.Write("\";");
        }

        public void WriteMedia(MediaQuery forMedia)
        {
            _wrapped.Write("@media ");
            forMedia.Write(_wrapped);
        }

        public void WriteFontFace(FontFaceBlock fontface)
        {
            _wrapped.Write("@font-face");
            StartClass();
            foreach (var rule in fontface.Properties.Cast<NameValueProperty>())
            {
                WriteRule(rule);
            }
            EndClass();
        }

        public void WriteKeyframes(KeyFramesBlock keyframes)
        {
            _wrapped.Write("@");
            _wrapped.Write(keyframes.Prefix);
            _wrapped.Write("keyframes ");
            _wrapped.Write(keyframes.Name);
            StartClass();
            foreach (var frame in keyframes.Frames)
            {
                var percents =
                    string.Join(
                        ",",
                        frame.Percentages.Select(
                            p =>
                            {
                                if (p == 100m) return "to";
                                if (p == 0m) return "from";

                                return p + "%";
                            }
                        )
                    );

                _wrapped.WriteLine(percents);
                StartClass();

                foreach (var rule in frame.Properties.Cast<NameValueProperty>())
                {
                    WriteRule(rule);
                }
                EndClass();
            }
            EndClass();
        }

        #region IDisposable Members

        public void Dispose()
        {
            _wrapped.Dispose();
        }

        #endregion
    }
}
