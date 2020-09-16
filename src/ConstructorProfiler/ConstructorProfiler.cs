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
            InstallHooks();
        }

        private void InstallHooks()
        {
            var asses = AppDomain.CurrentDomain.GetAssemblies().Where(x =>
                {
                    if (new[] { "ConstructorProfiler", "mscorlib" }.Any(y => x.FullName.Contains(y))) return false;
                    //try{if (x.Location.Contains("BepInEx")) return true;}
                    //catch{}


                    return true; //false; //AssFilter.Contains(x.FullName.Split(',')[0]);
                })
                .ToList(); //.Where(ass => AssFilter.Contains(ass.FullName.Split(',')[0])).ToList();
            var types = asses.SelectMany(ass =>
                {
                    try
                    {
                        return ass.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        return ex.Types.Where(x => x != null);
                    }
                }).Where(x => x.IsClass && !x.IsGenericType)
                //.Where(x =>
                //{
                //    if (x.Assembly.FullName.Contains("mscorlib"))
                //    {
                //        return  //(x.Namespace == null || x.Namespace == "System") && 
                //                !x.Name.Contains("Exception") && 
                //                !x.Name.Contains("Object") && 
                //                !x.Name.Contains("String") && 
                //                x.Namespace?.Contains("Diagnostics") != true;
                //    }
                //
                //    return true;
                //})
                .ToList();
            //var constructors = types.SelectMany(type => type.GetConstructors()).Where(x => !x.FullDescription().Contains("<") || !x.FullDescription().Contains(">")).ToList();
            var constructors = types.SelectMany(type => type.GetConstructors()).ToList();

            //int index = 0;
            foreach (var constructor in constructors)
            {
                //if(index > 1000)
                //    return;
                //index++;

                try
                {
                    //Logger.LogInfo($"Patching {constructor.FullDescription()}");
                    harmony.Patch(constructor, new HarmonyMethod(AddCallMethodInfo));
                }
                catch (Exception)
                {
                    Logger.LogWarning($"Exception patching {constructor.FullDescription()}");
                }
            }
        }

        private static MethodInfo AddCallMethodInfo = typeof(ConstructorProfiler).GetMethod(nameof(AddCall), AccessTools.all);
        private static void AddCall()
        {
            if (!run)
            {
                return;
            }
            var stackTrace = new StackTrace();
            var key = stackTrace.ToString();

            if (CallCounter.TryGetValue(key, out var data))
            {
                CallCounter[key].count = data.count + 1;
            }
            else
            {
                CallCounter.Add(key, new StackData(stackTrace));
            }
        }

        private static bool run;
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.B))
            {
                if (!run)
                {
                    // InstallHooks();
                    Logger.LogInfo("Started collecting data");

                    run = true;
                    return;
                }

                Logger.LogInfo($"Outputting data ({CallCounter.Count})");

                var counter = CallCounter;
                CallCounter = new Dictionary<string, StackData>();

                var results = counter.Values.OrderByDescending(x => x.count).Select(item =>
                {
                    var ctorFrame = item.stackTrace.GetFrame(1);
                    var createdType = ctorFrame.GetMethod().DeclaringType;
                    var createdTypeStr = createdType?.FullName ?? ctorFrame.ToString();
                    var stack = string.Join("\n", item.stackTrace.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Skip(2).ToArray());

                    //var stack = string.Join("\n", item.stackTrace.GetFrames().Skip(2).Select(x =>
                    //{
                    //    var m = x.GetMethod();
                    //    return m.DeclaringType?.FullName ?? "Unknown" + "." + m;
                    //}).ToArray());
                    return new { stack, createdTypeStr, count = item.count.ToString() };
                }).ToList();

                results.Insert(0, new { stack = "Stack", createdTypeStr = "Created object", count = "Count" });

                File.WriteAllLines(
                    Path.Combine(Paths.GameRootPath, $"ConstructorProfiler{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.csv"),
                    results.Select(x => $"\"{x.stack}\",\"{x.createdTypeStr}\",\"{x.count}\"").ToArray());
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
