using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using Mono.Cecil;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
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

            if(LoadOnStart.Value)
                ReloadPlugins();
        }

        void Update()
        {
            if(ReloadKey.Value.IsDown())
                ReloadPlugins();
        }

        void ReloadPlugins()
        {
            Destroy(scriptManager);
            scriptManager = new GameObject($"ScriptEngine_{DateTime.Now.Ticks}");
            DontDestroyOnLoad(scriptManager);

            var files = Directory.GetFiles(ScriptDirectory, "*.dll");
            if(files.Length > 0)
            {
                foreach(string path in Directory.GetFiles(ScriptDirectory, "*.dll"))
                    LoadDLL(path, scriptManager);

                Logger.LogMessage("Reloaded script plugins!");
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

            using(var dll = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = defaultResolver }))
            {
                dll.Name.Name = $"{dll.Name.Name}-{DateTime.Now.Ticks}";

                using(var ms = new MemoryStream())
                {
                    dll.Write(ms);
                    var ass = Assembly.Load(ms.ToArray());

                    try
                    {
                        foreach (Type type in ass.GetTypes())
                        {
                            if (typeof(BaseUnityPlugin).IsAssignableFrom(type))
                            {
                                var metadata = MetadataHelper.GetMetadata(type);
                                if (metadata != null)
                                {
                                    var typeDefinition = dll.MainModule.Types.First(x => x.FullName == type.FullName);
                                    var typeInfo = Chainloader.ToPluginInfo(typeDefinition);
                                    Chainloader.PluginInfos[metadata.GUID] = typeInfo;

                                    Logger.Log(LogLevel.Info, $"Reloading {metadata.GUID}");
                                    StartCoroutine(DelayAction(() => obj.AddComponent(type)));
                                }
                            }
                        }
                    }
                    catch (ReflectionTypeLoadException e)
                    {
                        StringBuilder strTypes = new StringBuilder();
                        foreach (var t in e.Types) { strTypes.Append(t + "\r\n"); }
                        StringBuilder strExceptions = new StringBuilder();
                        foreach (var l in e.LoaderExceptions) { strExceptions.Append(l + "\r\n"); }
                        Logger.LogError($"Error While loading {path} \r\n -- Types --\r\n{strTypes}\r\n-- LoaderExceptions --\r\n{strExceptions}\r\n -- StackTrace --\r\n{e.StackTrace}");
                    }
                }
            }
        }

        IEnumerator DelayAction(Action action)
        {
            yield return null;
            action();
        }
    }
}
