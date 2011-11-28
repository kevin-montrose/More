using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Parser;
using System.IO;

namespace MoreInternals.Model
{
    [Flags]
    public enum ErrorType
    {
        Parser,
        Compiler
    }

    public class Error
    {
        public int StartPosition { get; protected set; }
        public int EndPosition { get; protected set; }
        public ErrorType Type { get; protected set; }
        public string File { get; protected set; }
        public string Message { get; protected set; }

        public virtual string Snippet(TextReader file)
        {
            var text = file.ReadToEnd();

            int i = text.LastIndexOf('\n', Math.Min(text.Length - 1, StartPosition));
            int j = text.IndexOf('\n', Math.Min(text.Length - 1, EndPosition));

            if (i == -1) { i = 0; } else { i++; }
            if (j == -1) { j = text.Length; }

            return text.Substring(i, j - i);
        }

        public static Error Create(ErrorType type, int start, int stop, string msg, string file)
        {
            return new Error
            {
                StartPosition = start,
                EndPosition = stop,
                Type = type,
                File = file,
                Message = msg
            };
        }
    }

    class OutOfProcError : Error
    {
        public override string Snippet(TextReader file)
        {
            return "";
        }

        public static OutOfProcError Create(ErrorType type, string msg)
        {
            return new OutOfProcError
            {
                StartPosition = -1,
                EndPosition = -1,
                Type = type,
                File = Guid.NewGuid().ToString(),
                Message = msg
            };
        }
    }
}
