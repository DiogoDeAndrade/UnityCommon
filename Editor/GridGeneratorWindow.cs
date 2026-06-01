using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UC
{
    public class GridGeneratorWindow : EditorWindow
    {
        private readonly List<GameObject> prefabs = new();

        private bool useSelectedObjectAsPrefab = false;
        private bool useSameParentAsParent = false;
        private bool useSelectedObjectAsParent = false;
        private Vector3 startPosition = Vector3.zero;
        private Vector3 spacing = Vector3.one;

        [Min(1)]
        private int countX = 5;
        [Min(1)]
        private int countY = 1;
        [Min(1)]
        private int countZ = 5;

        private Vector3 radialNoise = Vector3.zero;

        private bool randomRotationY = false;
        private float rotationYMin = -180f;
        private float rotationYMax = 180f;

        private bool randomScale = false;
        private Vector3 scaleMin = Vector3.one;
        private Vector3 scaleMax = Vector3.one;

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

            useSelectedObjectAsPrefab = EditorGUILayout.ToggleLeft("Use Selected Object As Prefab", useSelectedObjectAsPrefab);

            EditorGUILayout.Space();

            if (!useSelectedObjectAsPrefab)
            {
                using (new EditorGUI.DisabledScope(useSelectedObjectAsPrefab))
                {
                    DrawPrefabList();
                }
            }
            else
            {
                useSameParentAsParent = EditorGUILayout.ToggleLeft("Use Selected Object's Parent As Parent", useSameParentAsParent);
            }

            EditorGUILayout.Space();

            // In selected-object mode the start position is the object's own world position,
            // so there is nothing to configure here.
            if (!useSelectedObjectAsPrefab)
            {
                startPosition = EditorGUILayout.Vector3Field("Start Position", startPosition);
            }

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

            if (!useSelectedObjectAsPrefab)
            {
                useSelectedObjectAsParent = EditorGUILayout.ToggleLeft("Use Selected Object As Parent", useSelectedObjectAsParent);
                using (new EditorGUI.DisabledScope(useSelectedObjectAsParent))
                {
                    parent = (Transform)EditorGUILayout.ObjectField("Parent", parent, typeof(Transform), true);
                }
            }
            else
            {
                parent = (Transform)EditorGUILayout.ObjectField("Override Parent", parent, typeof(Transform), true);
            }

            randomRotationY = EditorGUILayout.ToggleLeft("Random Rotation Y", randomRotationY);

            if (randomRotationY)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Range");
                rotationYMin = EditorGUILayout.FloatField(rotationYMin);
                EditorGUILayout.LabelField("to", GUILayout.Width(20));
                rotationYMax = EditorGUILayout.FloatField(rotationYMax);
                EditorGUILayout.EndHorizontal();
                EditorGUI.indentLevel--;
            }

            randomScale = EditorGUILayout.ToggleLeft("Random Scale", randomScale);

            if (randomScale)
            {
                EditorGUI.indentLevel++;
                scaleMin = EditorGUILayout.Vector3Field("Min", scaleMin);
                scaleMax = EditorGUILayout.Vector3Field("Max", scaleMax);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            bool canGenerate = useSelectedObjectAsPrefab
                ? Selection.activeGameObject != null
                : prefabs.Count > 0;

            using (new EditorGUI.DisabledScope(!canGenerate))
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
            List<GameObject> validPrefabs;
            GameObject selectedObject = null;

            if (useSelectedObjectAsPrefab)
            {
                selectedObject = Selection.activeGameObject;

                if (selectedObject == null)
                {
                    Debug.LogWarning("Use Selected Object As Prefab is enabled, but no object is selected.");
                    return;
                }

                validPrefabs = new List<GameObject> { selectedObject };
            }
            else
            {
                validPrefabs = prefabs.FindAll(p => p != null);

                if (validPrefabs.Count == 0)
                {
                    Debug.LogWarning("No prefabs assigned.");
                    return;
                }
            }

            Transform targetParent = null;

            if (useSelectedObjectAsPrefab)
            {
                if (useSameParentAsParent)
                {
                    targetParent = selectedObject.transform.parent;
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
            }
            else
            {
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
            }

            // In selected-object mode the grid origin is the object's current world position
            // and it already occupies the [0,0,0] slot, so we derive the start from it.
            Vector3 effectiveStartPosition = useSelectedObjectAsPrefab
                ? selectedObject.transform.position
                : startPosition;

            // In selected-object mode the grid axes follow the object's world orientation
            // so that spacing is applied along its local X/Y/Z rather than world axes.
            Quaternion effectiveOrientation = useSelectedObjectAsPrefab
                ? selectedObject.transform.rotation
                : Quaternion.identity;

            Undo.SetCurrentGroupName("Generate Prefab Grid");
            int undoGroup = Undo.GetCurrentGroup();

            for (int x = 0; x < countX; x++)
            {
                for (int y = 0; y < countY; y++)
                {
                    for (int z = 0; z < countZ; z++)
                    {
                        // The selected object already sits at [0,0,0] — skip it.
                        if (useSelectedObjectAsPrefab && x == 0 && y == 0 && z == 0)
                        {
                            continue;
                        }

                        GameObject prefab = validPrefabs[Random.Range(0, validPrefabs.Count)];

                        Vector3 gridPosition = effectiveStartPosition + effectiveOrientation * new Vector3(
                            x * spacing.x,
                            y * spacing.y,
                            z * spacing.z);

                        Vector3 noiseOffset = GetRadialNoiseOffset(radialNoise);

                        Quaternion rotation = Quaternion.identity;

                        if (randomRotationY)
                        {
                            rotation = Quaternion.Euler(0f, Random.Range(rotationYMin, rotationYMax), 0f);
                        }

                        GameObject instance = InstantiatePrefab(prefab, targetParent);

                        instance.transform.position = gridPosition + noiseOffset;
                        instance.transform.rotation = rotation;

                        if (randomScale)
                        {
                            instance.transform.localScale = new Vector3(
                                Random.Range(scaleMin.x, scaleMax.x),
                                Random.Range(scaleMin.y, scaleMax.y),
                                Random.Range(scaleMin.z, scaleMax.z));
                        }

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
