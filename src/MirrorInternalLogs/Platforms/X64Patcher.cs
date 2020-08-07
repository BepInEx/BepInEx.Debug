using System;
using System.Runtime.InteropServices;
using MirrorInternalLogs.Util;
using MonoMod.RuntimeDetour;

namespace MirrorInternalLogs.Platforms
{
    internal class X64Patcher : X86Patcher
    {
        private static PrintFDelegate original;

        protected override BytePattern[] Patterns { get; } =
        {
            @"
                48 89 4C 24 08        ; mov    QWORD PTR [rsp+0x8],rcx
                48 89 54 24 10        ; mov    QWORD PTR [rsp+0x10],rdx
                4C 89 44 24 18        ; mov    QWORD PTR [rsp+0x18],r8
                4C 89 4C 24 20        ; mov    QWORD PTR [rsp+0x20],r9
                48 83 EC 28           ; sub    rsp,0x28
                48 8B D1              ; mov    rdx,rcx
                4C 8D 44 24 38        ; lea    r8,[rsp+0x38]
                B9 05 00 00 00        ; mov    ecx,0x5
                E8                    ; call
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

        private static void OnUnityLog(ulong type, IntPtr message, IntPtr args)
        {
            InternalUnityLogger.OnUnityLog((InternalLogLevel) type, message, args);
            original(type, message, args);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void PrintFDelegate(ulong type, IntPtr pattern, IntPtr parts);
    }
}