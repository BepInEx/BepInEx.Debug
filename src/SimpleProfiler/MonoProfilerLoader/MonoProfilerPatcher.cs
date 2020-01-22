using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Logging;
using Mono.Cecil;

namespace MonoProfiler
{
    public static class MonoProfilerPatcher
    {
        private const string ProfilerOutputFilename = "MonoProfilerOutput.csv";
        private static Dump _dumpFunction;
        private static ManualLogSource _logger;

        public static IEnumerable<string> TargetDLLs { get; } = new string[0];

        public static bool IsInitialized => _dumpFunction != null;

        public static FileInfo RunProfilerDump()
        {
            if (_dumpFunction == null) throw new InvalidOperationException("Tried to trigger a profiler info dump before profiler was initialized");

            _dumpFunction();

            var dump = new FileInfo(Path.Combine(Paths.GameRootPath, ProfilerOutputFilename));
            if (!dump.Exists) throw new FileNotFoundException("Could not find the profiler dump file in " + dump.FullName);
            return dump;
        }

        public static void Initialize()
        {
            _logger = new ManualLogSource("MonoProfiler");
            Logger.Sources.Add(_logger);

            try
            {
                // Find address of the mono module
                var monoModule = Process.GetCurrentProcess().Modules.Cast<ProcessModule>().FirstOrDefault(module => module.ModuleName.Contains("mono"));
                if (monoModule == null)
                {
                    _logger.LogError("Failed to find the Mono module in current process");
                    return;
                }

                // Load profiler lib, it checks for the dll in the game root next to the .exe first
                var profilerPtr = LoadLibrary("MonoProfiler.dll");
                if (profilerPtr == IntPtr.Zero)
                {
                    _logger.LogWarning("Failed to load MonoProfiler.dll, make sure that it exists in game root and try again");
                    return;
                }

                // Subscribe the profiler in mono
                var addProfilerPtr = GetProcAddress(profilerPtr, "AddProfiler");
                if (addProfilerPtr == IntPtr.Zero)
                {
                    _logger.LogError("Failed to find function AddProfiler in MonoProfiler.dll");
                    return;
                }
                var addProfiler = (AddProfilerDelegate)Marshal.GetDelegateForFunctionPointer(addProfilerPtr, typeof(AddProfilerDelegate));
                addProfiler(monoModule.BaseAddress);

                // Prepare callback used to trigger a dump of collected profiler info
                var dumpPtr = GetProcAddress(profilerPtr, "Dump");
                if (dumpPtr == IntPtr.Zero)
                {
                    _logger.LogError("Failed to find function Dump in MonoProfiler.dll");
                    return;
                }
                _dumpFunction = (Dump)Marshal.GetDelegateForFunctionPointer(dumpPtr, typeof(Dump));

                _logger.LogDebug($"Load success! monoModule:{monoModule} profilerPtr:{profilerPtr} AddProfilerPtr:{addProfilerPtr} DumpPtr:{dumpPtr}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Encountered an unexpected exception: " + ex);
            }
            finally
            {
                Logger.Sources.Remove(_logger);
            }
        }

        public static void Patch(AssemblyDefinition ass) { }

        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string fileName);

        private delegate void AddProfilerDelegate(IntPtr mono);
        private delegate void Dump();
    }
}
