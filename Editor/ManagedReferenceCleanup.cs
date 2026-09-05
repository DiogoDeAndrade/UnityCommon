using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UC
{
    public static class ManagedReferenceCleanup
    {
        [MenuItem("Unity Common/Tools/List Missing Managed References")]
        private static void ListMissingManagedReferences()
        {
            var hosts = GetSelectedHosts();

            if (hosts.Count == 0)
            {
                DebugHelpers.LogWarning(
                    "Selection contains no MonoBehaviour or ScriptableObject hosts.");
                return;
            }

            int total = 0;

            foreach (var host in hosts)
            {
                if (!SerializationUtility.HasManagedReferencesWithMissingTypes(host))
                    continue;

                var missing =
                    SerializationUtility.GetManagedReferencesWithMissingTypes(host);

                foreach (var reference in missing)
                {
                    total++;

                    string typeName =
                        string.IsNullOrEmpty(reference.namespaceName)
                        ? reference.className
                        : $"{reference.namespaceName}.{reference.className}";

                    DebugHelpers.LogWarning(
                        $"Missing managed reference on '{host.name}':\n" +
                        $"  Host: {host.GetType().FullName}\n" +
                        $"  Reference ID: {reference.referenceId}\n" +
                        $"  Type: {typeName}\n" +
                        $"  Assembly: {reference.assemblyName}\n" +
                        $"  Serialized Data:\n{reference.serializedData}",
                        host);
                }
            }

            if (total == 0)
                DebugHelpers.Log("No missing managed references found.");
            else
                DebugHelpers.Log($"Found {total} missing managed reference(s).");
        }

        [MenuItem("Unity Common/Tools/Clear Missing Managed References")]
        private static void ClearMissingManagedReferences()
        {
            var hosts = GetSelectedHosts();

            if (hosts.Count == 0)
            {
                DebugHelpers.LogWarning(
                    "Selection contains no MonoBehaviour or ScriptableObject hosts.");
                return;
            }

            int clearedHosts = 0;

            foreach (var host in hosts)
            {
                if (!SerializationUtility.HasManagedReferencesWithMissingTypes(host))
                    continue;

                Undo.RecordObject(host, "Clear Missing Managed References");

                if (SerializationUtility.ClearAllManagedReferencesWithMissingTypes(host))
                {
                    EditorUtility.SetDirty(host);
                    clearedHosts++;

                    DebugHelpers.Log(
                        $"Cleared missing managed references from '{host.name}' " +
                        $"({host.GetType().FullName}).",
                        host);
                }
            }

            if (clearedHosts > 0)
            {
                AssetDatabase.SaveAssets();
                DebugHelpers.Log($"Cleaned {clearedHosts} serialization host(s).");
            }
            else
            {
                DebugHelpers.Log("No missing managed references found.");
            }
        }

        private static List<Object> GetSelectedHosts()
        {
            var result = new List<Object>();
            var ids = new HashSet<EntityId>();

            void AddHost(Object obj)
            {
                if (obj == null)
                    return;

                if ((obj is not MonoBehaviour) &&
                    (obj is not ScriptableObject))
                    return;

                if (ids.Add(obj.GetEntityId()))
                    result.Add(obj);
            }

            foreach (var selected in Selection.objects)
            {
                // Selected object itself might be the host.
                AddHost(selected);

                // A GameObject is not itself a SerializeReference host.
                // Its MonoBehaviours are.
                if (selected is GameObject go)
                {
                    foreach (var component in go.GetComponents<MonoBehaviour>())
                        AddHost(component);
                }

                // Also inspect sub-assets at the selected asset path.
                string path = AssetDatabase.GetAssetPath(selected);

                if (!string.IsNullOrEmpty(path))
                {
                    foreach (var subAsset in AssetDatabase.LoadAllAssetsAtPath(path))
                    {
                        AddHost(subAsset);

                        if (subAsset is GameObject subGameObject)
                        {
                            foreach (var component in
                                     subGameObject.GetComponents<MonoBehaviour>())
                            {
                                AddHost(component);
                            }
                        }
                    }
                }
            }

            return result;
        }

        [MenuItem("Unity Common/Tools/List ALL Missing Managed References")]
        private static void ListAllMissingManagedReferences()
        {
            string[] scriptableGuids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" });

            int total = 0;
            foreach (string guid in scriptableGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (obj is ScriptableObject)
                        total += ReportMissingReferences(obj, path);
                }
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null)
                    continue;

                foreach (MonoBehaviour component in
                         prefab.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component != null)
                        total += ReportMissingReferences(component, path);
                }
            }

            DebugHelpers.Log((total == 0) ? ("No missing managed references found in project assets.") : ($"Found {total} missing managed reference(s)."));
        }

        private static int ReportMissingReferences(Object host, string path)
        {
            if ((host == null) ||
                !SerializationUtility.HasManagedReferencesWithMissingTypes(host))
            {
                return 0;
            }

            var missing =
                SerializationUtility.GetManagedReferencesWithMissingTypes(host);

            foreach (var reference in missing)
            {
                string typeName =
                    string.IsNullOrEmpty(reference.namespaceName)
                        ? reference.className
                        : $"{reference.namespaceName}.{reference.className}";

                DebugHelpers.LogWarning(
                    $"Missing managed reference:\n" +
                    $"  Asset: {path}\n" +
                    $"  Host: {host.name}\n" +
                    $"  Host Type: {host.GetType().FullName}\n" +
                    $"  Reference ID: {reference.referenceId}\n" +
                    $"  Missing Type: {typeName}\n" +
                    $"  Assembly: {reference.assemblyName}",
                    host);
            }

            return missing.Length;
        }

        [MenuItem("Unity Common/Tools/List Missing Managed References In ALL Scenes")]
        private static void ListAllMissingManagedReferencesInScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var originalSetup = EditorSceneManager.GetSceneManagerSetup();

            int total = 0;

            try
            {
                string[] sceneGuids =
                    AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });

                foreach (string guid in sceneGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);

                    var scene =
                        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        foreach (MonoBehaviour component in
                                 root.GetComponentsInChildren<MonoBehaviour>(true))
                        {
                            if (component != null)
                                total += ReportMissingReferences(component, path);
                        }
                    }
                }
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }

            DebugHelpers.Log(
                total == 0
                    ? "No missing managed references found in scenes."
                    : $"Found {total} missing managed reference(s) in scenes.");
        }
    }
}
