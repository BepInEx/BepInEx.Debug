using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using MirrorInternalLogs.Platforms;
using MirrorInternalLogs.Util;
using Mono.Cecil;

namespace MirrorInternalLogs
{
    internal static class MirrorInternalLogsPatcher
    {
        internal static ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("MirrorInternalLogs");

        private static readonly ConfigFile Config =
            new ConfigFile(Path.Combine(Paths.ConfigPath, "MirrorInternalLogs.cfg"), true);

        private static IPlatformPatcher patcher;
        private static StreamWriter writer;

        internal static ConfigEntry<bool> LogToFile =
            Config.Bind("Logging.File", "Enabled", true, "Enables logging to file");

        internal static ConfigEntry<string> LogPath = Config.Bind("Logging.File", "Path", "unity_log.txt",
            new StringBuilder()
                .AppendLine(
                    "Path to the generated log file. If path contains directories, the directories are created automatically.")
                .AppendLine("The string supports templated inside curly brackets like \"log_{timestamp}.log\"")
                .AppendLine()
                .AppendLine("Supported template variables:")
                .AppendLine("timestamp - unix timestamp")
                .AppendLine("process - process name")
                .ToString());

        internal static ConfigEntry<string> LogFormat = Config.Bind("Logging.File", "LogFormat", "[{0}] {1}",
            new StringBuilder()
                .AppendLine("Format for log messages. Accepts same input as String.Format.")
                .AppendLine("Available parameters:")
                .AppendLine("0 - Log level as reported by unity")
                .AppendLine("1 - The actual log message")
                .AppendLine("2 - Current timestamp as DateTime object")
                .ToString());

        public static IEnumerable<string> TargetDLLs { get; } = new string[0];

        public static void Patch(AssemblyDefinition ass)
        {
        }

        public static void Initialize()
        {
            try
            {
                InitializeInternal();
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Failed to initialize log mirroring: ({e.GetType()}) {e.Message}. No mirrored logs will be generated.");
                Logger.LogDebug(e);
            }
        }

        private static void InitializeInternal()
        {
            if (LogToFile.Value)
                InitializeFileLog();
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

        private static void InitializeFileLog()
        {
            var path = Path.GetFullPath(LogPath.Value.Format(new Dictionary<string, Func<string>>
            {
                ["timestamp"] = () => DateTime.Now.Ticks.ToString(),
                ["process"] = () => Paths.ProcessName
            }));
            var dir = Path.GetDirectoryName(path);
            Directory.CreateDirectory(dir);
            writer = new StreamWriter(path, false, Encoding.UTF8) {AutoFlush = true};

            InternalUnityLogger.OnUnityInternalLog += InternalUnityLoggerOnOnUnityInternalLog;
        }

        private static void InternalUnityLoggerOnOnUnityInternalLog(object sender, UnityLogEventArgs e)
        {
            try
            {
                writer.WriteLine(LogFormat.Value, e.LogLevel, e.Message.Trim(), DateTime.Now);
            }
            catch (Exception)
            {
                // Pass on failed logging
            }
        }
    }
}