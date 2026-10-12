using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEditor.UnityLinker;

namespace UC
{
    // Generic serialization gets to types by their name and to fields by reflection, and a .data file
    // may well be the only place where some of them are used. Managed code stripping (IL2CPP builds,
    // mostly) can't tell, so it would take them out: a type nobody refers to, a constructor nobody
    // calls, a field nobody reads. This tells the linker what to leave alone on every build - all
    // that implements IGenericSerialization, and everything that can be reached from there.
    public class GenericSerializationLinkerProcessor : IUnityLinkerProcessor
    {
        const string linkXmlPath = "Library/UnityCommon/GenericSerialization/link.xml";

        public int callbackOrder => 0;

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            string path = Path.GetFullPath(linkXmlPath);

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, GenerateLinkXml());

            return path;
        }

        public static string GenerateLinkXml()
        {
            var types = new HashSet<Type>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<IGenericSerialization>())
            {
                GenericSerializer.CollectSerializedTypes(type, types);
            }

            // Only script assemblies that make it into a build are of any interest: no editor code,
            // and nothing of the engine's or of the framework's
            var playerAssemblies = new HashSet<string>(CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies).Select(assembly => assembly.name));

            var typeNamesByAssembly = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            foreach (var type in types)
            {
                // A generic type is kept as a whole, whatever it's a generic of
                Type definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;

                string assemblyName = definition.Assembly.GetName().Name;
                if (!playerAssemblies.Contains(assemblyName)) continue;

                if (!typeNamesByAssembly.TryGetValue(assemblyName, out var typeNames))
                {
                    typeNames = new SortedSet<string>(StringComparer.Ordinal);
                    typeNamesByAssembly[assemblyName] = typeNames;
                }

                // The linker separates a nested type from the one it's in with a slash
                typeNames.Add(definition.FullName.Replace('+', '/'));
            }

            var xml = new StringBuilder();
            xml.AppendLine("<linker>");
            foreach (var assembly in typeNamesByAssembly)
            {
                xml.AppendLine($"  <assembly fullname=\"{assembly.Key}\">");
                foreach (var typeName in assembly.Value)
                {
                    xml.AppendLine($"    <type fullname=\"{typeName}\" preserve=\"all\"/>");
                }
                xml.AppendLine("  </assembly>");
            }
            xml.AppendLine("</linker>");

            return xml.ToString();
        }
    }
}
