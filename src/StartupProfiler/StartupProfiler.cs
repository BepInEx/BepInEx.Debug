using HarmonyLib;
using Mono.Cecil;
using System;
using System.Collections.Generic;

namespace StartupProfiler
{
    public class StartupProfiler
    {
        public static IEnumerable<string> TargetDLLs { get; } = new string[0];
        public static void Patch(AssemblyDefinition ass) { }

        public static void Initialize()
        {
            var harmony = new Harmony("StartupProfiler");
            var target = AccessTools.Method("BepInEx.Preloader.Preloader:Run");
            var patch = AccessTools.Method(typeof(StartupProfiler), nameof(TestChainloaderPatch));
            harmony.Patch(target, new HarmonyMethod(patch));
        }

        private static void TestChainloaderPatch()
        {
            Console.WriteLine("HELLO THERE FELLOW HUMAN");
        }
    }
}
