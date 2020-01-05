using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class AssetUtils
{
    public static T CreateOrReplaceAsset<T>(T asset, string path) where T : Object
    {
#if UNITY_EDITOR
        T existingAsset = AssetDatabase.LoadAssetAtPath<T>(path);

        if (existingAsset == null)
        {
            AssetDatabase.CreateAsset(asset, path);
            existingAsset = asset;
        }
        else
        {
            EditorUtility.CopySerialized(asset, existingAsset);
        }

        return existingAsset;
#else
        Debug.LogError("CreateOrReplaceAsset not available in runtime!");
        return null;
#endif
    }

    public static T CreateOrReplaceAsset<T>(T oldAsset, T newAsset) where T : Object
    {
#if UNITY_EDITOR
        string path = AssetDatabase.GetAssetPath(oldAsset);
        if (path == "")
        {
            Debug.LogError("Can't find path for asset!");
            return null;
        }

        T existingAsset = AssetDatabase.LoadAssetAtPath<T>(path);

        if (existingAsset == null)
        {
            AssetDatabase.CreateAsset(newAsset, path);
            existingAsset = newAsset;
        }
        else
        {
            EditorUtility.CopySerialized(newAsset, existingAsset);
        }

        return existingAsset;
#else
        Debug.LogError("CreateOrReplaceAsset not available in runtime!");
        return null;
#endif
    }

}
