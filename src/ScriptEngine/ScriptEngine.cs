using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using Common;
using HarmonyLib;
using Mono.Cecil;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using UnityEngine;

namespace ScriptEngine
{
    [BepInPlugin(GUID, "Script Engine", Version)]
    public class ScriptEngine : BaseUnityPlugin
    {
        public const string GUID = "com.bepis.bepinex.scriptengine";
        public const string Version = Metadata.Version;

        public string ScriptDirectory => Path.Combine(Paths.BepInExRootPath, "scripts");

        private GameObject scriptManager;

        private ConfigEntry<bool> LoadOnStart { get; set; }
        private ConfigEntry<KeyboardShortcut> ReloadKey { get; set; }
        private ConfigEntry<bool> QuietMode { get; set; }
        private ConfigEntry<bool> EnableFileSystemWatcher { get; set; }
        private ConfigEntry<bool> IncludeSubdirectories { get; set; }
        private ConfigEntry<float> AutoReloadDelay { get; set; }

        private ConfigEntry<bool> DumpAssemblies { get; set; }
        private static readonly string DumpedAssembliesPath = Utility.CombinePaths(Paths.BepInExRootPath, "ScriptEngineDumpedAssemblies");

        private FileSystemWatcher fileSystemWatcher;
        private bool shouldReload;
        private float autoReloadTimer;
        private ScriptEngineResolver currentResolver;

        private void Awake()
        {
            LoadOnStart = Config.Bind("General", "LoadOnStart", false, new ConfigDescription("Load all plugins from the scripts folder when starting the application. This is done from inside of Chainloader's Awake, therefore not all plugis might be loaded yet. BepInDependency attributes are ignored."));
            ReloadKey = Config.Bind("General", "ReloadKey", new KeyboardShortcut(KeyCode.F6), new ConfigDescription("Press this key to reload all the plugins from the scripts folder"));
            QuietMode = Config.Bind("General", "QuietMode", false, new ConfigDescription("Disable all logging except for error messages."));
            IncludeSubdirectories = Config.Bind("General", "IncludeSubdirectories", false, new ConfigDescription("Also load plugins from subdirectories of the scripts folder."));
            EnableFileSystemWatcher = Config.Bind("AutoReload", "EnableFileSystemWatcher", false, new ConfigDescription("Watches the scripts directory for file changes and automatically reloads all plugins if any of the files gets changed (added/removed/modified)."));
            AutoReloadDelay = Config.Bind("AutoReload", "AutoReloadDelay", 3.0f, new ConfigDescription("Delay in seconds from detecting a change to files in the scripts directory to plugins being reloaded. Affects only EnableFileSystemWatcher."));
            DumpAssemblies = Config.Bind<bool>("AutoReload", "DumpAssemblies", false, "If enabled, BepInEx will save patched assemblies & symbols into BepInEx/ScriptEngineDumpedAssemblies.\nThis can be used by developers to inspect and debug plugins loaded by ScriptEngine.");

            if (Directory.Exists(DumpedAssembliesPath))
                Directory.Delete(DumpedAssembliesPath, true);

            if (LoadOnStart.Value)
                ReloadPlugins();

            if (EnableFileSystemWatcher.Value)
                StartFileSystemWatcher();
        }

        private void Update()
        {
            if (ReloadKey.Value.IsDown())
            {
                ReloadPlugins();
            }
            else if (shouldReload)
            {
                autoReloadTimer -= Time.unscaledDeltaTime;
                if (autoReloadTimer <= .0f)
                    ReloadPlugins();
            }
        }

