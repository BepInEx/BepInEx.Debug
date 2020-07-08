using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using MonoMod.Utils;

namespace RedirectInternalLogs.Util
{
    internal static class LibcHelper
    {
        [DynDllImport("libc", "vsprintf_s")] private static readonly VsPrintFsDelegate VsPrintF = null;

        public static string Format(IntPtr format, IntPtr args, int buffer = 8192)
        {
            var sb = new StringBuilder(buffer);
            VsPrintF(sb, sb.Capacity, format, args);
            return sb.ToString();
        }

        public static void Init()
        {
            typeof(LibcHelper).ResolveDynDllImports(new Dictionary<string, List<DynDllMapping>>
            {
                ["libc"] = new List<DynDllMapping>
                {
                    new DynDllMapping("msvcrt.dll"),
                    new DynDllMapping("libc.so.6"),
                    new DynDllMapping("libSystem.dylib")
                }
            });
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int VsPrintFsDelegate([MarshalAs(UnmanagedType.LPStr)] StringBuilder dest, int size, IntPtr fmt, IntPtr vaList);
    }
}