using System.Collections.Generic;
using System;
using UnityEngine;
using System.Linq;

namespace UC
{

    public static class InterfaceHelpers
    {
        public static List<Type> FindInterfaceTypes<TInterface>()
        {
            var interfaceType = typeof(TInterface);

            // Validate that the provided type is an interface
            if (!interfaceType.IsInterface)
            {
                Debug.LogError($"{interfaceType.Name} is not an interface.");
                return new List<Type>();
            }

            // Get all loaded assemblies in the current AppDomain
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            // Find all MonoBehaviours implementing the specified interface
            var result = new List<Type>();

            foreach (var assembly in assemblies)
            {
                // Get all types defined in the assembly
                var types = assembly.GetTypes();

                foreach (var type in types)
                {
                    // Ensure the type is a MonoBehaviour and implements the interface
                    if (typeof(MonoBehaviour).IsAssignableFrom(type) && interfaceType.IsAssignableFrom(type) && !type.IsAbstract)
                    {
                        result.Add(type);
                    }
                }
            }

            return result;
        }

        private static Dictionary<Type, List<Type>> _interfaceToImplementors = new();

        public static T GetFirstInterfaceComponent<T>() where T : class
        {
            Type interfaceType = typeof(T);

            // Step 1: Cache the MonoBehaviour types that implement the interface
            if (!_interfaceToImplementors.ContainsKey(interfaceType))
            {
                CacheImplementingTypes<T>();
            }

            var types = _interfaceToImplementors[interfaceType];
            foreach (var type in types)
            {
                var ret = GameObject.FindFirstObjectByType(type) as T;
                if (ret != null) return ret;
            }

            return null;
        }

        private static void CacheImplementingTypes<T>() where T : class
        {
            Type interfaceType = typeof(T);
            var implementingTypes = new List<Type>();

            foreach (Type type in AppDomain.CurrentDomain.GetAssemblies()
                         .SelectMany(assembly => assembly.GetTypes()))
            {
                if (interfaceType.IsAssignableFrom(type) && type.IsClass && !type.IsAbstract && typeof(MonoBehaviour).IsAssignableFrom(type))
                {
                    implementingTypes.Add(type);
                }
            }

            _interfaceToImplementors[interfaceType] = implementingTypes;
        }
    }
}