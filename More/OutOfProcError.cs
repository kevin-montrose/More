using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;
using System.IO;

namespace More
{
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
