using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using Mono.Cecil;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace StartupProfiler
{
    public class StartupProfiler
    {
        public static IEnumerable<string> TargetDLLs { get; } = new string[0];

        private static ManualLogSource logger;
        private static Harmony harmony;
        private static Stopwatch chainTimer;

        private static string[] unityMethods = new[] { "Awake", "Start", "Main" };
        private static readonly Dictionary<Type, KeyValuePair<BepInPlugin, Stopwatch>> timers = new Dictionary<Type, KeyValuePair<BepInPlugin, Stopwatch>>();

        public static void Patch(AssemblyDefinition ass) { }

        public static void Finish()
        {
            logger = Logger.CreateLogSource(nameof(StartupProfiler));
            harmony = new Harmony(nameof(StartupProfiler));

            harmony.Patch(typeof(Chainloader).GetMethod(nameof(Chainloader.Initialize)),
                          postfix: new HarmonyMethod(typeof(StartupProfiler).GetMethod(nameof(ChainloaderHook))));
        }

        public static void ChainloaderHook()
        {
            harmony.Patch(typeof(Chainloader).GetMethod(nameof(Chainloader.Start)),
                          new HarmonyMethod(typeof(StartupProfiler).GetMethod(nameof(ChainloaderPre))),
                          new HarmonyMethod(typeof(StartupProfiler).GetMethod(nameof(ChainloaderPost))),
                          new HarmonyMethod(typeof(StartupProfiler).GetMethod(nameof(FindPluginTypes))));
        }

        public static void ChainloaderPre()
        {
            chainTimer = new Stopwatch();
            chainTimer.Start();
        }

        public static IEnumerable<CodeInstruction> FindPluginTypes(IEnumerable<CodeInstruction> instructions)
        {
            foreach(var code in instructions)
            {
                if(code.opcode == OpCodes.Callvirt && code.operand.ToString().Contains("AddComponent"))
                    yield return new CodeInstruction(OpCodes.Call, typeof(StartupProfiler).GetMethod(nameof(PatchPlugin)));

                yield return code;
            }
        }

        public static Type PatchPlugin(Type type)
        {
            var bepInPlugin = (BepInPlugin)type.GetCustomAttributes(false).First(x => x.GetType() == typeof(BepInPlugin));
            timers[type] = new KeyValuePair<BepInPlugin, Stopwatch>(bepInPlugin, new Stopwatch());

            foreach(var unityMethod in unityMethods)
            {
                var methodInfo = type.GetMethod(unityMethod, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if(methodInfo == null) continue;
                harmony.Patch(methodInfo, new HarmonyMethod(StartTimerMethodInfo), new HarmonyMethod(StopTimerMethodInfo));
            }

            foreach(var methodInfo in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                harmony.Patch(methodInfo, new HarmonyMethod(StartTimerMethodInfo), new HarmonyMethod(StopTimerMethodInfo));

            return type;
        }

        public static MethodInfo StartTimerMethodInfo = typeof(StartupProfiler).GetMethod(nameof(StartTimer));
        public static void StartTimer(object __instance)
        {
            if(timers.TryGetValue(__instance.GetType(), out var watch))
                watch.Value.Start();
        }

        public static MethodInfo StopTimerMethodInfo = typeof(StartupProfiler).GetMethod(nameof(StopTimer));
        public static void StopTimer(object __instance)
        {
            if(timers.TryGetValue(__instance.GetType(), out var watch))
                watch.Value.Stop();
        }

        public static void ChainloaderPost()
        {
            chainTimer.Stop();
            ThreadingHelper.Instance.StartCoroutine(PrintResults());
        }

        public static IEnumerator PrintResults()
        {
            yield return null;

            logger.LogInfo($"Chainloader total: {chainTimer.ElapsedMilliseconds} ms");
            logger.LogInfo($"Plugins total: {timers.Sum(x => x.Value.Value.ElapsedMilliseconds)} ms");

            foreach(var timer in timers.OrderByDescending(x => x.Value.Value.ElapsedMilliseconds))
                logger.LogInfo($"{timer.Value.Key.GUID}: {timer.Value.Value.ElapsedMilliseconds} ms");

            harmony.UnpatchSelf();
        }
    }
}
