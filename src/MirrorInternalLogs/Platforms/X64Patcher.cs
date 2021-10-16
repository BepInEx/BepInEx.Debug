using System;
using System.Runtime.InteropServices;
using MirrorInternalLogs.Util;
using MonoMod.RuntimeDetour;

namespace MirrorInternalLogs.Platforms
{
    internal class X64Patcher : PatcherBase
    {

        protected override IPatch[] Patterns { get; } =
        {
            new InternalLogPatch<PrintFDelegate>(
                @"  # OldUnity
                48 89 4C 24 08        ; mov    QWORD PTR [rsp+0x8],rcx
                48 89 54 24 10        ; mov    QWORD PTR [rsp+0x10],rdx
                4C 89 44 24 18        ; mov    QWORD PTR [rsp+0x18],r8
                4C 89 4C 24 20        ; mov    QWORD PTR [rsp+0x20],r9
                48 83 EC 28           ; sub    rsp,0x28
                48 8B D1              ; mov    rdx,rcx
                4C 8D 44 24 38        ; lea    r8,[rsp+0x38]
                B9 ?  00 00 00        ; mov    ecx, ?
                E8                    ; call
            ", InternalUnityLogger.OnLogHook),
            new InternalLogPatch<PrintFDelegateNoType>(
                @"  # Unity2019
                48 89 4C 24 08                          ; mov     [rsp+8], rcx
                48 89 54 24 10                          ; mov     [rsp+10h], rdx
                4C 89 44 24 18                          ; mov     [rsp+18h], r8
                4C 89 4C 24 20                          ; mov     [rsp+20h], r9
                53                                      ; push    rbx
                57                                      ; push    rdi
                48 83 EC 28                             ; sub     rsp, 28h
                48 8B ?  ?  ?  ?  ?                     ; mov     rax, ?
                48 8D 7C 24 48                          ; lea     rdi, [rsp+48h]
                48 8B D9                                ; mov     rbx, rcx
                48 85 C0                                ; test    rax, rax
                74 11                                   ; jz      
                48 8B D1                                ; mov     rdx, rcx
                4C 8B C7                                ; mov     r8, rdi
                B9 ?  00 00 00                          ; mov     ecx, ?
                FF D0                                   ; call    rax
                84 C0                                   ; test    al, al
                74 0B                                   ; jz
                48 8B D7                                ; mov     rdx, rdi
                48 8B CB                                ; mov     rcx, rbx
                E8                                      ; call
            ", InternalUnityLogger.OnLogHook),
        };

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void PrintFDelegate(ulong type, IntPtr pattern, IntPtr parts);
        
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void PrintFDelegateNoType(IntPtr pattern, IntPtr parts);
    }
}