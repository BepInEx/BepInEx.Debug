using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StartupProfiler
{
    public static class Hooks
    {
        private static Harmony harmony;

        public static void PatchHarmony()
        {
            harmony = new Harmony("StartupProfiler");
            //var target = Assembly.GetCallingAssembly().GetType("BepInEx.Preloader.PreloaderConsoleListener").GetMethod("Dispose", AccessTools.all);
            var target = AppDomain.CurrentDomain.GetAssemblies().ToList().First(x => x.FullName.Contains("BepInEx,")).GetType("BepInEx.Bootstrap.Chainloader").GetMethod("Start", AccessTools.all);
            var patch = typeof(Hooks).GetMethod(nameof(ChainloaderPatch1), AccessTools.all);
            harmony.Patch(target, null, new HarmonyMethod(patch));
        }

        private static void ChainloaderPatch1()
        {
            Console.WriteLine("--------ChainloaderPatch1--------");

            //var target = AppDomain.CurrentDomain.GetAssemblies().ToList().First(x => x.FullName.Contains("BepInEx,")).GetType("BepInEx.Bootstrap.Chainloader").GetMethod("Start", AccessTools.all);
            //var patch = typeof(Class1).GetMethod(nameof(Class1.ChainloaderPatch2), AccessTools.all);
            //harmony.Patch(target, new HarmonyMethod(patch));
        }

        //public static void ChainloaderPatch2()
        //{
        //    Console.WriteLine("--------ChainloaderPatch2--------");
        //}
    }
}
