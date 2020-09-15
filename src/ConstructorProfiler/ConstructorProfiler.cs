using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using BepInEx;
using System.Reflection;
using System.Diagnostics;
using HarmonyLib;
using System.IO;

namespace ConstructorProfiler
{
    [BepInPlugin("keelhauled.constructorprofiler", "Constructor Profiler", "1.0.0")]
    public class ConstructorProfiler : BaseUnityPlugin
    {
        private static string[] AssFilter = new[] { "Assembly-CSharp", "UnityEngine" };
        private static Dictionary<string, StackData> CallCounter = new Dictionary<string, StackData>();
        private Harmony harmony = new Harmony(nameof(ConstructorProfiler));

        private void Awake()
        {
            var asses = AppDomain.CurrentDomain.GetAssemblies().Where(ass => AssFilter.Contains(ass.FullName.Split(',')[0])).ToList();
            var types = asses.SelectMany(ass => ass.GetTypes().Where(type => type.IsClass)).Where(x => !x.IsGenericType).ToList();
            //var constructors = types.SelectMany(type => type.GetConstructors()).Where(x => !x.FullDescription().Contains("<") || !x.FullDescription().Contains(">")).ToList();
            var constructors = types.SelectMany(type => type.GetConstructors()).ToList();

            //int index = 0;
            foreach(var constructor in constructors)
            {
                //if(index > 1000)
                //    return;
                //index++;

                try
                {
                    Logger.LogInfo($"Patching {constructor.FullDescription()}");
                    harmony.Patch(constructor, new HarmonyMethod(AddCallMethodInfo));
                }
                catch(Exception)
                {
                    Logger.LogWarning($"Exception patching {constructor.FullDescription()}");
                }
            }
        }

        private static MethodInfo AddCallMethodInfo = typeof(ConstructorProfiler).GetMethod(nameof(AddCall), AccessTools.all);
        private static void AddCall()
        {
            var stackTrace = new StackTrace();
            var key = stackTrace.ToString();

            if(CallCounter.TryGetValue(key, out var data))
            {
                CallCounter[key].count = data.count + 1;
            }
            else
            {
                CallCounter.Add(key, new StackData(stackTrace));
            }
        }

        private void Update()
        {
            if(Input.GetKeyDown(KeyCode.B))
            {
                Logger.LogInfo($"Outputting data ({CallCounter.Count})");

                foreach(var item in CallCounter.OrderBy(x => x.Value.count))
                {
                    Logger.LogInfo($"{item.Value.stackTrace}: {item.Value.count}");
                }
            }
        }

        public class StackData
        {
            public StackTrace stackTrace;
            public int count;

            public StackData(StackTrace stackTrace)
            {
                this.stackTrace = stackTrace;
                count = 0;
            }
        }
    }
}
