using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace UC
{
    [CustomEditor(typeof(RuntimeAssetDatabase))]
    public class RuntimeAssetDatabaseEditor : UnityEditor.Editor
    {
        private string _summary;

        public override void OnInspectorGUI()
        {
            var database = (RuntimeAssetDatabase)target;

            DrawDefaultInspector();

            EditorGUILayout.Space();

            if (!IsWhereItCanBeFound(database))
            {
                EditorGUILayout.HelpBox("This will only be found at runtime if it is called RuntimeAssetDatabase and located in a Resources folder.", MessageType.Warning);
            }

            _summary ??= GetSummary(database);
            EditorGUILayout.HelpBox(_summary, MessageType.Info);

            if (GUILayout.Button("Scan project", GUILayout.Height(22)))
            {
                Scan(database);
                _summary = null;

                DebugHelpers.Log($"{database.name}: {GetSummary(database)}", database);
            }
        }

        // Replaces the entries with every asset of the enabled types there is right now; false if
        // that's what they already were. Whatever can't exist in a build is left out: what's in an
        // Editor folder, and what's of an editor type.
        public static bool Scan(RuntimeAssetDatabase database)
        {
            var types = RuntimeAssetDatabase.GetTypes(database.assetTypes);
            var entries = new List<RuntimeAssetDatabase.Entry>();

            if (types.Count > 0)
            {
                // Several types in a filter means any of them
                string filter = string.Join(" ", types.Select(type => (type == typeof(GameObject)) ? "t:Prefab" : $"t:{type.Name}"));
                var editorOnlyAssemblies = GetEditorOnlyAssemblies();

                foreach (var guid in FindAssets(filter, database.searchFolders))
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (assetPath.Contains("/Editor/")) continue;

                    AddEntry(entries, AssetDatabase.LoadMainAssetAtPath(assetPath), database, types, editorOnlyAssemblies);
                    foreach (var subAsset in AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath))
                    {
                        AddEntry(entries, subAsset, database, types, editorOnlyAssemblies);
                    }
                }
            }

            // An asset comes before what's inside of it this way, and the file doesn't get shuffled every time
            entries.Sort((a, b) => string.CompareOrdinal(a.reference, b.reference));

            // Most of the time nothing is different, and then there's no reason to touch the asset
            if (AreTheSame(entries, database.entries)) return false;

            database.SetEntries(entries);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssetIfDirty(database);

            return true;
        }

        static bool AreTheSame(IReadOnlyList<RuntimeAssetDatabase.Entry> entries, IReadOnlyList<RuntimeAssetDatabase.Entry> otherEntries)
        {
            if (entries.Count != otherEntries.Count) return false;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var otherEntry = otherEntries[i];

                if ((entry.reference != otherEntry.reference) || (entry.name != otherEntry.name) || (entry.type != otherEntry.type)) return false;
                if (entry.asset.asset != otherEntry.asset.asset) return false;
            }

            return true;
        }

        static string[] FindAssets(string filter, IReadOnlyList<string> searchFolders)
        {
            if (searchFolders.Count == 0) return AssetDatabase.FindAssets(filter);

            var folders = new List<string>();
            foreach (var folder in searchFolders)
            {
                if (AssetDatabase.IsValidFolder(folder)) folders.Add(folder);
                else DebugHelpers.LogWarning($"{folder} is not a folder of this project, not scanned");
            }

            return (folders.Count > 0) ? AssetDatabase.FindAssets(filter, folders.ToArray()) : Array.Empty<string>();
        }

        static void AddEntry(List<RuntimeAssetDatabase.Entry> entries, UnityEngine.Object asset, RuntimeAssetDatabase database, List<Type> types, HashSet<string> editorOnlyAssemblies)
        {
            if ((asset == null) || (asset == database)) return;
            if (!types.Exists(type => type.IsInstanceOfType(asset))) return;

            // Scripts, assembly definitions and such are text assets as far as the editor goes, but not as far as a build does
            if ((asset is TextAsset) && (asset.GetType() != typeof(TextAsset))) return;

            // What the asset will be in a build. The editor has types of its own for some assets (an
            // AnimatorController, for one), which are only known by what they derive from out there.
            // That's no good for an asset that comes from a script, though: if the script is for
            // the editor only, so is the asset.
            Type type = asset.GetType();
            if ((asset is ScriptableObject) && (IsEditorOnly(type, editorOnlyAssemblies))) return;
            while (IsEditorOnly(type, editorOnlyAssemblies)) type = type.BaseType;

            entries.Add(new RuntimeAssetDatabase.Entry
            {
                reference = GenericSerializer.GetAssetReference(asset),
                name = asset.name,
                type = type.FullName,
                asset = asset,
            });
        }

        // The assemblies that are compiled for the editor and left out of the builds
        static HashSet<string> GetEditorOnlyAssemblies()
        {
            var assemblies = new HashSet<string>(CompilationPipeline.GetAssemblies(AssembliesType.Editor).Select(assembly => assembly.name));
            assemblies.ExceptWith(CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies).Select(assembly => assembly.name));

            return assemblies;
        }

        static bool IsEditorOnly(Type type, HashSet<string> editorOnlyAssemblies)
        {
            string assemblyName = type.Assembly.GetName().Name;

            return (assemblyName.StartsWith("UnityEditor")) || (editorOnlyAssemblies.Contains(assemblyName));
        }

        static bool IsWhereItCanBeFound(RuntimeAssetDatabase database)
        {
            string assetPath = AssetDatabase.GetAssetPath(database);

            return (string.IsNullOrEmpty(assetPath)) || (assetPath.EndsWith("/Resources/RuntimeAssetDatabase.asset"));
        }

        // "132 assets: 87 Sprite, 45 ScriptableObject"
        public static string GetSummary(RuntimeAssetDatabase database)
        {
            if (database.entries.Count == 0) return "No assets. Pick the types to include and scan the project.";

            var counts = new List<string>();
            foreach (var flag in (RuntimeAssetDatabase.AssetTypes[])Enum.GetValues(typeof(RuntimeAssetDatabase.AssetTypes)))
            {
                Type flagType = RuntimeAssetDatabase.GetTypes(flag)[0];

                int count = 0;
                foreach (var entry in database.entries)
                {
                    Type entryType = GenericSerializer.FindType(entry.type);
                    if ((entryType != null) && (flagType.IsAssignableFrom(entryType))) count++;
                }

                if (count > 0) counts.Add($"{count} {flag}");
            }

            return $"{database.entries.Count} assets: {string.Join(", ", counts)}";
        }
    }
}
