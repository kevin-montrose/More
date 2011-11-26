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

    class Context
    {
        internal Scope GlobalScope { get; set; }
        internal Options Options { get; set; }
        internal string WorkingDirectory { get; set; }
        internal string CurrentFilePath { get; set; }
        internal WriterMode WriterMode { get; set; }
        internal Dictionary<ErrorType, List<Error>> Errors { get; set; }
        internal Dictionary<ErrorType, List<Error>> Warnings { get; set; }
        internal List<string> InfoMessages { get; set; }

        public Context()
        {
            InfoMessages = new List<string>();
            Errors = new Dictionary<ErrorType, List<Error>>();
            Warnings = new Dictionary<ErrorType, List<Error>>();
            WriterMode = More.WriterMode.Pretty;
            Options = More.Options.None;
        }

        public Context(Scope scope, Options options, string workingDir, string currentFile, WriterMode writer)
            : this()
        {
            GlobalScope = scope;
            Options = options;
            WorkingDirectory = workingDir;
            CurrentFilePath = currentFile;
            WriterMode = writer;
        }
    }

    static class Current
    {
        private static ThreadLocal<Context> InnerContext = new ThreadLocal<Context>();

        public static Scope GlobalScope
        {
            get { return InnerContext.Value.GlobalScope; }
            set { InnerContext.Value.GlobalScope = value; }
        }

        public static Options Options
        {
            get { return InnerContext.Value.Options; }
            set { InnerContext.Value.Options = value; }
        }

        public static string WorkingDirectory {
            get { return InnerContext.Value.WorkingDirectory; }
            set { InnerContext.Value.WorkingDirectory = value; }
        }

        public static string CurrentFilePath
        {
            get { return InnerContext.Value.CurrentFilePath; }
            set { InnerContext.Value.CurrentFilePath = value; }
        }

        public static WriterMode WriterMode
        {
            get { return InnerContext.Value.WriterMode; }
            set { InnerContext.Value.WriterMode = value; }
        }

        public static void SetContext(Context context)
        {
            InnerContext.Value = context;
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

        public static void RecordError(ErrorType type, IPosition position, string message)
        {
            Dictionary<ErrorType, List<Error>> errors = InnerContext.Value.Errors;

            List<Error> ofType;
            if (!errors.TryGetValue(type, out ofType))
            {
                ofType = new List<Error>();
                errors[type] = ofType;
            }

            ofType.Add(Error.Create(type, position.Start, position.Stop, message, position.FilePath));
        }

        public static void RecordWarning(ErrorType type, IPosition position, string message)
        {
            if ((Current.Options & More.Options.WarningsAsErrors) != 0)
            {
                RecordError(type, position, message);
                return;
            }

            Dictionary<ErrorType, List<Error>> warnings = InnerContext.Value.Warnings;

            List<Error> ofType;
            if (!warnings.TryGetValue(type, out ofType))
            {
                ofType = new List<Error>();
                warnings[type] = ofType;
            }

            ofType.Add(Error.Create(type, position.Stop, position.Stop, message, position.FilePath));
        }

        public static void RecordInfo(string msg)
        {
            InnerContext.Value.InfoMessages.Add(msg);
        }

        public static List<string> GetInfo()
        {
            return InnerContext.Value.InfoMessages;
        }

        public static bool HasErrors()
        {
            return InnerContext.Value.Errors.Count > 0;
        }

        public static bool HasWarnings()
        {
            return InnerContext.Value.Warnings.Count > 0;
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

            var error = InnerContext.Value.Errors;
            List<Error> ret;
            if (!error.TryGetValue(ofType, out ret)) return new List<Error>();

            return ret;
        }

        public static List<Error> GetWarnings(ErrorType ofType)
        {
            if (!HasWarnings()) return new List<Error>();

            var warning = InnerContext.Value.Warnings;
            List<Error> ret;
            if (!warning.TryGetValue(ofType, out ret)) return new List<Error>();

            return ret;
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
