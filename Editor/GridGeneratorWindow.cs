using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UC
{
    public class GridGeneratorWindow : EditorWindow
    {
        private readonly List<GameObject> prefabs = new();

        private bool    useSelectedObjectAsParent = false;
        private Vector3 startPosition = Vector3.zero;
        private Vector3 spacing = Vector3.one;

        [Min(1)]
        private int     countX = 5;
        [Min(1)]
        private int     countY = 1;
        [Min(1)]
        private int     countZ = 5;

        private Vector3 radialNoise = Vector3.zero;

        private bool    randomRotationY = false;
        private Transform parent;

        [MenuItem("Unity Common/Spawn Objects in Grid", priority = 10)]
        public static void Open()
        {
            GetWindow<GridGeneratorWindow>("Grid Generator");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Prefab Grid Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawPrefabList();

            EditorGUILayout.Space();

            startPosition = EditorGUILayout.Vector3Field("Start Position", startPosition);
            spacing = EditorGUILayout.Vector3Field("Spacing", spacing);

            EditorGUILayout.Space();

            countX = Mathf.Max(1, EditorGUILayout.IntField("Count X", countX));
            countY = Mathf.Max(1, EditorGUILayout.IntField("Count Y", countY));
            countZ = Mathf.Max(1, EditorGUILayout.IntField("Count Z", countZ));

            EditorGUILayout.Space();

            radialNoise = EditorGUILayout.Vector3Field("Radial Noise", radialNoise);

            EditorGUILayout.HelpBox(
                "Radial Noise offsets each spawned object by a random point inside an ellipsoid. " +
                "The X, Y and Z values control the maximum noise radius on each axis.",
                MessageType.Info);

            EditorGUILayout.Space();

            useSelectedObjectAsParent = EditorGUILayout.Toggle("Use Selected Object As Parent", useSelectedObjectAsParent);

            using (new EditorGUI.DisabledScope(useSelectedObjectAsParent))
            {
                parent = (Transform)EditorGUILayout.ObjectField("Parent", parent, typeof(Transform), true);
            }

            randomRotationY = EditorGUILayout.Toggle("Random Rotation Y", randomRotationY);

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(prefabs.Count == 0))
            {
                if (GUILayout.Button("Generate Grid"))
                {
                    GenerateGrid();
                }
            }
        }

        private void DrawPrefabList()
        {
            EditorGUILayout.LabelField("Prefabs", EditorStyles.boldLabel);

            int removeIndex = -1;

            for (int i = 0; i < prefabs.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                prefabs[i] = (GameObject)EditorGUILayout.ObjectField(
                    prefabs[i],
                    typeof(GameObject),
                    false);

                if (GUILayout.Button("-", GUILayout.Width(24)))
                {
                    removeIndex = i;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (removeIndex >= 0)
            {
                prefabs.RemoveAt(removeIndex);
            }

            if (GUILayout.Button("Add Prefab"))
            {
                prefabs.Add(null);
            }
        }

        private void GenerateGrid()
        {
            List<GameObject> validPrefabs = prefabs.FindAll(p => p != null);

            if (validPrefabs.Count == 0)
            {
                Debug.LogWarning("No prefabs assigned.");
                return;
            }

            if (prefabs.Count == 0)
            {
                Debug.LogWarning("No prefabs assigned.");
                return;
            }

            Transform targetParent = null;

            if (useSelectedObjectAsParent)
            {
                GameObject selected = Selection.activeGameObject;

                if (selected != null)
                {
                    targetParent = selected.transform;
                }
                else
                {
                    Debug.LogWarning("Use Selected Object As Parent is enabled, but no object is selected.");
                    return;
                }
            }
            else
            {
                targetParent = parent;

                if (targetParent == null)
                {
                    GameObject root = new GameObject("Generated Grid");
                    Undo.RegisterCreatedObjectUndo(root, "Create Grid Parent");
                    targetParent = root.transform;
                }
            }

            Undo.SetCurrentGroupName("Generate Prefab Grid");
            int undoGroup = Undo.GetCurrentGroup();

            for (int x = 0; x < countX; x++)
            {
                for (int y = 0; y < countY; y++)
                {
                    for (int z = 0; z < countZ; z++)
                    {
                        GameObject prefab = validPrefabs[Random.Range(0, validPrefabs.Count)];

                        Vector3 gridPosition = startPosition + new Vector3(
                            x * spacing.x,
                            y * spacing.y,
                            z * spacing.z);

                        Vector3 noiseOffset = GetRadialNoiseOffset(radialNoise);

                        Quaternion rotation = Quaternion.identity;

                        if (randomRotationY)
                        {
                            rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                        }

                        GameObject instance = InstantiatePrefab(prefab, targetParent);

                        instance.transform.position = gridPosition + noiseOffset;
                        instance.transform.rotation = rotation;

                        Undo.RegisterCreatedObjectUndo(instance, "Generate Prefab Grid");
                    }
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        private static GameObject InstantiatePrefab(GameObject prefab, Transform parent)
        {
            GameObject instance;

            GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(prefab);

            if (prefabAsset != null)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, parent);
            }
            else
            {
                instance = Instantiate(prefab, parent);
            }

            return instance;
        }

        private static Vector3 GetRadialNoiseOffset(Vector3 noise)
        {
            if (noise == Vector3.zero)
            {
                return Vector3.zero;
            }

            Vector3 randomPoint = Random.insideUnitSphere;

            return new Vector3(randomPoint.x * noise.x, randomPoint.y * noise.y, randomPoint.z * noise.z);
        }
    }
}
