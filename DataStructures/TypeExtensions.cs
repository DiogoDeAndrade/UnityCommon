using System;
using System.Reflection;

namespace UC
{

    public static class TypeExtensions
    {
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

        public static MethodInfo GetPrivateMethod(this Type type, string name)
        {
            var currentType = type;
            while (currentType != null)
            {
                var ret = currentType.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (ret != null) return ret;

                currentType = currentType.BaseType;
            }

            return null;
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

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

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