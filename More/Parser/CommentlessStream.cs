using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace More.Parser
{
    class CommentlessStream : IDisposable
    {
        public int Position { get; private set; }

        private LinkedList<int> _buffered = new LinkedList<int>();

        private Stack<char> quotes = new Stack<char>();

        private TextReader _wrapped;
        public CommentlessStream(TextReader wrapped)
        {
            _wrapped = wrapped;
        }

        public string ReadToEnd()
        {
            var ret = new StringBuilder();

            int i;
            while ((i = Read()) != -1)
            {
                ret.Append((char)i);
            }

            return ret.ToString();
        }

        public int Peek()
        {
            int i = -1;
            if (_buffered.Count > 0)
            {
                i = _buffered.First();
            }
            else
            {
                i = _wrapped.Peek();
            }

            if (i == -1) return -1;
            if ((char)i != '/' || quotes.Count > 0) // If we're in quotes, Peek() always returns raw streams
            {
                return i;
            }

            // May be a comment
            int pending = DirectRead();

            int j = -1;
            if (_buffered.Count > 1)
            {
                j = _buffered.First();
            }
            else
            {
                j = _wrapped.Peek();
            }

            // Single line comment
            if (j == '/')
            {
                AdvancePast("\n");

                return Peek();
            }

            if (j == '*')
            {
                AdvancePast("*/", required: true);

                return Peek();
            }

            // Wasn't a comment, put it back in the stream
            PushBack(pending);

            return i;
        }

        private void AdvancePast(string needle, bool required = false)
        {
            int needlePos = 0;
            char lookingFor = needle[needlePos];

            bool found = false;
            int current;
            while ((current = DirectRead()) != -1)
            {
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

            if (!found && required)
            {
                throw new InvalidOperationException("Expected [" + needle + "] at " + Position);
            }
        }

        public void PushBack(int back)
        {
            _buffered.AddFirst(back);
            Position--;
        }

        public int DirectRead()
        {
            Position++;

            if (_buffered.Count > 0)
            {
                var value = _buffered.First();
                _buffered.RemoveFirst();
                return value;
            }
            else
            {
                return _wrapped.Read();
            }
        }

        public int Read()
        {
            int i = DirectRead();

            if (i == -1) return -1;
            if ((char)i != '/' || quotes.Count > 0) // No comments in quotes
            {
                if ((char)i == '"')
                {
                    if (quotes.Count > 0 && quotes.Peek() == '"')
                    {
                        quotes.Pop();
                    }
                    else
                    {
                        quotes.Push('"');
                    }
                }

                if ((char)i == '\'')
                {
                    if (quotes.Count > 0 && quotes.Peek() == '\'')
                    {
                        quotes.Pop();
                    }
                    else
                    {
                        quotes.Push('\'');
                    }
                }

                return i;
            }

            // May be a comment
            int j = DirectRead();

            // Single line comment
            if (j == '/')
            {
                AdvancePast("\n");

                return Read();
            }

            if (j == '*')
            {
                AdvancePast("*/", required: true);

                return Read();
            }

            // Wasn't a comment, put it back in the stream
            PushBack(j);

            return i;
        }

        #region IDisposable Members
        public void Dispose()
        {
            _wrapped.Dispose();
        }
        #endregion
    }
}
