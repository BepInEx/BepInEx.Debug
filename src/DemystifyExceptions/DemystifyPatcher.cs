using System;
using System.Collections.Generic;
using DemystifyExceptions.Demystify;
using HarmonyLib;
using Mono.Cecil;

namespace DemystifyExceptions
{
    public static class DemystifyPatcher
    {
        public static IEnumerable<string> TargetDLLs { get; } = new string[0];

        public static void Initialize()
        {
            Harmony.CreateAndPatchAll(typeof(DemystifyPatcher), "org.bepinex.debug.demystifyexceptions");
        }
    
        [HarmonyPatch(typeof(Exception), nameof(Exception.StackTrace), MethodType.Getter)]
        [HarmonyPrefix]
        public static bool ExceptionGetStackTrace(Exception __instance, ref string __result)
        {
            try
            {
                var exStackTrace = new EnhancedStackTrace(__instance);
                __result = exStackTrace.ToString();
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }
        
        [HarmonyPatch(typeof(Exception), nameof(Exception.ToString))]
        [HarmonyPrefix]
        public static bool ExceptionToStringHook(Exception __instance, ref string __result)
        {
            __result = __instance.ToStringDemystified();
            return false;
        }

        public static void Patch(AssemblyDefinition ad)
        {
        }
    }
}
