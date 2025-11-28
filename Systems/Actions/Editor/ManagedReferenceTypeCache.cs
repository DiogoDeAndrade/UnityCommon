// Assets/Editor/ManagedReferenceTypeCache.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace UC.Interaction.Editor
{

    public static class ManagedReferenceTypeCache
    {
        private static readonly Dictionary<Type, List<(string displayName, Type type)>> _cache = new();

        public static IReadOnlyList<(string displayName, Type type)> GetAssignableConcreteTypes(Type baseType)
        {
            if (!_cache.TryGetValue(baseType, out var list))
            {
                list = TypeCache.GetTypesDerivedFrom(baseType)
                    .Where(t => !t.IsAbstract && !t.IsGenericType && t.GetConstructor(Type.EmptyTypes) != null)
                    .Select(t =>
                    {
                        var nice = GetDisplayName(t, baseType);
                        return (nice, t);
                    })
                    .OrderBy(t => t.nice, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _cache[baseType] = list;
            }

            return list;
        }

        private static string GetDisplayName(Type type, Type baseType)
        {
            // Any attribute derived from PolymorphicNameAttribute is accepted:
            var attr = type.GetCustomAttributes(typeof(PolymorphicNameAttribute), inherit: false).FirstOrDefault() as PolymorphicNameAttribute;

            if (attr != null && !string.IsNullOrWhiteSpace(attr.Path))
                return attr.Path; // e.g. "WSL/Tokens/Add" -> GenericMenu creates submenus

            // Fallback = old behaviour
            return NicifyTypeName(type, baseType);
        }


        private static string NicifyTypeName(Type type, Type baseType)
        {
            var name = type.Name;

            if (baseType == typeof(Condition) && name.StartsWith("Condition_"))
                name = name.Substring("Condition_".Length);

            if (baseType == typeof(GameAction) && name.StartsWith("GameAction_"))
                name = name.Substring("GameAction_".Length);

            return ObjectNames.NicifyVariableName(name);
        }
    }
}