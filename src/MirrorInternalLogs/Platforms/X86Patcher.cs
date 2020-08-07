using System;
using System.Linq;
using System.Runtime.InteropServices;
using MirrorInternalLogs.Util;
using MonoMod.RuntimeDetour;

namespace MirrorInternalLogs.Platforms
{
    internal class X86Patcher : IPlatformPatcher
    {
        private static PrintFDelegate original;

        protected virtual BytePattern[] Patterns { get; } =
        {
            // New Unity
            @"
                55            ; push   ebp
                8B EC         ; mov    ebp,esp
                8D 45 0C      ; lea    eax,[ebp+0xc]
                50            ; push   eax
                FF 75 08      ; push   DWORD PTR [ebp+0x8]
                6A 05         ; push   0x5
                E8            ; call
            ",
            // Older Unity
            @"
                55            ; push   ebp
                8B EC         ; mov    ebp,esp
                8B 4D 08      ; mov    ecx,DWORD PTR [ebp+0x8]
                8D 45 0C      ; lea    eax,[ebp+0xc]
                50            ; push   eax
                51            ; push   ecx
                6A 05         ; push   0x5
                E8            ; call
            "
        };

        public void Patch(IntPtr unityModule, int moduleSize)
        {
            var match = FindMatch(unityModule, moduleSize);
            if (match == IntPtr.Zero)
                return;

            Apply(match);
        }

        protected virtual void Apply(IntPtr from)
        {
            var hookPtr =
                Marshal.GetFunctionPointerForDelegate(new PrintFDelegate(OnLogHook));
            var det = new NativeDetour(from, hookPtr, new NativeDetourConfig {ManualApply = true});
            original = det.GenerateTrampoline<PrintFDelegate>();
            det.Apply();
        }

        private static void OnLogHook(uint type, IntPtr pattern, IntPtr args)
        {
            InternalUnityLogger.OnUnityLog((InternalLogLevel) type, pattern, args);
            original(type, pattern, args);
        }

        private unsafe IntPtr FindMatch(IntPtr start, int maxSize)
        {
            var match = Patterns.Select(p => new {p, res = p.Match(start, maxSize)})
                .FirstOrDefault(m => m.res >= 0);
            if (match == null)
            {
                MirrorInternalLogsPatcher.Logger.LogWarning(
                    "No match found, cannot hook logging! Please report Unity version or game name to the developer!");
                return IntPtr.Zero;
            }

            var ptr = (byte*) start.ToPointer();
            MirrorInternalLogsPatcher.Logger.LogDebug($"Found at {match.res:X} ({start.ToInt64() + match.res:X})");
            var offset = *(int*) (ptr + match.res + match.p.Length);
            var jmpRva = unchecked((uint) (match.res + match.p.Length + sizeof(int)) + offset);
            var addr = start.ToInt64() + jmpRva;
            MirrorInternalLogsPatcher.Logger.LogDebug($"Parsed offset: {offset:X}");
            MirrorInternalLogsPatcher.Logger.LogDebug(
                $"Jump RVA: {jmpRva:X}, memory address: {addr:X} (image base: {start.ToInt64():X})");
            return new IntPtr(addr);
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void PrintFDelegate(uint type, IntPtr pattern, IntPtr parts);
    }
}