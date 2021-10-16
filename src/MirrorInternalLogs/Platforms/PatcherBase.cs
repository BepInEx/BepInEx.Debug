using System;
using System.Linq;
using MirrorInternalLogs.Util;

namespace MirrorInternalLogs.Platforms
{
    internal abstract class PatcherBase
    {
        protected abstract IPatch[] Patterns { get; }

        public void Patch(IntPtr unityModule, int moduleSize)
        {
            var match = FindMatch(unityModule, moduleSize, out var matchingPatcher);
            if (match == IntPtr.Zero)
                return;
            matchingPatcher.Apply(match);
        }

        private unsafe IntPtr FindMatch(IntPtr start, int maxSize, out IPatch matchingPatcher)
        {
            matchingPatcher = null;
            var match = Patterns.Select(p => new { p, res = p.Pattern.Match(start, maxSize) })
                .FirstOrDefault(m => m.res >= 0);
            if (match == null)
            {
                MirrorInternalLogsPatcher.Logger.LogWarning(
                    "No match found, cannot hook logging! Please report Unity version or game name to the developer!");
                return IntPtr.Zero;
            }

            matchingPatcher = match.p;
            MirrorInternalLogsPatcher.Logger.LogInfo($"Found match using pattern \"{match.p.Pattern.Name}\"");

            var ptr = (byte*)start.ToPointer();
            MirrorInternalLogsPatcher.Logger.LogDebug($"Found at {match.res:X} ({start.ToInt64() + match.res:X})");
            var offset = *(int*)(ptr + match.res + match.p.Pattern.Length);
            var jmpRva = unchecked((uint)(match.res + match.p.Pattern.Length + sizeof(int)) + offset);
            var addr = start.ToInt64() + jmpRva;
            MirrorInternalLogsPatcher.Logger.LogDebug($"Parsed offset: {offset:X}");
            MirrorInternalLogsPatcher.Logger.LogDebug(
                $"Jump RVA: {jmpRva:X}, memory address: {addr:X} (image base: {start.ToInt64():X})");
            return new IntPtr(addr);
        }
    }
}