        private void ReloadPlugins()
        {
            shouldReload = false;

            if (scriptManager != null)
            {
                if (!QuietMode.Value) Logger.Log(LogLevel.Info, "Unloading old plugin instances");

                foreach (var previouslyLoadedPlugin in scriptManager.GetComponents<BaseUnityPlugin>())
                {
                    var metadataGUID = previouslyLoadedPlugin.Info.Metadata.GUID;
                    if (Chainloader.PluginInfos.ContainsKey(metadataGUID))
                        Chainloader.PluginInfos.Remove(metadataGUID);
                }

                Destroy(scriptManager);
            }

            scriptManager = new GameObject($"ScriptEngine_{DateTime.Now.Ticks}");
            DontDestroyOnLoad(scriptManager);
            scriptManager.hideFlags = HideFlags.HideAndDontSave;

            var files = Directory.GetFiles(ScriptDirectory, "*.dll", IncludeSubdirectories.Value ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            if (files.Length > 0)
            {
                LoadDLLs(files, scriptManager);

                if (!QuietMode.Value)
                    Logger.LogMessage("Reloaded all plugins!");
            }
            else
            {
                if (!QuietMode.Value)
                    Logger.LogMessage("No plugins to reload");
            }
        }

        private Assembly LoadAssembly(AssemblyDefinition definition)
        {
            if (DumpAssemblies.Value)
            {
                // Dump assemblies & load them from disk
                if (!Directory.Exists(DumpedAssembliesPath))
                    Directory.CreateDirectory(DumpedAssembliesPath);

                string assemblyDumpPath = Path.Combine(DumpedAssembliesPath, definition.Name.Name + Path.GetExtension(definition.MainModule.Name));

                using (FileStream outFileStream = new FileStream(assemblyDumpPath, FileMode.Create))
                {
                    definition.Write(outFileStream, new WriterParameters()
                    {
                        WriteSymbols = true
                    });
                }

                var assembly = Assembly.LoadFile(assemblyDumpPath);
                if (!QuietMode.Value)
                    Logger.Log(LogLevel.Info, $"Loaded dumped Assembly from {assemblyDumpPath}");

                return assembly;
            }

            // Otherwise, load in memory.
            using (var stream = new MemoryStream())
            {
                definition.Write(stream);
                return Assembly.Load(stream.ToArray());
            }
        }

        private void LoadDLLs(IEnumerable<string> paths, GameObject obj)
        {
            var suffix = $"-{DateTime.Now.Ticks}";

            var resolver = new ScriptEngineResolver(LoadAssembly);
            resolver.AddSearchDirectory(ScriptDirectory);
            resolver.AddSearchDirectory(Paths.ManagedPath);
            resolver.AddSearchDirectory(Paths.BepInExAssemblyDirectory);

            // Load definitions.
            foreach (var path in paths)
            {
                var definition = AssemblyDefinition.ReadAssembly(path, new ReaderParameters
                {
                    AssemblyResolver = resolver,
                    ReadingMode = ReadingMode.Immediate,
                    ReadSymbols = true
                });

                var oldName = definition.Name.Name;
                var newName = $"{oldName}{suffix}";
                definition.Name.Name = newName;
                resolver.AddDefinition(oldName, definition, path);
            }

            // Update references.
            foreach (var definition in resolver.GetDefinitions())
            {
                foreach (var module in definition.Modules)
                {
                    foreach (var reference in module.AssemblyReferences)
                    {
                        if (resolver.RenameReference(reference.Name, out var renamed))
                            reference.Name = renamed;
                    }
                }
            }

            // Replace the old resolver.
            if (currentResolver != null)
            {
                AppDomain.CurrentDomain.AssemblyResolve -= currentResolver.LoadHandler;
                currentResolver.Dispose();
            }
            currentResolver = resolver;
            AppDomain.CurrentDomain.AssemblyResolve += resolver.LoadHandler;

            // Load assemblies.
            var assemblies = resolver.GetDefinitions().Select(resolver.LoadAssembly).ToList();

            // Reload plugins.
            foreach (var (definition, assembly) in assemblies)
            {
                foreach (var type in GetTypesSafe(assembly))
                {
                    try
                    {
                        if (!typeof(BaseUnityPlugin).IsAssignableFrom(type)) continue;

                        var metadata = MetadataHelper.GetMetadata(type);
                        if (metadata == null) continue;

                        if (!QuietMode.Value)
                            Logger.Log(LogLevel.Info, $"Loading {metadata.GUID}");

                        if (Chainloader.PluginInfos.TryGetValue(metadata.GUID, out var existingPluginInfo))
                            throw new InvalidOperationException($"A plugin with GUID {metadata.GUID} is already loaded! ({existingPluginInfo.Metadata.Name} v{existingPluginInfo.Metadata.Version})");

                        var typeDefinition = definition.MainModule.Types.First(x => x.FullName == type.FullName);
                        var pluginInfo = Chainloader.ToPluginInfo(typeDefinition);

                        StartCoroutine(DelayAction(() =>
                        {
                            try
                            {
                                // Need to add to PluginInfos first because BaseUnityPlugin constructor (called by AddComponent below)
                                // looks in PluginInfos for an existing PluginInfo and uses it instead of creating a new one.
                                Chainloader.PluginInfos[metadata.GUID] = pluginInfo;

                                var instance = obj.AddComponent(type);

                                // Fill in properties that are normally set by Chainloader
                                var tv = Traverse.Create(pluginInfo);
                                tv.Property<BaseUnityPlugin>(nameof(pluginInfo.Instance)).Value = (BaseUnityPlugin)instance;
                                // Loading the assembly from memory causes Location to be lost
                                tv.Property<string>(nameof(pluginInfo.Location)).Value = resolver.GetPath(definition);
                            }
                            catch (Exception e)
                            {
                                Logger.LogError($"Failed to load plugin {metadata.GUID} because of exception: {e}");
                                Chainloader.PluginInfos.Remove(metadata.GUID);
                            }
                        }));
                    }
                    catch (Exception e)
                    {
                        Logger.LogError($"Failed to load plugin {type.Name} because of exception: {e}");
                    }
                }
            }

            // We don't need the definitions anymore.
            resolver.DisposeDefinitions();
        }

        private void StartFileSystemWatcher()
        {
            fileSystemWatcher = new FileSystemWatcher(ScriptDirectory)
            {
                IncludeSubdirectories = IncludeSubdirectories.Value,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                Filter = "*.dll"
            };
            fileSystemWatcher.Changed += FileChangedEventHandler;
            fileSystemWatcher.Deleted += FileChangedEventHandler;
            fileSystemWatcher.Created += FileChangedEventHandler;
            fileSystemWatcher.Renamed += FileChangedEventHandler;
            fileSystemWatcher.EnableRaisingEvents = true;
        }

        private void FileChangedEventHandler(object sender, FileSystemEventArgs args)
        {
            if (!QuietMode.Value)
                Logger.LogInfo($"File {Path.GetFileName(args.Name)} changed. Delayed recompiling...");
            shouldReload = true;
            autoReloadTimer = AutoReloadDelay.Value;
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

        private IEnumerator DelayAction(Action action)
        {
            yield return null;
            action();
        }
    }
}
