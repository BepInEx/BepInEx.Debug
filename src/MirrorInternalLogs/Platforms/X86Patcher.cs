using System;
using System.Runtime.InteropServices;
using MirrorInternalLogs.Util;

namespace MirrorInternalLogs.Platforms
{
    internal class X86Patcher : PatcherBase
    {
        protected override IPatch[] Patterns { get; } =
        {
            new InternalLogPatch<PrintFDelegate>(
                @"  # New Unity
                55            ; push   ebp
                8B EC         ; mov    ebp,esp
                8D 45 0C      ; lea    eax,[ebp+0xc]
                50            ; push   eax
                FF 75 08      ; push   DWORD PTR [ebp+0x8]
                6A ?          ; push   
                E8            ; call
                ", InternalUnityLogger.OnLogHook),
            new InternalLogPatch<PrintFDelegate>(
                @"  # Old Unity
                55            ; push   ebp
                8B EC         ; mov    ebp,esp
                8B 4D 08      ; mov    ecx,DWORD PTR [ebp+0x8]
                8D 45 0C      ; lea    eax,[ebp+0xc]
                50            ; push   eax
                51            ; push   ecx
                6A ?          ; push   ?
                E8            ; call
                ", InternalUnityLogger.OnLogHook),
            new InternalLogPatch<PrintFDelegateNoType>(
                @"  # Unity2019
                55                                      ; push    ebp
                8B EC                                   ; mov     ebp, esp
                A1 ?  ?  ?  ?                           ; mov     eax, dword_112E6FE8
                56                                      ; push    esi
                8B 75 08                                ; mov     esi, [ebp+8]
                85 C0                                   ; test    eax, eax
                74 10                                   ; jz      short loc_10B06D10
                8D 4D 0C                                ; lea     ecx, [ebp+0Ch]
                51                                      ; push    ecx
                56                                      ; push    esi
                6A ?                                    ; push    ?
                FF D0                                   ; call    eax
                83 C4 0C                                ; add     esp, 0Ch
                84 C0                                   ; test    al, al
                74 0D                                   ; jz
                8D 45 0C                                ; lea     eax, [ebp+0Ch]
                50                                      ; push    eax
                56                                      ; push    esi
                E8                                      ; call
                ", InternalUnityLogger.OnLogHook)
        };

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void PrintFDelegate(uint type, IntPtr pattern, IntPtr parts);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void PrintFDelegateNoType(IntPtr pattern, IntPtr parts);
    }
}