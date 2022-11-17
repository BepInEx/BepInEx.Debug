using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using Mono.Cecil;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Common;
using UnityEngine;

namespace ScriptEngine
{
    [BepInPlugin(GUID, "Script Engine", Version)]
    public class ScriptEngine : BaseUnityPlugin
    {
        public const string GUID = "com.bepis.bepinex.scriptengine";
        public const string Version = Metadata.Version;

        public string ScriptDirectory => Path.Combine(Paths.BepInExRootPath, "scripts");

        GameObject scriptManager;

        ConfigEntry<bool> LoadOnStart { get; set; }
        ConfigEntry<KeyboardShortcut> ReloadKey { get; set; }
        
        // FS Watcher EDIT
        ConfigEntry<bool> QuietMode { get; set; }
        ConfigEntry<bool> EnableFileSystemWatcher { get; set; }
        ConfigEntry<bool> IncludeSubdirectories { get; set; }
        
        private FileSystemWatcher fileSystemWatcher;
        private bool shouldReload;
        //

        void Awake()
        {
            LoadOnStart = Config.Bind("General", "LoadOnStart", false, new ConfigDescription("Load all plugins from the scripts folder when starting the application"));
            ReloadKey = Config.Bind("General", "ReloadKey", new KeyboardShortcut(KeyCode.F6), new ConfigDescription("Press this key to reload all the plugins from the scripts folder"));
            QuietMode = Config.Bind("General", "QuietMode", false, new ConfigDescription("Suppress sending log messages to console except for the error ones."));
            EnableFileSystemWatcher = Config.Bind("General", "EnableFileSystemWatcher", false, new ConfigDescription("Watches the scripts directory for file changes and reloads all plugins if any of them gets changed."));
            IncludeSubdirectories = Config.Bind("General", "IncludeSubdirectories", false, new ConfigDescription("Also include subdirectories under the scripts folder."));

            if (LoadOnStart.Value)
                ReloadPlugins();

            if (EnableFileSystemWatcher.Value)
                StartFileSystemWatcher();
        }

        void Update()
        {
            if (shouldReload || ReloadKey.Value.IsDown())
                ReloadPlugins();
        }

        void ReloadPlugins()
        {
            shouldReload = false;
            if (!QuietMode.Value)
                Logger.Log(LogLevel.Info, "Unloading old plugin instances");
            Destroy(scriptManager);
            scriptManager = new GameObject($"ScriptEngine_{DateTime.Now.Ticks}");
            DontDestroyOnLoad(scriptManager);

            var files = Directory.GetFiles(ScriptDirectory, "*.dll", IncludeSubdirectories.Value ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            if (files.Length > 0)
            {
                foreach (string path in Directory.GetFiles(ScriptDirectory, "*.dll", IncludeSubdirectories.Value ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
                    LoadDLL(path, scriptManager);

                if (!QuietMode.Value)
                    Logger.LogMessage("Reloaded all plugins!");
            }
            else
            {
                if (!QuietMode.Value)
                    Logger.LogMessage("No plugins to reload");
            }
        }

        void LoadDLL(string path, GameObject obj)
        {
            var defaultResolver = new DefaultAssemblyResolver();
            defaultResolver.AddSearchDirectory(ScriptDirectory);
            defaultResolver.AddSearchDirectory(Paths.ManagedPath);
            defaultResolver.AddSearchDirectory(Paths.BepInExAssemblyDirectory);

            if (!QuietMode.Value)
                Logger.Log(LogLevel.Info, $"Loading plugins from {path}");

            using (var dll = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = defaultResolver }))
            {
                dll.Name.Name = $"{dll.Name.Name}-{DateTime.Now.Ticks}";

                using (var ms = new MemoryStream())
                {
                    dll.Write(ms);
                    var ass = Assembly.Load(ms.ToArray());

                    foreach (Type type in GetTypesSafe(ass))
                    {
                        try
                        {
                            if (typeof(BaseUnityPlugin).IsAssignableFrom(type))
                            {
                                var metadata = MetadataHelper.GetMetadata(type);
                                if (metadata != null)
                                {
                                    var typeDefinition = dll.MainModule.Types.First(x => x.FullName == type.FullName);
                                    var typeInfo = Chainloader.ToPluginInfo(typeDefinition);
                                    Chainloader.PluginInfos[metadata.GUID] = typeInfo;

                                    if (!QuietMode.Value)
                                        Logger.Log(LogLevel.Info, $"Loading {metadata.GUID}");
                                    StartCoroutine(DelayAction(() =>
                                    {
                                        try
                                        {
                                            obj.AddComponent(type);
                                        }
                                        catch (Exception e)
                                        {
                                            Logger.LogError($"Failed to load plugin {metadata.GUID} because of exception: {e}");
                                        }
                                    }));
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.LogError($"Failed to load plugin {type.Name} because of exception: {e}");
                        }
                    }
                }
            }
        }

        private void StartFileSystemWatcher()
        {
            fileSystemWatcher = new FileSystemWatcher(ScriptDirectory)
            {
                IncludeSubdirectories = IncludeSubdirectories.Value
            };
            fileSystemWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            fileSystemWatcher.Filter = "*.dll";
            fileSystemWatcher.Changed += (sender, args) =>
            {
                if (!QuietMode.Value)
                    Logger.LogInfo($"File {Path.GetFileName(args.Name)} changed. Recompiling...");
                shouldReload = true;
            };
            fileSystemWatcher.Deleted += (sender, args) =>
            {
                if (!QuietMode.Value)
                    Logger.LogInfo($"File {Path.GetFileName(args.Name)} removed. Recompiling...");
                shouldReload = true;
            };
            fileSystemWatcher.Created += (sender, args) =>
            {
                if (!QuietMode.Value)
                    Logger.LogInfo($"File {Path.GetFileName(args.Name)} created. Recompiling...");
                shouldReload = true;
            };
            fileSystemWatcher.Renamed += (sender, args) =>
            {
                if (!QuietMode.Value)
                    Logger.LogInfo($"File {Path.GetFileName(args.Name)} renamed. Recompiling...");
                shouldReload = true;
            };
            fileSystemWatcher.EnableRaisingEvents = true;
        }
        
        private IEnumerable<Type> GetTypesSafe(Assembly ass)
        {
            try
            {
                return ass.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                var sbMessage = new StringBuilder();
                sbMessage.AppendLine("\r\n-- LoaderExceptions --");
                foreach (var l in ex.LoaderExceptions)
                    sbMessage.AppendLine(l.ToString());
                sbMessage.AppendLine("\r\n-- StackTrace --");
                sbMessage.AppendLine(ex.StackTrace);
                Logger.LogError(sbMessage.ToString());
                return ex.Types.Where(x => x != null);
            }
        }

        IEnumerator DelayAction(Action action)
        {
            yield return null;
            action();
        }
    }
}
