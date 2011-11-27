using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MoreInternals.Helpers
{
    interface IFileLookup
    {
        TextReader Find(string path);
    }

    class NullFileLookup : IFileLookup
    {
        public TextReader Find(string path) { throw new NotImplementedException(); }
    }

    class FileLookup : IFileLookup
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
    }

    class TestLookup : IFileLookup
    {
        private Dictionary<string, string> Map;

        public TestLookup(Dictionary<string, string> map)
        {
            Map = map;
        }

        public TextReader Find(string name)
        {
            return new StringReader(Map[name]);
        }
    }
}
