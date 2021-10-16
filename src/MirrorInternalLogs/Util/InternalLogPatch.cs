using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace MirrorInternalLogs.Util
{
    internal interface IPatch
    {
        void Apply(IntPtr from);
        BytePattern Pattern { get; }
    }
    
    internal class InternalLogPatch<T> : IPatch where T : Delegate
    {
        private static T original;
        private T target;
        
        public BytePattern Pattern { get; }

        public InternalLogPatch(BytePattern pattern, T target)
        {
            Pattern = pattern;

            var delegateType = typeof(T);
            var invoke = delegateType.GetMethod("Invoke");
            var callParams = invoke.GetParameters();

            var dmd = new DynamicMethodDefinition($"{typeof(InternalLogPatch<T>).FullName}_Detour", invoke.ReturnType,
                callParams.Select(p => p.ParameterType).ToArray());
            var il = dmd.GetILGenerator();

            for (var index = 0; index < callParams.Length; index++)
                il.Emit(OpCodes.Ldarg, index);
            il.Emit(OpCodes.Call, target.Method);
            if (invoke.ReturnType != typeof(void))
                il.Emit(OpCodes.Pop);

            il.Emit(OpCodes.Ldsfld, typeof(InternalLogPatch<T>).GetField("original", BindingFlags.NonPublic | BindingFlags.Static) ?? throw new Exception("Could not find `original` field"));
            for (var index = 0; index < callParams.Length; index++)
                il.Emit(OpCodes.Ldarg, index);
            il.Emit(OpCodes.Call, invoke);
            il.Emit(OpCodes.Ret);

            this.target = (T)dmd.Generate().CreateDelegate(typeof(T));
        }

        public void Apply(IntPtr from)
        {
            var hookPtr = Marshal.GetFunctionPointerForDelegate(target);
            var det = new NativeDetour(from, hookPtr, new NativeDetourConfig {ManualApply = true});
            original = det.GenerateTrampoline<T>();
            det.Apply();
        }
    }
}
