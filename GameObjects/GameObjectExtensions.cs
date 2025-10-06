using System.Collections.Generic;
using UnityEngine;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UC
{

    public static class GameObjectExtensions
    {
        public static GameObject GetRootObject(this GameObject go)
        {
            if (go.transform.parent == null) return go;

            return go.transform.parent.gameObject.GetRootObject();
        }

        public static void DeleteAllChildren(this GameObject go)
        {
            List<Transform> toDestroy = new List<Transform>();

            foreach (Transform t in go.transform)
            {
                toDestroy.Add(t);
            }

            foreach (var t in toDestroy)
            {
#if UNITY_EDITOR
                if (Application.isPlaying)
                {
                    GameObject.Destroy(t.gameObject);
                }
                else
                {
                    GameObject.DestroyImmediate(t.gameObject);
                }
#else
            GameObject.Destroy(t.gameObject);
#endif
            }
        }

        public static GameObject FindByInstanceID(this GameObject go, int instanceID)
        {
            // Get all subobjects
            var allTransforms = go.GetComponentsInChildren<Transform>();
            foreach (var transform in allTransforms)
            {
                if (transform.gameObject.GetInstanceID() == instanceID)
                {
                    return transform.gameObject;
                }
            }

            return null;
        }

        public static GameObject FindByName(this GameObject go, string name)
        {
            // Get all subobjects
            var allTransforms = go.GetComponentsInChildren<Transform>();
            foreach (var transform in allTransforms)
            {
                if (transform.gameObject.name == name)
                {
                    return transform.gameObject;
                }
            }

            return null;
        }

        public static void Delete(this GameObject go)
        {
            if (Application.isPlaying)
            {
                GameObject.Destroy(go);
            }
            else
            {
#if UNITY_EDITOR
                // Check if this is part of a prefab
                if (PrefabUtility.IsPartOfPrefabInstance(go))
                {
                    var prefab = PrefabUtility.GetCorrespondingObjectFromSource(go);
                    string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefab);

                    if (assetPath == "")
                    {
                        Debug.LogWarning("Can't delete object, can't find asset path!");
                        return;
                    }

                    // Load the contents of the Prefab Asset.
                    GameObject contentsRoot = PrefabUtility.LoadPrefabContents(assetPath);

                    // Modify Prefab contents.
                    GameObject objectToDelete = contentsRoot.FindByName(go.name);
                    GameObject.DestroyImmediate(objectToDelete);

                    // Save contents back to Prefab Asset and unload contents.
                    PrefabUtility.SaveAsPrefabAsset(contentsRoot, assetPath);
                    PrefabUtility.UnloadPrefabContents(contentsRoot);
                    return;
                }
#endif
                GameObject.DestroyImmediate(go);
            }
        }

        public static GameObject FindObjectInLayer(this GameObject go, int layer)
        {
            var objects = go.FindObjectsInLayer(layer);

            if (objects.Count == 0) return null;

            return objects[0];

        }

        public static List<GameObject> FindObjectsInLayer(this GameObject go, int layer)
        {
            List<GameObject> ret = new List<GameObject>();

            for (int i = 0; i < go.transform.childCount; i++)
            {
                var child = go.transform.GetChild(i);
                if (child == null) continue;

                if (child.gameObject.layer == layer)
                {
                    ret.Add(child.gameObject);
                }

                ret.AddRange(child.gameObject.FindObjectsInLayer(layer));
            }

            return ret;
        }

        public static bool IsChildOf(this Transform child, Transform parent)
        {
            if (child == null || parent == null) return false;

            Transform current = child;

            while (current != null)
            {
                if (current == parent)
                    return true;

                current = current.parent; // Move up the hierarchy
            }

            return false;
        }

        public static T[] FindAllInRadius<T>(this GameObject gameObject, Vector3 pos, float range) where T : Component
        {
            // Get all objects of type T in the scene
            T[] allObjects = Object.FindObjectsByType<T>(FindObjectsSortMode.None);

            // List to store objects within the range
            List<(T component, float distance)> objectsInRange = new List<(T, float)>();

            // Iterate through all objects to check their distance
            foreach (T obj in allObjects)
            {
                // Get the object's position
                Vector3 objPos = obj.transform.position;

                // Calculate the distance from the given position
                float distance = Vector3.Distance(pos, objPos);

                // If within range, add to the list
                if (distance <= range)
                {
                    objectsInRange.Add((obj, distance));
                }
            }

            // Sort the list by distance
            objectsInRange.Sort((a, b) => a.distance.CompareTo(b.distance));

            // Extract and return the components as an array
            return objectsInRange.ConvertAll(item => item.component).ToArray();
        }

        public static T GetInterfaceComponent<T>(this GameObject gameObject) where T : class
        {
            foreach (var component in gameObject.GetComponents<Component>())
            {
                if (component is T tComponent)
                {
                    return tComponent;
                }
            }
            return null;
        }

        public static T[] GetComponentsInChildrenAndParent<T>(this GameObject gameObject, bool includeInactive = false) where T : Component
        {
            var children = gameObject.GetComponentsInChildren<T>();
            var parent = gameObject.GetComponentsInParent<T>();
            if (children.Length == 0)
            {
                return parent;
            }
            else if (parent.Length == 0)
            {
                return children;
            }

            HashSet<T> ret = new();
            foreach (var c in children) ret.Add(c);
            foreach (var p in parent) ret.Add(p);

            return ret.ToArray();
        }

        public static T[] GetComponentsInChildrenAndParent<T>(this Component component, bool includeInactive = false) where T : Component
        {
            var children = component.GetComponentsInChildren<T>();
            var parent = component.GetComponentsInParent<T>();
            if (children.Length == 0)
            {
                return parent;
            }
            else if (parent.Length == 0)
            {
                return children;
            }

            HashSet<T> ret = new();
            foreach (var c in children) ret.Add(c);
            foreach (var p in parent) ret.Add(p);

            return ret.ToArray();
        }
    }
}