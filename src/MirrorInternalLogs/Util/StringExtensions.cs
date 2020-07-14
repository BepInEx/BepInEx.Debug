using System;
using System.Collections.Generic;
using System.Linq;

namespace MirrorInternalLogs.Util
{
    internal static class StringExtensions
    {
        public static string Format(this string fmt, Dictionary<string, Func<string>> vars)
        {
            return vars.Aggregate(fmt, (str, kv) => str.Replace($"{{{kv.Key}}}", kv.Value()));
        }
    }
}