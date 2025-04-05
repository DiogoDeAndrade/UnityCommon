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
    }
}