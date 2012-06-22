using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Model;
using System.Collections;
using System.Security.Cryptography;
using System.IO;

namespace MoreInternals.Compiler.Tasks
{
    /// <summary>
    /// Finds all references to external resources and adds automagic cache breakers to them.
    /// 
    /// The rules are essentially:
    ///   - if we can *find* the resource, hash it and use that
    ///   - if we can't find it, generate a reference that won't collide
    /// </summary>
    public class CacheBreak
    {
        private static object RandomLock = new object();
        private static RandomNumberGenerator SharedRandom = RandomNumberGenerator.Create();
        private static byte[] GetRandom(int n)
        {
            var ret = new byte[n];
            lock (RandomLock)
            {
                SharedRandom.GetBytes(ret);
            }
            return ret;
        }

        private static readonly IOrderedEnumerable<char> EncodingCharacters = "0123456789ABCDEFGHIJKLMNOPQRSTUV".OrderBy(c => c);
        // base 32 encoding this byte array, using some seriously conservative characters
        private static string Encode(byte[] arr)
        {
            var ret = new List<char>();

            var bits = new BitArray(arr);

            var chunks = bits.Count / 5;

            for (var i = 0; i < chunks; i++)
            {
                var start = i * 5;
                var index = bits[start] ? 16 : 0;
                index += bits[start + 1] ? 8 : 0;
                index += bits[start + 2] ? 4 : 0;
                index += bits[start + 3] ? 2 : 0;
                index += bits[start + 4] ? 1 : 0;

                ret.Add(EncodingCharacters.ElementAt(index));
            }

            // handle the last chunk
            if (bits.Count % 5 != 0)
            {
                var start = chunks * 5;
                var index = 0;

                index += start < bits.Count && bits[start] ? 16 : 0;
                start++;

                index += start < bits.Count && bits[start] ? 8 : 0;
                start++;

                index += start < bits.Count && bits[start] ? 4 : 0;
                start++;

                index += start < bits.Count && bits[start] ? 2 : 0;
                start++;

                index += start < bits.Count && bits[start] ? 1 : 0;
                start++;

                ret.Add(EncodingCharacters.ElementAt(index));
            }

            return new string(ret.ToArray());
        }

