using System;
using System.Globalization;
using System.Linq;

namespace MirrorInternalLogs.Util
{
    internal class BytePattern
    {
        private readonly byte[] pattern;
        private int[] jumpTable;

        public BytePattern(string bytes)
        {
            pattern = bytes.ParseHexBytes();
            CreateJumpTable();
        }

        public int Length => pattern.Length;

        public static implicit operator BytePattern(string pattern)
        {
            return new BytePattern(pattern);
        }

        private void CreateJumpTable()
        {
            jumpTable = new int[pattern.Length];

            var substrCandidate = 0;
            jumpTable[0] = -1;
            for (var i = 1; i < pattern.Length; i++, substrCandidate++)
                if (pattern[i] == pattern[substrCandidate])
                {
                    jumpTable[i] = jumpTable[substrCandidate];
                }
                else
                {
                    jumpTable[i] = substrCandidate;
                    while (substrCandidate >= 0 && pattern[i] != pattern[substrCandidate])
                        substrCandidate = jumpTable[substrCandidate];
                }
        }

        public unsafe int Match(IntPtr start, int maxSize)
        {
            var ptr = (byte*) start.ToPointer();
            for (int j = 0, k = 0; j < maxSize;)
                if (ptr[j] == pattern[k])
                {
                    j++;
                    k++;
                    if (k == pattern.Length)
                        return j - k;
                }
                else
                {
                    k = jumpTable[k];
                    if (k >= 0) continue;
                    j++;
                    k++;
                }

            return -1;
        }
    }
}