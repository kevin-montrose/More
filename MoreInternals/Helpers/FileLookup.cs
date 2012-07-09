using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MoreInternals.Helpers
{
    public interface IFileLookup
    {
        bool Exists(string path);
        Stream ReadRaw(string path);
        TextReader Find(string path);
        TextWriter OpenWrite(string path);
    }

    public class FileLookup : IFileLookup
    {
        public static readonly FileLookup Singleton = new FileLookup();

        private FileLookup() { }

        public bool Exists(string file)
        {
            return File.Exists(file);
        }

        public Stream ReadRaw(string file)
        {
            return File.Open(file, FileMode.Open);
        }

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
            return new StreamWriter(File.Create(file));
        }
    }

    class TestAllExistLookup : TestLookup
    {
        internal TestAllExistLookup() : base(new Dictionary<string,string>(), null) { }

        public override bool Exists(string path)
        {
            return true;
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

        public virtual bool Exists(string path)
        {
            return ReadMap.ContainsKey(path) || (InnerLookup != null && InnerLookup.Exists(path));
        }

        public Stream ReadRaw(string path)
        {
            if (InnerLookup != null && InnerLookup.Exists(path)) return InnerLookup.ReadRaw(path);

            var text = Find(path);

            return new MemoryStream(Encoding.UTF8.GetBytes(text.ReadToEnd()));
        }

        public TextReader Find(string name)
        {
            if (InnerLookup != null && !ReadMap.Any(x => name.EndsWith(x.Key))) return InnerLookup.Find(name);

            return new StringReader(ReadMap.Single(x => name.EndsWith(x.Key)).Value);
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
