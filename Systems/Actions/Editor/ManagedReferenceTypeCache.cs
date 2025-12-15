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
                var items = TypeCache.GetTypesDerivedFrom(baseType)
                    .Where(t => !t.IsAbstract && !t.IsGenericType && t.GetConstructor(Type.EmptyTypes) != null)
                    .Select(t =>
                    {
                        var nice = GetDisplayName(t, baseType);
                        return (displayName: nice, type: t);
                    })
                    .ToList();

                // Build a set of "folder" paths (prefixes that have children).
                // Example: if we have "RPG/Turn Driver/Composite", then
                // "RPG" and "RPG/Turn Driver" are folders.
                var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var it in items)
                {
                    var parts = it.displayName.Split('/');
                    if (parts.Length <= 1)
                        continue;

                    string prefix = parts[0];
                    folders.Add(prefix);

                    for (int i = 1; i < parts.Length - 1; i++)
                    {
                        prefix += "/" + parts[i];
                        folders.Add(prefix);
                    }
                }

                items.Sort((a, b) => CompareMenuPathsFoldersFirst(a.displayName, b.displayName, folders));

                list = items;
                _cache[baseType] = list;
            }

            return list;
        }

        static int CompareMenuPathsFoldersFirst(string a, string b, HashSet<string> folders)
        {
            // Split into segments
            var ap = a.Split('/');
            var bp = b.Split('/');

            int min = Math.Min(ap.Length, bp.Length);

            string aPrefix = "";
            string bPrefix = "";

            for (int i = 0; i < min; i++)
            {
                if (i == 0)
                {
                    aPrefix = ap[0];
                    bPrefix = bp[0];
                }
                else
                {
                    aPrefix += "/" + ap[i];
                    bPrefix += "/" + bp[i];
                }

                // Same segment name at this level -> keep going deeper
                int nameCmp = StringComparer.OrdinalIgnoreCase.Compare(ap[i], bp[i]);
                if (nameCmp == 0)
                    continue;

                // Folder-first at this level
                // We test "is this segment a folder node" by checking if its prefix exists in `folders`.
                bool aIsFolderHere = folders.Contains(aPrefix);
                bool bIsFolderHere = folders.Contains(bPrefix);

                if (aIsFolderHere != bIsFolderHere)
                    return aIsFolderHere ? -1 : 1;

                // Otherwise alphabetical
                return nameCmp;
            }

            // All common segments equal; shorter path (leaf) after longer path (which implies deeper hierarchy)
            // If you prefer exact Explorer behavior (folders before files with same name), this is correct.
            if (ap.Length != bp.Length)
                return ap.Length.CompareTo(bp.Length);

            return 0;
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