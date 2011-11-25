using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace More.Helpers
{
    class FastString
    {
        private Dictionary<char, int[]> CharToIndexes;
        private char[] Characters;

        public string Raw { get; private set; }

        public FastString(string str)
        {
            Raw = str;
            Characters = str.ToCharArray();

            var temp = new Dictionary<char, List<int>>();

            for(int i = 0; i < Characters.Length; i++)
            {
                var c = Characters[i];

                List<int> indexes;
                if (!temp.TryGetValue(c, out indexes))
                {
                    indexes = new List<int>();
                    temp[c] = indexes;
                }

                indexes.Add(i);
            }

            CharToIndexes = temp.ToDictionary(d => d.Key, d => d.Value.ToArray());
        }

        /// <summary>
        /// Returns the strings that occur in common between these strings.
        /// 
        /// Returned tuples are of the form [substring, index in this, index in other]
        /// </summary>
        public List<Tuple<string, int, int>> SubStrings(string other, int minLength = 5)
        {
            var otherChars = other.ToCharArray();

            var ret = new List<Tuple<string, int, int>>();

            for (int i = 0; i < otherChars.Length; i++)
            {
                var c = otherChars[i];

                if (!CharToIndexes.ContainsKey(c)) continue;

                var occurrances = CharToIndexes[c];

                int skip = 0;

                for (int j = 0; j < occurrances.Length; j++)
                {
                    var lead = occurrances[j];

                    int length = 1;

                    while (i + length < otherChars.Length &&
                          lead + length < Characters.Length &&
                          Characters[lead + length] == otherChars[i + length])
                    {
                        length++;
                    }

                    if (length >= minLength)
                    {
                        var substr = new string(Characters, lead, length);

                        var tuple = Tuple.Create(substr, lead, i);

                        ret.Add(tuple);
                        skip = Math.Max(skip, length - 1);
                    }
                }

                i += skip;
            }

#if DEBUG
            foreach (var r in ret)
            {
                var substr = r.Item1;
                var thisSubstr = this.Raw.Substring(r.Item2, substr.Length);
                var otherSubstr = other.Substring(r.Item3, substr.Length);

                if (!(substr == thisSubstr && substr == otherSubstr))
                    throw new InvalidOperationException();
            }
#endif

            return ret;
        }
    }
}
