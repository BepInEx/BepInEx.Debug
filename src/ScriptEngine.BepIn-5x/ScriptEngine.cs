using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Mono.Cecil;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ScriptEngine
{
    [BepInPlugin(GUID: GUID, Name: "Script Engine", Version: Version)]
    public class ScriptEngine : BaseUnityPlugin
    {
        public const string GUID = "com.bepis.bepinex.scriptengine.bepin5";
        public const string Version = "1.0";

        public string ScriptDirectory => Path.Combine(Paths.BepInExRootPath, "scripts");

        private GameObject scriptManager = new GameObject();

        void Awake()
        {
            //ReloadPlugins();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.RightAlt) && Event.current.control)
            {
                ReloadPlugins();
            }
        }

        void ReloadPlugins()
        {
            Destroy(scriptManager);

            scriptManager = new GameObject();

            DontDestroyOnLoad(scriptManager);

            foreach (string path in Directory.GetFiles(ScriptDirectory, "*.dll"))
            {
                LoadDLL(path, scriptManager);
            }

	        Logger.Log(LogLevel.Message, "Reloaded script plugins!");
        }

        private void LoadDLL(string path, GameObject obj)
        {
            var defaultResolver = new DefaultAssemblyResolver();
            defaultResolver.AddSearchDirectory(ScriptDirectory);
	        defaultResolver.AddSearchDirectory(Paths.ManagedPath);
	        defaultResolver.AddSearchDirectory(Paths.BepInExAssemblyDirectory);

            using(var dll = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = defaultResolver }))
            {
                dll.Name.Name = $"{dll.Name.Name}-{DateTime.Now.Ticks}";

                using (var ms = new MemoryStream())
                {
                    dll.Write(ms);
                    var assembly = Assembly.Load(ms.ToArray());

                    foreach (Type t in assembly.GetTypes())
                    {
                        if (typeof(BaseUnityPlugin).IsAssignableFrom(t))
                        {
                            var metadata = MetadataHelper.GetMetadata(t);
                            var typeDefinition = dll.MainModule.Types.First(x => x.FullName == t.FullName);
                            var typeInfo = Chainloader.ToPluginInfo(typeDefinition);
                            Chainloader.PluginInfos[metadata.GUID] = typeInfo;

                            Logger.Log(LogLevel.Info, $"Reloading {metadata.GUID}");
                            obj.AddComponent(t);
                        }
                    }
                }
            }
        }
    }
}
