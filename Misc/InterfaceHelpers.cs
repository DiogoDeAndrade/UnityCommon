using System.Collections.Generic;
using System;
using UnityEngine;

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
}
