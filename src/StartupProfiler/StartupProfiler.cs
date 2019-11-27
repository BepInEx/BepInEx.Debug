using System;
using Mono.Cecil;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace StartupProfiler
{
    public class StartupProfiler
    {
        public static IEnumerable<string> TargetDLLs { get; } = new string[0];

        public static void Patch(AssemblyDefinition ass)
        {
        }
        
        public static void Finish()
        {
            Hooks.Patch();
        }
    }
}