        /// <summary>
        /// OK, this is probably going overboard but you got to have some fun sometime.
        /// 
        /// Truncating a hash isn't a safe operation, while an ideal hash would have equal entropy
        /// in each "half" there's no proof that any extant hash algorithm behaves that way.
        /// 
        /// A construction proposed by Kelsey in 2005 is:
        ///  - pick a fixed IV for length N
        ///    * every potentional length has a different IV
        ///    * we're going to truncate to 8 bytes always, so there's only one IV but in principle...
        ///  - let H(IV ^ 0xCCC...CCC, N) be IV_H
        ///  - let H(IV_H, message) be HASH
        ///  - truncate HASH to N bits
        ///  
        /// This is not (to my knowledge) a battle tested construction, 
        /// but since we're only using it for cache breaking there's not
        /// much attack surface.  It's also almost certainly better than anything
        /// I can cook up or naive truncation.
        /// </summary>
        private static byte[] HashAndTruncate(Stream input, HashAlgorithm hash)
        {
            const ulong length8IV = 0x16E0145A5825C1F7;

            var ivBytes = new List<byte>();
            ivBytes.AddRange(BitConverter.GetBytes(length8IV ^ 0xcccccccccccccccc)); // IV
            ivBytes.AddRange(BitConverter.GetBytes(8)); // N

            var ivh = hash.ComputeHash(ivBytes.ToArray());

            var message = new List<byte>();
            message.AddRange(ivh);

            int i;
            var buffer = new byte[4096];
            while ((i = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                message.AddRange(buffer.Take(i));
            }

            var untruncated = hash.ComputeHash(message.ToArray());

            return untruncated.Take(8).ToArray();
        }

        private static string GenerateCacheBreaker(string path)
        {
            if (!Current.FileLookup.Exists(path))
            {
                var breaker = new List<byte>(8);
                breaker.AddRange(GetRandom(4));

                var low = (uint)(DateTime.UtcNow.Ticks & 0xFFFFFFFF);

                breaker.AddRange(BitConverter.GetBytes(low));

                return Encode(breaker.ToArray());
            }

            using (var stream = Current.FileLookup.ReadRaw(path))
            using (var sha = SHA256.Create())
            {
                var hash = HashAndTruncate(stream, sha);

                return Encode(hash);
            }
        }

        private static Value AddCacheBreaker(Value val)
        {
            var asStr = val as StringValue;
            if (asStr != null)
            {
                var breaker = GenerateCacheBreaker(asStr.Value);
                if (asStr.Value.Contains('?'))
                {
                    breaker = "&_=" + breaker;
                }
                else
                {
                    breaker = "?_=" + breaker;
                }

                return new StringValue(asStr.Value + breaker);
            }

            var asQuoted = val as QuotedStringValue;
            if (asQuoted != null)
            {
                var breaker = GenerateCacheBreaker(asQuoted.Value);
                if (asQuoted.Value.Contains('?'))
                {
                    breaker = "&_=" + breaker;
                }
                else
                {
                    breaker = "?_=" + breaker;
                }

                return new QuotedStringValue(asQuoted.Value + breaker);
            }

            throw new InvalidOperationException("Expected string or quoted string value, found [" + val + "]");
        }

        private static Value CacheBreakValue(Value val)
        {
            var compound = val as CompoundValue;
            if (compound != null)
            {
                return new CompoundValue(compound.Values.Select(v => CacheBreakValue(v)).ToArray());
            }

            var comma = val as CommaDelimittedValue;
            if (comma != null)
            {
                return new CommaDelimittedValue(comma.Values.Select(v => CacheBreakValue(v)).ToList());
            }

            var url = val as UrlValue;
            if (url != null)
            {
                return new UrlValue(AddCacheBreaker(url.UrlPath));
            }

            return val;
        }

        private static NameValueProperty CacheBreakProperty(NameValueProperty prop)
        {
            return new NameValueProperty(prop.Name, CacheBreakValue(prop.Value), prop.Start, prop.Stop, prop.FilePath);
        }

        private static SelectorAndBlock CacheBreakBlock(SelectorAndBlock block)
        {
            var ret = new List<Property>();

            foreach (var prop in block.Properties.Cast<NameValueProperty>())
            {
                ret.Add(CacheBreakProperty(prop));
            }

            return new SelectorAndBlock(block.Selector, ret, block.ResetContext, block.Start, block.Stop, block.FilePath);
        }

        public static List<Block> Task(List<Block> blocks)
        {
            var ret = new List<Block>();

            // Gotta maintain order at this point
            foreach (var block in blocks)
            {
                var selBlock = block as SelectorAndBlock;
                if (selBlock != null)
                {
                    ret.Add(CacheBreakBlock(selBlock));
                    continue;
                }

                var mediaBlock = block as MediaBlock;
                if (mediaBlock != null)
                {
                    var mediaRet = new List<Block>();
                    foreach (var subBlock in mediaBlock.Blocks.Cast<SelectorAndBlock>())
                    {
                        mediaRet.Add(CacheBreakBlock(subBlock));
                    }

                    ret.Add(new MediaBlock(mediaBlock.MediaQuery, mediaRet, mediaBlock.Start, mediaBlock.Stop, mediaBlock.FilePath));
                    continue;
                }

                var keyframesBlock = block as KeyFramesBlock;
                if (keyframesBlock != null)
                {
                    var keyframeRet = new List<KeyFrame>();
                    foreach (var frame in keyframesBlock.Frames)
                    {
                        var frameProp = new List<Property>();
                        foreach (var prop in frame.Properties.Cast<NameValueProperty>())
                        {
                            frameProp.Add(CacheBreakProperty(prop));
                        }

                        keyframeRet.Add(new KeyFrame(frame.Percentages.ToList(), frameProp, frame.Start, frame.Stop, frame.FilePath));
                    }

                    ret.Add(new KeyFramesBlock(keyframesBlock.Prefix, keyframesBlock.Name, keyframeRet, keyframesBlock.Variables.ToList(), keyframesBlock.Start, keyframesBlock.Stop, keyframesBlock.FilePath));
                    continue;
                }

                var fontBlock = block as FontFaceBlock;
                if (fontBlock != null)
                {
                    var fontRet = new List<Property>();
                    foreach (var rule in fontBlock.Properties.Cast<NameValueProperty>())
                    {
                        fontRet.Add(CacheBreakProperty(rule));
                    }

                    ret.Add(new FontFaceBlock(fontRet, fontBlock.Start, fontBlock.Stop, fontBlock.FilePath));
                    continue;
                }

                var import = block as Model.Import;
                if (import != null)
                {
                    ret.Add(new Model.Import(CacheBreakValue(import.ToImport), import.MediaQuery, import.Start, import.Stop, import.FilePath));
                    continue;
                }

                ret.Add(block);
            }

            return ret;
        }
    }
}
