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
using UnityEngine;

namespace ScriptEngine
{
    [BepInPlugin(GUID, "Script Engine", Version)]
    public class ScriptEngine : BaseUnityPlugin
    {
        public const string GUID = "com.bepis.bepinex.scriptengine";
        public const string Version = "1.0.1";

        public string ScriptDirectory => Path.Combine(Paths.BepInExRootPath, "scripts");

        GameObject scriptManager;

        ConfigEntry<bool> LoadOnStart { get; set; }
        ConfigEntry<KeyboardShortcut> ReloadKey { get; set; }

        void Awake()
        {
            LoadOnStart = Config.Bind("General", "LoadOnStart", false, new ConfigDescription("Load all plugins from the scripts folder when starting the application"));
            ReloadKey = Config.Bind("General", "ReloadKey", new KeyboardShortcut(KeyCode.F6), new ConfigDescription("Press this key to reload all the plugins from the scripts folder"));

            if (LoadOnStart.Value)
                ReloadPlugins();
        }

        void Update()
        {
            if (ReloadKey.Value.IsDown())
                ReloadPlugins();
        }

        void ReloadPlugins()
        {
            Logger.Log(LogLevel.Info, "Unloading old plugin instances");
            Destroy(scriptManager);
            scriptManager = new GameObject($"ScriptEngine_{DateTime.Now.Ticks}");
            DontDestroyOnLoad(scriptManager);

            var files = Directory.GetFiles(ScriptDirectory, "*.dll");
            if (files.Length > 0)
            {
                foreach (string path in Directory.GetFiles(ScriptDirectory, "*.dll"))
                    LoadDLL(path, scriptManager);

                Logger.LogMessage("Reloaded all plugins!");
            }
            else
            {
                Logger.LogMessage("No plugins to reload");
            }
        }

        void LoadDLL(string path, GameObject obj)
        {
            var defaultResolver = new DefaultAssemblyResolver();
            defaultResolver.AddSearchDirectory(ScriptDirectory);
            defaultResolver.AddSearchDirectory(Paths.ManagedPath);
            defaultResolver.AddSearchDirectory(Paths.BepInExAssemblyDirectory);

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
