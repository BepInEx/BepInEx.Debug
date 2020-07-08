using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using Mono.Cecil;

namespace RedirectInternalLogs
{
    internal static class RedirectInternalLogsPatcher
    {
        internal static ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("RedirectInternalLogs");

        private static IPlatformPatcher patcher;
        public static IEnumerable<string> TargetDLLs { get; } = new string[0];

        public static void Patch(AssemblyDefinition ass)
        {
        }

        public static void Initialize()
        {
            InternalUnityLogger.OnUnityInternalLog += InternalUnityLoggerOnOnUnityInternalLog;
            LibcHelper.Init();

            bool IsUnityPlayer(ProcessModule p)
            {
                return p.ModuleName.ToLowerInvariant().Contains("unityplayer");
            }

            var proc = Process.GetCurrentProcess().Modules
                .Cast<ProcessModule>()
                .FirstOrDefault(IsUnityPlayer) ?? Process.GetCurrentProcess().MainModule;

            if (IntPtr.Size == 8)
                patcher = new X64Patcher();
            else
                patcher = new X86Patcher();

            patcher.Patch(proc.BaseAddress, proc.ModuleMemorySize);
        }

        private static void InternalUnityLoggerOnOnUnityInternalLog(object sender, UnityLogEventArgs e)
        {
            // TODO: Make better, right now can fail because of access violation
            File.AppendAllText("myLog.log", $"[{e.LogLevel}] {e.Message}");
        }
    }
}