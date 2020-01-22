using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Mono.Cecil;

namespace MonoProfilerLoader
{
    public static class BepinProfilerPatcher
    {
        public static IEnumerable<string> TargetDLLs { get; } = new string[0];

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string fileName);

        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        public static void Initialize()
        {
            var profiler = LoadLibrary("MonoProfiler.dll");
            Console.WriteLine($"Profiler: {profiler}");

            if (profiler == IntPtr.Zero)
                return;

            var addProfilerFun = GetProcAddress(profiler, "AddProfiler");
            Console.WriteLine($"AddProfiler: {addProfilerFun}");

            if (addProfilerFun == IntPtr.Zero)
                return;

            var addProfiler =
                Marshal.GetDelegateForFunctionPointer(addProfilerFun, typeof(AddProfilerDelegate)) as
                    AddProfilerDelegate;

            ProcessModule monoModule = null;
            foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
                if (module.ModuleName.Contains("mono"))
                {
                    monoModule = module;
                    break;
                }

            Console.WriteLine($"Got mono: {monoModule}");
            if (monoModule == null)
                return;

            addProfiler(monoModule.BaseAddress);
        }

        public static void Patch(AssemblyDefinition ass)
        {
        }

        private delegate void AddProfilerDelegate(IntPtr mono);
    }
}