using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace More.Model
{
    interface IPosition
    {
        int Start { get; }
        int Stop { get; }
        string FilePath { get; }
    }

    class Position : IPosition
    {
        public int Start { get; private set; }
        public int Stop { get; private set; }
        public string FilePath { get; private set; }

        public static readonly IPosition NoSite = new Position(-1, -1, null);

        private Position(int start, int stop, string file)
        {
            Start = start;
            Stop = stop;
            FilePath = file;
        }

        public static IPosition Create(int start, int stop, string file)
        {
            return new Position(start, stop, file);
        }
    }
}
