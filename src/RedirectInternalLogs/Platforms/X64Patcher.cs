using System;
using System.Runtime.InteropServices;
using MonoMod.RuntimeDetour;
using RedirectInternalLogs.Util;

namespace RedirectInternalLogs.Platforms
{
    internal class X64Patcher : X86Patcher
    {
        private static PrintFDelegate original;

        protected override BytePattern[] Patterns { get; } =
        {
            @"
                48 89 4C 24 08
                48 89 54 24 10
                4C 89 44 24 18
                4C 89 4C 24 20
                48 83 EC 28
                48 8B D1
                4C 8D 44 24 38
                B9 05 00 00 00
                E8
            "
        };

        protected override void Apply(IntPtr from)
        {
            var hookPtr =
                Marshal.GetFunctionPointerForDelegate(new PrintFDelegate(OnUnityLog));
            var det = new NativeDetour(from, hookPtr, new NativeDetourConfig {ManualApply = true});
            original = det.GenerateTrampoline<PrintFDelegate>();
            det.Apply();
        }

        private static void OnUnityLog(ulong type, string message, IntPtr args)
        {
            InternalUnityLogger.OnUnityLog((InternalLogLevel) type, message, args);
            original(type, message, args);
        }

        [UnmanagedFunctionPointer(CallingConvention.FastCall)]
        private delegate void PrintFDelegate(ulong type, [MarshalAs(UnmanagedType.LPStr)] string pattern, IntPtr parts);
    }
}