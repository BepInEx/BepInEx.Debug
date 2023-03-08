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

        private static PatcherBase patcher;
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
                Logger.LogWarning(
                    $"Failed to initialize log mirroring: ({e.GetType()}) {e.Message}. No mirrored logs will be generated.");
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
                .FirstOrDefault(IsUnityPlayer) ?? Process.GetCurrentProcess().MainModule ?? throw new InvalidOperationException("Could not find Process.MainModule to patch");

            if (IntPtr.Size == 8)
                patcher = new X64Patcher();
            else
                patcher = new X86Patcher();
            Logger.LogDebug($"Using patcher: {patcher}");

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
            Directory.CreateDirectory(dir ?? throw new InvalidOperationException("Path.GetDirectoryName is null for path: " + path));
            if (!TryCreateFile(path, out writer))
            {
                Logger.LogWarning(
                    "Couldn't create log file because the file is likely in use, skipping mirroring logs...");
                return;
            }

            InternalUnityLogger.OnUnityInternalLog += InternalUnityLoggerOnOnUnityInternalLog;
        }

        private static bool TryCreateFile(string path, out StreamWriter sw, int max = 50)
        {
            var dir = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Path.GetDirectoryName is null for path: " + path);
            var filename = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);

            for (var i = 0; i < max; i++)
                try
                {
                    var filePath = Path.Combine(dir, $"{filename}{(i > 0 ? $"_{i}" : "")}{ext}");
                    sw = new StreamWriter(filePath, false, Encoding.UTF8) { AutoFlush = true };
                    return true;
                }
                catch (Exception)
                {
                    // skip
                }

            sw = null;
            return false;
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
