using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MirrorInternalLogs.Util
{
    internal static class StringExtensions
    {
        public static string Format(this string fmt, Dictionary<string, Func<string>> vars)
        {
            return vars.Aggregate(fmt, (str, kv) => str.Replace($"{{{kv.Key}}}", kv.Value()));
        }

        public static byte?[] ParseHexBytes(this string str, out string name)
        {
            name = string.Empty;
            static bool IsHexChar(char lowerC) => '0' <= lowerC && lowerC <= '9' || 'a' <= lowerC && lowerC <= 'f';
            var result = new List<byte?>();

            var sr = new StringReader(str);
            while (sr.Peek() > 0)
            {
                var c = char.ToLower((char) sr.Read());

                if (char.IsWhiteSpace(c))
                    continue;

                if (c == '#')
                {
                    name = sr.ReadLine()?.Trim() ?? name;
                }
                else if (c == ';')
                {
                    sr.ReadLine();
                }
                else if (c == '?')
                {
                    result.Add(null);
                }
                else if (IsHexChar(c) && sr.Peek() > 0)
                {
                    var other = char.ToLower((char) sr.Peek());
                    if (!IsHexChar(other)) continue;
                    sr.Read();
                    result.Add(byte.Parse($"{c}{other}", NumberStyles.HexNumber));
                }
            }

            return result.ToArray();
        }
    }
}