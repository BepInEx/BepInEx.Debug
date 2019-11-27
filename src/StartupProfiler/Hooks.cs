using HarmonyLib;
using System;
using BepInEx.Bootstrap;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;

namespace StartupProfiler
{
    public static class Hooks
    {
        private static Harmony harmony;

        public static void Patch()
        {
            harmony = new Harmony("StartupProfiler");
            harmony.Patch(typeof(Chainloader).GetMethod(nameof(Chainloader.Initialize)),
                          prefix: new HarmonyMethod(typeof(Hooks).GetMethod(nameof(ChainloaderHook))));
        }

        public static void ChainloaderHook()
        {
            harmony.Patch(typeof(Chainloader).GetMethod(nameof(Chainloader.Start)),
                          prefix: new HarmonyMethod(typeof(Hooks).GetMethod(nameof(TestPatch))),
                          transpiler: new HarmonyMethod(typeof(Hooks).GetMethod(nameof(FindPluginTypes))));
        }

        public static void TestPatch()
        {
            Console.WriteLine("---------------CHAINLOADER PREFIX-----------------");
        }

        public static IEnumerable<CodeInstruction> FindPluginTypes(IEnumerable<CodeInstruction> instructions)
        {
            Console.WriteLine("---------------CHAINLOADER TRANSPILER-----------------");

            foreach(var code in instructions)
            {
                //if(code.opcode == OpCodes.Callvirt && code.operand == typeof(Dictionary<string, Assembly>).GetMethod("set_Item"))
                {
                    Console.WriteLine(code);
                }
            }

            return instructions;
        }
    }
}
