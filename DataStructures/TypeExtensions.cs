using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Assemblies;

namespace UC
{

    public static class TypeExtensions
    {
        const BindingFlags instanceMemberFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        public static FieldInfo GetPrivateField(this Type type, string name)
        {
            var currentType = type;
            while (currentType != null)
            {
                var ret = currentType.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (ret != null) return ret;

                currentType = currentType.BaseType;
            }

            return null;
        }

        // Every method called <name> declared by the first type in the chain that declares it - a
        // name redeclared further down hides the ones above it, the way GetMethod picks a level too.
        //
        // Unlike GetMethod this never throws on overloads: the caller gets all the candidates and
        // decides, which is the only way to report *which* name was ambiguous instead of letting an
        // AmbiguousMatchException surface with the name nowhere in it.
        public static List<MethodInfo> GetPrivateMethods(this Type type, string name)
        {
            var ret = new List<MethodInfo>();

            var currentType = type;
            while (currentType != null)
            {
                foreach (var method in currentType.GetMethods(instanceMemberFlags))
                {
                    if (method.Name == name) ret.Add(method);
                }
                if (ret.Count > 0) break;

                currentType = currentType.BaseType;
            }

            return ret;
        }

        // The single method called <name>, or null when there is none. Overloads can't be told apart
        // by name alone, so they are an error here rather than a silent pick: the message names the
        // type and lists the signatures, and callers that can narrow it down further (by argument
        // count, say) should use GetPrivateMethods instead.
        public static MethodInfo GetPrivateMethod(this Type type, string name)
        {
            var methods = type.GetPrivateMethods(name);

            if (methods.Count == 1) return methods[0];
            if (methods.Count > 1)
            {
                DebugHelpers.LogError($"\"{name}\" is ambiguous in {type.Name} - {DescribeMethods(methods)}; a method looked up by name alone can't be overloaded!");
            }

            return null;
        }

        // "TLHDialogueContext.HasResource(String, Single)", for error messages
        public static string DescribeMethod(this MethodInfo method)
        {
            var parameters = method.GetParameters();
            var parameterTypes = new string[parameters.Length];
            for (int i = 0; i < parameters.Length; i++) parameterTypes[i] = parameters[i].ParameterType.Name;

            return $"{method.DeclaringType.Name}.{method.Name}({string.Join(", ", parameterTypes)})";
        }

        public static string DescribeMethods(IReadOnlyList<MethodInfo> methods)
        {
            var descriptions = new string[methods.Count];
            for (int i = 0; i < methods.Count; i++) descriptions[i] = methods[i].DescribeMethod();

            return string.Join(", ", descriptions);
        }

        public static Type GetTypeByName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return null;

            // 1. Try exact match first (namespace + typename)
            var type = Type.GetType(typeName, throwOnError: false);
            if (type != null)
                return type;

            // 2. Search all loaded assemblies for a simple type name
            typeName = typeName.Trim();

            var assemblies = CurrentAssemblies.GetLoadedAssemblies();

            foreach (var asm in assemblies)
            {
                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (t.Name == typeName)
                            return t;
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // ignore assemblies that fail to load completely
                }
            }

            return null;
        }
    }
}
