using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using MonoMod.Cil;
using MonoMod.Utils;

namespace HarmonyPatchRegressionChecker
{
    public class Class1
    {

        private static void Start()
        {
            //todo add record mode and compare mode. save results at application close on record, load and compare on load, write a diff somewhere at close including missing/extra calls

            var hi = new Harmony("HarmonyPatchRegressionChecker");
            // harmonyx only?
            var targetType = AccessTools.TypeByName("HarmonyLib.Internal.Patching.HarmonyManipulator") ?? throw new MissingMethodException("HarmonyLib.Internal.Patching.HarmonyManipulator");
            var targetMethod = AccessTools.Method(targetType, "Manipulate") ?? throw new MissingMethodException("Manipulate");
            var patchMethod = new HarmonyMethod(typeof(Class1), nameof(Class1.PatchHook));
            hi.Patch(targetMethod, patchMethod);
        }

        static Dictionary<string, List<string>> _patchDict = new Dictionary<string, List<string>>();

        private static void PatchHook(MethodBase original, PatchInfo patchInfo)
        {
            var targetMethodHash = ByteArrayToString(HashMethod(original));

            foreach (var patch in patchInfo.transpilers.Concat(patchInfo.finalizers).Concat(patchInfo.postfixes).Concat(patchInfo.prefixes))
            {
                var patchHash = ByteArrayToString(HashMethod(patch.patch));

                _patchDict.TryGetValue(patchHash, out var hl);
                if (hl == null)
                {
                    hl = new List<string>();
                    _patchDict[patchHash] = hl;
                }

                // todo add method signatures somewhere to have human readable form, maybe make hashes be full method signature + il hash
                hl.Add(targetMethodHash);
            }

        }
        public static string ByteArrayToString(byte[] ba)
        {
            return BitConverter.ToString(ba).Replace("-", "");
        }
        public static byte[] HashMethod(MethodBase mb)
        {
            var body = mb.GetMethodBody();
            if (body == null)
                return null;

            var sha1 = SHA1.Create();
            var fullName = Encoding.UTF8.GetBytes(mb.GetID());
            sha1.TransformBlock(fullName, 0, fullName.Length, null, 0);
            var il = body.GetILAsByteArray();
            var hash = sha1.TransformFinalBlock(il, 0, il.Length);

            return hash;
        }
    }
}
