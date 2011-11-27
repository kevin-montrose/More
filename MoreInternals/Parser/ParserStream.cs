using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MoreInternals.Parser
{
    class ParserStream : IDisposable
    {
        public class Mark
        {
            public int Position { get; set; }

            public override string ToString()
            {
                return "Char: " + Position;
            }
        }

        private CommentlessStream _wrapped { get; set; }

        private Queue<char> _buffer = new Queue<char>();

        public int Position
        {
            get
            {
                return _wrapped.Position - _buffer.Count;
            }
        }

        public ParserStream(TextReader wrapped)
        {
            _wrapped = new CommentlessStream(wrapped);
        }

        /// <summary>
        /// Puts these characters back into the stream
        /// </summary>
        public void PushBack(IEnumerable<char> pushBack)
        {
            foreach (var c in pushBack)
            {
                _buffer.Enqueue(c);
            }
        }
        
        /// <summary>
        /// Returns true if there are more characters to be read from the stream.
        /// </summary>
        /// <returns></returns>
        public bool HasMore()
        {
            return _buffer.Count > 0 || _wrapped.Peek() != -1;
        }

        private int DirectPeek()
        {
            int i;
            if (_buffer.Count > 0)
            {
                i = _buffer.Peek();
            }
            else
            {
                i = _wrapped.Peek();
            }

            return i;
        }

        /// <summary>
        /// Looks at the next character in the stream
        /// </summary>
        public char Peek()
        {
            int i = DirectPeek();

            if (i == -1) throw new InvalidOperationException("Tried to read past end of stream");

            var ret = (char)i;

            return ret;
        }

        private int DirectRead()
        {
            int i;
            if (_buffer.Count > 0)
            {
                i = _buffer.Dequeue();
            }
            else
            {
                i = _wrapped.Read();
            }

            return i;
        }

        /// <summary>
        /// Read a single character from the stream.
        /// </summary>
        public char Read()
        {
            int i = DirectRead();

            if (i == -1) throw new InvalidOperationException("Tried to read past end of stream");

            var ret = (char)i;

            return ret;
        }

        /// <summary>
        /// Advance the stream by one character.
        /// </summary>
        public void Advance()
        {
            Read();
        }

        /// <summary>
        /// Advances until c is found, placing everything before needle into buffer.
        /// Advances past needle as well.
        /// 
        /// Differs from ScanUtil in that needles between quotes and ()'s are not counted as terminating.
        /// </summary>
        public void ScanUntilWithNesting(StringBuilder buffer, char needle, bool requireFound = true)
        {
            var start = Position;

            var nonTerminals = new Stack<char>();
            bool found = false;
            while (HasMore())
            {
                var c = Read();

                if (nonTerminals.Count > 0 && nonTerminals.Peek() == c)
                {
                    nonTerminals.Pop();
                    buffer.Append(c);
                    continue;
                }
                else
                {
                    if (c == '\'' || c == '"')
                    {
                        nonTerminals.Push(c);
                    }

                    if (c == '(')
                    {
                        nonTerminals.Push(')');
                    }
                }

                if (nonTerminals.Count == 0 && c == needle)
                {
                    found = true;
                    break;
                }

                buffer.Append(c);
            }

            if (!found && requireFound)
            {
                Current.RecordError(Model.ErrorType.Parser, Model.Position.Create(start, Position, Current.CurrentFilePath), "Expected '" + needle + "'");
                throw new StoppedParsingException();
            }
        }

        /// <summary>
        /// Advances the stream until one of needles is found, placing everything *before* needle into buffer.
        /// Advances past the needle as well.
        /// 
        /// If none of the needles are found, an error is encountered.
        /// </summary>
        public char? ScanUntil(StringBuilder buffer, params char[] needles)
        {
            char? found = null;
            while (HasMore())
            {
                var c = Read();

                if (c.In(needles))
                {
                    found = c;
                    break;
                }

                buffer.Append(c);
            }

            return found;
        }

        /// <summary>
        /// Advances the stream until one of the strings passed in is encountered (using a case insensitive compare).
        /// 
        /// Returns which of strings was encountered.
        /// </summary>
        public string WhichNextInsensitive(StringBuilder rejected, params string[] options)
        {
            string found = null;

            int max = options.Select(s => s.Length).Max();
            var count = 0;

            var read = new StringBuilder();

            while (HasMore() && count <= max)
            {
                read.Append(Read());
                count++;

                if (options.Any(a => a.Equals(read.ToString(), StringComparison.InvariantCultureIgnoreCase)))
                {
                    found = options.Single(a => a.Equals(read.ToString(), StringComparison.InvariantCultureIgnoreCase));
                    break;
                }
            }

            if (found == null) rejected.Append(read);

            return found;
        }

        /// <summary>
        /// Advance the stream until needle is encountered, then advance past it.
        /// 
        /// If requireFind = true (which is the default) this method throws an error
        /// if needle can not be found.
        /// </summary>
        public void AdvancePast(string needle, bool requireFind = true)
        {
            var start = Position;

            int needlePos = 0;
            char lookingFor = needle[needlePos];

            bool found = false;
            while (HasMore())
            {
                var current = Read();
                if (current == lookingFor)
                {
                    needlePos++;
                    if (needlePos >= needle.Length)
                    {
                        found = true;
                        break;
                    }
                }
                else
                {
                    needlePos = 0;
                }

                lookingFor = needle[needlePos];
            }

            if (!found && requireFind)
            {
                Current.RecordError(Model.ErrorType.Parser, Model.Position.Create(start, Position, Current.CurrentFilePath), "Expected '" + needle + "'");
                throw new StoppedParsingException();
            }
        }


        /// <summary>
        /// Like AdvancePast, but for all whitespace characters.
        /// 
        /// Does not error if not white space is found.
        /// </summary>
        public void AdvancePastWhiteSpace()
        {
            int i;
            while (HasMore() && (i = Peek()) != -1)
            {
                if (char.IsWhiteSpace((char)i))
                {
                    Advance();
                }
                else
                {
                    return;
                }
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            _wrapped.Dispose();
        }

        #endregion
    }
}
