﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace More
{
    public static class ExtensionsMethods
    {
        public static bool In<T>(this T t, params T[] items)
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].Equals(t)) return true;
            }

            return false;
        }

        public static string RebaseFile(this string inputFile, string relativeToFile = null)
        {
            if (inputFile.StartsWith("~"))
            {
                var fragment = inputFile.Substring(1).TrimStart(Path.DirectorySeparatorChar);
                var lead = Current.WorkingDirectory;

                if (!lead.EndsWith("" + Path.DirectorySeparatorChar)) lead += Path.DirectorySeparatorChar;

                return lead + fragment;
            }

            if (relativeToFile.HasValue() && !Path.IsPathRooted(inputFile))
            {
                inputFile = Path.GetDirectoryName(relativeToFile) + Path.DirectorySeparatorChar + inputFile.TrimStart(Path.DirectorySeparatorChar);
            }

            if (inputFile.Contains(Path.DirectorySeparatorChar + ".."))
            {
                var parts = inputFile.Split(Path.DirectorySeparatorChar);
                var ret = new LinkedList<string>();
                foreach (var p in parts)
                {
                    if (p == "..")
                    {
                        ret.RemoveLast();
                    }
                    else
                    {
                        ret.AddLast(p);
                    }
                }

                return string.Join("" + Path.DirectorySeparatorChar, ret);
            }

            return inputFile;
        }

        public static void Each<T>(this IEnumerable<T> that, Action<T> a)
        {
            foreach (var t in that) a(t);
        }

        public static bool IsNullOrEmpty(this string str)
        {
            return string.IsNullOrEmpty(str);
        }

        public static bool HasValue(this string str)
        {
            return !IsNullOrEmpty(str);
        }
    }
}
