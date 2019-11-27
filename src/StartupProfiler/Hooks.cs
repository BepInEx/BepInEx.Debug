using HarmonyLib;
using System;
using BepInEx.Bootstrap;

namespace StartupProfiler
{
    public static class Hooks
    {
        private static Harmony harmony;

        public static void PatchHarmony()
        {
            harmony = new Harmony("StartupProfiler");
            harmony.Patch(typeof(Chainloader).GetMethod(nameof(Chainloader.Initialize)),
                          prefix: new HarmonyMethod(typeof(Hooks).GetMethod(nameof(ChainloaderPatch1))));
        }

        public static void ChainloaderPatch1()
        {
            harmony.Patch(typeof(Chainloader).GetMethod(nameof(Chainloader.Start)),
                          postfix: new HarmonyMethod(typeof(Hooks).GetMethod(nameof(Patch2))));
        }

        public static void Patch2()
        {
            Console.WriteLine("--------ChainloaderPatch1--------");
        }
    }
}
