using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using Common;
using UnityEngine;

namespace MonoProfiler
{
    [BepInPlugin(GUID, "MonoProfiler Controller", Version)]
    public class MonoProfilerController : BaseUnityPlugin
    {
        public const string GUID = "MonoProfiler";
        public const string Version = Metadata.Version;

        private ConfigEntry<bool> _uniqueNames;
        private ConfigEntry<KeyboardShortcut> _key;

        private void Awake()
        {
            if (!MonoProfilerPatcher.IsInitialized)
            {
                enabled = false;
                Logger.LogWarning("MonoProfiler was not initialized, can't proceed! Make sure that all profiler dlls are in the correct directories.");
                return;
            }

            _key = Config.Bind("Capture", "Dump collected data", new KeyboardShortcut(KeyCode.BackQuote), "Key used to dump all information to a file. Only includes information that was captured since the last time a dump was triggered.");
            _uniqueNames = Config.Bind("Capture", "Give dumps unique names", true, "If true each dump will be saved to a new file. If false old dump will be overwritten instead.");
        }

        private void Update()
        {
            if (_key.Value.IsDown())
            {
                var dumpFile = MonoProfilerPatcher.RunProfilerDump();

                if (_uniqueNames.Value)
                {
                    var containingDirectory = dumpFile.DirectoryName ?? throw new InvalidOperationException("dumpFile.DirectoryName is null for " + dumpFile);
                    dumpFile.MoveTo(Path.Combine(containingDirectory, $"{Path.GetFileNameWithoutExtension(dumpFile.Name)}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}{dumpFile.Extension}"));
                }

                Logger.LogMessage("Saved profiler dump to " + dumpFile.FullName);
            }
        }
    }
}