using BepInEx.Logging;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace ScriptEngine
{
    internal class ScriptEngineResolver : DefaultAssemblyResolver
    {
        public class ResolvedAssembly
        {
            public readonly AssemblyDefinition Definition;
            public readonly Assembly Assembly;

            public ResolvedAssembly(AssemblyDefinition definition, Assembly assembly)
            {
                Definition = definition;
                Assembly = assembly;
            }

            public void Deconstruct(out AssemblyDefinition definition, out Assembly assembly)
            {
                definition = Definition;
                assembly = Assembly;
            }
        }

        private readonly Func<AssemblyDefinition, Assembly> assemblyLoader;
        private readonly List<AssemblyDefinition> definitions = new List<AssemblyDefinition>();
        private readonly Dictionary<string, AssemblyDefinition> overrides = new Dictionary<string, AssemblyDefinition>();
        private readonly Dictionary<string, string> paths = new Dictionary<string, string>();
        private readonly Dictionary<string, ResolvedAssembly> cache = new Dictionary<string, ResolvedAssembly>();

        public ScriptEngineResolver(Func<AssemblyDefinition, Assembly> assemblyLoader) => this.assemblyLoader = assemblyLoader;

        public void AddDefinition(string oldAssemblyName, AssemblyDefinition assemblyDefinition, string path)
        {
            // Both the old and new names map to the same definition.
            overrides.Add(oldAssemblyName, assemblyDefinition);
            overrides.Add(assemblyDefinition.Name.Name, assemblyDefinition);
            paths.Add(assemblyDefinition.Name.Name, path);
            definitions.Add(assemblyDefinition);
        }

        public string GetPath(AssemblyDefinition definition) => paths.TryGetValue(definition.Name.Name, out var path) ? path : "";

        public IEnumerable<AssemblyDefinition> GetDefinitions() => definitions;

        public bool RenameReference(string assemblyName, out string renamed)
        {
            if (overrides.TryGetValue(assemblyName, out var definition))
            {
                renamed = definition.Name.Name;
                return true;
            }

            renamed = "";
            return false;
        }

        public override AssemblyDefinition Resolve(AssemblyNameReference name) => Resolve(name, new ReaderParameters());

        public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters) => overrides.TryGetValue(name.Name, out var assembly) ? assembly : base.Resolve(name, parameters);

        public Assembly LoadHandler(object sender, ResolveEventArgs args) => LoadAssembly(new AssemblyName(args.Name).Name)?.Assembly;

        public ResolvedAssembly LoadAssembly(AssemblyDefinition definition) => LoadAssembly(definition.Name.Name);

        private ResolvedAssembly LoadAssembly(string name)
        {
            if (cache.TryGetValue(name, out var resolvedAssembly))
                return resolvedAssembly;
            if (!overrides.TryGetValue(name, out var definition))
                return null;

            resolvedAssembly = new ResolvedAssembly(definition, assemblyLoader(definition));
            cache.Add(name, resolvedAssembly);
            return resolvedAssembly;
        }

        // Definitions must be disposed to release file handles on the scripts/ assemblies they were read from.
        // This should only be called once the definitions are no longer needed.
        internal void DisposeDefinitions()
        {
            foreach (var definition in definitions)
                definition.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            DisposeDefinitions();
            definitions.Clear();
            overrides.Clear();
            paths.Clear();
            cache.Clear();
        }
    }
}
