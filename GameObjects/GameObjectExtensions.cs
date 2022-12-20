using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
                string  assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefab);

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
}

    