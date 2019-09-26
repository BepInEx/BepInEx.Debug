using System;
using System.Linq;

namespace DemystifyExceptions.Demystify
{
    public static class Extensions
    {
        public static T Lazy<T>(ref T val, Func<T> initializer) where T : class
        {
            return val ?? (val = initializer());
        }

        public static bool IsNullOrWhitespace(this string str)
        {
            return str == null || str.All(char.IsWhiteSpace);
        }
    }
}