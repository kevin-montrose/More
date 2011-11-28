using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MoreInternals.Helpers
{
    public interface IFileLookup
    {
        TextReader Find(string path);
        TextWriter OpenWrite(string path);
    }

    public class FileLookup : IFileLookup
    {
        public static readonly FileLookup Singleton = new FileLookup();

        private FileLookup() { }

        public TextReader Find(string file)
        {
            if (Path.GetExtension(file).IsNullOrEmpty() && !File.Exists(file))
            {
                var more = file + ".more";
                var css = file + ".css";
                if (File.Exists(more))
                {
                    file = more;
                }
                else
                {
                    if (File.Exists(css))
                    {
                        file = css;
                    }
                }
            }

            return new StreamReader(File.OpenRead(file));
        }

        public TextWriter OpenWrite(string file)
        {
            return new StreamWriter(File.OpenWrite(file));
        }
    }

    class TestLookup : IFileLookup
    {
        class DummyWriter : TextWriter
        {
            public override Encoding Encoding
            {
                get { return Wrapped.Encoding; }
            }

            private TextWriter Wrapped;
            public event Action<DummyWriter> Disposed = delegate { };

            public DummyWriter(TextWriter wrapped)
            {
                Wrapped = wrapped;
            }

            public override void Write(char value)
            {
                Wrapped.Write(value);
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                Disposed(this);
            }
        }

        private Dictionary<string, string> ReadMap;
        internal Dictionary<string, string> WriteMap = new Dictionary<string, string>();

        private IFileLookup InnerLookup;

        public TestLookup(Dictionary<string, string> map, IFileLookup inner)
        {
            ReadMap = map;
            InnerLookup = inner;
        }

        public TextReader Find(string name)
        {
            if (InnerLookup != null && !ReadMap.ContainsKey(name)) return InnerLookup.Find(name);

            return new StringReader(ReadMap[name]);
        }

        public TextWriter OpenWrite(string name)
        {
            var stringWriter = new StringWriter();

            var ret = new DummyWriter(stringWriter);
            ret.Disposed += (DummyWriter d) => { WriteMap[name] = stringWriter.ToString(); };

            return ret;
        }
    }
}
