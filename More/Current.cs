using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using More.Helpers;
using System.IO;
using System.Threading;
using More.Parser;
using More.Model;

namespace More
{
    enum WriterMode
    {
        Pretty,
        Minimize
    }

    [Flags]
    enum Options
    {
        None = 0,
        WarningsAsErrors,
        Minify,
        OptimizeCompression
    }

    static class Current
    {
        private static ThreadLocal<Scope> _globalScope = new ThreadLocal<Scope>();
        public static Scope GlobalScope
        {
            get
            {
                return _globalScope.IsValueCreated ? _globalScope.Value : new Scope(new Dictionary<string, Value>(), new Dictionary<string, MixinBlock>());
            }

            set
            {
                _globalScope.Value = value;
            }
        }

        private static ThreadLocal<Options> _options = new ThreadLocal<Options>();
        public static Options Options
        {
            get
            {
                return _options.IsValueCreated ? _options.Value : More.Options.None;
            }
            private set
            {
                _options.Value = value;
            }
        }

        private static ThreadLocal<string> _workingDir = new ThreadLocal<string>();
        public static string WorkingDirectory {
            get
            {
                return _workingDir.Value;
            }
            private set
            {
                _workingDir.Value = value;
            }
        }

        private static ThreadLocal<string> _currentFilePath = new ThreadLocal<string>();
        public static string CurrentFilePath
        {
            get
            {
                return _currentFilePath.Value;
            }
            private set
            {
                _currentFilePath.Value = value;
            }
        }

        private static ThreadLocal<WriterMode> _writerMode = new ThreadLocal<WriterMode>();
        public static WriterMode WriterMode
        {
            get
            {
                return _writerMode.Value;
            }
            private set
            {
                _writerMode.Value = value;
            }
        }

        static Current()
        {
            SetWriterMode(More.WriterMode.Pretty);
        }

        public static void Reset()
        {
            try
            {
                _currentFilePath.Dispose();
                _globalScope.Dispose();
                _options.Dispose();
                _workingDir.Dispose();
                _writerMode.Dispose();
            }
            catch { }

            _currentFilePath = new ThreadLocal<string>();
            _globalScope = new ThreadLocal<Scope>();
            _options = new ThreadLocal<Options>();
            _workingDir = new ThreadLocal<string>();
            _writerMode = new ThreadLocal<WriterMode>();
        }

        public static void SetWorkingDirectory(string path)
        {
            WorkingDirectory = path;
        }

        public static void SwitchToFile(string path)
        {
            CurrentFilePath = path;
        }

        public static void SetWriterMode(WriterMode mode)
        {
            WriterMode = mode;
        }

        public static ICssWriter GetWriter(TextWriter writer)
        {
            switch (WriterMode)
            {
                case More.WriterMode.Pretty: return new PrettyCssWriter(writer);
                case More.WriterMode.Minimize: return new MinimalCssWriter(writer);
                default: throw new InvalidOperationException("Unknown writer mode [" + WriterMode + "]");
            }
        }

        private static ThreadLocal<Dictionary<ErrorType, List<Error>>> Errors = new ThreadLocal<Dictionary<ErrorType, List<Error>>>();
        public static void RecordError(ErrorType type, IPosition position, string message)
        {
            Dictionary<ErrorType, List<Error>> errors;
            if (!Errors.IsValueCreated)
            {
                errors = new Dictionary<ErrorType, List<Error>>();
                Errors.Value = errors;
            }
            else
            {
                errors = Errors.Value;
            }

            List<Error> ofType;
            if (!errors.TryGetValue(type, out ofType))
            {
                ofType = new List<Error>();
                errors[type] = ofType;
            }

            ofType.Add(Error.Create(type, position.Start, position.Stop, message, position.FilePath));
        }

        private static ThreadLocal<Dictionary<ErrorType, List<Error>>> Warnings = new ThreadLocal<Dictionary<ErrorType, List<Error>>>();
        public static void RecordWarning(ErrorType type, IPosition position, string message)
        {
            if ((Current.Options & More.Options.WarningsAsErrors) != 0)
            {
                RecordError(type, position, message);
                return;
            }

            Dictionary<ErrorType, List<Error>> warnings;
            if (!Warnings.IsValueCreated)
            {
                warnings = new Dictionary<ErrorType, List<Error>>();
                Warnings.Value = warnings;
            }
            else
            {
                warnings = Warnings.Value;
            }

            List<Error> ofType;
            if (!warnings.TryGetValue(type, out ofType))
            {
                ofType = new List<Error>();
                warnings[type] = ofType;
            }

            ofType.Add(Error.Create(type, position.Stop, position.Stop, message, position.FilePath));
        }

        private static ThreadLocal<List<string>> InfoMessages = new ThreadLocal<List<string>>();
        public static void RecordInfo(string msg)
        {
            if (!InfoMessages.IsValueCreated)
            {
                InfoMessages.Value = new List<string>();
            }

            InfoMessages.Value.Add(msg);
        }

        public static List<string> GetInfo()
        {
            if (!InfoMessages.IsValueCreated) return new List<string>();

            return InfoMessages.Value;
        }

        public static bool HasErrors()
        {
            return Errors.IsValueCreated;
        }

        public static bool HasWarnings()
        {
            return Warnings.IsValueCreated;
        }

        public static List<Error> GetAllErrors()
        {
            var ret = new List<Error>();

            ret.AddRange(GetErrors(ErrorType.Compiler));
            ret.AddRange(GetErrors(ErrorType.Parser));

            return ret;
        }

        public static List<Error> GetAllWarnings()
        {
            var ret = new List<Error>();

            ret.AddRange(GetWarnings(ErrorType.Compiler));
            ret.AddRange(GetWarnings(ErrorType.Parser));

            return ret;
        }

        public static List<Error> GetErrors(ErrorType ofType)
        {
            if (!HasErrors()) return new List<Error>();

            var error = Errors.Value;
            List<Error> ret;
            if (!error.TryGetValue(ofType, out ret)) return new List<Error>();

            return ret;
        }

        public static List<Error> GetWarnings(ErrorType ofType)
        {
            if (!HasWarnings()) return new List<Error>();

            var warning = Warnings.Value;
            List<Error> ret;
            if (!warning.TryGetValue(ofType, out ret)) return new List<Error>();

            return ret;
        }

        public static void ClearErrors()
        {
            Errors = new ThreadLocal<Dictionary<ErrorType, List<Error>>>();
            Warnings = new ThreadLocal<Dictionary<ErrorType, List<Error>>>();
            InfoMessages = new ThreadLocal<List<string>>();
        }

        public static void SetOptions(Options opts)
        {
            if(!Options.HasFlag(opts))
            {
                Options = Options | opts;
            }
        }

        public static void ClearOptions(Options opts)
        {
            if (Options.HasFlag(opts))
            {
                Options = Options ^ opts;
            }
        }

        public static void SetGlobalScope(Scope scope)
        {
            GlobalScope = scope;
        }
    }
}
